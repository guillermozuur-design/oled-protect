# OLED Sleeper (eigen versie)

Aangepaste versie van [OLED Sleeper 2.2.0](https://github.com/Quorthon13/OLED-Sleeper) (MIT, Quorthon13),
voor 2× LG UltraGear+ OLED (2560×1440, DDC/CI werkt). Doel: inbranden voorkomen als één scherm stilstaat
terwijl je op het andere werkt.

## Wat het doet

Origineel: per scherm bijhouden of het idle is (muis op dat scherm / actief venster / systeeminput) en na
de ingestelde tijd **blackout** of **dimmen** (DDC/CI).

Toegevoegd in deze versie:

| Functie | Wat | Waar in de code |
|---|---|---|
| **Fade** | Zwart overlay faded in 1,5 s in en 0,25 s uit. Dimmen gaat in 8 stapjes (~0,7 s). | `Features/MonitorBlackout/Services/OverlayWindow.cs`, `MonitorDimmingService.FadeBrightness` |
| **Sterrenhemel** | Gedrag "Sterren": de echte hemel boven Nederland van vannacht (vanaf 22:00) met alleen de helderdere sterren (tot mag 4,0), ruim 20 bekende sterrenbeelden met Nederlandse namen, planeten en de maan (met fase) op hun echte plek. Hemel draait 30× versneld en de blik pant langzaam. Twee schermen naast elkaar zijn één doorlopend beeld (één gezamenlijke camera van 150°, elk scherm toont zijn helft). Planeten als bolletjes: Saturnus met ring, Jupiter met strepen, Mars met poolkap. Af en toe een rustige vallende ster die ook over beide schermen doorloopt (beide schermen rekenen dezelfde meteoren uit de klok), tijdens een meteorenzwerm (Leoniden, Perseïden, …) vanuit de echte radiant. Bijna alle pixels echt zwart, niets staat stil. | `Features/Sky/` |
| **Helderheid op tijden** | Per scherm een schema, bv. 08:00 → 80%, 19:00 → 50%, 23:00 → 30%. Wordt alleen geschreven als het schema verspringt; zelf bijstellen blijft staan tot de volgende tijd. Is een scherm gedimd, dan wordt het bij wakker worden op de schema-waarde gezet. | `Features/BrightnessSchedule/` |
| **Snelmenu (Alt+`)** | Zoals Win+P: paneel rechtsonder met alle keuzes, huidige stand gemarkeerd. Houd **Alt** vast en tik **`**: eerste tik opent, elke volgende tik schuift door, **loslaten = kiezen** (niet doorgeschoven = niets veranderen). Daarna blijft het paneel een paar tellen staan om te klikken (langer zolang de muis erop staat). Esc sluit. Keuzes: Bescherming aan, Werkmodus, Werkmodus 1 uur, Sterren bekijken, App openen. | `UI/Views/QuickPanelWindow.xaml(.cs)` |
| **Werkmodus** | Alle schermen meteen wakker en blijven aan (video, lezen over twee schermen). Ook "1 uur", daarna gaat de bescherming vanzelf weer aan. Tray-icoon krijgt een oranje pauze-badge + melding. Start altijd uit. | `Features/WorkMode/` |
| **Sneltoets instelbaar** | Onderaan het hoofdvenster: klik op het vak en druk je combinatie. Opgeslagen in `Data/hotkey.json`. Is Alt+` bij een ander programma in gebruik, dan Ctrl+Alt+O. | `GlobalHotkeyService`, `MainWindow.xaml.cs` |
| **Muis-limiet** | Per scherm onder "Mouse cursor is on this monitor": *toch uit na X min zonder beweging*. Een geparkeerde muis houdt het scherm dan niet eindeloos wakker; muis bewegen of typen maakt het weer wakker. | `MonitorIdleDetectionService.MousePresenceCounts` |
| **DDC/CI-fix** | Origineel keurde de LG UltraGear-schermen af (geven geen "capabilities string"); nu telt een werkende helderheidsuitlezing als ondersteuning. | `MonitorInfoProvider.cs` |

Nieuwe teksten in de app zijn Nederlands, de rest van de originele UI is nog Engels.
Waarnemerslocatie staat vast op midden-Nederland (52,1° N, 5,1° O) in `SkyView.cs`.

## Bouwen en starten

Er staat geen .NET SDK systeembreed; de SDK staat lokaal in `.tmp\dotnet` (gitignored). `.tmp\dn.ps1`
zet de juiste omgevingsvariabelen en roept die `dotnet` aan.

```powershell
# testen (204 tests)
powershell -File .tmp\dn.ps1 test OLED-Sleeper.sln

# portable build (instellingen in artifacts\portable\Data)
powershell -File .tmp\dn.ps1 publish OLED-Sleeper\OLED-Sleeper.csproj -c Release -r win-x64 --self-contained false -p:Portable=true -o artifacts\portable
artifacts\portable\OLED-Sleeper.exe

# plaatje van de sterrenhemel renderen (zonder de app)
$env:SKY_SNAPSHOT="$PWD\.tmp\sky.png"; powershell -File .tmp\dn.ps1 test OLED-Sleeper.sln --filter SkyViewRenderTests
```

Is `.tmp` weg, installeer de SDK opnieuw lokaal met `dotnet-install.ps1 -Channel 8.0 -InstallDir .tmp\dotnet -NoPath`
(script van https://dot.net/v1/dotnet-install.ps1).

Sterrendata opnieuw genereren: download `stars.6.json`, `constellations.lines.json` en `constellations.json`
uit [d3-celestial](https://github.com/ofrohn/d3-celestial/tree/master/data) naar `.tmp\sky` en draai
`python -I tools\make_sky_data.py .tmp\sky OLED-Sleeper\Assets\Sky\sky.txt`.

## Status (16-11-2026)

- Draait vanaf `artifacts\portable\OLED-Sleeper.exe`; de oude geïnstalleerde versie is verwijderd.
- 204 unit tests groen, waaronder astronomie tegen echte gebeurtenissen (grote conjunctie Jupiter/Saturnus
  21-12-2020, lente-equinox 2000, nieuwe/volle maan april 2024, hoogte Poolster).
- Live getest: DDC/CI op beide schermen, snelmenu (vasthouden + tikken + loslaten, snelle tik), werkmodus
  1 uur, sneltoets-instelling, knoppen in het schema-blok.
- Nog niet live gezien: de sterrenhemel op de echte schermen (wel als render), maan/planeten in beeld,
  fade bij dimmen, helderheidsschema dat op een tijdstip verspringt.
- Niet automatisch opstarten met Windows: zet dat aan via tray → "Run at Startup".

## Ideeën voor later
- Waarnemerslocatie en tijdversnelling instelbaar
- Rest van de UI vertalen
- Echte installer (Inno Setup-script staat in `installer/`)

## Licenties
- Code: MIT (origineel Quorthon13, zie `LICENSE`).
- Sterrendata: d3-celestial © 2015 Olaf Frohn, BSD-3-Clause (`OLED-Sleeper/Assets/Sky/LICENSE-d3-celestial.txt`).
