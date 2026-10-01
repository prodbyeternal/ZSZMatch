using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;
using ZSZMatch.Util;

namespace ZSZMatch.Core;

/// <summary>
/// The pre-game knife round.
///
/// Rules enforced here:
///   * knives only - everything else is stripped on spawn, on pickup and on purchase,
///   * full kevlar (mp_free_armor) but no money at all (mp_startmoney 0),
///   * mp_buytime 0, so the buy menu is closed and nobody can buy anything,
///   * the winner earns the right to pick the starting side,
///   * a draw replays the round instead of deadlocking the match.
/// </summary>
public sealed class KnifeRoundController
{
    private readonly ZSZMatchPlugin _plugin;

    public KnifeRoundController(ZSZMatchPlugin plugin) => _plugin = plugin;

    private MatchState State => _plugin.State;
    private Config.KnifeRoundSettings Settings => _plugin.Config.KnifeRound;

    /// <summary>Items that are not weapons and must survive the knife-round clean-up.</summary>
    private static readonly HashSet<string> AllowedItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "item_assaultsuit",
        "item_kevlar",
        "item_cutters",
        "item_defuser",
        "item_healthshot",
    };

    public void Begin()
    {
        State.Phase = MatchPhase.KnifeRound;
        State.KnifeAnnounced = false;
        State.KnifeRoundAttempt = 0;
        State.WaitingForPlayers = false;

        _plugin.Logger.LogInformation("[ZSZ] Warmup finished - starting the knife round.");

        _plugin.Cvars.ApplyKnifeRound();
        _plugin.EndEngineWarmup();
    }

    /// <summary>Round 1 (or a replay) of the knife round just went live.</summary>
    public void OnRoundStart()
    {
        if (State.Phase != MatchPhase.KnifeRound)
        {
            return;
        }

        _plugin.Cvars.ApplyKnifeRound();

        foreach (var player in Utilities.GetPlayers())
        {
            // Only touch players that are already alive; the ones that are about to respawn
            // get their knife loadout from OnPlayerSpawn instead.
            if (IsPlaying(player) && Settings.GiveKnifeOnly && IsAlive(player))
            {
                EnforceKnifeLoadout(player);
            }
        }

        if (State.KnifeAnnounced)
        {
            return;
        }

        State.KnifeAnnounced = true;

        Chat.Alert(Lang.KnifeRoundLine1);
        Chat.All(Lang.KnifeRoundLine2);

        // The tournament announcement: "[ZSZ] LIVE!" from the moment the knife round starts.
        _plugin.SpamLive();
    }

    /// <summary>Players respawn with a knife and nothing else, and with an empty wallet.</summary>
    public void OnPlayerSpawn(CCSPlayerController player)
    {
        if (State.Phase != MatchPhase.KnifeRound || !player.IsValid)
        {
            return;
        }

        if (Settings.GiveKnifeOnly)
        {
            EnforceKnifeLoadout(player);
        }
        else
        {
            ZeroMoney(player);
        }
    }

    /// <summary>A purchase somehow got through: undo it immediately.</summary>
    public void OnItemPurchase(CCSPlayerController player, string weapon)
    {
        if (State.Phase != MatchPhase.KnifeRound)
        {
            return;
        }

        if (!IsBuyable(weapon))
        {
            return;
        }

        Chat.WarnTo(player, Lang.KnifeNoBuy);

        // Let the engine finish handing the item over before we take it away again.
        _plugin.AddTimer(0.1f, () =>
        {
            if (player.IsValid)
            {
                EnforceKnifeLoadout(player);
            }
        });
    }

    /// <summary>Same for a weapon picked up from the ground.</summary>
    public void OnItemPickup(CCSPlayerController player, string item)
    {
        if (State.Phase != MatchPhase.KnifeRound || !Settings.GiveKnifeOnly)
        {
            return;
        }

        if (string.IsNullOrEmpty(item) || !IsBuyable(item) || IsKnife(item))
        {
            return;
        }

        Chat.WarnTo(player, Lang.KnifeNoBuy);

        _plugin.AddTimer(0.1f, () =>
        {
            if (player.IsValid)
            {
                EnforceKnifeLoadout(player);
            }
        });
    }

    /// <summary>Strip everything, hand back exactly one knife and clear the wallet.</summary>
    public void EnforceKnifeLoadout(CCSPlayerController player)
    {
        if (!player.IsValid || !IsPlaying(player))
        {
            return;
        }

        try
        {
            player.RemoveWeapons();
            player.GiveNamedItem("weapon_knife");

            if (Settings.FreeArmor >= 2)
            {
                player.GiveNamedItem("item_assaultsuit");
            }
            else if (Settings.FreeArmor == 1)
            {
                player.GiveNamedItem("item_kevlar");
            }
        }
        catch (Exception ex)
        {
            _plugin.Logger.LogWarning("[ZSZ] Could not reset the knife loadout of {Name}: {Message}",
                player.PlayerName, ex.Message);
        }

        ZeroMoney(player);
    }

    /// <summary>
    /// mp_maxmoney is already 0 during the knife round, but a player could still carry money
    /// over from a previous round - so the wallet is emptied explicitly as well.
    /// </summary>
    public static void ZeroMoney(CCSPlayerController player)
    {
        var money = player.InGameMoneyServices;
        if (money != null)
        {
            money.Account = 0;
        }
    }

    /// <summary>Knife round decided: hand the winning team to the side pick.</summary>
    public void HandleRoundEnd(int reportedWinner)
    {
        if (State.Phase != MatchPhase.KnifeRound)
        {
            return;
        }

        var winnerTeam = DetermineWinner();

        if (winnerTeam == null)
        {
            // Nobody won: the round timed out with the two teams indistinguishable, or it ended in
            // a RoundEndReason.RoundDraw.
            var maxReplays = Math.Max(0, Settings.MaxReplays);

            if (Settings.ReplayOnDraw && State.KnifeRoundAttempt < maxReplays)
            {
                State.KnifeRoundAttempt++;
                Chat.Warn(Lang.KnifeRoundDraw);
                _plugin.Logger.LogInformation("[ZSZ] Knife round {Attempt}/{Max} was a draw - replaying.",
                    State.KnifeRoundAttempt, maxReplays);

                _plugin.Cvars.ApplyKnifeRound();
                Server.ExecuteCommand("mp_restartgame 1");
                return;
            }

            if (Settings.ReplayOnDraw)
            {
                // Replay budget spent. Two teams refusing to fight must not be able to stall the
                // whole tournament, so the side pick is decided by a coin flip.
                winnerTeam = Random.Shared.Next(2) == 0 ? CsTeam.CounterTerrorist : CsTeam.Terrorist;

                // Just the "it was a coin flip" line: the shared block below announces the winner,
                // so naming the team here too would say it twice in a row.
                Chat.Alert(Lang.KnifeRoundCoinFlip);
                _plugin.Logger.LogWarning(
                    "[ZSZ] Knife round drawn {Attempt} times - coin flip gave the side pick to {Team}.",
                    State.KnifeRoundAttempt, winnerTeam.Value);
            }
            else
            {
                // A draw with replays disabled: there is no winner to pick a side, so the
                // match simply starts on the sides both teams are already on.
                Chat.Warn(Lang.KnifeRoundDraw);
                _plugin.StartLiveMatch(null, null);
                return;
            }
        }

        State.KnifeRoundWinner = winnerTeam;
        Chat.Alert(string.Format(Lang.KnifeRoundWinnerFormat, Lang.TeamName(winnerTeam.Value)));
        _plugin.Logger.LogInformation("[ZSZ] Knife round won by {Team} (engine reported {Reported}).",
            Lang.TeamName(winnerTeam.Value), (CsTeam)reportedWinner);

        if (_plugin.Config.SidePick.Enabled)
        {
            _plugin.SidePick.Begin(winnerTeam.Value);
        }
        else
        {
            _plugin.StartLiveMatch(winnerTeam, winnerTeam);
        }
    }

    /// <summary>
    /// Who actually won the knife round, worked out from the players rather than from
    /// round_end.winner.
    ///
    /// The engine has no concept of "who was ahead" in a knife round: a round that runs out of
    /// time is simply awarded to the CTs, because the Ts "failed to plant the bomb" - and there is
    /// no bomb to plant. So the reported winner can hand the side pick to the wrong captain, which
    /// is the one thing the knife round exists to decide.
    ///
    /// Decide it ourselves instead: more players left alive wins, then more total hp wins. Returns
    /// null when neither separates the teams - and that genuinely means "nobody was ahead".
    ///
    /// The engine's report is deliberately NOT consulted, even though it is right there in the
    /// event. The likeliest drawn knife round is exactly the tie below: a passive standoff that
    /// runs the clock out with both teams untouched, 5 alive at 100 hp each. Believing the engine
    /// there would award the side pick to the CTs purely because the Ts "failed to plant the
    /// bomb" - in a round that has no bomb. Better to say nobody won and let it be replayed.
    /// </summary>
    private static CsTeam? DetermineWinner()
    {
        var aliveCt = 0;
        var aliveT = 0;
        var hpCt = 0;
        var hpT = 0;

        foreach (var player in Utilities.GetPlayers())
        {
            if (!IsPlaying(player) || !IsAlive(player))
            {
                continue;
            }

            var hp = player.PlayerPawn?.Value?.Health ?? 0;

            switch (player.Team)
            {
                case CsTeam.CounterTerrorist:
                    aliveCt++;
                    hpCt += hp;
                    break;
                case CsTeam.Terrorist:
                    aliveT++;
                    hpT += hp;
                    break;
            }
        }

        if (aliveCt != aliveT)
        {
            return aliveCt > aliveT ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
        }

        if (hpCt != hpT)
        {
            return hpCt > hpT ? CsTeam.CounterTerrorist : CsTeam.Terrorist;
        }

        // Dead level - nobody was ahead, so there is no winner to report.
        return null;
    }

    /// <summary>The knife round is being abandoned (admin force start / reset / plugin unload).</summary>
    public void Cancel()
    {
        if (State.Phase == MatchPhase.KnifeRound)
        {
            _plugin.Cvars.Set("mp_buytime", _plugin.Config.Ruleset.BuyTime);
        }
    }

    private static bool IsPlaying(CCSPlayerController player)
        => player.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist;

    private static bool IsAlive(CCSPlayerController player)
        => player.PlayerPawn?.Value is { IsValid: true } pawn && pawn.Health > 0;

    private static bool IsKnife(string item)
        => item.Contains("knife", StringComparison.OrdinalIgnoreCase);

    private static bool IsBuyable(string item)
        => !string.IsNullOrEmpty(item) && !AllowedItems.Contains(item);
}
