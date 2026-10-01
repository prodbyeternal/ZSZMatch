using System.Text;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Modules.Timers;
using Microsoft.Extensions.Logging;
using ZSZMatch.Util;
using CssTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace ZSZMatch.Core;

/// <summary>
/// The 1:45 / 0:25 warmup.
///
/// The engine warmup is deliberately held open with mp_warmup_pausetimer 1 so it can never
/// decide anything on its own; this class owns the clock instead. That makes the two
/// requirements exact:
///   * 1:45 from the moment the map starts,
///   * the moment the 10th player is on a team the clock is cut to 25 seconds.
/// </summary>
public sealed class WarmupController
{
    private readonly ZSZMatchPlugin _plugin;
    private CssTimer? _tick;

    public WarmupController(ZSZMatchPlugin plugin) => _plugin = plugin;

    private MatchState State => _plugin.State;
    private Config.WarmupSettings Settings => _plugin.Config.Warmup;

    /// <summary>Starts (or restarts) the warmup phase. Called on map start and on css_zsz_reset.</summary>
    public void Begin(bool announce = true)
    {
        var seconds = Math.Max(5, Settings.LongWarmupSeconds);

        State.Phase = MatchPhase.WarmupLong;
        State.WaitingForPlayers = false;
        State.NextWaitingNotice = 0f;
        State.WarmupEndPending = false;
        State.WarmupEndRetries = 0;
        State.KnifeAnnounced = false;
        State.WarmupDeadline = Server.CurrentTime + seconds;

        _plugin.Cvars.ApplyWarmup();
        _plugin.EnsureEngineWarmup();

        if (announce)
        {
            Chat.All(string.Format(Lang.WarmupStartedFormat, FormatClock(seconds)));
        }

        StartTick();
        UpdateHud();
    }

    /// <summary>Idempotent: makes sure the clock is running and the engine stays in warmup.</summary>
    public void EnsureRunning()
    {
        if (!State.Phase.IsWarmup())
        {
            return;
        }

        _plugin.Cvars.ApplyWarmup();

        if (State.WarmupDeadline <= 0f)
        {
            Begin(announce: false);
            return;
        }

        StartTick();
    }

    public void StartTick()
    {
        _tick?.Kill();
        _tick = _plugin.AddTimer(1.0f, Tick, TimerFlags.REPEAT);
    }

    public void Stop()
    {
        _tick?.Kill();
        _tick = null;
    }

    /// <summary>Admin "skip the warmup now".</summary>
    public void EndNow(string reason)
    {
        if (!State.Phase.IsWarmup())
        {
            return;
        }

        Chat.Warn(reason);
        _plugin.OnWarmupFinished();
    }

    private void Tick()
    {
        if (!State.Phase.IsWarmup())
        {
            Stop();
            return;
        }

        var now = Server.CurrentTime;
        var connected = _plugin.CountReadyPlayers();
        var required = _plugin.RequiredPlayers;

        // The engine ended the warmup on its own (map change, another plugin, a stray
        // mp_warmup_end): put it back so the flow stays under our control.
        var rules = _plugin.GameRules();
        if (rules != null && !rules.WarmupPeriod && !State.WarmupEndPending)
        {
            _plugin.Logger.LogWarning("[ZSZ] Engine warmup ended unexpectedly - restarting it.");
            _plugin.EnsureEngineWarmup();
        }

        // All players are here: cut 1:45 down to 0:25, exactly once.
        if (State.Phase == MatchPhase.WarmupLong && connected >= required)
        {
            var shortSeconds = Math.Max(5, Settings.ShortWarmupSeconds);
            State.Phase = MatchPhase.WarmupShort;
            State.WaitingForPlayers = false;
            State.WarmupDeadline = now + shortSeconds;

            Chat.All(string.Format(Lang.AllPlayersConnectedFormat, shortSeconds));
            _plugin.Logger.LogInformation("[ZSZ] {Count}/{Required} players connected - warmup shortened to {Short}s.",
                connected, required, shortSeconds);

            UpdateHud();
            return;
        }

        if (State.WarmupDeadline - now > 0f)
        {
            UpdateHud();
            return;
        }

        // Clock is up.
        var mayStart = State.Phase == MatchPhase.WarmupShort
                       || connected >= required
                       || Settings.StartWithoutFullServer;

        if (mayStart)
        {
            _plugin.OnWarmupFinished();
            return;
        }

        // Not enough players and we are told to wait: keep the server parked in warmup
        // and remind everybody why nothing is happening.
        State.WaitingForPlayers = true;
        State.WarmupDeadline = now + Math.Max(10, Settings.WaitingForPlayersNoticeSeconds);

        if (now >= State.NextWaitingNotice)
        {
            State.NextWaitingNotice = now + Math.Max(10, Settings.WaitingForPlayersNoticeSeconds);
            Chat.Warn(string.Format(Lang.WaitingForPlayersFormat, connected, required));
        }

        UpdateHud();
    }

    private void UpdateHud()
    {
        if (!Settings.CountdownHud)
        {
            return;
        }

        var connected = _plugin.CountReadyPlayers();
        var required = _plugin.RequiredPlayers;
        var remaining = Math.Max(0f, State.WarmupDeadline - Server.CurrentTime);

        var html = new StringBuilder();
        html.Append(Chat.Line(Chat.Bold(Chat.Color("gold", Lang.WarmupTitle))));

        if (State.WaitingForPlayers)
        {
            html.Append(Chat.Line(Chat.Bold(Chat.Color("white",
                string.Format(Lang.WaitingForPlayersFormat, connected, required)))));
        }
        else
        {
            var color = State.Phase == MatchPhase.WarmupShort ? "lime" : "white";
            html.Append(Chat.Line(Chat.Bold(Chat.Color(color, FormatClock((int)Math.Ceiling(remaining))))));
            html.Append(Chat.Line(Chat.Color("grey", string.Format(Lang.WarmupPlayersFormat, connected, required))));

            if (State.Phase == MatchPhase.WarmupShort)
            {
                html.Append(Chat.Color("lime", Lang.WarmupReadyLine));
            }
        }

        Chat.CenterAll(html.ToString(), 2);
    }

    private static string FormatClock(int totalSeconds)
    {
        var seconds = Math.Max(0, totalSeconds);
        return $"{seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>Called when the warmup is over: the knife round (or the match itself) takes over.</summary>
    public void Finish()
    {
        Stop();
        State.WaitingForPlayers = false;
        State.WarmupDeadline = 0f;
    }
}
