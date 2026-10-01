using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace ZSZMatch.Util;

/// <summary>
/// Small wrapper around the CounterStrikeSharp chat / HUD helpers so every message
/// the plugin prints looks identical: " [ZSZ] tekst".
/// </summary>
public static class Chat
{
    /// <summary>Loaded from ZSZMatch.json (ChatPrefix).</summary>
    public static string Prefix { get; set; } = "[ZSZ]";

    public static void All(string message)
        => Server.PrintToChatAll($" {ChatColors.Lime}{Prefix}{ChatColors.Default} {message}");

    public static void To(CCSPlayerController player, string message)
        => player.PrintToChat($" {ChatColors.Lime}{Prefix}{ChatColors.Default} {message}");

    public static void Warn(string message)
        => Server.PrintToChatAll($" {ChatColors.Red}{Prefix}{ChatColors.Default} {message}");

    public static void WarnTo(CCSPlayerController player, string message)
        => player.PrintToChat($" {ChatColors.Red}{Prefix}{ChatColors.Default} {message}");

    /// <summary>The loud "[ZSZ] LIVE!" style tournament alert.</summary>
    public static void Alert(string message)
        => Server.PrintToChatAll($" {ChatColors.Gold}{Prefix}{ChatColors.Default} {ChatColors.Red}{message}{ChatColors.Default}");

    /// <summary>Prints the same line to a single player.</summary>
    public static void AlertTo(CCSPlayerController player, string message)
        => player.PrintToChat($" {ChatColors.Gold}{Prefix}{ChatColors.Default} {ChatColors.Red}{message}{ChatColors.Default}");

    public static void Center(CCSPlayerController player, string html, int seconds)
        => player.PrintToCenterHtml(html, seconds);

    public static void CenterAll(string html, int seconds)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            player.PrintToCenterHtml(html, seconds);
        }
    }

    public static void CenterAlertAll(string message)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            player.PrintToCenterAlert(message);
        }
    }

    /// <summary>Wraps text in a HUD colour tag.</summary>
    public static string Color(string color, string text) => $"<font color='{color}'>{text}</font>";

    public static string Bold(string text) => $"<b>{text}</b>";

    public static string Line(string text) => $"{text}<br>";
}
