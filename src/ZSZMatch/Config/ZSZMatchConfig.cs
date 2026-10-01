using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace ZSZMatch.Config;

/// <summary>
/// Everything the plugin does is driven from here so a tournament admin never has to
/// recompile it. The file is written to:
///   csgo/addons/counterstrikesharp/configs/plugins/ZSZMatch/ZSZMatch.json
/// </summary>
public sealed class ZSZMatchConfig : BasePluginConfig
{
    /// <summary>Prefix used for every chat line the plugin prints.</summary>
    [JsonPropertyName("ChatPrefix")]
    public string ChatPrefix { get; set; } = "[ZSZ]";

    [JsonPropertyName("Warmup")]
    public WarmupSettings Warmup { get; set; } = new();

    [JsonPropertyName("KnifeRound")]
    public KnifeRoundSettings KnifeRound { get; set; } = new();

    [JsonPropertyName("SidePick")]
    public SidePickSettings SidePick { get; set; } = new();

    [JsonPropertyName("Stats")]
    public StatsSettings Stats { get; set; } = new();

    [JsonPropertyName("Ruleset")]
    public RulesetSettings Ruleset { get; set; } = new();

    [JsonPropertyName("Announcements")]
    public AnnouncementSettings Announcements { get; set; } = new();

    [JsonPropertyName("TestMode")]
    public TestModeSettings TestMode { get; set; } = new();
}

public sealed class WarmupSettings
{
    /// <summary>Warmup length in seconds while the server is waiting for all players. 105 = 1:45.</summary>
    [JsonPropertyName("LongWarmupSeconds")]
    public int LongWarmupSeconds { get; set; } = 105;

    /// <summary>Warmup is cut down to this many seconds the moment the last player connects. 25 = 0:25.</summary>
    [JsonPropertyName("ShortWarmupSeconds")]
    public int ShortWarmupSeconds { get; set; } = 25;

    /// <summary>How many players have to be connected (and on T/CT) before the short warmup starts.</summary>
    [JsonPropertyName("PlayersRequired")]
    public int PlayersRequired { get; set; } = 10;

    /// <summary>Bots count towards <see cref="PlayersRequired"/>. Handy for testing with css_zsz_bots.</summary>
    [JsonPropertyName("CountBotsTowardsPlayerCount")]
    public bool CountBots { get; set; } = true;

    /// <summary>Spectators count towards <see cref="PlayersRequired"/>.</summary>
    [JsonPropertyName("CountSpectatorsTowardsPlayerCount")]
    public bool CountSpectators { get; set; } = false;

    /// <summary>
    /// false (recommended for a real match): if the 1:45 runs out with fewer than
    /// <see cref="PlayersRequired"/> players the server simply keeps waiting.
    /// true: the match starts anyway, exactly like a literal reading of "1:45 warmup".
    /// </summary>
    [JsonPropertyName("StartMatchWhenWarmupExpiresWithoutFullServer")]
    public bool StartWithoutFullServer { get; set; } = false;

    /// <summary>Show the big centre-screen warmup countdown.</summary>
    [JsonPropertyName("CountdownHudEnabled")]
    public bool CountdownHud { get; set; } = true;

    /// <summary>While waiting for the server to fill up, re-announce the player count this often.</summary>
    [JsonPropertyName("WaitingForPlayersNoticeSeconds")]
    public int WaitingForPlayersNoticeSeconds { get; set; } = 30;
}

public sealed class KnifeRoundSettings
{
    /// <summary>Run a knife round after warmup to decide who picks sides.</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("FreezeTime")]
    public int FreezeTime { get; set; } = 15;

    [JsonPropertyName("RoundTimeMinutes")]
    public double RoundTime { get; set; } = 1.92;

    /// <summary>Money every player is given for the knife round. 0 = "no money at start".</summary>
    [JsonPropertyName("StartMoney")]
    public int StartMoney { get; set; } = 0;

    /// <summary>0 = none, 1 = kevlar, 2 = full kevlar + helmet (this is what "full kevlar" means).</summary>
    [JsonPropertyName("FreeArmor")]
    public int FreeArmor { get; set; } = 2;

    /// <summary>Strip everything except the knife, and give the knife back if a player loses it.</summary>
    [JsonPropertyName("GiveKnifeOnly")]
    public bool GiveKnifeOnly { get; set; } = true;

    /// <summary>Knife rounds end in a draw surprisingly often - replay instead of giving up.</summary>
    [JsonPropertyName("ReplayOnDraw")]
    public bool ReplayOnDraw { get; set; } = true;

    /// <summary>
    /// How many times a drawn knife round may be replayed before the plugin decides it by coin
    /// flip. Two teams that both refuse to fight can otherwise stall the whole tournament.
    /// </summary>
    [JsonPropertyName("MaxReplays")]
    public int MaxReplays { get; set; } = 3;
}

public sealed class SidePickSettings
{
    /// <summary>When false the knife round winner stays on its current side (no captain menu).</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Freeze time used to hold the game still while the winning captain decides.
    /// Nobody can move or buy during it, and the match is restarted right after.
    /// </summary>
    [JsonPropertyName("HoldFreezeTime")]
    public int HoldFreezeTime { get; set; } = 60;

    /// <summary>How long the captain has to pick a side before the plugin decides for them.</summary>
    [JsonPropertyName("TimeoutSeconds")]
    public int TimeoutSeconds { get; set; } = 45;

    /// <summary>Warn the captain this many seconds before the timeout.</summary>
    [JsonPropertyName("WarningSeconds")]
    public int WarningSeconds { get; set; } = 15;

    /// <summary>
    /// If the winning team somehow has no captain (bot team, captain left, ...) let the
    /// other captain make the call instead of stalling. Used by solo testing with bots.
    /// </summary>
    [JsonPropertyName("AllowOtherCaptainFallback")]
    public bool AllowOtherCaptainFallback { get; set; } = true;

    /// <summary>Also accept !pickct / !pickt in chat from the picking captain.</summary>
    [JsonPropertyName("AllowChatFallbackCommands")]
    public bool AllowChatFallbackCommands { get; set; } = true;
}

public sealed class StatsSettings
{
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Print the compact per-team summary in chat at the end of every round.</summary>
    [JsonPropertyName("ChatSummary")]
    public bool ChatSummary { get; set; } = true;

    /// <summary>Show the FACEIT style centre-screen scoreboard panel at the end of every round.</summary>
    [JsonPropertyName("HudPanel")]
    public bool HudPanel { get; set; } = true;

    [JsonPropertyName("HudDurationSeconds")]
    public int HudDurationSeconds { get; set; } = 7;

    /// <summary>How many players per team are listed on the HUD panel.</summary>
    [JsonPropertyName("HudTopPlayersPerTeam")]
    public int HudTopPlayersPerTeam { get; set; } = 3;

    /// <summary>Track damage taken/dealt so ADR can be calculated.</summary>
    [JsonPropertyName("TrackDamage")]
    public bool TrackDamage { get; set; } = true;
}

public sealed class RulesetSettings
{
    /// <summary>Push the FACEIT competitive cvars (MR12, overtime, timings, economy).</summary>
    [JsonPropertyName("EnforceFaceitCvars")]
    public bool EnforceFaceitCvars { get; set; } = true;

    /// <summary>Re-apply the cvars at the start of every live round so nobody can nudge the match.</summary>
    [JsonPropertyName("EnforceCvarsEveryRound")]
    public bool EnforceCvarsEveryRound { get; set; } = true;

    [JsonPropertyName("MaxRounds")]
    public int MaxRounds { get; set; } = 24;

    [JsonPropertyName("OvertimeEnabled")]
    public bool OvertimeEnabled { get; set; } = true;

    [JsonPropertyName("OvertimeMaxRounds")]
    public int OvertimeMaxRounds { get; set; } = 6;

    [JsonPropertyName("OvertimeStartMoney")]
    public int OvertimeStartMoney { get; set; } = 10000;

    [JsonPropertyName("FreezeTime")]
    public int FreezeTime { get; set; } = 15;

    /// <summary>Round time in minutes. 1.92 = 1:55 (FACEIT/CS2 competitive).</summary>
    [JsonPropertyName("RoundTimeMinutes")]
    public double RoundTimeMinutes { get; set; } = 1.92;

    [JsonPropertyName("BuyTime")]
    public int BuyTime { get; set; } = 20;

    [JsonPropertyName("StartMoney")]
    public int StartMoney { get; set; } = 800;

    [JsonPropertyName("MaxMoney")]
    public int MaxMoney { get; set; } = 16000;

    /// <summary>Free armour during the real match. 0 = players buy their own, like FACEIT.</summary>
    [JsonPropertyName("FreeArmor")]
    public int FreeArmor { get; set; } = 0;

    /// <summary>
    /// Escape hatch: any extra cvars you want pushed at match start, e.g.
    /// { "sv_deadtalk": "0", "mp_display_kill_assists": "1" }
    /// </summary>
    [JsonPropertyName("AdditionalCvars")]
    public Dictionary<string, string> AdditionalCvars { get; set; } = new();
}

public sealed class AnnouncementSettings
{
    /// <summary>Chat message used for the "match has begun" spam. Printed as "[ZSZ] LIVE!".</summary>
    [JsonPropertyName("LiveMessage")]
    public string LiveMessage { get; set; } = "LIVE!";

    /// <summary>How many times "[ZSZ] LIVE!" is spammed.</summary>
    [JsonPropertyName("LiveSpamCount")]
    public int LiveSpamCount { get; set; } = 4;

    [JsonPropertyName("LiveSpamIntervalSeconds")]
    public float LiveSpamIntervalSeconds { get; set; } = 0.75f;

    /// <summary>Printed as soon as the real match is live. Printed as "[ZSZ] Życzymy miłej rozgrywki!".</summary>
    [JsonPropertyName("GoodLuckMessage")]
    public string GoodLuckMessage { get; set; } = "Życzymy miłej rozgrywki!";

    /// <summary>Also flash "LIVE!" in the middle of the screen.</summary>
    [JsonPropertyName("LiveCenterAlert")]
    public bool LiveCenterAlert { get; set; } = true;
}

public sealed class TestModeSettings
{
    /// <summary>Relaxes the player requirement so a single player can drive the whole flow.</summary>
    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = false;

    /// <summary>Players required while test mode is on.</summary>
    [JsonPropertyName("PlayersRequired")]
    public int PlayersRequired { get; set; } = 1;
}
