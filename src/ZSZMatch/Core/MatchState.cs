using CounterStrikeSharp.API.Modules.Utils;

namespace ZSZMatch.Core;

/// <summary>
/// A team captain. Stored by SteamID so the role survives team switches, deaths and reconnects -
/// exactly what "the captain persists after switching" means.
/// </summary>
public sealed class Captain
{
    public Captain(ulong steamId64, string name, CsTeam registeredTeam)
    {
        SteamId64 = steamId64;
        Name = name;
        RegisteredTeam = registeredTeam;
    }

    public ulong SteamId64 { get; }

    public string Name { get; set; }

    /// <summary>The side this player registered on during warmup. Informational only.</summary>
    public CsTeam RegisteredTeam { get; }

    public override string ToString() => $"{Name} ({SteamId64})";
}

/// <summary>
/// Per player counter used for the FACEIT style end of round statistics.
/// Totals are kept for the whole match, the Round* fields are reset every round.
/// </summary>
public sealed class PlayerMatchStats
{
    public PlayerMatchStats(string id, ulong? steamId64, string name, CsTeam team)
    {
        Id = id;
        SteamId64 = steamId64;
        Name = name;
        Team = team;
    }

    /// <summary>SteamID64 for humans, "bot:&lt;slot&gt;" for bots.</summary>
    public string Id { get; }

    public ulong? SteamId64 { get; }

    public string Name { get; set; }

    public CsTeam Team { get; set; }

    public int Kills { get; set; }

    public int Assists { get; set; }

    public int Deaths { get; set; }

    public int Headshots { get; set; }

    public int Damage { get; set; }

    public int RoundKills { get; set; }

    public int RoundAssists { get; set; }

    public int RoundDeaths { get; set; }

    public int RoundDamage { get; set; }

    public void ResetRound()
    {
        RoundKills = 0;
        RoundAssists = 0;
        RoundDeaths = 0;
        RoundDamage = 0;
    }

    /// <summary>Average damage per round, the number FACEIT players care about.</summary>
    public double Adr(int rounds) => rounds <= 0 ? 0d : Math.Round((double)Damage / rounds, 1);

    public double Kd() => Deaths == 0 ? Kills : Math.Round((double)Kills / Deaths, 2);
}

/// <summary>Everything the plugin needs to know about the current match.</summary>
public sealed class MatchState
{
    public MatchPhase Phase { get; set; } = MatchPhase.WarmupLong;

    public List<Captain> Captains { get; } = new();

    public Dictionary<string, PlayerMatchStats> Stats { get; } = new();

    /// <summary>Absolute engine time (see Server.CurrentTime) at which the warmup countdown hits zero.</summary>
    public float WarmupDeadline { get; set; }

    /// <summary>True once the 1:45 is up but the server is still not full (and we decided to keep waiting).</summary>
    public bool WaitingForPlayers { get; set; }

    public float NextWaitingNotice { get; set; }

    /// <summary>Set while a mp_warmup_end was issued so the watchdog can retry if it did not take.</summary>
    public bool WarmupEndPending { get; set; }

    public int WarmupEndRetries { get; set; }

    public bool KnifeAnnounced { get; set; }

    public int KnifeRoundAttempt { get; set; }

    public CsTeam? KnifeRoundWinner { get; set; }

    /// <summary>The team whose captain has to pick a side.</summary>
    public CsTeam? SidePickWinnerTeam { get; set; }

    /// <summary>SteamID of the captain who is allowed to pick (may be the other captain in fallback mode).</summary>
    public ulong? SidePickCaptainSteamId { get; set; }

    public bool SidePickCompleted { get; set; }

    public CsTeam? ChosenSide { get; set; }

    /// <summary>How many live (non knife, non warmup) rounds have been played. Used for ADR.</summary>
    public int LiveRoundsPlayed { get; set; }

    /// <summary>Side based score, only used if the engine scoreboard cannot be read.</summary>
    public int FallbackCtRounds { get; set; }

    public int FallbackTRounds { get; set; }

    /// <summary>
    /// The engine's own per side score, cached from the team_score event. Used when the team
    /// manager entity cannot be read; it is still authoritative, because it is the same number
    /// the engine prints on the scoreboard (so halftime swaps are already applied).
    /// </summary>
    public int EventCtScore { get; set; }

    public int EventTScore { get; set; }

    /// <summary>True once at least one team_score arrived, so the event cache beats our own counter.</summary>
    public bool EventScoreSeen { get; set; }

    public string MapName { get; set; } = string.Empty;

    public float MatchStartTime { get; set; }

    public bool StatsAvailable => LiveRoundsPlayed > 0;

    public Captain? FindCaptain(ulong steamId64)
        => Captains.FirstOrDefault(c => c.SteamId64 == steamId64);

    public void Reset()
    {
        Phase = MatchPhase.WarmupLong;
        Captains.Clear();
        Stats.Clear();
        WarmupDeadline = 0f;
        WaitingForPlayers = false;
        NextWaitingNotice = 0f;
        WarmupEndPending = false;
        WarmupEndRetries = 0;
        KnifeAnnounced = false;
        KnifeRoundAttempt = 0;
        KnifeRoundWinner = null;
        SidePickWinnerTeam = null;
        SidePickCaptainSteamId = null;
        SidePickCompleted = false;
        ChosenSide = null;
        LiveRoundsPlayed = 0;
        FallbackCtRounds = 0;
        FallbackTRounds = 0;
        EventCtScore = 0;
        EventTScore = 0;
        EventScoreSeen = false;
        MatchStartTime = 0f;
    }
}
