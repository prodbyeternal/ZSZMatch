using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ZSZMatch.Config;
using ZSZMatch.Core;
using ZSZMatch.Util;

namespace ZSZMatch;

/// <summary>
/// ZSZ Match - a FACEIT style match flow for a school LAN tournament.
///
///   warmup (1:45, cut to 0:25 once everybody is in)
///     -> !kapitan picks one captain per side
///     -> knife round (knives only, no money, no buying)
///     -> winning captain picks the starting side
///     -> mp_restartgame: the match starts at 0-0 with the FACEIT ruleset
///     -> per round FACEIT style statistics
/// </summary>
[MinimumApiVersion(80)]
public sealed class ZSZMatchPlugin : BasePlugin, IPluginConfig<ZSZMatchConfig>
{
    public override string ModuleName => "ZSZ Match";

    public override string ModuleVersion => "1.0.0";

    public override string ModuleAuthor => "ZSZ";

    public override string ModuleDescription =>
        "FACEIT-style LAN match flow: warmup, captains, knife round, side pick, stats.";

    public ZSZMatchConfig Config { get; set; } = new();

    internal MatchState State { get; } = new();

    internal CvarManager Cvars { get; private set; } = null!;

    internal CaptainService Captains { get; private set; } = null!;

    internal WarmupController Warmup { get; private set; } = null!;

    internal KnifeRoundController Knife { get; private set; } = null!;

    internal SidePickController SidePick { get; private set; } = null!;

    internal StatsTracker Stats { get; private set; } = null!;

    private bool _testMode;

    // ------------------------------------------------------------------ lifecycle

    public void OnConfigParsed(ZSZMatchConfig config)
    {
        Chat.Prefix = string.IsNullOrWhiteSpace(config.ChatPrefix) ? "[ZSZ]" : config.ChatPrefix.Trim();
        _testMode = config.TestMode.Enabled;
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        Cvars = new CvarManager(this);
        Captains = new CaptainService(this);
        Warmup = new WarmupController(this);
        Knife = new KnifeRoundController(this);
        SidePick = new SidePickController(this);
        Stats = new StatsTracker(this);

        RegisterEvents();

        RegisterListener<Listeners.OnMapStart>(OnMapStart);

        RegisterCommands();

        Logger.LogInformation("[ZSZ] Loaded (test mode: {TestMode}, hot reload: {HotReload}).", _testMode, hotReload);

        // A match has to be started whenever the plugin shows up on a map that is already running:
        // a first load after the map started, or a reload that happens to catch the engine still in
        // warmup. A reload in the middle of a real match is the one case that must NOT restart
        // anything - swapping the DLL should never wipe the score (see README section 4).
        // If the engine state cannot be read (EngineInWarmup() == null) a genuine first load still
        // starts a match; only a reload is treated as "a match is probably already running".
        if (!string.IsNullOrEmpty(Server.MapName))
        {
            if (EngineInWarmup() == true || !hotReload)
            {
                BeginNewMatch(announce: true);
            }
            else
            {
                ResumeAfterReload();
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        Warmup.Stop();
        SidePick.Cancel();
        Knife.Cancel();

        foreach (var timer in Timers.ToList())
        {
            timer.Kill();
        }
    }

    /// <summary>
    /// True while the engine itself is still in warmup, false once it is not, and null when the
    /// game rules cannot be read at all. That is the only moment it is safe to begin a fresh match,
    /// because no scored round is in progress yet.
    /// </summary>
    private bool? EngineInWarmup()
    {
        try
        {
            return GameRules()?.WarmupPeriod;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[ZSZ] Could not read the engine warmup state: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// A reload cannot carry over the scoreboard, the captains or the stats - they lived in the
    /// old instance. Carry on as if we had been there all along so stats and announcements keep
    /// working, and leave the engine's own match completely untouched.
    /// </summary>
    private void ResumeAfterReload()
    {
        State.Phase = MatchPhase.Live;
        State.MatchStartTime = Server.CurrentTime;
        State.MapName = Server.MapName;

        // A reload during the side pick would otherwise inherit ApplySidePickHold's mp_freezetime
        // hold, mp_buytime 0 and mp_startmoney 0 until the next round_start puts the match cvars
        // back, stranding the players in one long, money-less round. Pushing the real match cvars
        // now drops the hold; it does not restart or score anything.
        Cvars.ApplyMatch(full: true);

        Chat.Warn(Lang.ReloadedMidMatch);

        Logger.LogWarning(
            "[ZSZ] Reloaded while a match was already running. The engine was left alone, so the " +
            "score survives, but the plugin's own captains and per round stats were lost with the " +
            "old instance. Use css_zsz_reset if you want to start the match over.");
    }

    private void RegisterEvents()
    {
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
        RegisterEventHandler<EventTeamScore>(OnTeamScore);
        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
        RegisterEventHandler<EventItemPurchase>(OnItemPurchase);
        RegisterEventHandler<EventItemPickup>(OnItemPickup);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventCsWinPanelMatch>(OnMatchEnd);
    }

    private void RegisterCommands()
    {
        // Player commands.
        AddCommand("css_kapitan", "Zostan kapitanem swojej druzyny", Captains.OnKapitanCommand);
        AddCommand("css_captain", "Become the captain of your team", Captains.OnKapitanCommand);
        AddCommand("css_stats", "Statystyki meczu", OnStatsCommand);
        AddCommand("css_wynik", "Statystyki meczu", OnStatsCommand);
        AddCommand("css_pickct", "Wybierz strone CT jako kapitan", (p, i) => SidePick.OnPickCommand(p, i, CsTeam.CounterTerrorist));
        AddCommand("css_pickt", "Wybierz strone T jako kapitan", (p, i) => SidePick.OnPickCommand(p, i, CsTeam.Terrorist));
        AddCommand("css_zsz_status", "Status meczu", OnStatusCommand);

        // Admin commands (server console, or a player in test mode, or @css/root).
        AddCommand("css_zsz_skipwarmup", "Pomin rozgrzewke", OnSkipWarmupCommand);
        AddCommand("css_zsz_forcestart", "Wymus start meczu", OnForceStartCommand);
        AddCommand("css_zsz_reset", "Reset meczu (powrot do rozgrzewki)", OnResetCommand);
        AddCommand("css_zsz_pick", "Wymus wybor strony: css_zsz_pick ct|t", OnForcePickCommand);
        AddCommand("css_zsz_cvars", "Ponownie zastosuj cVary", OnApplyCvarsCommand);
        AddCommand("css_zsz_bots", "Dodaj boty do pelnego skladu", OnBotsCommand);
        AddCommand("css_zsz_testmode", "Przelacz tryb testowy", OnTestModeCommand);
        AddCommand("css_zsz_reloadconfig", "Przeladuj ZSZMatch.json", OnReloadConfigCommand);
    }

    // ------------------------------------------------------------------ flow

    /// <summary>Everything is reset and the 1:45 warmup starts again.</summary>
    internal void BeginNewMatch(bool announce = true)
    {
        State.Reset();
        Stats.Reset();

        _testMode = Config.TestMode.Enabled;
        SidePick.Cancel();

        Warmup.Begin(announce);

        Logger.LogInformation("[ZSZ] New match on {Map}: warmup {Long}s (short {Short}s, {Required} players).",
            Server.MapName, Config.Warmup.LongWarmupSeconds, Config.Warmup.ShortWarmupSeconds, RequiredPlayers);
    }

    /// <summary>The warmup clock hit zero: hand over to the knife round (or straight to the match).</summary>
    internal void OnWarmupFinished()
    {
        if (!State.Phase.IsWarmup())
        {
            return;
        }

        Warmup.Finish();

        if (Config.KnifeRound.Enabled)
        {
            Knife.Begin();
            return;
        }

        Logger.LogInformation("[ZSZ] Knife round disabled - starting the match directly.");
        StartLiveMatch(null, null);
    }

    /// <summary>
    /// The real match: swap sides if the winning captain picked the other side, push the
    /// FACEIT cvars, restart the score to 0-0 and spam the tournament announcement.
    /// </summary>
    internal void StartLiveMatch(CsTeam? knifeWinner, CsTeam? chosenSide)
    {
        // The warmup is deliberately pinned open by the plugin (mp_warmup_pausetimer 1), so it will
        // never end by itself. Whoever gets here - the knife round, a map without one, or an admin
        // forcing the start - has to close it, otherwise the "live" match runs inside a warmup:
        // no scored rounds, and every warmup round gets counted as a match round.
        EndEngineWarmup();

        if (knifeWinner.HasValue && chosenSide.HasValue && knifeWinner.Value != chosenSide.Value)
        {
            SwapTeams();
            Chat.All(Lang.SidesSwapped);
        }

        State.Phase = MatchPhase.Live;
        State.MatchStartTime = Server.CurrentTime;

        // The knife round is over and done with; forget its result so nothing can replay it.
        State.KnifeRoundWinner = null;
        State.ChosenSide = null;

        Cvars.ApplyMatch(full: true);

        // Reset the scoreboard: the knife round never happened as far as the match is concerned.
        Server.ExecuteCommand("mp_restartgame 3");

        Stats.Reset();

        Logger.LogInformation("[ZSZ] Match live. Knife winner: {Winner}, chosen side: {Side}.",
            knifeWinner?.ToString() ?? "none", chosenSide?.ToString() ?? "none");

        // Give the restart time to happen, then announce.
        AddTimer(3.5f, () =>
        {
            // The whole announcement chain is three timers deep, so a reset (or a map change, or a
            // second forcestart) landing in the middle of it must not spray "[ZSZ] LIVE!" and the
            // good luck message over a fresh warmup.
            if (State.Phase != MatchPhase.Live)
            {
                return;
            }

            Chat.Alert(Lang.MatchStarting);
            SpamLive(() => AddTimer(1.5f, () =>
            {
                if (State.Phase == MatchPhase.Live)
                {
                    Chat.All(Config.Announcements.GoodLuckMessage);
                }
            }));
        });
    }

    /// <summary>
    /// "[ZSZ] LIVE!" a few times in a row, exactly like the tournament asked for. Called both when
    /// the knife round starts and when the real match starts, so it must not assume either phase.
    /// </summary>
    internal void SpamLive(Action? onFinished = null)
    {
        var count = Math.Clamp(Config.Announcements.LiveSpamCount, 1, 20);
        var interval = Math.Clamp(Config.Announcements.LiveSpamIntervalSeconds, 0.1f, 10f);

        // The phase this spam belongs to (KnifeRound or Live). If it changes underneath us the match
        // was reset / restarted / the map changed, and spraying "LIVE!" into a fresh warmup would
        // only confuse everybody.
        var expectedPhase = State.Phase;

        if (Config.Announcements.LiveCenterAlert)
        {
            Chat.CenterAlertAll(Config.Announcements.LiveMessage);
        }

        for (var i = 0; i < count; i++)
        {
            var isLast = i == count - 1;

            AddTimer(interval * (i + 1), () =>
            {
                if (State.Phase != expectedPhase)
                {
                    return;
                }

                Chat.Alert(Config.Announcements.LiveMessage);

                if (isLast)
                {
                    onFinished?.Invoke();
                }
            });
        }
    }

    private void SwapTeams()
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid)
            {
                continue;
            }

            switch (player.Team)
            {
                case CsTeam.Terrorist:
                    player.SwitchTeam(CsTeam.CounterTerrorist);
                    break;
                case CsTeam.CounterTerrorist:
                    player.SwitchTeam(CsTeam.Terrorist);
                    break;
            }
        }
    }

    // ------------------------------------------------------------------ helpers used by the controllers

    /// <summary>How many players have to be on a team before the short warmup is triggered.</summary>
    internal int RequiredPlayers =>
        Math.Max(1, _testMode ? Config.TestMode.PlayersRequired : Config.Warmup.PlayersRequired);

    internal int CountReadyPlayers()
    {
        var count = 0;

        foreach (var player in Utilities.GetPlayers())
        {
            if (player.IsHLTV)
            {
                continue;
            }

            if (player.IsBot && !Config.Warmup.CountBots)
            {
                continue;
            }

            var onTeam = player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist;
            if (!onTeam && !Config.Warmup.CountSpectators)
            {
                continue;
            }

            count++;
        }

        return count;
    }

    internal ulong? SteamIdOf(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid || player.IsBot)
        {
            return null;
        }

        var authorized = player.AuthorizedSteamID;
        if (authorized != null && authorized.SteamId64 != 0)
        {
            return authorized.SteamId64;
        }

        var raw = player.SteamID;
        return raw == 0 ? null : raw;
    }

    internal string StatsIdOf(CCSPlayerController player)
    {
        var steamId = SteamIdOf(player);
        return steamId.HasValue
            ? steamId.Value.ToString(CultureInfo.InvariantCulture)
            : $"bot:{player.Slot}";
    }

    internal CCSPlayerController? FindPlayerBySteamId(ulong steamId)
        => Utilities.GetPlayers().FirstOrDefault(p => SteamIdOf(p) == steamId);

    internal CCSGameRules? GameRules()
    {
        try
        {
            var proxy = Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").FirstOrDefault();
            return proxy?.GameRules;
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[ZSZ] Could not read the game rules: {Message}", ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Reads the live scoreboard out of the game itself, so halftime swaps are handled for us.
    /// Three layers, cheapest and most authoritative first:
    ///   1. the team manager entity (exactly what the engine draws on the scoreboard),
    ///   2. the engine's own team_score event, cached,
    ///   3. rounds we counted ourselves from round_end.
    /// </summary>
    internal (int Ct, int T) GetTeamScores()
    {
        var ct = -1;
        var t = -1;

        try
        {
            foreach (var team in Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager"))
            {
                if (!team.IsValid)
                {
                    continue;
                }

                switch ((CsTeam)team.TeamNum)
                {
                    case CsTeam.CounterTerrorist:
                        ct = team.Score;
                        break;
                    case CsTeam.Terrorist:
                        t = team.Score;
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning("[ZSZ] Could not read the team scores: {Message}", ex.Message);
        }

        if (ct >= 0 && t >= 0)
        {
            return (ct, t);
        }

        // Layer 2: the engine told us through team_score. Still engine authoritative.
        if (State.EventScoreSeen)
        {
            return (State.EventCtScore, State.EventTScore);
        }

        // Layer 3: our own tally, kept from round_end.
        return (State.FallbackCtRounds, State.FallbackTRounds);
    }

    /// <summary>
    /// Keeps an engine authoritative, side based score cached, so a failed team manager read
    /// never makes the plugin display a stale or wrong scoreboard.
    /// </summary>
    private HookResult OnTeamScore(EventTeamScore @event, GameEventInfo info)
    {
        switch ((CsTeam)@event.Teamid)
        {
            case CsTeam.CounterTerrorist:
                State.EventCtScore = @event.Score;
                State.EventScoreSeen = true;
                break;
            case CsTeam.Terrorist:
                State.EventTScore = @event.Score;
                State.EventScoreSeen = true;
                break;
            default:
                return HookResult.Continue;
        }

        // A fresh match starts at 0:0, so once the engine reports a score for both sides we
        // no longer need the counted fallback at all.
        State.FallbackCtRounds = State.EventCtScore;
        State.FallbackTRounds = State.EventTScore;

        return HookResult.Continue;
    }

    /// <summary>Makes sure the engine is in a warmup it cannot end by itself.</summary>
    internal void EnsureEngineWarmup()
    {
        var rules = GameRules();
        if (rules == null)
        {
            return;
        }

        if (!rules.WarmupPeriod)
        {
            Server.ExecuteCommand("mp_warmup_start");
            AddTimer(0.5f, () => Cvars.ApplyWarmup());
        }
    }

    /// <summary>Ends the engine warmup and makes sure it really ended.</summary>
    internal void EndEngineWarmup()
    {
        State.WarmupEndPending = true;
        State.WarmupEndRetries = 0;

        Server.ExecuteCommand("mp_warmup_end");
        AddTimer(1.5f, VerifyWarmupEnded);
    }

    private void VerifyWarmupEnded()
    {
        var rules = GameRules();

        if (rules == null || !rules.WarmupPeriod)
        {
            State.WarmupEndPending = false;
            return;
        }

        State.WarmupEndRetries++;

        if (State.WarmupEndRetries == 1)
        {
            Logger.LogWarning("[ZSZ] mp_warmup_end did not take effect - letting the engine timer expire.");
            Server.ExecuteCommand("mp_warmup_pausetimer 0");
            Server.ExecuteCommand("mp_warmuptime 1");
            AddTimer(2.0f, VerifyWarmupEnded);
            return;
        }

        if (State.WarmupEndRetries == 2)
        {
            Server.ExecuteCommand("mp_warmup_end");
            AddTimer(2.0f, VerifyWarmupEnded);
            return;
        }

        // Nothing more we can do - mp_warmup_end should never be blocked, but if it is, the
        // match will simply start from the warmup round instead of getting stuck here.
        Logger.LogError("[ZSZ] The warmup is still active. Check for another plugin or config blocking mp_warmup_end.");
        State.WarmupEndPending = false;
    }

    // ------------------------------------------------------------------ events

    private void OnMapStart(string mapName)
    {
        State.MapName = mapName;
        BeginNewMatch(announce: true);
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (State.Phase.IsWarmup())
        {
            Warmup.EnsureRunning();
            return HookResult.Continue;
        }

        switch (State.Phase)
        {
            case MatchPhase.KnifeRound:
                Knife.OnRoundStart();
                break;

            case MatchPhase.Live:
                Stats.SyncTeams();

                if (Config.Ruleset.EnforceCvarsEveryRound)
                {
                    Cvars.ApplyMatch(full: false);
                }

                break;
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        // The plugin issues mp_restartgame itself (match start, side pick, knife replay), and the
        // engine reports that as a round_end with GameCommencing. Without this guard every restart
        // would be booked as a round that was actually played: a phantom "RUNDA 1" with no stats,
        // a bumped fallback score, and - during the knife round - an extra round to replay.
        if (@event.Reason == (int)RoundEndReason.GameCommencing)
        {
            return HookResult.Continue;
        }

        switch (State.Phase)
        {
            case MatchPhase.KnifeRound:
                Knife.HandleRoundEnd(@event.Winner);
                break;

            case MatchPhase.Live:
                TrackTeamScore(@event.Winner);
                Stats.OnRoundEnd(@event);
                break;
        }

        return HookResult.Continue;
    }

    /// <summary>
    /// Fallback scoreboard for the (unlikely) case where cs_team_manager cannot be read.
    /// It also gives the plugin something sensible to print at the very first round end.
    /// </summary>
    private void TrackTeamScore(int winner)
    {
        switch ((CsTeam)winner)
        {
            case CsTeam.CounterTerrorist:
                State.FallbackCtRounds++;
                break;
            case CsTeam.Terrorist:
                State.FallbackTRounds++;
                break;
        }
    }

    private HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player == null || !player.IsValid)
        {
            return HookResult.Continue;
        }

        if (State.Phase == MatchPhase.KnifeRound)
        {
            Knife.OnPlayerSpawn(player);
        }
        else if (State.Phase == MatchPhase.Live && Config.Stats.Enabled)
        {
            Stats.Ensure(player);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        Stats.OnDeath(@event);
        return HookResult.Continue;
    }

    private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
    {
        Stats.OnHurt(@event);
        return HookResult.Continue;
    }

    private HookResult OnItemPurchase(EventItemPurchase @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player != null && player.IsValid)
        {
            Knife.OnItemPurchase(player, @event.Weapon);
        }

        return HookResult.Continue;
    }

    private HookResult OnItemPickup(EventItemPickup @event, GameEventInfo info)
    {
        var player = @event.Userid;

        if (player != null && player.IsValid)
        {
            Knife.OnItemPickup(player, @event.Item);
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (State.Phase.IsWarmup())
        {
            Warmup.EnsureRunning();
        }

        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        Captains.OnPlayerDisconnected(@event.Userid, @event.Xuid);
        return HookResult.Continue;
    }

    private HookResult OnMatchEnd(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        if (State.Phase != MatchPhase.Live)
        {
            return HookResult.Continue;
        }

        State.Phase = MatchPhase.MatchEnded;

        Chat.Alert(Lang.MatchEnded);
        Stats.PrintScoreboard(null);

        Logger.LogInformation("[ZSZ] Match finished after {Rounds} rounds.", State.LiveRoundsPlayed);

        return HookResult.Continue;
    }

    // ------------------------------------------------------------------ player commands

    private void OnStatsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Config.Stats.Enabled || !State.StatsAvailable)
        {
            if (player != null && player.IsValid)
            {
                Chat.WarnTo(player, Lang.StatsNoData);
            }
            else
            {
                info.ReplyToCommand($"{Chat.Prefix} {Lang.StatsNoData}");
            }

            return;
        }

        Stats.PrintScoreboard(player);
    }

    private void OnStatusCommand(CCSPlayerController? player, CommandInfo info)
    {
        var (ct, t) = GetTeamScores();

        var text = string.Format(Lang.StatusFormat,
            State.Phase.Describe(),
            CountReadyPlayers(),
            RequiredPlayers,
            Captains.CaptainNames(),
            State.LiveRoundsPlayed,
            ct,
            t);

        if (player != null && player.IsValid)
        {
            Chat.To(player, text);
        }
        else
        {
            info.ReplyToCommand($"{Chat.Prefix} {text}");
        }
    }

    // ------------------------------------------------------------------ admin commands

    private void OnSkipWarmupCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        if (!State.Phase.IsWarmup())
        {
            Reply(player, info, Lang.WarmupSkipped);
            return;
        }

        Warmup.EndNow(Lang.WarmupSkipped);
    }

    private void OnForceStartCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        // StartLiveMatch swaps the sides and restarts the score to 0-0, so running it twice, or
        // running it while the match is already going, would throw away the running match.
        if (State.Phase is MatchPhase.Live or MatchPhase.MatchEnded)
        {
            Reply(player, info, Lang.ForceStartNotNow);
            return;
        }

        Warmup.Finish();
        SidePick.Cancel();
        Knife.Cancel();

        Chat.Alert(Lang.ForceStartDone);
        StartLiveMatch(State.KnifeRoundWinner, State.ChosenSide);
    }

    private void OnResetCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        Chat.Alert(Lang.MatchReset);
        BeginNewMatch(announce: false);
    }

    private void OnForcePickCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        var argument = info.ArgCount > 1 ? info.GetArg(1).Trim().ToLowerInvariant() : string.Empty;

        var side = argument switch
        {
            "ct" or "counterterrorist" or "counter-terrorist" or "c" or "3" => CsTeam.CounterTerrorist,
            "t" or "terrorist" or "tt" or "2" => CsTeam.Terrorist,
            _ => CsTeam.None,
        };

        if (side == CsTeam.None)
        {
            Reply(player, info, string.Format(Lang.UnknownSideFormat, argument));
            return;
        }

        if (SidePick.ForceChoose(side))
        {
            return;
        }

        Reply(player, info, Lang.SidePickNotNow);
    }

    private void OnApplyCvarsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        switch (State.Phase)
        {
            case MatchPhase.WarmupLong:
            case MatchPhase.WarmupShort:
                Cvars.ApplyWarmup();
                break;
            case MatchPhase.KnifeRound:
                Cvars.ApplyKnifeRound();
                break;
            case MatchPhase.SidePick:
                Cvars.ApplySidePickHold(Math.Max(Config.SidePick.HoldFreezeTime, Config.SidePick.TimeoutSeconds + 15));
                break;
            default:
                Cvars.ApplyMatch(full: true);
                break;
        }

        Reply(player, info, Lang.CvarsReapplied);
    }

    private void OnBotsCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        var target = Math.Max(2, Config.Warmup.PlayersRequired);
        var needed = target - CountReadyPlayers();

        if (needed <= 0)
        {
            Reply(player, info, string.Format(Lang.BotsAddedFormat, CountReadyPlayers()));
            return;
        }

        for (var i = 0; i < needed; i++)
        {
            Server.ExecuteCommand(i % 2 == 0 ? "bot_add_t" : "bot_add_ct");
        }

        // Bots take a moment to actually appear, so report the count afterwards.
        AddTimer(0.5f, () => Chat.Warn(string.Format(Lang.BotsAddedFormat, CountReadyPlayers())));
    }

    private void OnTestModeCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        _testMode = !_testMode;

        var message = _testMode
            ? string.Format(Lang.TestModeOnFormat, RequiredPlayers)
            : Lang.TestModeOff;

        Chat.Alert(message);
    }

    private void OnReloadConfigCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (!Authorize(player, info))
        {
            return;
        }

        Config.Reload();
        Chat.Prefix = string.IsNullOrWhiteSpace(Config.ChatPrefix) ? "[ZSZ]" : Config.ChatPrefix.Trim();

        Reply(player, info, Lang.ConfigReloaded);
    }

    /// <summary>Server console, test mode, or a CSS admin with @css/root.</summary>
    private bool Authorize(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
        {
            return true;
        }

        if (_testMode)
        {
            return true;
        }

        if (AdminManager.PlayerHasPermissions(player, "@css/root"))
        {
            return true;
        }

        Chat.WarnTo(player, Lang.NoPermission);
        return false;
    }

    private static void Reply(CCSPlayerController? player, CommandInfo info, string message)
    {
        if (player != null && player.IsValid)
        {
            Chat.To(player, message);
        }
        else
        {
            info.ReplyToCommand($"{Chat.Prefix} {message}");
        }
    }
}
