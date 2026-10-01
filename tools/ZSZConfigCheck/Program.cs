using System.Text.Json;
using ZSZMatch.Config;

// Mirrors CounterStrikeSharp's ConfigManager.JsonSerializerOptions.
var options = new JsonSerializerOptions
{
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
};

var root = AppContext.BaseDirectory;
var path = args.Length > 0 ? args[0] : Path.Combine(root, "ZSZMatch.example.json");

if (!File.Exists(path))
{
    Console.Error.WriteLine($"Nie znaleziono pliku: {path}");
    return 1;
}

var json = File.ReadAllText(path);

ZSZMatchConfig? config;
try
{
    config = JsonSerializer.Deserialize<ZSZMatchConfig>(json, options);
}
catch (JsonException ex)
{
    Console.Error.WriteLine($"BLAD JSON: {ex.Message}");
    return 1;
}

if (config == null)
{
    Console.Error.WriteLine("Deserializacja zwrocila null.");
    return 1;
}

var defaults = new ZSZMatchConfig();
var failures = 0;

void Check(string label, object? actual, object? expected)
{
    var ok = Equals(actual, expected);
    if (!ok)
    {
        failures++;
    }

    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {label,-52} = {actual}");
}

Console.WriteLine($"Plik: {path}");
Console.WriteLine(new string('-', 90));

Check("ChatPrefix", config.ChatPrefix, defaults.ChatPrefix);
Check("Version", config.Version, defaults.Version);

Check("Warmup.LongWarmupSeconds", config.Warmup.LongWarmupSeconds, 105);
Check("Warmup.ShortWarmupSeconds", config.Warmup.ShortWarmupSeconds, 25);
Check("Warmup.PlayersRequired", config.Warmup.PlayersRequired, 10);
Check("Warmup.CountBots", config.Warmup.CountBots, true);
Check("Warmup.CountSpectators", config.Warmup.CountSpectators, false);
Check("Warmup.StartWithoutFullServer", config.Warmup.StartWithoutFullServer, false);
Check("Warmup.CountdownHud", config.Warmup.CountdownHud, true);
Check("Warmup.WaitingForPlayersNoticeSeconds", config.Warmup.WaitingForPlayersNoticeSeconds, 30);

Check("KnifeRound.Enabled", config.KnifeRound.Enabled, true);
Check("KnifeRound.FreezeTime", config.KnifeRound.FreezeTime, 15);
Check("KnifeRound.RoundTime", config.KnifeRound.RoundTime, 1.92);
Check("KnifeRound.StartMoney", config.KnifeRound.StartMoney, 0);
Check("KnifeRound.FreeArmor", config.KnifeRound.FreeArmor, 2);
Check("KnifeRound.GiveKnifeOnly", config.KnifeRound.GiveKnifeOnly, true);
Check("KnifeRound.ReplayOnDraw", config.KnifeRound.ReplayOnDraw, true);
Check("KnifeRound.MaxReplays", config.KnifeRound.MaxReplays, 3);

Check("SidePick.Enabled", config.SidePick.Enabled, true);
Check("SidePick.HoldFreezeTime", config.SidePick.HoldFreezeTime, 60);
Check("SidePick.TimeoutSeconds", config.SidePick.TimeoutSeconds, 45);
Check("SidePick.WarningSeconds", config.SidePick.WarningSeconds, 15);
Check("SidePick.AllowOtherCaptainFallback", config.SidePick.AllowOtherCaptainFallback, true);
Check("SidePick.AllowChatFallbackCommands", config.SidePick.AllowChatFallbackCommands, true);

Check("Stats.Enabled", config.Stats.Enabled, true);
Check("Stats.ChatSummary", config.Stats.ChatSummary, true);
Check("Stats.HudPanel", config.Stats.HudPanel, true);
Check("Stats.HudDurationSeconds", config.Stats.HudDurationSeconds, 7);
Check("Stats.HudTopPlayersPerTeam", config.Stats.HudTopPlayersPerTeam, 3);
Check("Stats.TrackDamage", config.Stats.TrackDamage, true);

Check("Ruleset.EnforceFaceitCvars", config.Ruleset.EnforceFaceitCvars, true);
Check("Ruleset.EnforceCvarsEveryRound", config.Ruleset.EnforceCvarsEveryRound, true);
Check("Ruleset.MaxRounds", config.Ruleset.MaxRounds, 24);
Check("Ruleset.OvertimeEnabled", config.Ruleset.OvertimeEnabled, true);
Check("Ruleset.OvertimeMaxRounds", config.Ruleset.OvertimeMaxRounds, 6);
Check("Ruleset.OvertimeStartMoney", config.Ruleset.OvertimeStartMoney, 10000);
Check("Ruleset.FreezeTime", config.Ruleset.FreezeTime, 15);
Check("Ruleset.RoundTimeMinutes", config.Ruleset.RoundTimeMinutes, 1.92);
Check("Ruleset.BuyTime", config.Ruleset.BuyTime, 20);
Check("Ruleset.StartMoney", config.Ruleset.StartMoney, 800);
Check("Ruleset.MaxMoney", config.Ruleset.MaxMoney, 16000);
Check("Ruleset.FreeArmor", config.Ruleset.FreeArmor, 0);
Check("Ruleset.AdditionalCvars.Count", config.Ruleset.AdditionalCvars.Count, 0);

Check("Announcements.LiveMessage", config.Announcements.LiveMessage, "LIVE!");
Check("Announcements.LiveSpamCount", config.Announcements.LiveSpamCount, 4);
Check("Announcements.LiveSpamIntervalSeconds", config.Announcements.LiveSpamIntervalSeconds, 0.75f);
Check("Announcements.GoodLuckMessage", config.Announcements.GoodLuckMessage, "Życzymy miłej rozgrywki!");
Check("Announcements.LiveCenterAlert", config.Announcements.LiveCenterAlert, true);

Check("TestMode.Enabled", config.TestMode.Enabled, false);
Check("TestMode.PlayersRequired", config.TestMode.PlayersRequired, 1);

Console.WriteLine(new string('-', 90));

if (failures > 0)
{
    Console.Error.WriteLine($"{failures} wartosci NIE zgadza sie z oczekiwanymi.");
    return 1;
}

// Round trip: confirm Polish characters survive a save/load cycle.
var saved = JsonSerializer.Serialize(config, options);
var reloaded = JsonSerializer.Deserialize<ZSZMatchConfig>(saved, options)!;

if (reloaded.Announcements.GoodLuckMessage != config.Announcements.GoodLuckMessage)
{
    Console.Error.WriteLine("Polskie znaki nie przetrwaly zapisu/odczytu!");
    return 1;
}

Console.WriteLine("WSZYSTKO OK - konfiguracja parsuje sie poprawnie, polskie znaki dzialaja.");
return 0;
