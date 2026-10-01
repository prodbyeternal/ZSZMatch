using CounterStrikeSharp.API.Modules.Utils;

namespace ZSZMatch.Core;

/// <summary>
/// Wszystkie teksty widoczne dla graczy (poprawna polszczyzna z ogonkami).
/// All player facing text lives here - edit this file and rebuild to change the wording.
/// Dwie linijki turniejowe ("[ZSZ] LIVE!" i "[ZSZ] Życzymy miłej rozgrywki!") są dodatkowo
/// dostępne w ZSZMatch.json, więc można je zmienić bez kompilowania pluginu.
/// </summary>
public static class Lang
{
    // ---------------------------------------------------------------- rozgrzewka
    public const string WarmupTitle = "ROZGRZEWKA";
    public const string WarmupPlayersFormat = "{0} / {1} graczy";
    public const string WarmupReadyLine = "Wszyscy gracze połączeni!";

    public const string WarmupStartedFormat =
        "Rozgrzewka: {0}. Wpisz !kapitan, aby zostać kapitanem swojej drużyny.";

    public const string WaitingForPlayersFormat = "Oczekiwanie na graczy: {0}/{1}";

    public const string AllPlayersConnectedFormat =
        "Wszyscy gracze połączeni! Rozgrzewka skrócona do {0} s - przygotujcie się.";

    public const string WarmupSkipped = "Rozgrzewka pominięta przez administratora.";

    // ---------------------------------------------------------------- runda nożowa
    public const string KnifeRoundTitle = "RUNDA NOŻOWA";
    public const string KnifeRoundLine1 = "Runda nożowa! Wygraj, aby wybrać stronę startową.";
    public const string KnifeRoundLine2 = "Tylko noże - bez broni, bez granatów, bez pieniędzy.";
    public const string KnifeNoBuy = "W rundzie nożowej nie można kupować!";
    public const string KnifeRoundDraw = "Remis w rundzie nożowej - powtarzamy!";
    public const string KnifeRoundWinnerFormat = "Rundę nożową wygrywa drużyna {0}!";

    /// <summary>
    /// Shown when the replay budget is spent, just before the usual winner line - the players have
    /// to be told the knife round was decided by chance rather than by their play.
    /// </summary>
    public const string KnifeRoundCoinFlip =
        "Runda nożowa wciąż nierozstrzygnięta - o stronie decyduje losowanie!";

    // ---------------------------------------------------------------- wybór strony
    public const string SidePickMenuTitle = "WYBIERZ STRONĘ STARTOWĄ";
    public const string SidePickOptionCt = "Counter-Terrorist (CT)";
    public const string SidePickOptionT = "Terroryści (T)";

    public const string SidePickStartedFormat =
        "Kapitan drużyny {0} wybiera stronę startową... ({1} s)";

    public const string SidePickTimeoutWarningFormat = "Zostało {0} s na wybór strony!";

    public const string SidePickOtherCaptainFormat =
        "Kapitan drużyny {0} jest niedostępny - decyduje kapitan drużyny {1}.";

    public const string SidePickNoCaptainFormat =
        "Drużyna {0} nie ma kapitana - zostaje na swojej obecnej stronie.";

    public const string SidePickDoneFormat = "Drużyna {0} wybrała stronę: {1}.";
    public const string SidePickAutoFormat = "Czas minął - drużyna {0} zostaje na swojej obecnej stronie.";

    public const string SidePickOnlyCaptain = "Tylko kapitan wybiera stronę startową.";
    public const string SidePickNotNow = "Wybór strony nie jest teraz dostępny.";

    public const string SidesSwapped = "Drużyny zamieniły się stronami.";

    // ---------------------------------------------------------------- mecz
    public const string MatchStarting = "Start meczu! Zasady FACEIT: MR12, 15 s freeze time, dogrywka.";
    public const string MatchEnded = "Mecz zakończony! Dziękujemy za grę.";
    public const string MatchReset = "Reset meczu - powrót do rozgrzewki.";
    public const string ForceStartDone = "Wymuszony start meczu.";
    public const string ForceStartNotNow = "Mecz już trwa. Użyj css_zsz_reset, żeby zacząć od nowa.";

    /// <summary>Shown when the plugin was reloaded in the middle of a running match.</summary>
    public const string ReloadedMidMatch =
        "Plugin został przeładowany w trakcie meczu. Wynik pozostaje, ale statystyki i kapitanowie startują od nowa.";

    // ---------------------------------------------------------------- kapitanowie
    public const string CaptainRegisteredFormat = "{0} jest teraz kapitanem drużyny {1}!";
    public const string CaptainAlreadyYou = "Już jesteś kapitanem swojej drużyny.";
    public const string CaptainTeamTakenFormat = "Drużyna {0} ma już kapitana: {1}.";
    public const string CaptainNeedTeam = "Najpierw wybierz drużynę (T albo CT).";
    public const string CaptainsLocked = "Kapitanowie są już wybrani i nie można ich zmienić.";
    public const string CaptainLeftFormat = "Kapitan drużyny {0} ({1}) rozłączył się.";
    public const string CaptainSteamIdError = "Nie można odczytać SteamID - spróbuj ponownie.";
    public const string NoCaptainsRegistered = "brak";

    // ---------------------------------------------------------------- statystyki
    public const string HudRoundTitle = "RUNDA";
    public const string RoundHeaderFormat = "RUNDA {0}  |  CT {1} : {2} T  |  {3}";
    public const string MatchStatsHeaderFormat = "STATYSTYKI MECZU  |  CT {0} : {1} T  |  runda {2}";
    public const string TeamStatsLineFormat = "{0}  |  Z {1}  A {2}  Ś {3}  |  DMG {4}  |  ADR {5}";
    public const string PlayerHudLineFormat = "{0}{1}  Z {2}  A {3}  Ś {4}  ADR {5}";
    public const string MvpFormat = "MVP rundy: {0} ({1} Z, {2} DMG)";
    public const string StatsNoData = "Brak statystyk - mecz jeszcze się nie rozpoczął.";
    public const string CaptainMark = "*";

    public const string TeamCt = "CT";
    public const string TeamT = "T";
    public const string TeamCtLong = "Counter-Terrorist";
    public const string TeamTLong = "Terroryści";
    public const string TeamSpectator = "WIDZ";

    // ---------------------------------------------------------------- admin
    public const string NoPermission = "Brak uprawnień do tej komendy.";
    public const string TestModeOnFormat = "TEST MODE WŁĄCZONY - wystarczy {0} gracz(y).";
    public const string TestModeOff = "TEST MODE WYŁĄCZONY.";
    public const string BotsAddedFormat = "Dodano boty - na serwerze jest {0} graczy.";
    public const string MustBeInGame = "Musisz być w grze, aby użyć tej komendy.";
    public const string StatusFormat =
        "Faza: {0} | gracze: {1}/{2} | kapitani: {3} | runda: {4} | wynik: CT {5} : {6} T";
    public const string ForcedSideFormat = "Wymuszono stronę {0} dla drużyny {1}.";
    public const string ConfigReloaded = "Konfiguracja przeładowana.";
    public const string UnknownSideFormat = "Nieznana strona: {0}. Użyj ct albo t.";
    public const string CvarsReapplied = "cVary zostały ponownie zastosowane.";

    public static string TeamName(CsTeam team) => team switch
    {
        CsTeam.CounterTerrorist => TeamCt,
        CsTeam.Terrorist => TeamT,
        _ => TeamSpectator,
    };

    public static string TeamNameLong(CsTeam team) => team switch
    {
        CsTeam.CounterTerrorist => TeamCtLong,
        CsTeam.Terrorist => TeamTLong,
        _ => TeamSpectator,
    };

    /// <summary>Polska nazwa powodu zakończenia rundy (RoundEndReason).</summary>
    public static string RoundEndReasonName(int reason) => reason switch
    {
        0x1 => "bomba wybuchła",
        0x7 => "bomba rozbrojona",
        0x8 => "CT wygrywa rundę",
        0x9 => "Terroryści wygrywają rundę",
        0xA => "remis",
        0xB => "zakładnicy uratowani",
        0xC => "bomba nie została podłożona",
        0x11 => "Terroryści się poddali",
        0x12 => "CT się poddali",
        0x13 => "bomba podłożona",
        _ => "koniec rundy",
    };
}
