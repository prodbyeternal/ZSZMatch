# ZSZ Match — plugin FACEIT dla turnieju szkolnego (CS2)

Plugin do **CounterStrikeSharp** (CS2), który prowadzi mecz jak na FACEIT:
rozgrzewka → kapitanowie (`!kapitan`) → runda nożowa → wybór strony → mecz na zasadach
FACEIT (MR12) → statystyki po każdej rundzie.

---

## Szybki start (English)

> Full details are in Polish below — this is the short version.

1. **Server:** install the CS2 dedicated server (SteamCMD, app `730`).
2. **Metamod:Source 2.x:** unpack into `game/csgo/`, then add `Game csgo/addons/metamod`
   to the `SearchPaths` block of `game/csgo/gameinfo.gi` (just above the `Game csgo` line).
3. **CounterStrikeSharp 1.0.376+:** unpack the **with-runtime** release into
   `game/csgo/addons/counterstrikesharp/`.
4. **This plugin:** copy `ZSZMatch.dll` **and** `ZSZMatch.example.json` into
   `game/csgo/addons/counterstrikesharp/plugins/ZSZMatch/`
   (the folder **must** be called `ZSZMatch`).
5. **Run:** `cs2.exe -dedicated -insecure +map de_dust2 +sv_lan 1`
6. On first load the plugin creates `addons/counterstrikesharp/configs/plugins/ZSZMatch/ZSZMatch.json`.
7. **Solo test:** set `"Enabled": true` under `TestMode` in that file, then in the server
   console type `css_zsz_bots` to fill both teams with bots and drive the whole flow alone.
   Type `!kapitan` in chat, win/decide the knife round, and the match starts at 0-0.

**In-game (chat):** `!kapitan` · `!stats` · `!pickct` · `!pickt`
**Server console:** `css_zsz_bots`, `css_zsz_skipwarmup`, `css_zsz_forcestart`,
`css_zsz_reset`, `css_zsz_pick ct|t`, `css_zsz_status`, `css_zsz_cvars`,
`css_zsz_reloadconfig`, `css_zsz_testmode`

---

## 1. Co robi plugin (przebieg meczu)

| Krok | Co się dzieje |
|---|---|
| 1. Rozgrzewka | Na starcie mapy rusza **1:45** rozgrzewki (licznik na środku ekranu). Gracze mają nóż + pistolet. |
| 2. Komplet graczy | Gdy na drużynach jest **10 graczy**, rozgrzewka jest natychmiast skracana do **25 sekund**. |
| 3. Kapitanowie | Gracze wpisują `!kapitan`. Jeden kapitan na CT i jeden na T. Po starcie rundy nożowej **nie można już zmienić** kapitanów. |
| 4. Runda nożowa | Tylko noże. Zero kasy, **brak możliwości kupowania** (`mp_buytime 0`), pełna kamizelka z hełmem. Serwer spamuje `[ZSZ] LIVE!`. |
| 5. Wybór strony | Kapitan drużyny, która wygrała rundę nożową, wybiera stronę startową (menu na ekranie + `!pickct` / `!pickt`). |
| 6. Start meczu | Drużyny zamieniają się stronami, jeśli trzeba. `mp_restartgame` → **wynik 0:0**, wchodzą zasady FACEIT. Ponownie leci `[ZSZ] LIVE!`, a potem `[ZSZ] Życzymy miłej rozgrywki!`. |
| 7. Statystyki | Po **każdej rundzie**: podsumowanie obu drużyn na czacie + panel na środku ekranu (Z / A / Ś / DMG / ADR). |

**Uwaga o kamizelce:** pełna kamizelka (`mp_free_armor 2`) jest w **rundzie nożowej** —
bo tam gracze nie mogą kupić. W meczu właściwym obowiązują zasady FACEIT (startowe 800 $,
kamizelkę się kupuje). Jeśli chcesz darmową kamizelkę przez cały mecz, ustaw
`Ruleset.FreeArmor` na `2`.

**Uwaga o kapitanach:** kapitan jest zapamiętany po SteamID, więc **przeżywa zamianę stron**
(to jest właśnie wymagane „kapitan zostaje po zmianie stron"). Po zamianie stron kapitan
nadal jest kapitanem swojej drużyny.

Zgłoszenia `!kapitan` przyjmowane są **do końca rozgrzewki**. Gdy startuje runda nożowa,
kapitanowie są zablokowani — nikt nie może ich zmienić ani dołączyć jako nowy.

**Uwaga o zwycięzcy rundy nożowej:** plugin **nie ufa** temu, kogo wygłaszają silnik jako
zwycięzcę tej rundy. Przy braku bomby runda, która skończy się na czas, jest automatycznie
przyznawana drużynie CT — a to wypaczyłoby cały sens rundy nożowej. Dlatego plugin sam
sprawdza, kto ma więcej żywych graczy, a przy równej liczbie — więcej łącznych punktów życia.

Jeśli i to jest nierozstrzygnięte (np. obie drużyny stoją w miejscu i runda kończy się
5 vs 5 przy pełnym życiu), plugin uznaje, że **nikt nie był lepszy**, i powtarza rundę.
Po `KnifeRound.MaxReplays` (domyślnie 3) powtórzeniach rozstrzyga ją **losowanie** — inaczej
dwie grające „na remis" drużyny zablokowałyby cały turniej.

---

## 2. Wymagania

**Żeby uruchomić serwer:**
- CS2 Dedicated Server (SteamCMD, app id **730**)
- [Metamod:Source 2.x](https://www.sourcemm.net/downloads.php?branch=dev)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) —
  wersja **1.0.376 lub nowsza**, najlepiej paczka **`counterstrikesharp-with-runtime-...`**
  (zawiera własne .NET, nic więcej nie trzeba instalować)

**Żeby zbudować plugin ze źródeł:**
- **.NET 10 SDK** (plugin jest kompilowany pod `net10.0`, bo takie API dostarcza CSS 1.0.376)

---

## 3. Instalacja (krok po kroku)

### 3.1 Serwer CS2

```bash
# SteamCMD
steamcmd +force_install_dir C:\cs2server +login anonymous +app_update 730 validate +quit
```

Serwer znajdzie się w `C:\cs2server\game\csgo\`.

### 3.2 Metamod:Source

1. Pobierz **Metamod:Source 2.x (dev)** dla Windows.
2. Rozpakuj archiwum tak, żeby `addons\metamod` znalazło się w
   `C:\cs2server\game\csgo\addons\metamod`.
3. Otwórz `C:\cs2server\game\csgo\gameinfo.gi` w Notatniku i znajdź sekcję `SearchPaths`:

```
			SearchPaths
			{
				Game_LowViolence	csgo_lv
				Game				csgo
				Game				csgo_imported
				Game				csgo_core
				Game				core
			}
```

4. Dodaj linijkę z Metamodem **bezpośrednio nad** `Game csgo`:

```
				Game				csgo/addons/metamod
				Game				csgo
```

> ⚠️ **Uwaga:** aktualizacje CS2 nadpisują `gameinfo.gi`. Po każdej aktualizacji serwera
> trzeba powtórzyć ten krok (albo przywrócić kopię pliku).

### 3.3 CounterStrikeSharp

1. Pobierz z [GitHub Releases](https://github.com/roflmuffin/CounterStrikeSharp/releases)
   paczkę **`counterstrikesharp-with-runtime-build-XXXX-windows.zip`**.
2. Rozpakuj ją do `C:\cs2server\game\csgo\addons\` — powstanie
   `C:\cs2server\game\csgo\addons\counterstrikesharp\`.
3. Uruchom serwer i sprawdź w konsoli, czy pojawi się informacja o załadowaniu
   CounterStrikeSharp. Jeśli tak — Metamod + CSS działają.

### 3.4 Plugin ZSZMatch

1. Zbuduj plugin (patrz [sekcja 8](#8-budowanie-ze-źródeł)) **albo** weź gotowy plik.
2. Skopiuj **oba** pliki do nowego folderu:

```
C:\cs2server\game\csgo\addons\counterstrikesharp\plugins\ZSZMatch\
    ZSZMatch.dll
    ZSZMatch.example.json
```

> ⚠️ **Folder MUSI nazywać się `ZSZMatch`.** CounterStrikeSharp szuka pliku konfiguracyjnego
> po nazwie folderu pluginu. Jeśli zmienisz nazwę folderu, zmieni się też nazwa pliku
> konfiguracji.

3. Uruchom serwer. Plugin utworzy konfigurację:

```
C:\cs2server\game\csgo\addons\counterstrikesharp\configs\plugins\ZSZMatch\ZSZMatch.json
```

Plik powstaje przez skopiowanie `ZSZMatch.example.json` — z polskimi znakami i komentarzami.
**Komentarze (`//`) są dozwolone** i można je zostawić.

### 3.5 Uruchomienie serwera

```bash
C:\cs2server\game\bin\win64\cs2.exe -dedicated -insecure +map de_dust2 +sv_lan 1
```

- `-insecure` — brak VAC, nie potrzebujesz tokenu GSLT (typowe dla LAN-a).
- `+sv_lan 1` — serwer lokalny, nie pojawia się publicznie.
- Gracze łączą się przez `connect <IP>`, np. `connect 192.168.1.50:27015`.

Jeśli klient odmawia połączenia z serwerem `-insecure`, uruchom CS2 na kliencie również
z flagą `-insecure`.

Przydatne wpisy w `cfg`/konsoli serwera:

```
hostname "[ZSZ] Turniej"
sv_password ""            // albo hasło dla graczy
mp_autoteambalance 0      // plugin i tak wymusza
mp_limitteams 0
bot_quota 0               // bez botów na turnieju
```

---

## 4. Komendy

### Gracze (na czacie, w grze)

| Komenda | Działanie |
|---|---|
| `!kapitan` (albo `/kapitan`, `!captain`) | Zgłoś się na kapitana swojej drużyny. Działa tylko w rozgrzewce i rundzie nożowej. |
| `!stats` (albo `!wynik`) | Pełne statystyki meczu: wynik, Z/A/Ś, HS, DMG, ADR dla obu drużyn. |
| `!pickct` / `!pickt` | Wybór strony przez kapitana (alternatywa dla menu). |
| `!zsz_status` | Aktualny stan meczu (faza, gracze, kapitani, wynik). |

### Administrator

Te komendy działają **w konsoli serwera** (dokładnie tak, jak w tabeli, z `css_`) oraz
**w grze** dla gracza z uprawnieniem `@css/root` albo gdy włączony jest tryb testowy.
W grze wpisujesz je na czacie z `!`, np. `!zsz_skipwarmup`.

| Komenda | Działanie |
|---|---|
| `css_zsz_status` | Status meczu (faza, ilu graczy, kapitani, wynik). |
| `css_zsz_skipwarmup` | Kończy rozgrzewkę natychmiast (przechodzi do rundy nożowej). |
| `css_zsz_forcestart` | Pomija wszystko i startuje mecz od razu (0:0). Nie działa, gdy mecz już trwa. |
| `css_zsz_pick ct` / `css_zsz_pick t` | Wymusza wybór strony za kapitana (gdy np. wyszedł z serwera). |
| `css_zsz_reset` | Reset meczu: czyści kapitanów i statystyki, wraca do rozgrzewki. |
| `css_zsz_cvars` | Ponownie nakłada cvary dla aktualnej fazy. |
| `css_zsz_bots` | Dodaje boty, żeby uzupełnić składy do `PlayersRequired` (5 vs 5). |
| `css_zsz_testmode` | Włącza/wyłącza tryb testowy (wystarczy 1 gracz). |
| `css_zsz_reloadconfig` | Przeładowuje `ZSZMatch.json` bez restartu serwera. |

> Wpisanie komendy na czacie wymaga `!` (albo `/`, żeby nie było widać jej na czacie).
> W konsoli serwera wpisujesz ją bez `!`, ale z `css_`.

**Przeładowanie pluginu bez restartu serwera** (przydatne po podmianie DLL-a):

```
css_plugins reload ZSZMatch
```

> ⚠️ **Nie przeładowuj pluginu w trakcie meczu.** Wynik meczu (po stronie silnika) przetrwa,
> ale **kapitanowie i statystyki graczy przepadną** — żyły w starej instancji pluginu. Plugin
> wykryje to, napisze o tym na czacie i będzie dalej zbierał statystyki od zera.
> Jeśli chcesz zacząć mecz od nowa, użyj `css_zsz_reset`.

---

## 5. Konfiguracja (`ZSZMatch.json`)

Plik: `...\configs\plugins\ZSZMatch\ZSZMatch.json`. Po zmianach: `css_zsz_reloadconfig`.

### Najczęściej zmieniane

| Klucz | Domyślnie | Znaczenie |
|---|---|---|
| `ChatPrefix` | `"[ZSZ]"` | Prefiks wszystkich wiadomości. |
| `Warmup.LongWarmupSeconds` | `105` | Rozgrzewka przed kompletem graczy (1:45). |
| `Warmup.ShortWarmupSeconds` | `25` | Rozgrzewka po skompletowaniu składów (0:25). |
| `Warmup.PlayersRequired` | `10` | Ilu graczy skraca rozgrzewkę. |
| `Warmup.CountBotsTowardsPlayerCount` | `true` | Czy boty liczą się do kompletu. |
| `KnifeRound.Enabled` | `true` | Czy jest runda nożowa. |
| `KnifeRound.StartMoney` | `0` | Kasa w rundzie nożowej (0 = brak). |
| `KnifeRound.FreeArmor` | `2` | Kamizelka w rundzie nożowej (2 = z hełmem). |
| `KnifeRound.MaxReplays` | `3` | Ile razy powtórzyć remisową rundę nożową, zanim plugin rozstrzygnie ją losowaniem. |
| `SidePick.TimeoutSeconds` | `45` | Ile czasu ma kapitan na wybór strony. |
| `Stats.HudPanel` | `true` | Panel statystyk na środku ekranu. |
| `Ruleset.EnforceFaceitCvars` | `true` | Wymuszanie zasad FACEIT (MR12 itd.). |
| `Ruleset.FreeArmor` | `0` | Darmowa kamizelka w meczu (0 = kupujesz sam). |
| `Announcements.LiveMessage` | `"LIVE!"` | Tekst wyświetlany jako `[ZSZ] LIVE!`. |
| `Announcements.LiveSpamCount` | `4` | Ile razy spamować `[ZSZ] LIVE!`. |
| `Announcements.GoodLuckMessage` | `"Życzymy miłej rozgrywki!"` | Wiadomość po starcie meczu. |
| `TestMode.Enabled` | `false` | Tryb testowy dla 1 osoby. **Wyłącz przed turniejem!** |

Pełny opis każdego klucza jest w komentarzach w samym pliku JSON.

### Zasady FACEIT wymuszane przez plugin

```
mp_maxrounds 24            // MR12
mp_overtime_enable 1
mp_overtime_maxrounds 6
mp_overtime_startmoney 10000
mp_match_can_clinch 1      // 1 = mecz kończy się na 13 wygranych rundach
mp_freezetime 15
mp_roundtime 1.92          // 1:55
mp_buytime 20
mp_startmoney 800
mp_maxmoney 16000
mp_friendlyfire 1
mp_autoteambalance 0
mp_limitteams 0
```

> ⚠️ **`mp_match_can_clinch` musi zostać `1`.** Nazwa jest myląca: `0` **nie** oznacza
> „mecz nie kończy się przed czasem", tylko „serwer rozegra **wszystkie** rundy z `mp_maxrounds`".
> Przy MR12 dałoby to 24 rundy niezależnie od wyniku — mecz 13:0 grałby jeszcze 11 martwych rund,
> a `cs_win_panel_match` nigdy by nie zadziałał.

Cvary są nakładane przy starcie meczu **i ponawiane na początku każdej rundy**
(`Ruleset.EnforceCvarsEveryRound`), żeby nikt ich nie zmienił w trakcie gry.
Dodatkowe własne cvary dodasz w `Ruleset.AdditionalCvars`.

---

## 6. Test samemu (1 osoba)

Cały przebieg można przejść w pojedynkę — z botami.

### Wariant A: tryb testowy (najszybszy)

1. Na komputerze z serwerem otwórz
   `...\configs\plugins\ZSZMatch\ZSZMatch.json` i ustaw:

```json
  "TestMode": { "Enabled": true, "PlayersRequired": 1 }
```

2. Uruchom serwer i wejdź do gry (`connect 127.0.0.1` jeśli grasz na tym samym PC).
3. Wybierz drużynę. **Rozgrzewka od razu skróci się do 25 sekund** (bo wystarczy 1 gracz) —
   to test mechanizmu „skrócenia rozgrzewki".
4. Wpisz na czacie `!kapitan` — zostaniesz kapitanem.
5. Dorzuć boty, żeby zagrać rundę nożową: w konsoli serwera `css_zsz_bots`
   (uzupełni składy do 10).
6. Poczekaj na koniec rozgrzewki → startuje **runda nożowa** (tylko noże, brak kupowania,
   spam `[ZSZ] LIVE!`).
7. Wygraj rundę nożową ze swoimi botami. Gdy Twoja drużyna wygra, otworzy się menu wyboru
   strony (klawisze 1–2) — wybierz stronę.
   - Jeśli wygrają boty z drugiej drużyny, a Ty jesteś jedynym kapitanem, plugin pozwoli
     **Tobie** wybrać stronę (opcja `SidePick.AllowOtherCaptainFallback`).
   - Jeśli nic nie wybierzesz w 45 s, plugin sam zdecyduje (drużyna zostaje na swojej stronie).
8. Mecz startuje od **0:0**, leci `[ZSZ] LIVE!` i `[ZSZ] Życzymy miłej rozgrywki!`.
9. Graj rundę — po jej zakończeniu zobaczysz podsumowanie na czacie i panel na środku ekranu.
10. `!stats` pokaże pełną tabelę.

### Wariant B: bez botów, bez trybu testowego

Użyj komend administratora, żeby przejść przez kroki szybciej:

```
css_zsz_testmode        // włącz tryb testowy (albo edytuj JSON)
css_zsz_skipwarmup      // pomiń 1:45 i przejdź do rundy nożowej
css_zsz_pick t          // wymuś wybór strony (pomiń czekanie na kapitana)
css_zsz_forcestart      // pomiń wszystko i startuj mecz od 0:0
css_zsz_status          // sprawdź, w której fazie jesteś
css_zsz_reset           // zacznij od nowa
```

### Czego szukać podczas testu

- [ ] Licznik rozgrzewki pokazuje `1:45` i odlicza.
- [ ] Po dojściu kompletu graczy czas przeskakuje na `0:25` + komunikat na czacie.
- [ ] `!kapitan` działa, drugi `!kapitan` na tej samej drużynie jest odrzucany.
- [ ] W rundzie nożowej: brak możliwości kupowania (menu zakupów się nie otwiera),
      0 $, pełna kamizelka, tylko nóż.
- [ ] `[ZSZ] LIVE!` pojawia się kilka razy.
- [ ] Po wyborze strony wynik jest `0:0`, a komunikaty lecą ponownie + „Życzymy miłej rozgrywki!".
- [ ] Po każdej rundzie widać statystyki (czat + ekran).
- [ ] Liczby Z/A/Ś zgadzają się z tablicą wyników CS2 (Tab).

### Przed turniejem

```json
  "TestMode": { "Enabled": false, "PlayersRequired": 1 }
```

---

## 7. Rozwiązywanie problemów

| Objaw | Przyczyna / rozwiązanie |
|---|---|
| Plugin się nie ładuje | Sprawdź logi w `addons/counterstrikesharp/logs/`. Najczęściej: zła nazwa folderu (musi być `ZSZMatch`) albo brak Metamodu. |
| `requires a newer version of CounterStrikeSharp` | Zaktualizuj CounterStrikeSharp do wersji 1.0.376+. |
| Rozgrzewka nie kończy się | Inny plugin/config zmienia `mp_warmup_*`. Sprawdź logi — plugin ostrzega, gdy `mp_warmup_end` nie zadziała. |
| Polskie znaki są krzakami | Plik JSON musi być w **UTF-8**. Zapisz `ZSZMatch.json` jako UTF-8 (bez BOM też jest OK). |
| Komendy `css_*` nie działają w konsoli | Wpisz dokładnie z przedrostkiem `css_`, np. `css_zsz_status`. |
| Gracz nie może zostać kapitanem | Może trwa już runda nożowa (kapitanowie są zablokowani) — użyj `css_zsz_reset`, żeby zacząć od nowa. |
| Mecz nie kończy się przy 13:0 | Ktoś nadpisał `mp_match_can_clinch`. Musi być `1` — szczegóły w [sekcji 5](#zasady-faceit-wymuszane-przez-plugin). |
| Po `css_plugins reload` zniknęły statystyki | Przeładowanie w trakcie meczu gubi kapitanów i statystyki (wynik zostaje). Nie przeładowuj w trakcie meczu. |
| Kapitan wyszedł przed wyborem strony | Plugin sam przekaże wybór drugiemu kapitanowi, a jeśli go nie ma — zdecyduje po czasie. W ostateczności: `css_zsz_pick ct`. |
| Gracze nie mogą się połączyć | Sprawdź firewall (UDP 27015) i to samo `sv_lan 1` po obu stronach. Przy `-insecure` klient też może wymagać `-insecure`. |
| Chcę zacząć mecz od nowa na tej samej mapie | `css_zsz_reset` — czyści wszystko i wraca do rozgrzewki. |

---

## 8. Budowanie ze źródeł

Wymagany **.NET 10 SDK** (sprawdź: `dotnet --list-sdks`).

Najprościej — użyj dołączonego skryptu (Windows):

```bat
build.bat                     :: tylko kompilacja
build.bat C:\cs2server        :: kompilacja + skopiowanie pluginu na serwer
```

Albo ręcznie:

```bash
cd src/ZSZMatch
dotnet build -c Release
```

Wynik: `src/ZSZMatch/bin/Release/net10.0/` — skopiuj **cały folder** jako
`addons/counterstrikesharp/plugins/ZSZMatch/`:

```
ZSZMatch.dll
ZSZMatch.example.json
ZSZMatch.pdb            (opcjonalnie, pomaga w diagnostyce)
```

### Weryfikacja konfiguracji (narzędzie pomocnicze)

```bash
cd tools/ZSZConfigCheck
dotnet run -- "C:/sciezka/do/ZSZMatch.example.json"
```

Sprawdza, czy plik JSON parsuje się poprawnie i czy każdy klucz trafia w swoje pole
(przydatne, jeśli dopisujesz własne opcje).

### Struktura projektu

```
src/ZSZMatch/
    ZSZMatchPlugin.cs          główna klasa: zdarzenia, komendy, orkiestracja
    ZSZMatch.example.json      domyślna konfiguracja (kopiowana do configs/)
    Config/
        ZSZMatchConfig.cs      wszystkie opcje turnieju
    Core/
        MatchPhase.cs          fazy meczu
        MatchState.cs          stan meczu + statystyki graczy
        Lang.cs                wszystkie teksty widoczne dla graczy (PL)
        CvarManager.cs         nakładanie cvarów dla każdej fazy
        WarmupController.cs    rozgrzewka 1:45 → 0:25
        CaptainService.cs      !kapitan
        KnifeRoundController.cs runda nożowa
        SidePickController.cs  wybór strony startowej
        StatsTracker.cs        statystyki po każdej rundzie
    Util/
        Chat.cs                czat i HUD
tools/ZSZConfigCheck/          weryfikacja pliku konfiguracyjnego
```

---

## Licencja i uwagi

Projekt przygotowany na szkolny turniej LAN. Możesz go dowolnie modyfikować —
wszystkie teksty dla graczy są w `Core/Lang.cs`, a ustawienia turnieju w pliku JSON.
