"""Render the original demo song "Glass Room" to a WAV file plus a word-timed LRC file.

The song, melody and lyrics were written for this project, so they are safe to ship.
Usage: python3 Tools/render_glass_room.py  (writes into Assets/_Project/Audio)
"""
import math
import pathlib
import wave

import numpy as np

SR = 44100
BEAT = 60 / 96
BARS = 20
LENGTH = BARS * 4 * BEAT + 1.5

CHORDS = ["Am", "F", "Am", "F", "C", "G", "Am", "F", "C", "G", "F", "G", "Am", "C", "F", "G", "C", "C", "Am", "F"]
CHORD_NOTES = {"Am": [57, 60, 64], "F": [53, 57, 60], "C": [52, 55, 60], "G": [55, 59, 62]}
ROOT = {"Am": 45, "F": 41, "C": 48, "G": 43}

# (start bar, [(word, beat within the two-bar phrase, length in beats, midi note)])
MELODY = [
    (2, [("light", 0, 1, 64), ("comes", 1, .5, 67), ("in", 1.5, .5, 69), ("through", 2, 1, 69), ("the", 3, 1, 67), ("glass", 4, 3, 64)]),
    (4, [("every", 0, 1, 72), ("corner", 1.5, 1.5, 71), ("starts", 3, .5, 69), ("to", 3.5, .5, 67), ("glow", 4, 3, 67)]),
    (6, [("put", 0, .5, 64), ("your", .5, .5, 67), ("hands", 1, 1, 69), ("into", 2, 1, 72), ("the", 3, .5, 71), ("sound", 3.5, 3.5, 69)]),
    (8, [("feel", 0, 1, 72), ("the", 1, .5, 71), ("room", 1.5, 1.5, 69), ("begin", 3, 1, 67), ("to", 4, .5, 69), ("move", 4.5, 3, 71)]),
    (10, [("we", 0, .5, 72), ("are", .5, .5, 72), ("dancing", 1, 1, 74), ("in", 2, .5, 72), ("a", 2.5, .5, 71), ("glass", 3, 1, 72), ("room", 4, 3, 76)]),
    (12, [("every", 0, 1, 76), ("heartbeat", 1, 1.5, 74), ("on", 2.5, .5, 72), ("the", 3, .5, 71), ("wall", 4, 3, 69)]),
    (14, [("hold", 0, 1, 72), ("the", 1, .5, 74), ("music", 1.5, 1.5, 76), ("let", 3, .5, 74), ("it", 3.5, .5, 72), ("hold", 4, 1, 74), ("you", 5, 2.5, 71)]),
    (16, [("we", 0, .5, 72), ("are", .5, .5, 72), ("dancing", 1, 1, 74), ("in", 2, .5, 72), ("a", 2.5, .5, 71), ("glass", 3, 1, 74), ("room", 4, 3, 72)]),
]

VOWELS = {"a": (800, 1150, 2900), "o": (450, 800, 2830), "e": (400, 2000, 2550), "i": (300, 2200, 2950), "u": (325, 700, 2530)}

rng = np.random.default_rng(7)
mix = np.zeros(int(LENGTH * SR) + SR)


def mtof(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def add(start, signal):
    i = int(start * SR)
    mix[i:i + len(signal)] += signal[: len(mix) - i]


def saw(freq, t, detune_cents=0.0):
    f = freq * 2 ** (detune_cents / 1200)
    return 2 * ((t * f) % 1.0) - 1


def one_pole_lowpass(x, cutoff):
    a = math.exp(-2 * math.pi * cutoff / SR)
    y = np.empty_like(x)
    acc = 0.0
    for n, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[n] = acc
    return y


def bandpass(x, freq, q):
    w0 = 2 * math.pi * freq / SR
    alpha = math.sin(w0) / (2 * q)
    a0 = 1 + alpha
    b0, b2 = alpha / a0, -alpha / a0
    a1, a2 = -2 * math.cos(w0) / a0, (1 - alpha) / a0
    y = np.empty_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for n, v in enumerate(x):
        out = b0 * v + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, v, y1, out
        y[n] = out
    return y


def envelope(n, attack, release):
    env = np.ones(n)
    a, r = int(attack * SR), int(release * SR)
    if a: env[:a] = np.linspace(0, 1, a)
    if r: env[-r:] *= np.linspace(1, 0, r)
    return env


def kick(start):
    t = t_axis(0.4)
    freq = 42 + 98 * np.exp(-t / 0.045)
    phase = 2 * np.pi * np.cumsum(freq) / SR
    add(start, np.sin(phase) * np.exp(-t * 8) * 0.9)


def hat(start, vol):
    t = t_axis(0.06)
    noise = np.diff(rng.standard_normal(len(t) + 1))
    add(start, noise * np.exp(-t * 70) * vol)


def snare(start):
    t = t_axis(0.2)
    noise = rng.standard_normal(len(t))
    body = noise - np.convolve(noise, np.ones(8) / 8, mode="same")
    tone = (2 * np.abs(2 * ((t * 190) % 1) - 1) - 1) * np.exp(-t * 30) * 0.15
    add(start, body * np.exp(-t * 22) * 0.35 + tone)


def bass(start, midi, dur, vol, cutoff):
    t = t_axis(dur)
    sig = one_pole_lowpass(saw(mtof(midi), t), cutoff) * np.exp(-t * 3.5) * vol
    add(start, sig * envelope(len(t), 0.005, 0.03))


def pad(start, notes, dur):
    t = t_axis(dur + 0.1)
    sig = sum(saw(mtof(m), t, d) for m in notes for d in (-6, 6))
    sig = one_pole_lowpass(sig, 1100) * 0.022
    add(start, sig * envelope(len(t), 0.25, 0.3))


def voice(start, midi, dur, word):
    t = t_axis(dur + 0.1)
    vib = 1 + 0.008 * np.sin(2 * np.pi * 5.2 * t) * np.clip(t / 0.3, 0, 1)
    freq = mtof(midi)
    phase = np.cumsum(freq * vib) / SR
    src = (2 * (phase % 1) - 1) + (2 * ((phase * 1.0046) % 1) - 1)
    vowel = next((VOWELS[c] for c in word if c in VOWELS), VOWELS["a"])
    sig = sum(g * bandpass(src * 0.5, f, q) for f, q, g in zip(vowel, (5, 8, 10), (1, .55, .3)))
    add(start, sig * envelope(len(t), 0.05, 0.12) * 1.6)


def render():
    e8 = BEAT / 2
    for bar, chord in enumerate(CHORDS):
        section = "intro" if bar < 2 else "verse" if bar < 10 else "chorus" if bar < 18 else "outro"
        t0 = bar * 4 * BEAT
        pad(t0, CHORD_NOTES[chord], 4 * BEAT)
        for e in range(8):
            t, on, q = t0 + e * e8, e % 2 == 0, e // 2
            if section in ("verse", "chorus"):
                if on and (section == "chorus" or q % 2 == 0): kick(t)
                if on and q % 2 == 1: snare(t)
                hat(t, 0.05 if on else 0.08)
            if bar == 1: hat(t, 0.05)
            if section == "verse" and on: bass(t, ROOT[chord], BEAT * 0.9, 0.2, 500)
            if section == "chorus": bass(t, ROOT[chord] + (12 if e % 2 else 0), e8 * 0.9, 0.18, 700)
        if bar == 18:
            kick(t0)
            bass(t0, ROOT[chord], BEAT * 4, 0.2, 400)

    for bar, words in MELODY:
        for word, beat, length, midi in words:
            voice(bar * 4 * BEAT + beat * BEAT, midi, length * BEAT * 0.92, word)


def stamp(seconds):
    return f"{int(seconds // 60):02d}:{seconds % 60:05.2f}"


def write_lrc(path):
    lines = ["[ti:Glass Room]", "[ar:Music In Your Hands demo]"]
    for bar, words in MELODY:
        start = bar * 4 * BEAT
        timed = " ".join(f"<{stamp(start + beat * BEAT)}>{word}" for word, beat, _, _ in words)
        lines.append(f"[{stamp(start)}]{timed}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_notes(path):
    """One sung note per line: start time (s), MIDI pitch, length (s). Worlds use it to play in time with the melody."""
    lines = ["# time midi length"]
    for bar, words in MELODY:
        start = bar * 4 * BEAT
        for _, beat, length, midi in words:
            lines.append(f"{start + beat * BEAT:.3f} {midi} {length * BEAT:.3f}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_wav(path):
    out = np.tanh(mix * 1.2)
    out /= max(1e-6, np.abs(out).max()) / 0.9
    pcm = (out * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


if __name__ == "__main__":
    out_dir = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "_Project" / "Audio"
    out_dir.mkdir(parents=True, exist_ok=True)
    render()
    write_wav(out_dir / "GlassRoom.wav")
    write_lrc(out_dir / "GlassRoom.lrc")
    write_notes(out_dir / "GlassRoom.notes")
    print("Wrote", out_dir / "GlassRoom.wav", "GlassRoom.lrc and GlassRoom.notes")
