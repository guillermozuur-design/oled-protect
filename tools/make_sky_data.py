"""Zet de d3-celestial catalogus (BSD-3, Olaf Frohn) om naar het compacte sky.txt dat in de app zit.

Gebruik (vanuit de projectroot, met de bronbestanden in .tmp/sky):
    python -I tools/make_sky_data.py .tmp/sky OLED-Sleeper/Assets/Sky/sky.txt

Formaat van sky.txt (RA/Dec in graden, J2000):
    S ra dec mag bv              een ster
    L abbr ra,dec ra,dec ...     een lijnstuk van een sterrenbeeld
    N abbr ra dec naam           label van een sterrenbeeld
"""
import json
import sys
from pathlib import Path

MAX_MAG = 4.8  # met het blote oog in een redelijk donkere stad; houdt het rustig

DUTCH = {
    "And": "Andromeda", "Aql": "Arend", "Aqr": "Waterman", "Ari": "Ram", "Aur": "Voerman",
    "Boo": "Ossenhoeder", "Cam": "Giraffe", "Cnc": "Kreeft", "CVn": "Jachthonden", "CMa": "Grote Hond",
    "CMi": "Kleine Hond", "Cap": "Steenbok", "Cas": "Cassiopeia", "Cep": "Cepheus", "Cet": "Walvis",
    "Com": "Hoofdhaar", "CrB": "Noorderkroon", "Crv": "Raaf", "Crt": "Beker", "Cyg": "Zwaan",
    "Del": "Dolfijn", "Dra": "Draak", "Equ": "Veulen", "Eri": "Eridanus", "Gem": "Tweelingen",
    "Her": "Hercules", "Hya": "Waterslang", "Lac": "Hagedis", "Leo": "Leeuw", "LMi": "Kleine Leeuw",
    "Lep": "Haas", "Lib": "Weegschaal", "Lyn": "Lynx", "Lyr": "Lier", "Mon": "Eenhoorn",
    "Oph": "Slangendrager", "Ori": "Orion", "Peg": "Pegasus", "Per": "Perseus", "Psc": "Vissen",
    "PsA": "Zuidervis", "Sge": "Pijl", "Sgr": "Schutter", "Sco": "Schorpioen", "Ser": "Slang",
    "Sct": "Schild", "Sex": "Sextant", "Tau": "Stier", "Tri": "Driehoek", "UMa": "Grote Beer",
    "UMi": "Kleine Beer", "Vir": "Maagd", "Vul": "Vosje", "Col": "Duif", "Pup": "Achtersteven",
    "Pyx": "Kompas", "Ant": "Luchtpomp", "Scl": "Beeldhouwer", "For": "Oven", "CrA": "Zuiderkroon",
    "Lup": "Wolf", "Cen": "Centaur", "Gru": "Kraanvogel", "Phe": "Feniks", "Mic": "Microscoop",
}


def ra(lon):
    return lon + 360 if lon < 0 else lon


def main(src, dst):
    src = Path(src)
    stars = json.load(open(src / "stars.6.json", encoding="utf-8"))["features"]
    lines = json.load(open(src / "constellations.lines.json", encoding="utf-8"))["features"]
    names = json.load(open(src / "constellations.json", encoding="utf-8"))["features"]

    out = ["# Bron: d3-celestial (c) 2015 Olaf Frohn, BSD-3-Clause. Gegenereerd door tools/make_sky_data.py"]
    n_stars = 0
    for f in stars:
        mag = f["properties"]["mag"]
        if mag > MAX_MAG:
            continue
        lon, dec = f["geometry"]["coordinates"]
        try:
            bv = float(f["properties"].get("bv") or 0.6)
        except ValueError:
            bv = 0.6
        out.append(f"S {ra(lon):.4f} {dec:.4f} {mag:.2f} {bv:.2f}")
        n_stars += 1

    n_lines = 0
    for f in lines:
        for poly in f["geometry"]["coordinates"]:
            pts = " ".join(f"{ra(lon):.3f},{dec:.3f}" for lon, dec in poly)
            out.append(f"L {f['id']} {pts}")
            n_lines += 1

    for f in names:
        abbr = f["id"]
        lon, dec = f["geometry"]["coordinates"]
        name = DUTCH.get(abbr, f["properties"]["name"])
        out.append(f"N {abbr} {ra(lon):.2f} {dec:.2f} {name}")

    Path(dst).parent.mkdir(parents=True, exist_ok=True)
    Path(dst).write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"{n_stars} sterren, {n_lines} lijnen, {len(names)} namen -> {dst} ({Path(dst).stat().st_size} bytes)")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
