using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ZSZMatch.Util;

namespace ZSZMatch.Core;

/// <summary>
/// Owns the "!kapitan" flow. One captain per side, locked in as soon as the knife round
/// starts - from that point on the only way to change it is css_zsz_reset.
/// Captaincy is stored by SteamID64, so it survives the side switch that happens
/// right before the live match (and any death / respawn in between).
/// </summary>
public sealed class CaptainService
{
    private readonly ZSZMatchPlugin _plugin;

    public CaptainService(ZSZMatchPlugin plugin) => _plugin = plugin;

    private MatchState State => _plugin.State;

    /// <summary>The captain entry of this player, or null if they are not a captain.</summary>
    public Captain? GetCaptain(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid)
        {
            return null;
        }

        var steamId = _plugin.SteamIdOf(player);
        return steamId.HasValue ? State.FindCaptain(steamId.Value) : null;
    }

    public bool IsCaptain(CCSPlayerController? player) => GetCaptain(player) != null;

    /// <summary>The captain currently sitting on the given side, if any.</summary>
    public Captain? GetCaptainOnTeam(CsTeam team)
    {
        foreach (var captain in State.Captains)
        {
            var controller = _plugin.FindPlayerBySteamId(captain.SteamId64);
            if (controller != null && controller.Team == team)
            {
                return captain;
            }
        }

        return null;
    }

    public CCSPlayerController? GetCaptainControllerOnTeam(CsTeam team)
    {
        var captain = GetCaptainOnTeam(team);
        return captain == null ? null : _plugin.FindPlayerBySteamId(captain.SteamId64);
    }

    /// <summary>The captain of the *other* team - used as the side-pick fallback.</summary>
    public Captain? GetOtherCaptain(CsTeam team)
    {
        foreach (var captain in State.Captains)
        {
            var controller = _plugin.FindPlayerBySteamId(captain.SteamId64);
            if (controller != null && controller.Team != team && controller.Team != CsTeam.Spectator)
            {
                return captain;
            }
        }

        return null;
    }

    public string CaptainNames()
    {
        if (State.Captains.Count == 0)
        {
            return Lang.NoCaptainsRegistered;
        }

        return string.Join(", ", State.Captains.Select(c =>
        {
            var controller = _plugin.FindPlayerBySteamId(c.SteamId64);
            var team = controller?.Team ?? c.RegisteredTeam;
            return $"{c.Name} ({Lang.TeamName(team)})";
        }));
    }

    /// <summary>Chat command: !kapitan / !captain / css_kapitan.</summary>
    public void OnKapitanCommand(CCSPlayerController? player, CommandInfo info)
    {
        if (player == null || !player.IsValid)
        {
            info.ReplyToCommand($"{Chat.Prefix} {Lang.MustBeInGame}");
            return;
        }

        if (!State.Phase.CaptainsAllowed())
        {
            Chat.WarnTo(player, Lang.CaptainsLocked);
            return;
        }

        // The knife round is already under way, so a team that never registered is out of time:
        // this is what "no changes afterwards" means, and it stops a random player from grabbing
        // the captaincy just before the side pick - and with it the right to pick the side.
        // CaptainsAllowed() deliberately still lets a *registered* captain be released if they
        // disconnect during the knife round.
        if (State.Phase == MatchPhase.KnifeRound)
        {
            Chat.WarnTo(player, Lang.CaptainsLocked);
            return;
        }

        if (IsCaptain(player))
        {
            Chat.WarnTo(player, Lang.CaptainAlreadyYou);
            return;
        }

        var team = player.Team;
        if (team is not (CsTeam.Terrorist or CsTeam.CounterTerrorist))
        {
            Chat.WarnTo(player, Lang.CaptainNeedTeam);
            return;
        }

        var existing = GetCaptainOnTeam(team);
        if (existing != null)
        {
            Chat.WarnTo(player, string.Format(Lang.CaptainTeamTakenFormat, Lang.TeamName(team), existing.Name));
            return;
        }

        var steamId = _plugin.SteamIdOf(player);
        if (!steamId.HasValue)
        {
            Chat.WarnTo(player, Lang.CaptainSteamIdError);
            return;
        }

        State.Captains.Add(new Captain(steamId.Value, player.PlayerName, team));

        Chat.Alert(string.Format(Lang.CaptainRegisteredFormat, player.PlayerName, Lang.TeamName(team)));
        _plugin.Logger.LogInformation("[ZSZ] {Name} ({Team}) is now captain.", player.PlayerName, Lang.TeamName(team));
    }

    /// <summary>
    /// A captain leaving before the match is live hands the role back, otherwise the
    /// side pick would be stuck without anyone able to make the call.
    ///
    /// Depending on when the engine reports the disconnect the controller may still be
    /// valid, so the actual check is deferred by a second and done against the live
    /// player list.
    /// </summary>
    public void OnPlayerDisconnected(CCSPlayerController? player, ulong xuid)
    {
        var steamId = _plugin.SteamIdOf(player) ?? (xuid == 0 ? null : xuid);

        if (!steamId.HasValue)
        {
            return;
        }

        _plugin.AddTimer(1.0f, () => ReleaseIfGone(steamId.Value));
    }

    private void ReleaseIfGone(ulong steamId)
    {
        var captain = State.FindCaptain(steamId);

        if (captain == null || _plugin.FindPlayerBySteamId(steamId) != null)
        {
            return;
        }

        // The side pick may be waiting on exactly this person, even though the roster is
        // already locked by then.
        if (State.Phase == MatchPhase.SidePick)
        {
            _plugin.SidePick.OnCaptainLost(captain);
            return;
        }

        if (!State.Phase.CaptainsAllowed())
        {
            return;
        }

        State.Captains.Remove(captain);
        Chat.Warn(string.Format(Lang.CaptainLeftFormat, Lang.TeamName(captain.RegisteredTeam), captain.Name));

        _plugin.SidePick.OnCaptainLost(captain);
    }
}
