using System.Globalization;
using System.Text;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using ZSZMatch.Util;

namespace ZSZMatch.Core;

/// <summary>
/// FACEIT style statistics: a compact per-team summary in chat plus a centre-screen panel
/// at the end of every round, and a full !stats scoreboard on demand.
///
/// Kill / assist / death / headshot / damage are all taken from the game events, so the
/// numbers always agree with the CS2 scoreboard. ADR is damage per live (non knife,
/// non warmup) round.
/// </summary>
public sealed class StatsTracker
{
    private readonly ZSZMatchPlugin _plugin;

    public StatsTracker(ZSZMatchPlugin plugin) => _plugin = plugin;

    private MatchState State => _plugin.State;
    private Config.StatsSettings Settings => _plugin.Config.Stats;

    private bool Tracking => Settings.Enabled && State.Phase == MatchPhase.Live;

    private static string Num(double value) => value.ToString("0.0", CultureInfo.InvariantCulture);

    private static string Truncate(string text, int length)
        => text.Length <= length ? text : text[..Math.Max(1, length - 1)] + ".";

    // ------------------------------------------------------------------ tracking

    public PlayerMatchStats? GetStats(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
        {
            return null;
        }

        var id = _plugin.StatsIdOf(player);
        return State.Stats.TryGetValue(id, out var stats) ? stats : null;
    }

    public PlayerMatchStats Ensure(CCSPlayerController player)
    {
        var id = _plugin.StatsIdOf(player);

        if (State.Stats.TryGetValue(id, out var existing))
        {
            existing.Name = player.PlayerName;
            existing.Team = player.Team;
            return existing;
        }

        var created = new PlayerMatchStats(id, _plugin.SteamIdOf(player), player.PlayerName, player.Team);
        State.Stats[id] = created;

        return created;
    }

    /// <summary>
    /// Damage is counted from player_hurt. The killing blow is reported through player_hurt
    /// too, so nothing is missed and nothing is double counted.
    /// </summary>
    public void OnHurt(EventPlayerHurt @event)
    {
        if (!Tracking || !Settings.TrackDamage)
        {
            return;
        }

        var victim = @event.Userid;
        if (victim == null || !victim.IsValid)
        {
            return;
        }

        // Only damage between enemies counts towards ADR (friendly fire does not).
        if (!IsEnemyOf(victim, @event.Attacker))
        {
            return;
        }

        var damage = Math.Max(0, @event.DmgHealth) + Math.Max(0, @event.DmgArmor);
        if (damage <= 0)
        {
            return;
        }

        var stats = Ensure(@event.Attacker!);
        stats.Damage += damage;
        stats.RoundDamage += damage;
    }

    public void OnDeath(EventPlayerDeath @event)
    {
        if (!Tracking)
        {
            return;
        }

        var victim = @event.Userid;
        if (victim == null || !victim.IsValid)
        {
            return;
        }

        Ensure(victim).RoundDeaths++;

        var attacker = @event.Attacker;
        if (attacker == null || !attacker.IsValid || !IsEnemyOf(victim, attacker))
        {
            return;
        }

        var killer = Ensure(attacker);
        killer.RoundKills++;

        if (@event.Headshot)
        {
            killer.Headshots++;
        }

        var assister = @event.Assister;
        if (assister != null && assister.IsValid && IsEnemyOf(victim, assister))
        {
            Ensure(assister).RoundAssists++;
        }
    }

    /// <summary>Round is over: fold the round counters into the match totals and report.</summary>
    public void OnRoundEnd(EventRoundEnd @event)
    {
        if (!Tracking)
        {
            return;
        }

        State.LiveRoundsPlayed++;

        var rounds = Math.Max(1, State.LiveRoundsPlayed);

        foreach (var stats in State.Stats.Values)
        {
            stats.Kills += stats.RoundKills;
            stats.Assists += stats.RoundAssists;
            stats.Deaths += stats.RoundDeaths;
        }

        var reason = Lang.RoundEndReasonName(@event.Reason);

        if (Settings.ChatSummary)
        {
            PrintRoundChat(@event, reason, rounds);
        }

        if (Settings.HudPanel)
        {
            Chat.CenterAll(BuildRoundHud(@event, reason, rounds), Math.Max(2, Settings.HudDurationSeconds));
        }

        ResetRoundCounters();
    }

    public void ResetRoundCounters()
    {
        foreach (var stats in State.Stats.Values)
        {
            stats.ResetRound();
        }
    }

    public void Reset()
    {
        State.Stats.Clear();
        State.LiveRoundsPlayed = 0;

        // Called right after mp_restartgame, so the scoreboard is about to be 0:0. Drop both
        // cached scores too, otherwise a stale number could survive into the new match header.
        State.FallbackCtRounds = 0;
        State.FallbackTRounds = 0;
        State.EventCtScore = 0;
        State.EventTScore = 0;
        State.EventScoreSeen = false;
    }

    /// <summary>Drops players that are no longer on a team (e.g. they moved to spectators).</summary>
    public void SyncTeams()
    {
        foreach (var player in CounterStrikeSharp.API.Utilities.GetPlayers())
        {
            var stats = GetStats(player);
            if (stats != null)
            {
                stats.Name = player.PlayerName;
                stats.Team = player.Team;
            }
        }
    }

    // ------------------------------------------------------------------ reporting

    public List<PlayerMatchStats> ForTeam(CsTeam team)
        => State.Stats.Values.Where(s => s.Team == team).ToList();

    private void PrintRoundChat(EventRoundEnd @event, string reason, int rounds)
    {
        var (ctScore, tScore) = _plugin.GetTeamScores();

        Chat.All(string.Format(Lang.RoundHeaderFormat, rounds, ctScore, tScore, reason));

        PrintTeamLine(CsTeam.CounterTerrorist, rounds);
        PrintTeamLine(CsTeam.Terrorist, rounds);

        var mvp = State.Stats.Values
            .Where(s => s.RoundKills > 0 || s.RoundDamage > 0)
            .OrderByDescending(s => s.RoundKills)
            .ThenByDescending(s => s.RoundDamage)
            .FirstOrDefault();

        if (mvp != null)
        {
            Chat.All(string.Format(Lang.MvpFormat, mvp.Name, mvp.RoundKills, mvp.RoundDamage));
        }
    }

    private void PrintTeamLine(CsTeam team, int rounds)
    {
        var players = ForTeam(team);
        if (players.Count == 0)
        {
            return;
        }

        var kills = players.Sum(p => p.Kills);
        var assists = players.Sum(p => p.Assists);
        var deaths = players.Sum(p => p.Deaths);
        var damage = players.Sum(p => p.Damage);

        // ADR is per player, so a team of five adds up to five times the damage per round.
        var adr = damage / (double)(rounds * Math.Max(1, players.Count));

        Chat.All(string.Format(Lang.TeamStatsLineFormat,
            Lang.TeamName(team), kills, assists, deaths, damage, Num(adr)));
    }

    private string BuildRoundHud(EventRoundEnd @event, string reason, int rounds)
    {
        var (ctScore, tScore) = _plugin.GetTeamScores();

        var html = new StringBuilder();
        html.Append(Chat.Bold(Chat.Color("gold",
            $"{Chat.Prefix} {Lang.HudRoundTitle} {rounds}"))).Append("<br>");

        html.Append(Chat.Bold(Chat.Color("lightblue", $"CT {ctScore}")));
        html.Append(Chat.Color("white", " : "));
        html.Append(Chat.Bold(Chat.Color("gold", $"{tScore} T")));
        html.Append(Chat.Color("white", $"   {reason}")).Append("<br>");

        AppendTeamHud(html, CsTeam.CounterTerrorist, "lightblue", rounds);
        AppendTeamHud(html, CsTeam.Terrorist, "gold", rounds);

        return html.ToString();
    }

    private void AppendTeamHud(StringBuilder html, CsTeam team, string color, int rounds)
    {
        var players = ForTeam(team)
            .OrderByDescending(p => p.Adr(rounds))
            .ThenByDescending(p => p.Kills)
            .Take(Math.Max(1, Settings.HudTopPlayersPerTeam))
            .ToList();

        if (players.Count == 0)
        {
            return;
        }

        html.Append(Chat.Line(Chat.Bold(Chat.Color(color, Lang.TeamNameLong(team)))));

        foreach (var player in players)
        {
            var isCaptain = player.SteamId64.HasValue && State.FindCaptain(player.SteamId64.Value) != null;
            var mark = isCaptain ? Lang.CaptainMark + " " : "  ";

            html.Append(Chat.Color(color, mark + Truncate(player.Name, 14))).Append(' ');
            html.Append(Chat.Color("white",
                $"Z {player.Kills}  A {player.Assists}  Ś {player.Deaths}")).Append(' ');
            html.Append(Chat.Color("grey", $"ADR {Num(player.Adr(rounds))}")).Append("<br>");
        }
    }

    /// <summary>Full match scoreboard, both the chat version and the HUD version.</summary>
    public void PrintScoreboard(CCSPlayerController? to)
    {
        var rounds = Math.Max(1, State.LiveRoundsPlayed);
        var (ctScore, tScore) = _plugin.GetTeamScores();

        var header = string.Format(Lang.MatchStatsHeaderFormat, ctScore, tScore, State.LiveRoundsPlayed);

        if (to != null && to.IsValid)
        {
            Chat.To(to, header);
            PrintTeamScoreboard(to, CsTeam.CounterTerrorist, rounds);
            PrintTeamScoreboard(to, CsTeam.Terrorist, rounds);
        }
        else
        {
            Chat.All(header);
            PrintTeamScoreboard(null, CsTeam.CounterTerrorist, rounds);
            PrintTeamScoreboard(null, CsTeam.Terrorist, rounds);
        }
    }

    private void PrintTeamScoreboard(CCSPlayerController? to, CsTeam team, int rounds)
    {
        var players = ForTeam(team)
            .OrderByDescending(p => p.Kills)
            .ThenByDescending(p => p.Adr(rounds))
            .ToList();

        if (players.Count == 0)
        {
            return;
        }

        var line = new StringBuilder();
        line.Append(Chat.Color(team == CsTeam.CounterTerrorist ? "lightblue" : "gold", Lang.TeamNameLong(team)));
        line.Append(ChatColors.Default).Append(":");

        foreach (var player in players)
        {
            var isCaptain = player.SteamId64.HasValue && State.FindCaptain(player.SteamId64.Value) != null;
            var mark = isCaptain ? Lang.CaptainMark : string.Empty;

            line.Append(" ").Append(ChatColors.Lime).Append(mark).Append(Truncate(player.Name, 12))
                .Append(ChatColors.Default)
                .Append($" ({player.Kills}/{player.Assists}/{player.Deaths} | Ś {player.Headshots} | DMG {player.Damage} | ADR {Num(player.Adr(rounds))})");
        }

        if (to != null && to.IsValid)
        {
            to.PrintToChat($" {line}");
        }
        else
        {
            CounterStrikeSharp.API.Server.PrintToChatAll($" {line}");
        }
    }

    private static bool IsEnemyOf(CCSPlayerController victim, CCSPlayerController? attacker)
    {
        if (attacker == null || !attacker.IsValid || attacker == victim)
        {
            return false;
        }

        var attackerTeam = attacker.Team;
        var victimTeam = victim.Team;

        if (attackerTeam is not (CsTeam.Terrorist or CsTeam.CounterTerrorist)
            || victimTeam is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
        {
            return false;
        }

        return attackerTeam != victimTeam;
    }
}
