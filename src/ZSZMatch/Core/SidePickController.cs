using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ZSZMatch.Util;
using CssTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace ZSZMatch.Core;

/// <summary>
/// The knife round winner's captain picks the starting side. While they think, the game is
/// held still with a long freeze time (and no money at all), then the match is restarted to
/// 0-0 with the FACEIT cvars in place.
///
/// The captain was recorded by SteamID during warmup, so the person picking is the same
/// person no matter what happened to the teams in between - which is exactly what
/// "the captain persists after switching" means.
/// </summary>
public sealed class SidePickController
{
    private readonly ZSZMatchPlugin _plugin;

    private CssTimer? _tick;
    private bool _warningSent;

    public SidePickController(ZSZMatchPlugin plugin) => _plugin = plugin;

    private MatchState State => _plugin.State;
    private Config.SidePickSettings Settings => _plugin.Config.SidePick;

    public float Deadline { get; private set; }

    public void Begin(CsTeam winnerTeam)
    {
        State.Phase = MatchPhase.SidePick;
        State.SidePickWinnerTeam = winnerTeam;
        State.SidePickCompleted = false;
        State.SidePickCaptainSteamId = null;
        State.ChosenSide = null;

        _warningSent = false;

        var timeout = Math.Max(5, Settings.TimeoutSeconds);
        var hold = Math.Max(Settings.HoldFreezeTime, timeout + 15);

        // Hold everything still: no movement, no shopping, no round clock.
        _plugin.Cvars.ApplySidePickHold(hold);
        Server.ExecuteCommand("mp_restartgame 1");

        Deadline = Server.CurrentTime + timeout + 1.5f;

        var captain = ResolvePickingCaptain(winnerTeam);
        if (captain == null)
        {
            // Nobody can decide - keep both teams on the sides they are already on.
            Chat.Warn(string.Format(Lang.SidePickNoCaptainFormat, Lang.TeamName(winnerTeam)));
            _plugin.Logger.LogWarning("[ZSZ] No captain available for the side pick. Keeping current sides.");
            Complete(winnerTeam);
            return;
        }

        State.SidePickCaptainSteamId = captain.Value.captain.SteamId64;

        Chat.Alert(string.Format(Lang.SidePickStartedFormat, Lang.TeamName(winnerTeam), timeout));
        _plugin.Logger.LogInformation("[ZSZ] Side pick: {Captain} ({Team}) decides.",
            captain.Value.captain.Name, Lang.TeamName(winnerTeam));

        OpenMenu(captain.Value.controller);
        StartTick();
    }

    /// <summary>The winning team's captain, or - if allowed - the other team's captain as a fallback.</summary>
    private (Captain captain, CCSPlayerController controller)? ResolvePickingCaptain(CsTeam winnerTeam)
    {
        var own = _plugin.Captains.GetCaptainControllerOnTeam(winnerTeam);
        if (own != null)
        {
            return (_plugin.Captains.GetCaptain(own)!, own);
        }

        if (!Settings.AllowOtherCaptainFallback)
        {
            return null;
        }

        var other = _plugin.Captains.GetOtherCaptain(winnerTeam);
        if (other == null)
        {
            return null;
        }

        var controller = _plugin.FindPlayerBySteamId(other.SteamId64);
        if (controller == null)
        {
            return null;
        }

        Chat.Warn(string.Format(Lang.SidePickOtherCaptainFormat,
            Lang.TeamName(winnerTeam), Lang.TeamName(controller.Team)));

        return (other, controller);
    }

    private void OpenMenu(CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu(Lang.SidePickMenuTitle, _plugin)
        {
            PostSelectAction = PostSelectAction.Close,
            ExitButton = false,
        };

        menu.AddMenuOption(Lang.SidePickOptionCt, (p, _) => Choose(CsTeam.CounterTerrorist, p));
        menu.AddMenuOption(Lang.SidePickOptionT, (p, _) => Choose(CsTeam.Terrorist, p));

        MenuManager.OpenCenterHtmlMenu(_plugin, player, menu);
    }

    private void StartTick()
    {
        _tick?.Kill();
        _tick = _plugin.AddTimer(1.0f, Tick, TimerFlags.REPEAT);
    }

    private void StopTick()
    {
        _tick?.Kill();
        _tick = null;
    }

    private void Tick()
    {
        if (State.Phase != MatchPhase.SidePick || State.SidePickCompleted)
        {
            StopTick();
            return;
        }

        var remaining = Deadline - Server.CurrentTime;

        if (remaining <= 0f)
        {
            AutoComplete();
            return;
        }

        if (!_warningSent && remaining <= Math.Max(3, Settings.WarningSeconds))
        {
            _warningSent = true;
            Chat.Warn(string.Format(Lang.SidePickTimeoutWarningFormat, (int)Math.Ceiling(remaining)));
        }
    }

    /// <summary>Chat fallback: !pickct / !pickt (the menu can be missed on a busy screen).</summary>
    public void OnPickCommand(CCSPlayerController? player, CommandInfo info, CsTeam side)
    {
        if (player == null || !player.IsValid)
        {
            info.ReplyToCommand($"{Chat.Prefix} {Lang.MustBeInGame}");
            return;
        }

        if (State.Phase != MatchPhase.SidePick || State.SidePickCompleted
            || !Settings.AllowChatFallbackCommands)
        {
            Chat.WarnTo(player, Lang.SidePickNotNow);
            return;
        }

        var steamId = _plugin.SteamIdOf(player);
        if (steamId == null || steamId.Value != State.SidePickCaptainSteamId)
        {
            Chat.WarnTo(player, Lang.SidePickOnlyCaptain);
            return;
        }

        Choose(side, player);
    }

    /// <summary>Used by the menu, the chat fallback and the admin override.</summary>
    public void Choose(CsTeam side, CCSPlayerController? chooser)
    {
        if (State.Phase != MatchPhase.SidePick || State.SidePickCompleted)
        {
            if (chooser != null && chooser.IsValid)
            {
                Chat.WarnTo(chooser, Lang.SidePickNotNow);
            }

            return;
        }

        if (chooser != null && chooser.IsValid)
        {
            MenuManager.CloseActiveMenu(chooser);
        }

        Chat.Alert(string.Format(Lang.SidePickDoneFormat,
            Lang.TeamName(State.SidePickWinnerTeam ?? CsTeam.None), Lang.TeamName(side)));
        _plugin.Logger.LogInformation("[ZSZ] Side pick: {Side} chosen.", Lang.TeamName(side));

        Complete(side);
    }

    /// <summary>Admin override - css_zsz_pick ct|t.</summary>
    public bool ForceChoose(CsTeam side)
    {
        if (State.Phase != MatchPhase.SidePick || State.SidePickCompleted)
        {
            return false;
        }

        Choose(side, null);
        return true;
    }

    private void AutoComplete()
    {
        var winnerTeam = State.SidePickWinnerTeam ?? CsTeam.None;

        // Staying where they are is the closest thing to "no decision".
        var current = _plugin.Captains.GetCaptainControllerOnTeam(winnerTeam)?.Team ?? winnerTeam;

        Chat.Warn(string.Format(Lang.SidePickAutoFormat, Lang.TeamName(winnerTeam)));
        _plugin.Logger.LogInformation("[ZSZ] Side pick timed out - {Team} keeps its current side.",
            Lang.TeamName(winnerTeam));

        Complete(current);
    }

    private void Complete(CsTeam chosenSide)
    {
        State.ChosenSide = chosenSide;

        StopTick();

        var captain = State.SidePickCaptainSteamId.HasValue
            ? _plugin.FindPlayerBySteamId(State.SidePickCaptainSteamId.Value)
            : null;

        if (captain != null && captain.IsValid)
        {
            MenuManager.CloseActiveMenu(captain);
        }

        State.SidePickCompleted = true;

        _plugin.StartLiveMatch(State.SidePickWinnerTeam, chosenSide);
    }

    /// <summary>
    /// The picking captain disconnected mid-decision: hand the call to the other captain
    /// (or settle for the current sides) instead of leaving the server frozen.
    /// </summary>
    public void OnCaptainLost(Captain captain)
    {
        if (State.Phase != MatchPhase.SidePick || State.SidePickCompleted)
        {
            return;
        }

        if (State.SidePickCaptainSteamId != captain.SteamId64)
        {
            return;
        }

        State.SidePickCaptainSteamId = null;

        var winnerTeam = State.SidePickWinnerTeam ?? CsTeam.None;
        var replacement = ResolvePickingCaptain(winnerTeam);

        if (replacement == null)
        {
            AutoComplete();
            return;
        }

        State.SidePickCaptainSteamId = replacement.Value.captain.SteamId64;
        OpenMenu(replacement.Value.controller);
    }

    /// <summary>Plugin unload / match reset: drop every timer without touching the game state.</summary>
    public void Cancel()
    {
        StopTick();
        State.SidePickCompleted = false;
        State.SidePickCaptainSteamId = null;
    }
}
