"""Bake the app's lettering into signed distance field atlases.

Every word in the app is drawn from these atlases (see TypeFace.cs and Lettering.shader). They never change while the
app runs, so a word's letters can't move out from under it, and a distance field stays sharp at any size and gives clean
outlines and glows without touching the neighbouring letters.
Usage: python3 Tools/bake_type.py  (writes Assets/_Project/Resources/Type/<Face>.png and .txt)
File format: "face em atlas spread", then "g code advance left bottom right top u v" per letter (metrics in ems, u, v
the atlas position of the letter's bottom-left corner), then "k first second amount" per kerning pair.
"""
import pathlib

import numpy as np
from fontTools.ttLib import TTFont
from PIL import Image, ImageDraw, ImageFont

TOOLS = pathlib.Path(__file__).resolve().parent
OUT = TOOLS.parent / "Assets" / "_Project" / "Resources" / "Type"
EM = 80            # pixels per em in the atlas
SPREAD = 14        # how far the field reaches from a letter's edge (pixels)
OVERSAMPLE = 4     # letters are rendered this much larger to measure distances from
LATIN = [*range(0x20, 0x7F), *range(0xA0, 0x180), *range(0x2010, 0x2016), *range(0x2018, 0x201F), 0x2026, 0x20AC, 0x2122]
GREEK_CYRILLIC = [*range(0x391, 0x3CF), *range(0x400, 0x460)]
FACES = [("Bold", "Roboto-Bold.ttf", LATIN + GREEK_CYRILLIC, 2048), ("Marker", "PermanentMarker-Regular.ttf", LATIN, 2048)]


def distance(mask, reach):
    """Distance from each pixel to the nearest True pixel, correct up to reach."""
    far = float(reach * reach * 2 + 1)
    field = np.where(mask, 0.0, far).astype(np.float32)
    for axis in (0, 1):
        source = field.copy()
        for step in range(1, reach + 1):
            cost = float(step * step)
            field[tuple(slice(step, None) if a == axis else slice(None) for a in (0, 1))] = np.minimum(
                field[tuple(slice(step, None) if a == axis else slice(None) for a in (0, 1))],
                source[tuple(slice(None, -step) if a == axis else slice(None) for a in (0, 1))] + cost)
            field[tuple(slice(None, -step) if a == axis else slice(None) for a in (0, 1))] = np.minimum(
                field[tuple(slice(None, -step) if a == axis else slice(None) for a in (0, 1))],
                source[tuple(slice(step, None) if a == axis else slice(None) for a in (0, 1))] + cost)
    return np.sqrt(field)


def bake_letter(font, char):
    """The letter's field as 8-bit values, with its metrics. None for a letter with no ink."""
    size = EM * OVERSAMPLE
    left, top, right, bottom = font.getbbox(char, anchor="ls")
    if right <= left or bottom <= top:
        return None
    pad = SPREAD + 1
    x0, x1 = left // OVERSAMPLE - pad, -(-right // OVERSAMPLE) + pad
    y0, y1 = top // OVERSAMPLE - pad, -(-bottom // OVERSAMPLE) + pad
    canvas = Image.new("L", ((x1 - x0) * OVERSAMPLE, (y1 - y0) * OVERSAMPLE), 0)
    ImageDraw.Draw(canvas).text((-x0 * OVERSAMPLE, -y0 * OVERSAMPLE), char, font=font, fill=255, anchor="ls")
    ink = np.asarray(canvas) >= 128
    if not ink.any():
        return None
    reach = (SPREAD + 2) * OVERSAMPLE
    signed = (distance(ink, reach) - distance(~ink, reach)) / OVERSAMPLE
    signed += (ink * 1.0 - 0.5) / OVERSAMPLE
    h, w = y1 - y0, x1 - x0
    field = signed.reshape(h, OVERSAMPLE, w, OVERSAMPLE).mean(axis=(1, 3))
    values = np.clip(0.5 - field / (2 * SPREAD), 0, 1)
    return (values * 255 + 0.5).astype(np.uint8), (left / size, -bottom / size, right / size, -top / size), (x0, y0)


def pair_values(subtable):
    """Yields (first glyph, second glyph, x advance change) from a GPOS pair positioning subtable."""
    if subtable.Format == 1:
        for first, pairs in zip(subtable.Coverage.glyphs, subtable.PairSet):
            for record in pairs.PairValueRecord:
                if record.Value1 is not None and getattr(record.Value1, "XAdvance", 0):
                    yield first, record.SecondGlyph, record.Value1.XAdvance
    elif subtable.Format == 2:
        classes = subtable.ClassDef2.classDefs
        seconds = {}
        for glyph, index in classes.items():
            seconds.setdefault(index, []).append(glyph)
        for first in subtable.Coverage.glyphs:
            row = subtable.Class1Record[subtable.ClassDef1.classDefs.get(first, 0)].Class2Record
            for index, glyphs in seconds.items():
                value = row[index].Value1
                if value is not None and getattr(value, "XAdvance", 0):
                    for second in glyphs:
                        yield first, second, value.XAdvance


def kerning(path, codes):
    """Kerning pairs among the baked ASCII letters, in ems."""
    font = TTFont(path)
    units = font["head"].unitsPerEm
    cmap = font.getBestCmap()
    wanted = {cmap[c]: c for c in codes if c in cmap and c < 0x7F}
    pairs = {}
    if "GPOS" in font:
        table = font["GPOS"].table
        indices = {i for f in table.FeatureList.FeatureRecord if f.FeatureTag == "kern" for i in f.Feature.LookupListIndex}
        for index in sorted(indices):
            for subtable in table.LookupList.Lookup[index].SubTable:
                subtable = getattr(subtable, "ExtSubTable", subtable)
                if subtable.LookupType != 2:
                    continue
                for first, second, amount in pair_values(subtable):
                    if first in wanted and second in wanted:
                        pairs.setdefault((wanted[first], wanted[second]), amount)
    elif "kern" in font:
        for subtable in font["kern"].kernTables:
            for (first, second), amount in subtable.kernTable.items():
                if first in wanted and second in wanted:
                    pairs.setdefault((wanted[first], wanted[second]), amount)
    return {k: v / units for k, v in pairs.items() if v}


def bake(name, file, codes, atlas_size):
    path = TOOLS / "fonts" / file
    cmap = TTFont(path).getBestCmap()
    codes = sorted({c for c in codes if c in cmap})
    font = ImageFont.truetype(str(path), EM * OVERSAMPLE, layout_engine=ImageFont.Layout.BASIC)
    letters = []
    lines = [f"face {EM} {atlas_size} {SPREAD}"]
    for code in codes:
        char = chr(code)
        advance = font.getlength(char) / (EM * OVERSAMPLE)
        baked = bake_letter(font, char)
        if baked is None:
            lines.append(f"g {code} {advance:.5f} 0 0 0 0 0 0")
        else:
            letters.append((code, advance, baked))

    # Shelf packing, tallest letters first. The padding baked into each letter keeps neighbours apart.
    atlas = np.zeros((atlas_size, atlas_size), dtype=np.uint8)
    x = y = shelf = 0
    for code, advance, (values, metrics, (ox, oy)) in sorted(letters, key=lambda l: -l[2][0].shape[0]):
        h, w = values.shape
        if x + w > atlas_size:
            x, y, shelf = 0, y + shelf, 0
        if y + h > atlas_size:
            raise SystemExit(f"{name}: the atlas is too small")
        atlas[y:y + h, x:x + w] = values
        shelf = max(shelf, h)
        left, bottom, right, top = metrics
        # The atlas position of the letter's own bottom-left corner, inside its padding. The image's top row is v = 1.
        u = (x + left * EM - ox) / atlas_size
        v = 1 - (y + (-bottom * EM - oy)) / atlas_size
        lines.append(f"g {code} {advance:.5f} {left:.5f} {bottom:.5f} {right:.5f} {top:.5f} {u:.6f} {v:.6f}")
        x += w

    pairs = kerning(path, codes)
    lines += [f"k {a} {b} {amount:.5f}" for (a, b), amount in sorted(pairs.items())]
    Image.fromarray(atlas, "L").save(OUT / f"{name}.png", optimize=True)
    (OUT / f"{name}.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{name}: {len(codes)} letters, {len(pairs)} kerning pairs, atlas used to row {y + shelf} of {atlas_size}")


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for face in FACES:
        bake(*face)
