using System.Globalization;
using CounterStrikeSharp.API;
using ZSZMatch.Config;

namespace ZSZMatch.Core;

/// <summary>
/// Owns every cvar the match flow depends on. Three named sets are pushed at the three
/// moments that matter: warmup, knife round and the real (FACEIT) match.
/// </summary>
public sealed class CvarManager
{
    private readonly ZSZMatchPlugin _plugin;

    public CvarManager(ZSZMatchPlugin plugin) => _plugin = plugin;

    private RulesetSettings Ruleset => _plugin.Config.Ruleset;
    private WarmupSettings Warmup => _plugin.Config.Warmup;
    private KnifeRoundSettings Knife => _plugin.Config.KnifeRound;

    public void Set(string name, string value) => Server.ExecuteCommand($"{name} {value}");

    public void Set(string name, int value)
        => Server.ExecuteCommand($"{name} {value.ToString(CultureInfo.InvariantCulture)}");

    public void Set(string name, double value)
        => Server.ExecuteCommand($"{name} {value.ToString(CultureInfo.InvariantCulture)}");

    /// <summary>Sets a cvar to an empty value, which is how CS2 disables e.g. default weapons.</summary>
    public void Clear(string name) => Server.ExecuteCommand($"{name} \"\"");

    /// <summary>
    /// Warmup is put under plugin control: the engine timer is paused so it can never end the
    /// warmup behind our back, and we end it ourselves with mp_warmup_end.
    /// </summary>
    public void ApplyWarmup()
    {
        Set("mp_warmuptime", Warmup.LongWarmupSeconds);
        Set("mp_warmup_pausetimer", 1);
        Set("mp_warmuptime_all_players_connected", 0);

        Set("mp_autoteambalance", 0);
        Set("mp_limitteams", 0);
        Set("mp_freezetime", 5);
        Set("mp_buytime", Ruleset.BuyTime);
        Set("mp_buy_anywhere", 0);
        Set("mp_startmoney", Ruleset.StartMoney);
        Set("mp_maxmoney", Ruleset.MaxMoney);
        Set("mp_free_armor", 0);
        Set("mp_give_player_c4", 1);
        Set("mp_defuser_allocation", 0);

        // A normal warmup loadout: knife + pistol, so people can actually warm up.
        Set("mp_ct_default_melee", "weapon_knife");
        Set("mp_t_default_melee", "weapon_knife");
        Set("mp_ct_default_secondary", "weapon_hkp2000");
        Set("mp_t_default_secondary", "weapon_glock");
        Clear("mp_ct_default_primary");
        Clear("mp_t_default_primary");
        Clear("mp_ct_default_grenades");
        Clear("mp_t_default_grenades");

        ApplyAdditionalCvars();
    }

    /// <summary>
    /// Knife round: knives only, full kevlar, and buying is impossible.
    /// mp_buytime 0 closes the buy menu, mp_startmoney/mp_maxmoney 0 leave nothing to spend,
    /// and KnifeRoundController strips anything that still finds its way into an inventory.
    /// </summary>
    public void ApplyKnifeRound()
    {
        Set("mp_warmup_pausetimer", 0);

        // If the engine is somehow still in warmup, let it expire within a second instead of
        // resuming the 1:45 we pinned earlier.
        Set("mp_warmuptime", 1);

        Set("mp_freezetime", Knife.FreezeTime);
        Set("mp_roundtime", Knife.RoundTime);
        Set("mp_roundtime_defuse", Knife.RoundTime);
        Set("mp_startmoney", Knife.StartMoney);
        Set("mp_maxmoney", Knife.StartMoney);
        Set("mp_free_armor", Knife.FreeArmor);
        Set("mp_buytime", 0);
        Set("mp_buy_anywhere", 0);
        Set("mp_give_player_c4", 0);
        Set("mp_defuser_allocation", 0);
        Set("mp_autoteambalance", 0);
        Set("mp_limitteams", 0);

        Set("mp_ct_default_melee", "weapon_knife");
        Set("mp_t_default_melee", "weapon_knife");
        Clear("mp_ct_default_secondary");
        Clear("mp_t_default_secondary");
        Clear("mp_ct_default_primary");
        Clear("mp_t_default_primary");
        Clear("mp_ct_default_grenades");
        Clear("mp_t_default_grenades");

        ApplyAdditionalCvars();
    }

    /// <summary>FACEIT ruleset: MR12, overtime, 15 s freeze time, 1:55 rounds, 800 start money.</summary>
    public void ApplyMatch(bool full)
    {
        if (Ruleset.EnforceFaceitCvars)
        {
            if (full)
            {
                Set("mp_maxrounds", Ruleset.MaxRounds);
                Set("mp_overtime_enable", Ruleset.OvertimeEnabled ? 1 : 0);
                Set("mp_overtime_maxrounds", Ruleset.OvertimeMaxRounds);
                Set("mp_overtime_startmoney", Ruleset.OvertimeStartMoney);
                // MUST stay 1 for MR12. This cvar reads as "the match may end early", but 0 does
                // not mean "no early end because of a timeout" - it means the server plays every
                // single scheduled round. With mp_maxrounds 24 that is 24 rounds no matter the
                // score, so a 13-0 match would play 11 dead rounds and cs_win_panel_match would
                // never fire. 1 lets the match end the moment 13 rounds are reached (FACEIT).
                Set("mp_match_can_clinch", 1);
                Set("mp_friendlyfire", 1);
                Set("mp_autoteambalance", 0);
                Set("mp_limitteams", 0);
                Set("mp_solid_teammates", 1);
                Set("mp_teammates_are_enemies", 0);
                Set("mp_forcecamera", 1);
                Set("mp_c4timer", 40);
                Set("mp_display_kill_assists", 1);
                Set("mp_ignore_round_win_conditions", 0);
            }

            Set("mp_freezetime", Ruleset.FreezeTime);
            Set("mp_roundtime", Ruleset.RoundTimeMinutes);
            Set("mp_roundtime_defuse", Ruleset.RoundTimeMinutes);
            Set("mp_buytime", Ruleset.BuyTime);
            Set("mp_buy_anywhere", 0);
            Set("mp_startmoney", Ruleset.StartMoney);
            Set("mp_maxmoney", Ruleset.MaxMoney);
            Set("mp_free_armor", Ruleset.FreeArmor);
            Set("mp_give_player_c4", 1);
            Set("mp_defuser_allocation", 0);
            Set("mp_warmup_pausetimer", 0);

            // Competitive default: nobody spawns with a pistol, you buy your own.
            Set("mp_ct_default_melee", "weapon_knife");
            Set("mp_t_default_melee", "weapon_knife");
            Clear("mp_ct_default_secondary");
            Clear("mp_t_default_secondary");
            Clear("mp_ct_default_primary");
            Clear("mp_t_default_primary");
            Clear("mp_ct_default_grenades");
            Clear("mp_t_default_grenades");
        }

        ApplyAdditionalCvars();
    }

    /// <summary>Used while the captain is deciding a side: nobody moves, nobody buys.</summary>
    public void ApplySidePickHold(int freezeTime)
    {
        Set("mp_freezetime", freezeTime);
        Set("mp_buytime", 0);
        Set("mp_startmoney", 0);
        Set("mp_maxmoney", 0);
    }

    private void ApplyAdditionalCvars()
    {
        foreach (var (name, value) in Ruleset.AdditionalCvars)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (string.IsNullOrEmpty(value))
            {
                Clear(name);
            }
            else
            {
                Set(name, value);
            }
        }
    }
}
