"""Offline, deterministic large-repeat PBR variant of the retained CC0 Concrete030.

All three maps share phase offsets/blend weights; no random color noise or baked fake shadows.
Requires Pillow (already used by the local asset workflow). No rotations/mirrors of normals.
The 12 m output repeats only beyond the old 2 m tile; world-aligned UVs join adjacent pieces.
"""
from pathlib import Path
import math
import random
from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[2]
TEX = ROOT / "Assets/Art/Maps/BoatyardLookSample/Textures"
SIZE, CELL, SOURCE = 4096, 1024, 1024
rng = random.Random(20261009)
offsets = [[(rng.randrange(SOURCE), rng.randrange(SOURCE)) for _ in range(4)] for _ in range(4)]

def smooth(t):
    t = min(1, max(0, t))
    return t * t * (3 - 2 * t)

# A narrow, softly curved overlap, rather than four-way texture blur everywhere.
weights = bytearray(CELL * CELL)
for y in range(CELL):
    for x in range(CELL):
        u, v = x / CELL, y / CELL
        curve = .055 * math.sin(v * math.tau) * math.sin(u * math.pi)
        weights[y * CELL + x] = round(255 * smooth((u + curve - .65) / .30))
mask_x = Image.frombytes("L", (CELL, CELL), bytes(weights))
mask_y = mask_x.transpose(Image.Transpose.TRANSPOSE)

for kind in ("Color", "NormalGL", "Roughness"):
    source = Image.open(TEX / f"Concrete030_2K-JPG_{kind}.jpg").convert("RGB").resize((SOURCE, SOURCE), Image.Resampling.LANCZOS)
    result = Image.new("RGB", (SIZE, SIZE))
    for cy in range(4):
        for cx in range(4):
            patches = [ImageChops.offset(source, *offsets[y % 4][x % 4])
                       for y, x in ((cy, cx), (cy, cx + 1), (cy + 1, cx), (cy + 1, cx + 1))]
            upper = Image.composite(patches[1], patches[0], mask_x)
            lower = Image.composite(patches[3], patches[2], mask_x)
            result.paste(Image.composite(lower, upper, mask_y), (cx * CELL, cy * CELL))
    if kind == "Color":
        # Palette adaptation retains aggregate/pores; less broad stain contrast than 6325193.
        gray = result.convert("L")
        mean = sum(i * n for i, n in enumerate(gray.histogram())) / (SIZE * SIZE)
        channels = [gray.point([round(255 * tint * min(1.35, max(.65, 1 + (i / mean - 1) * .60))) for i in range(256)])
                    for tint in (.72, .70, .65)]
        result = Image.merge("RGB", channels)
    result.save(TEX / f"ConcreteContinuous_{kind}.png")
    print(f"Wrote ConcreteContinuous_{kind}.png ({SIZE} square)", flush=True)
