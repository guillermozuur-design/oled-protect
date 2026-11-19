# OledProtect 🌙

> **English:** OledProtect protects OLED monitors against burn-in on multi-monitor setups. When a screen
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
- **Zwart, dimmen, sterren of vliegtuigen** na een eigen wachttijd. Zwart faded zacht in en snel weer uit; dimmen gaat geleidelijk (DDC/CI).
- **Wakker houden als** de muis op dat scherm staat, het actieve venster er staat, of je ergens muis/toetsenbord gebruikt.
- **Maar toch uit na X minuten** zonder muis of toets, ook als er een venster gefocust op staat.
- **Helderheid op tijden**, bijvoorbeeld 08:00 → 80 %, 19:00 → 50 %, 23:00 → 30 %.

**Sterrenhemel-screensaver**
- De hemel boven Nederland: ruim 500 heldere sterren uit een echte catalogus, de bekende sterrenbeelden met Nederlandse namen,
  de planeten (Saturnus met ring, Jupiter met wolkenbanden) en de maan met de juiste fase.
- De hemel draait versneld en de blik beweegt langzaam: niets staat stil en vrijwel alle pixels zijn echt uit, dus geen inbranden.
- Twee schermen naast elkaar vormen **één doorlopend panorama**, inclusief de ruimte tussen de schermen (instelbaar en te **kalibreren** met schuine lijnen).
- Af en toe een realistische **vallende ster**; tijdens een meteorenzwerm (Perseïden, Leoniden, …) vanuit de echte richting.

**Vliegtuigen-screensaver**
- Een donkere kaart (hoofdwegen, kleinere wegen als dun wit lijntje en de kustlijn, plaatsnamen, luchthavens met hun landingsbanen en code) rond een **speldje** dat je zelf plaatst, met de
  **echte vliegtuigen van dit moment** erboven: eigen symbolen per soort toestel (jumbo, widebody, 737/A320, regionaal,
  klein vliegtuig, helikopter), vluchtnummer, route (bijv. RTM→TFS; anders het toesteltype) en hoogte met ↑/↓
  (stijgt of daalt). Toestellen in de landing hebben kleine landingslichten vanuit de vleugels; bijzondere toestellen (A380, 747, Beluga,
  An-124 ...) krijgen een gouden gloed en hun naam. Rivieren en kanalen als donkerblauwe lijn; bij elk vliegveld ook
  het aantal banen, de langste baan en de hoogte. Bij het volgen van een vlucht kiest hij liefst een landing of start.
- De kaart drijft, draait en zoomt heel langzaam in een patroon dat zich niet herhaalt en start elke keer ergens anders,
  zodat niets op dezelfde pixels blijft. Over twee schermen één doorlopende kaart.
- **Rondreis** (optie): elke 10–60 seconden fadet het beeld naar een ander druk vliegveld in de landen,
  plekken, eilanden of vliegveldcodes die je invult (bijv. "Nederland, Amerika, Tenerife, JFK"; elk krijgt evenveel beurten), afwisselend op het ene en het andere scherm. Om de paar vliegvelden volgt hij een tijdje
  één vlucht (vluchtnummer, route, type, hoogte, snelheid). Voor elk vliegveld kijkt hij eerst bij een paar velden van dat land of die plek hoeveel er op dat
  moment landt en opstijgt, en kiest het drukste. Een vliegveld waar op dat moment bijna niets vliegt
  (minder dan 2 toestellen in de lucht) ruilt hij in voor een ander veld in hetzelfde land of dezelfde plek; is daar
  alles rustig, dan komt die plek minder vaak (maar wel) langs. Bij elk vliegveld staan code, naam en land. Vliegveld en gevolgd vliegtuig staan elke keer
  ergens anders en bewegen langzaam. Slaapt maar één scherm,
  dan komt alles (ook het speldje) op dat scherm. Zo is er altijd wat te zien.
- Start je zelf een screensaver (snelmenu, tray of instellingen) terwijl een scherm al slaapt, dan wordt dat scherm eerst
  gewekt: je ziet alleen wat je zelf koos.
- Kaart van OpenStreetMap (via OpenFreeMap, één keer per plek opgehaald en bewaard), vliegtuigen van adsb.fi met adsb.lol als reserve (open ADS-B-data), routes van adsbdb.com.
- **Privacy**: zolang er vliegtuigen in beeld zijn (de screensaver of het voorbeeld in de instellingen) vraagt de app
  elke 8 seconden de vliegtuigen op rond je speldje of het vliegveld in beeld. Die plek gaat **afgerond op ongeveer
  1 km** naar adsb.fi (of adsb.lol als reserve). De kaarttegels rond die plek komen van OpenFreeMap, vluchtroutes
  (alleen het vluchtnummer) van adsbdb.com, en wat je zoekt gaat naar Nominatim. Verder blijft alles op je pc, in
  `%APPDATA%\OledProtect`. Bij het verwijderen vraagt de installer of die map ook weg mag.

**Taakbalk dimmen** (optie in Algemeen): staat de muis 5 seconden niet bij de taakbalk, dan wordt die donkerder (een
doorzichtige laag waar je doorheen klikt); met de muis erheen is hij meteen weer gewoon. Niet bij volledig scherm.

**Altijd bij de hand**
- **Werkmodus**: alle schermen blijven aan (video, lezen over twee schermen), ook voor een uur.
- **Snelmenu** zoals Win+P (ook met een klik op het pictogram in het systeemvak; dubbelklik = instellingen): houd **Alt** vast en tik **`** (toets onder Esc), tik verder om te kiezen, loslaten = toepassen. Sneltoets is instelbaar.
  In het snelmenu staat ook **Helderheid**: sta op die rij en draai aan het muiswiel om alle schermen per 5% lichter
  of donkerder te zetten (via het scherm zelf, tot de volgende stap van het helderheidsschema).
- **Alle schermen uit als je weg bent** na X minuten — behalve het scherm waarop een video speelt.
- **Apps die het scherm aan houden**: vink apps aan (VLC, Netflix-app, OBS, Zoom, Teams, …) of kies zelf een open programma. Zolang die app openstaat blijft het scherm waar hij op staat aan — of alle schermen — wat de timers ook zeggen.
- Start verborgen met Windows; staat in het systeemvak.

## Installeren

Download de nieuwste versie bij [Releases](../../releases):

| Bestand | Wat |
|---|---|
| `OledProtect-…-Setup.exe` | Installatieprogramma (klein; haalt zo nodig de .NET 8 Desktop Runtime op) |
| `OledProtect-…-Portable.exe` | Eén bestand: dubbelklikken en klaar, niets installeren; instellingen in `Data\` naast de exe |
| `OledProtect-…-Portable-x64.zip` | Hetzelfde als map: uitpakken en starten |

De bestanden zijn niet digitaal ondertekend. Waarschuwt Windows SmartScreen, kies dan **Meer info → Toch uitvoeren**.

Windows 10 of 11. Dimmen en helderheid op tijden werken met schermen die DDC/CI ondersteunen (de meeste moderne schermen).

**Tip:** zet in Windows (Instellingen → Systeem → Energie) *Scherm uitschakelen* op **Nooit** of veel langer; OledProtect regelt het dan per scherm.

## Zelf bouwen

Nodig: de [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet test OledProtect.sln                       # 331 tests
dotnet run --project OLED-Sleeper                  # starten
.\build.ps1                                        # installer + portable zip (Inno Setup nodig voor de installer)
```

Een release maken: tag `v<versie>` op `master` pushen; GitHub Actions bouwt dan de installer en de portable zip
en zet een concept-release klaar.

Sterrendata opnieuw genereren: download `stars.6.json`, `constellations.lines.json` en `constellations.json` uit
[d3-celestial](https://github.com/ofrohn/d3-celestial/tree/master/data) en draai
`python tools/make_sky_data.py <map> OledProtect/Assets/Sky/sky.txt`. Het icoon komt uit `tools/make_icon.ps1`.

## Waar zit wat

| Onderdeel | Map |
|---|---|
| Sterrenhemel (catalogus, astronomie, weergave, panorama) | `OledProtect/Features/Sky/` |
| Vliegtuigen (kaarttegels, vliegtuigdata, drift, weergave) | `OledProtect/Features/Flights/` |
| Wanneer is een scherm stil (incl. limieten, video-uitzondering) | `OledProtect/Features/MonitorIdleDetection/` |
| Apps die het scherm aan houden | `OledProtect/Features/KeepAwakeApps/` |
| Zwart / fade | `OledProtect/Features/MonitorBlackout/` |
| Dimmen en helderheid (DDC/CI) | `OledProtect/Features/MonitorDimming/`, `Features/BrightnessSchedule/` |
| Werkmodus en sneltoets | `OledProtect/Features/WorkMode/` |
| Vensters (hoofdvenster, snelmenu, kalibratie) | `OledProtect/MainWindow.xaml`, `OledProtect/UI/` |

## Licenties

- Code: MIT — origineel © Diego Demétrio (Quorthon13), zie [`LICENSE`](LICENSE). Uitbreidingen vallen onder dezelfde licentie.
- Sterrendata: [d3-celestial](https://github.com/ofrohn/d3-celestial) © Olaf Frohn, BSD-3-Clause — zie
  [`OledProtect/Assets/Sky/LICENSE-d3-celestial.txt`](OledProtect/Assets/Sky/LICENSE-d3-celestial.txt).

- Kaartdata: © [OpenStreetMap](https://www.openstreetmap.org/copyright)-bijdragers (ODbL), tegels via
  [OpenFreeMap](https://openfreemap.org) / [OpenMapTiles](https://openmaptiles.org). Plaatsen zoeken: Nominatim.
- Vliegtuigdata: [adsb.fi](https://adsb.fi) (open data, persoonlijk gebruik) en [adsb.lol](https://adsb.lol) (open data, ODbL); routes: [adsbdb](https://www.adsbdb.com); vliegvelden voor de rondreis: [OurAirports](https://ourairports.com/data/) (publiek domein).

Dit project is niet verbonden aan de makers van OLED Sleeper, d3-celestial, OpenStreetMap, OpenFreeMap, adsb.fi, adsb.lol, adsbdb of OurAirports.
