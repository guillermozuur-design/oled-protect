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
| **Sterren-screensaver** | Derde gedrag "Sterren": zwarte hemel, twinkelende sterren die langzaam omlaag drijven en af en toe een vallende ster. Bijna alle pixels echt uit, niets staat stil. | `Features/MonitorBlackout/Services/StarfieldView.cs` |
| **Helderheid op tijden** | Per scherm een schema, bv. 08:00 → 80%, 19:00 → 50%, 23:00 → 30%. Wordt alleen geschreven op het moment dat het schema verspringt, dus zelf bijstellen blijft staan tot de volgende tijd. Is een scherm op dat moment gedimd, dan wordt het bij wakker worden op de schema-waarde gezet. | `Features/BrightnessSchedule/` |
| **Werkmodus** | **Ctrl+Alt+O** (of tray-menu "Werkmodus (alles aan)"): alle schermen meteen wakker en blijven aan, bv. bij video of lezen over twee schermen. Nog eens drukken = bescherming weer aan. Tray-icoon krijgt een oranje pauze-badge + melding. Start altijd uit. | `Features/WorkMode/`, `UI/Services/TrayIconService.cs` |
| **Screensaver bekijken** | Tray-menu "Sterren-screensaver bekijken": toont de sterren op het scherm onder de muis tot je de muis beweegt. | `TrayIconService.ShowScreensaverPreview` |

Nieuwe teksten in de app zijn Nederlands, de rest van de originele UI is nog Engels.

## Bouwen en starten

Er staat geen .NET SDK systeembreed; de SDK staat lokaal in `.tmp\dotnet` (gitignored). `.tmp\dn.ps1`
zet de juiste omgevingsvariabelen en roept die `dotnet` aan.

```powershell
# testen (192 tests)
powershell -File .tmp\dn.ps1 test OLED-Sleeper.sln

# portable build (instellingen naast de exe, los van de geïnstalleerde versie)
powershell -File .tmp\dn.ps1 publish OLED-Sleeper\OLED-Sleeper.csproj -c Release -r win-x64 --self-contained false -p:Portable=true -o artifacts\portable
artifacts\portable\OLED-Sleeper.exe
```

Is `.tmp` weg, installeer de SDK opnieuw lokaal:
`.tmp\dotnet-install.ps1 -Channel 8.0 -InstallDir .tmp\dotnet -NoPath` (script van https://dot.net/v1/dotnet-install.ps1).

Let op: deze versie en de geïnstalleerde originele OLED Sleeper kunnen niet tegelijk draaien (zelfde
single-instance mutex). Sluit de oude eerst via het tray-icoon → Exit.

## Status

- Bouwt, 192 unit tests groen (169 origineel + 23 nieuw: schema-berekening, fade, schema tijdens dimmen, werkmodus, pauze).
- Nog niet getest op de echte schermen: zie de checklist hieronder.

### Handmatige checklist
- [ ] Tray → "Sterren-screensaver bekijken": sterren zichtbaar, weg bij muisbeweging
- [ ] Scherm 2 op "Sterren", korte idle-tijd: fade-in, en snel weg als muis erheen gaat
- [ ] Blackout: zachte fade
- [ ] Ctrl+Alt+O: alles wakker, icoon met pauze-badge, nog eens = weer actief
- [ ] Helderheid op tijden: tijd net in de toekomst zetten en kijken of hij verspringt

## Ideeën voor later
- Werkmodus automatisch uit na X uur
- Sneltoets instelbaar maken
- Rest van de UI vertalen
