# OLED Protect 🌙

> **English:** OLED Protect guards OLED monitors against burn-in on multi-monitor setups. A screen that sits idle
> fades to black, dims, or shows a calm screensaver: the real night sky of today, or a dark map with the live
> air traffic overhead. Both span all your screens as one panorama. It adds a work mode, a quick panel (Alt+`),
> one brightness schedule for all screens, taskbar dimming and a video-aware general timer. The interface is Dutch.

Bescherm je OLED-schermen tegen inbranden als je met meerdere schermen werkt. Kijk je op het ene scherm en staat het
andere stil, dan gaat dat andere scherm na een ingestelde tijd **zacht op zwart**, **dimt** het, of laat het een
rustige screensaver zien: de **echte sterrenhemel** of de **vliegtuigen van dit moment**.

## Wat kan het

### Per scherm
- **Zwart, dimmen, sterren of vliegtuigen** na een eigen wachttijd. Zwart komt zacht op en gaat snel weer weg; dimmen
  gaat geleidelijk, via het scherm zelf (DDC/CI).
- **Wakker houden zolang** de muis op dat scherm staat, het actieve venster erop staat, of je ergens muis of
  toetsenbord gebruikt.
- **Toch uit na X minuten** zonder muis of toets, ook als er een venster op staat.

### Sterrenhemel
- De hemel boven Nederland van vandaag: ruim 500 heldere sterren, de bekende sterrenbeelden met Nederlandse namen,
  de planeten (Saturnus met ring, Jupiter met wolkenbanden) en de maan met de juiste fase.
- Alles beweegt heel langzaam en vrijwel alle pixels zijn echt uit, dus er brandt niets in.
- Af en toe een vallende ster; tijdens een meteorenzwerm (Perseïden, Leoniden, …) vanuit de echte richting.

### Vliegtuigen
- Een donkere kaart (hoofdwegen, kleinere wegen, kust, rivieren, plaatsnamen) met de **echte vliegtuigen van dit
  moment**: eigen symbolen per soort toestel, vluchtnummer, route (bijvoorbeeld RTM→TFS), hoogte met ↑/↓,
  landingslichten bij naderende toestellen en een gouden gloed voor bijzondere toestellen (A380, 747, Beluga, …).
- Vliegvelden met hun hele terrein (banen, taxibanen, platforms), code, naam, land en een paar weetjes.
- **Rond je speldje**, of als **rondreis** langs drukke vliegvelden in de landen of plekken die je invult
  (bijvoorbeeld "Nederland, Tenerife, JFK"), het liefst waar op dat moment geland en opgestegen wordt. Af en toe
  volgt hij een tijdje één vlucht.
- De kaart drijft, draait en zoomt langzaam en start elke keer ergens anders; niets blijft op dezelfde pixels.

Beide screensavers lopen over twee schermen naast elkaar als **één panorama**, inclusief de ruimte tussen de schermen
(in te stellen en te kalibreren in Algemeen). Slaapt maar één scherm, dan komt alles op dat scherm.

### Altijd bij de hand
- **Snelmenu** zoals Win+P: houd **Alt** vast en tik **`** (de toets onder Esc), of klik op het pictogram in het
  systeemvak. Met werkmodus, de screensavers, en **Helderheid**: sta op die rij en draai aan het muiswiel om alle
  schermen per 5 % lichter of donkerder te zetten.
- **Werkmodus**: alle schermen blijven aan (video, lezen over twee schermen), ook voor een uur.
- **Helderheid op tijden**: één schema voor alle schermen, bijvoorbeeld 08:00 → 80 %, 19:00 → 50 %, 23:00 → 30 %.
- **Taakbalk dimmen**: staat de muis 5 seconden niet bij de taakbalk, dan wordt die donkerder; je klikt er gewoon
  doorheen. Ga je met de muis naar een taakbalk, dan lichten ze op alle schermen tegelijk op en worden ze ook
  samen weer donker.
- **Alle schermen uit als je weg bent**, behalve het scherm waarop een video speelt.
- **Apps die het scherm aan houden**: zolang zo'n app openstaat (VLC, Netflix, OBS, Zoom, Teams of een eigen keuze)
  blijft het scherm waarop hij staat aan, of alle schermen.
- Start verborgen met Windows en staat in het systeemvak.

### Privacy
Alles blijft op je pc, in `%APPDATA%\OledProtect`, met deze uitzonderingen zolang er vliegtuigen in beeld zijn (de
screensaver of het voorbeeld in de instellingen):
- elke 8 seconden gaat de plek van je speldje of van het vliegveld in beeld, **afgerond op ongeveer 1 km**, naar
  adsb.fi (of adsb.lol als reserve) om de vliegtuigen eromheen op te halen;
- kaarttegels rond die plek komen van OpenFreeMap (en worden bewaard), vluchtroutes van adsbdb.com (alleen het
  vluchtnummer gaat erheen);
- wat je bij het speldje zoekt, gaat naar Nominatim.

De sterrenhemel gebruikt geen internet. Bij het verwijderen vraagt de installer of je instellingen ook weg mogen.

## Installeren

Download de nieuwste versie bij [Releases](../../releases):

| Bestand | Wat |
|---|---|
| `OledProtect-…-Setup.exe` | Installatieprogramma (klein; haalt zo nodig de .NET 8 Desktop Runtime op) |
| `OledProtect-…-Portable.exe` | Eén bestand: dubbelklikken en klaar; instellingen in `Data\` naast de exe |
| `OledProtect-…-Portable-x64.zip` | Hetzelfde als map: uitpakken en starten |

Windows 10 of 11. Dimmen en helderheid werken met schermen die DDC/CI ondersteunen (de meeste moderne schermen).
De bestanden zijn niet digitaal ondertekend; waarschuwt Windows SmartScreen, kies dan **Meer info → Toch uitvoeren**.

**Tip:** zet in Windows (Instellingen → Systeem → Energie) *Scherm uitschakelen* op **Nooit** of veel langer;
OLED Protect regelt het dan per scherm.

Heette je versie nog "OLED Space Sleeper"? Installeer gewoon over de oude heen: je instellingen, kaarten en
autostart worden overgenomen en de oude programmabestanden opgeruimd.

## Zelf bouwen

Nodig: de [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (en [Inno Setup 6](https://jrsoftware.org/isinfo.php) voor de installer).

```powershell
dotnet test OledProtect.sln                 # alle tests
dotnet run --project OledProtect            # starten
.\build.ps1                                 # installer, portable exe en zip
```

Een release maken: tag `v<versie>` op `master` pushen. GitHub Actions bouwt dan alle bestanden en zet een
concept-release klaar.

De sterrendata komt uit [d3-celestial](https://github.com/ofrohn/d3-celestial/tree/master/data)
(`stars.6.json`, `constellations.lines.json`, `constellations.json`), omgezet met
`python tools/make_sky_data.py <map> OledProtect/Assets/Sky/sky.txt`. Het icoon komt uit `tools/make_icon.ps1`.

## Waar zit wat

| Onderdeel | Map |
|---|---|
| Wanneer is een scherm stil | `OledProtect/Features/MonitorIdleDetection/` |
| Schermen herkennen en volgen | `OledProtect/Features/MonitorInformation/`, `Features/MonitorState/` |
| Zwart, fade en screensaver-vensters | `OledProtect/Features/MonitorBlackout/` |
| Dimmen en helderheid (DDC/CI) | `OledProtect/Features/MonitorDimming/`, `Features/BrightnessSchedule/` |
| Sterrenhemel | `OledProtect/Features/Sky/` |
| Vliegtuigen en rondreis | `OledProtect/Features/Flights/` |
| Taakbalk dimmen | `OledProtect/Features/TaskbarDim/` |
| Werkmodus en sneltoets | `OledProtect/Features/WorkMode/` |
| Apps die het scherm aan houden | `OledProtect/Features/KeepAwakeApps/` |
| Instellingen opslaan | `OledProtect/Storage/`, `Features/UserSettings/` |
| Vensters (instellingen, snelmenu, kalibratie) | `OledProtect/MainWindow.xaml`, `OledProtect/UI/` |
| Opstarten | `OledProtect/Infrastructure/` |

## Licentie

[MIT](LICENSE) © 2026 guillermozuur-design. Gegevens en bibliotheken van anderen staan in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
