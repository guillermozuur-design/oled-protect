# OLED Space Sleeper 🌙

> **English:** OLED Space Sleeper protects OLED monitors against burn-in on multi-monitor setups. When a screen
> sits idle it fades to black, dims, or shows a real-time **night sky** screensaver (real stars, constellations,
> planets and the Moon) that spans all your screens as one panorama. Includes a work mode, a Win+P-style quick
> panel (Alt+`), brightness schedules and a video-aware general timer. The UI is in Dutch.
> It is a fork of [OLED Sleeper](https://github.com/Quorthon13/OLED-Sleeper) by Quorthon13 (MIT).

Bescherm je OLED-schermen tegen inbranden als je met meerdere schermen werkt. Kijk je op het ene scherm iets en
staat het andere stil, dan gaat dat andere scherm na een ingestelde tijd **zacht op zwart**, **dimt** het, of laat
het de **echte sterrenhemel** zien.

Gebaseerd op [OLED Sleeper 2.2.0](https://github.com/Quorthon13/OLED-Sleeper) van Quorthon13; dit is een eigen,
onofficiële versie met veel uitbreidingen.

## Wat kan het

**Per scherm**
- **Zwart, dimmen of sterren** na een eigen wachttijd. Zwart faded zacht in en snel weer uit; dimmen gaat geleidelijk (DDC/CI).
- **Wakker houden als** de muis op dat scherm staat, het actieve venster er staat, of je ergens muis/toetsenbord gebruikt.
- **Maar toch uit na X minuten** zonder muis of toets, ook als er een venster gefocust op staat.
- **Helderheid op tijden**, bijvoorbeeld 08:00 → 80 %, 19:00 → 50 %, 23:00 → 30 %.

**Sterrenhemel-screensaver**
- De hemel boven Nederland: ruim 500 heldere sterren uit een echte catalogus, de bekende sterrenbeelden met Nederlandse namen,
  de planeten (Saturnus met ring, Jupiter met wolkenbanden) en de maan met de juiste fase.
- De hemel draait versneld en de blik beweegt langzaam: niets staat stil en vrijwel alle pixels zijn echt uit, dus geen inbranden.
- Twee schermen naast elkaar vormen **één doorlopend panorama**, inclusief de ruimte tussen de schermen (instelbaar en te **kalibreren** met schuine lijnen).
- Af en toe een realistische **vallende ster**; tijdens een meteorenzwerm (Perseïden, Leoniden, …) vanuit de echte richting.

**Altijd bij de hand**
- **Werkmodus**: alle schermen blijven aan (video, lezen over twee schermen), ook voor een uur.
- **Snelmenu** zoals Win+P: houd **Alt** vast en tik **`** (toets onder Esc), tik verder om te kiezen, loslaten = toepassen. Sneltoets is instelbaar.
- **Alle schermen uit als je weg bent** na X minuten — behalve het scherm waarop een video speelt.
- Start verborgen met Windows; staat in het systeemvak.

## Installeren

Download de nieuwste versie bij [Releases](../../releases):

| Bestand | Wat |
|---|---|
| `OLED-Space-Sleeper-…-Setup.exe` | Installatieprogramma (klein; haalt zo nodig de .NET 8 Desktop Runtime op) |
| `OLED-Space-Sleeper-…-Portable-x64.zip` | Uitpakken en starten, niets installeren; instellingen staan in `Data\` naast de exe |

Windows 10 of 11. Dimmen en helderheid op tijden werken met schermen die DDC/CI ondersteunen (de meeste moderne schermen).

**Tip:** zet in Windows (Instellingen → Systeem → Energie) *Scherm uitschakelen* op **Nooit** of veel langer; OLED Space Sleeper regelt het dan per scherm.

## Zelf bouwen

Nodig: de [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test OLED-Sleeper.sln                       # 209 tests
dotnet run --project OLED-Sleeper                  # starten
.\build.ps1                                        # installer + portable zip (Inno Setup nodig voor de installer)
```

Een release maken: tag `v<versie>` op `master` pushen; GitHub Actions bouwt dan de installer en de portable zip
en zet een concept-release klaar.

Sterrendata opnieuw genereren: download `stars.6.json`, `constellations.lines.json` en `constellations.json` uit
[d3-celestial](https://github.com/ofrohn/d3-celestial/tree/master/data) en draai
`python tools/make_sky_data.py <map> OLED-Sleeper/Assets/Sky/sky.txt`. Het icoon komt uit `tools/make_icon.ps1`.

## Waar zit wat

| Onderdeel | Map |
|---|---|
| Sterrenhemel (catalogus, astronomie, weergave, panorama) | `OLED-Sleeper/Features/Sky/` |
| Wanneer is een scherm stil (incl. limieten, video-uitzondering) | `OLED-Sleeper/Features/MonitorIdleDetection/` |
| Zwart / fade | `OLED-Sleeper/Features/MonitorBlackout/` |
| Dimmen en helderheid (DDC/CI) | `OLED-Sleeper/Features/MonitorDimming/`, `Features/BrightnessSchedule/` |
| Werkmodus en sneltoets | `OLED-Sleeper/Features/WorkMode/` |
| Vensters (hoofdvenster, snelmenu, kalibratie) | `OLED-Sleeper/MainWindow.xaml`, `OLED-Sleeper/UI/` |

## Licenties

- Code: MIT — origineel © Diego Demétrio (Quorthon13), zie [`LICENSE`](LICENSE). Uitbreidingen vallen onder dezelfde licentie.
- Sterrendata: [d3-celestial](https://github.com/ofrohn/d3-celestial) © Olaf Frohn, BSD-3-Clause — zie
  [`OLED-Sleeper/Assets/Sky/LICENSE-d3-celestial.txt`](OLED-Sleeper/Assets/Sky/LICENSE-d3-celestial.txt).

Dit project is niet verbonden aan de makers van OLED Sleeper of d3-celestial.
