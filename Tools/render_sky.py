"""Render the Ceiling stars world's Milky Way: a soft band of light with dark dust lanes and faint nebula colour.

The sharp stars are drawn by Sky.shader; this is only the glow behind them. It is made from noise here, so it is ours.
Usage: python3 Tools/render_sky.py  (writes Assets/_Project/Materials/NightSky.png)
"""
import pathlib

import numpy as np
from PIL import Image

SIZE = 1024
OUT = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "_Project" / "Materials" / "NightSky.png"


def clouds(seed, roughness):
    """Soft noise, 0-1: white noise with its fine detail filtered away."""
    random = np.random.default_rng(seed)
    frequency = np.hypot(*np.meshgrid(np.fft.fftfreq(SIZE), np.fft.fftfreq(SIZE)))
    frequency[0, 0] = 1
    spectrum = np.fft.fft2(random.normal(size=(SIZE, SIZE))) / frequency ** roughness
    field = np.real(np.fft.ifft2(spectrum))
    return (field - field.min()) / (field.max() - field.min())


def main():
    y, x = np.mgrid[0:SIZE, 0:SIZE] / SIZE * 2 - 1
    # The band crosses the sky at a slant, wandering a little, and thins towards its ends.
    along, across = x * 0.82 + y * 0.57, -x * 0.57 + y * 0.82
    across = across + (clouds(1, 1.8) - 0.5) * 0.35
    band = np.exp(-(across / 0.15) ** 2) * (0.55 + 0.45 * np.cos(along * 1.5))
    core = np.exp(-(across / 0.09) ** 2 - (along / 0.5) ** 2)
    dust = np.clip((clouds(2, 1.45) - 0.42) * 4.5, 0, 1)
    light = band * (0.35 + 0.65 * clouds(3, 1.3)) * (1 - 0.8 * dust * np.exp(-(across / 0.13) ** 2)) + core * 0.35 * (1 - 0.75 * dust)

    # Faint colour: blue in the band, a warm heart, and the odd wisp of violet nebula away from it.
    glow = light[..., None] * np.array([0.17, 0.22, 0.42]) + (core * (1 - dust))[..., None] * np.array([0.3, 0.2, 0.13]) * 0.6
    wisps = np.clip(clouds(4, 1.6) - 0.6, 0, 1) * 1.6
    glow += wisps[..., None] * np.array([0.14, 0.05, 0.2])
    glow += (np.clip(clouds(5, 1.6) - 0.62, 0, 1) * 1.6)[..., None] * np.array([0.02, 0.09, 0.15])
    image = np.clip(glow, 0, 1) ** (1 / 2.2)
    Image.fromarray((image * 255 + 0.5).astype(np.uint8), "RGB").save(OUT, optimize=True)
    print(f"{OUT.name}: {OUT.stat().st_size // 1024} KB")


if __name__ == "__main__":
    main()
