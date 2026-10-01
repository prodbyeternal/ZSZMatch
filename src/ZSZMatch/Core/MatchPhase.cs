namespace ZSZMatch.Core;

/// <summary>
/// The whole match is a small state machine driven by these phases:
/// WarmupLong -> WarmupShort -> KnifeRound -> SidePick -> Live -> MatchEnded
/// </summary>
public enum MatchPhase
{
    /// <summary>Waiting for players. The engine warmup is held open and the plugin owns the 1:45 clock.</summary>
    WarmupLong,

    /// <summary>Everybody is connected: warmup has been cut down to the short countdown.</summary>
    WarmupShort,

    /// <summary>Knife only round. Its result decides which captain picks a side.</summary>
    KnifeRound,

    /// <summary>The game is frozen while the winning captain chooses CT or T.</summary>
    SidePick,

    /// <summary>The real match. Score was reset to 0-0, FACEIT cvars are enforced, stats are tracked.</summary>
    Live,

    /// <summary>A team won the match (or it was terminated). Only css_zsz_reset leaves this phase.</summary>
    MatchEnded,
}

public static class MatchPhaseExtensions
{
    public static bool IsWarmup(this MatchPhase phase)
        => phase is MatchPhase.WarmupLong or MatchPhase.WarmupShort;

    /// <summary>Captains can still be registered while this is true.</summary>
    public static bool CaptainsAllowed(this MatchPhase phase)
        => phase is MatchPhase.WarmupLong or MatchPhase.WarmupShort or MatchPhase.KnifeRound;

    /// <summary>Polish description used by the css_zsz_status admin command.</summary>
    public static string Describe(this MatchPhase phase) => phase switch
    {
        MatchPhase.WarmupLong => "rozgrzewka (oczekiwanie na graczy)",
        MatchPhase.WarmupShort => "rozgrzewka (wszyscy gracze połączeni)",
        MatchPhase.KnifeRound => "runda nożowa",
        MatchPhase.SidePick => "wybór strony",
        MatchPhase.Live => "mecz na żywo",
        MatchPhase.MatchEnded => "mecz zakończony",
        _ => phase.ToString(),
    };
}
