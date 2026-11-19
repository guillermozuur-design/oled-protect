# Gebruikte onderdelen van anderen

OLED Protect zelf valt onder de [MIT-licentie](LICENSE). De app gebruikt deze onderdelen en gegevens van anderen.

## Gegevens

| Wat | Van | Licentie |
|---|---|---|
| Sterren en sterrenbeelden (`OledProtect/Assets/Sky/sky.txt`) | [d3-celestial](https://github.com/ofrohn/d3-celestial) © Olaf Frohn | BSD-3-Clause, zie [`LICENSE-d3-celestial.txt`](OledProtect/Assets/Sky/LICENSE-d3-celestial.txt) |
| Vliegvelden en banen (`OledProtect/Assets/Flights/airports.txt`) | [OurAirports](https://ourairports.com/data/) | Publiek domein, zie [`LICENSE-OurAirports.txt`](OledProtect/Assets/Flights/LICENSE-OurAirports.txt) |
| Kaarttegels (online opgehaald) | © [OpenStreetMap](https://www.openstreetmap.org/copyright)-bijdragers, via [OpenFreeMap](https://openfreemap.org) / [OpenMapTiles](https://openmaptiles.org) | ODbL (gegevens) |
| Vliegtuigposities (online opgehaald) | [adsb.fi](https://adsb.fi), reserve [adsb.lol](https://adsb.lol) | Open data (adsb.lol: ODbL) |
| Vluchtroutes (online opgehaald) | [adsbdb](https://www.adsbdb.com) | Open data |
| Plaatsen zoeken (online) | [Nominatim](https://nominatim.org) / OpenStreetMap | ODbL |

## Bibliotheken (NuGet)

| Pakket | Licentie |
|---|---|
| Hardcodet.NotifyIcon.Wpf (pictogram in het systeemvak) | MIT |
| CommandLineParser (opstartopties) | MIT |
| Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Options | MIT |
| Serilog, Serilog.Sinks.File (logboek) | Apache-2.0 |
| MinVer (versienummer, alleen tijdens het bouwen) | Apache-2.0 |

Alleen voor de tests: xUnit, Xunit.StaFact, Moq, coverlet en Microsoft.NET.Test.Sdk.

OLED Protect is niet verbonden aan de makers van deze onderdelen of diensten.
