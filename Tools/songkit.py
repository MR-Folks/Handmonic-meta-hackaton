"""Shared instruments and writers for the demo songs in Tools/songs.

A Song holds the mix, its tempo and its sung melody. Each song script builds one, plays instruments into it and
returns it; render_songs.py writes its WAV, its word-timed LRC and its .notes melody.
"""
import math
import wave

import numpy as np

SR = 44100

VOWELS = {"a": (800, 1150, 2900), "o": (450, 800, 2830), "e": (400, 2000, 2550), "i": (300, 2200, 2950), "u": (325, 700, 2530)}


def mtof(m):
    return 440.0 * 2 ** ((m - 69) / 12)


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


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


def echo(x, delay, feedback, repeats=4):
    """A plain feedback delay: each repeat `delay` seconds later and `feedback` times quieter."""
    y = x.copy()
    d = int(delay * SR)
    for k in range(1, repeats + 1):
        y[d * k:] += x[: len(x) - d * k] * feedback ** k
    return y


def reverb(x, seconds, wet, seed=3):
    """A soft room: x convolved with decaying, slightly darkened noise. Uses its own random numbers."""
    n = int(seconds * SR)
    ir = np.random.default_rng(seed).standard_normal(n) * np.exp(-np.arange(n) / SR * 6.9 / seconds)
    ir = np.convolve(ir, np.ones(6) / 6, mode="same")
    ir /= np.sqrt(np.sum(ir ** 2))
    size = 1 << int(math.ceil(math.log2(len(x) + n)))
    tail = np.fft.irfft(np.fft.rfft(x, size) * np.fft.rfft(ir, size), size)[: len(x)]
    return x + tail * wet


class Song:
    def __init__(self, title, bpm, bars, beats_per_bar=4, tail=1.5, seed=7, artist="Handmonic demo"):
        self.title, self.artist = title, artist
        self.beat = 60 / bpm
        self.beats_per_bar = beats_per_bar
        self.length = bars * beats_per_bar * self.beat + tail
        self.mix = np.zeros(int(self.length * SR) + SR)
        self.rng = np.random.default_rng(seed)
        self.buses = {}
        # (start bar, [(word, beat within the phrase, length in beats, midi note)])
        self.melody = []

    def bar_time(self, bar):
        return bar * self.beats_per_bar * self.beat

    def bus(self, name):
        """A separate track that the song processes (echo, ducking) before adding it to the mix."""
        if name not in self.buses: self.buses[name] = np.zeros_like(self.mix)
        return self.buses[name]

    def add(self, start, signal, bus=None):
        target = self.mix if bus is None else self.bus(bus)
        i = int(start * SR)
        target[i:i + len(signal)] += signal[: len(target) - i]

    def duck(self, times, depth, release):
        """A sidechain envelope that dips at each of `times` and recovers over `release` seconds."""
        env = np.ones_like(self.mix)
        n = int(release * 5 * SR)
        curve = 1 - depth * np.exp(-np.arange(n) / SR / release)
        for t in times:
            i = int(t * SR)
            seg = env[i:i + n]
            np.minimum(seg, curve[: len(seg)], out=seg)
        return env

    # Drums

    def kick(self, start, vol=0.9, low=42, punch=98, decay=8, bus=None):
        t = t_axis(0.4)
        freq = low + punch * np.exp(-t / 0.045)
        phase = 2 * np.pi * np.cumsum(freq) / SR
        self.add(start, np.sin(phase) * np.exp(-t * decay) * vol, bus)

    def hat(self, start, vol, decay=70, length=0.06):
        t = t_axis(length)
        noise = np.diff(self.rng.standard_normal(len(t) + 1))
        self.add(start, noise * np.exp(-t * decay) * vol)

    def snare(self, start, vol=1.0, decay=22, bus=None):
        t = t_axis(0.2)
        noise = self.rng.standard_normal(len(t))
        body = noise - np.convolve(noise, np.ones(8) / 8, mode="same")
        tone = (2 * np.abs(2 * ((t * 190) % 1) - 1) - 1) * np.exp(-t * 30) * 0.15
        self.add(start, (body * np.exp(-t * decay) * 0.35 + tone) * vol, bus)

    def clap(self, start, vol=0.3):
        t = t_axis(0.25)
        noise = self.rng.standard_normal(len(t))
        hits = sum(np.exp(-np.clip(t - d, 0, None) * 140) * (t >= d) for d in (0, 0.011, 0.023))
        env = hits + np.exp(-t * 18) * 0.5 * (t >= 0.023)
        self.add(start, bandpass(noise, 1300, 1.2) * env * vol)

    def brush(self, start, vol=0.06):
        t = t_axis(0.18)
        noise = np.diff(self.rng.standard_normal(len(t) + 1))
        env = np.clip(t / 0.012, 0, 1) * np.exp(-t * 24)
        self.add(start, noise * env * vol)

    def riser(self, start, dur, vol=0.12):
        t = t_axis(dur)
        noise = np.diff(self.rng.standard_normal(len(t) + 1))
        self.add(start, noise * (t / dur) ** 2 * vol)

    def crackle(self, vol=0.05, per_second=9):
        """Vinyl dust: scattered tiny clicks and a little hiss over the whole song."""
        clicks = np.zeros_like(self.mix)
        count = int(per_second * len(clicks) / SR)
        clicks[self.rng.integers(0, len(clicks), count)] = self.rng.standard_normal(count) * vol
        clicks = np.convolve(clicks, np.exp(-np.arange(40) / 6), mode="same")
        hiss = np.convolve(self.rng.standard_normal(len(clicks)), np.ones(12) / 12, mode="same") * vol * 0.15
        self.mix += clicks + hiss

    # Bass and chords

    def bass(self, start, midi, dur, vol, cutoff, bus=None):
        t = t_axis(dur)
        sig = one_pole_lowpass(saw(mtof(midi), t), cutoff) * np.exp(-t * 3.5) * vol
        self.add(start, sig * envelope(len(t), 0.005, 0.03), bus)

    def sub(self, start, midi, dur, vol, bus=None):
        """A round sine bass with a touch of second harmonic, like an upright or a sub."""
        t = t_axis(dur)
        f = mtof(midi)
        sig = (np.sin(2 * np.pi * f * t) + 0.3 * np.sin(4 * np.pi * f * t) * np.exp(-t * 6)) * np.exp(-t * 2.2)
        self.add(start, sig * envelope(len(t), 0.008, 0.04) * vol, bus)

    def pad(self, start, notes, dur, vol=0.022, cutoff=1100, bus=None):
        t = t_axis(dur + 0.1)
        sig = sum(saw(mtof(m), t, d) for m in notes for d in (-6, 6))
        sig = one_pole_lowpass(sig, cutoff) * vol
        self.add(start, sig * envelope(len(t), 0.25, 0.3), bus)

    def stab(self, start, notes, vol=0.05, bus=None):
        """A short bright house chord stab."""
        t = t_axis(0.22)
        sig = sum(saw(mtof(m), t, d) for m in notes for d in (-9, 9))
        sig = one_pole_lowpass(sig, 2600) * np.exp(-t * 14) * vol
        self.add(start, sig * envelope(len(t), 0.003, 0.02), bus)

    def ep(self, start, notes, dur, vol=0.05, roll=0.02, bus=None):
        """A Rhodes-like electric piano: soft FM bells, rolled from the lowest note up, with a slow tremolo."""
        for i, m in enumerate(notes):
            t = t_axis(dur + 0.4)
            f = mtof(m) * (1 + 0.0025 * math.sin(start * 1.7 + i))
            index = 1.4 * np.exp(-t * 3) + 0.2
            sig = np.sin(2 * np.pi * f * t + index * np.sin(2 * np.pi * f * t)) * np.exp(-t * 0.9)
            sig += 0.12 * np.sin(2 * np.pi * f * 7 * t) * np.exp(-t * 25)
            sig *= 1 + 0.1 * np.sin(2 * np.pi * 4.2 * t)
            self.add(start + i * roll, sig * envelope(len(t), 0.003, 0.4) * vol, bus)

    def pluck(self, start, midi, dur, vol=0.12, bright=0.18, bus=None):
        """A nylon guitar string: harmonics that die away faster the higher they are."""
        t = t_axis(dur)
        f = mtof(midi)
        sig = np.zeros(len(t))
        for k in range(1, 18):
            if f * k > 9000: break
            amp = abs(math.sin(math.pi * k * bright)) / k
            decay = 1.1 + 0.5 * k ** 1.3 * (f / 220) ** 0.5
            sig += amp * np.sin(2 * np.pi * f * k * (1 + 0.0003 * k * k) * t) * np.exp(-t * decay)
        self.add(start, sig * envelope(len(t), 0.002, 0.06) * vol, bus)

    def bell(self, start, midi, vol=0.06, bus=None):
        """A glockenspiel tone: a few inharmonic partials that ring and fade."""
        t = t_axis(1.6)
        f = mtof(midi)
        sig = sum(a * np.sin(2 * np.pi * f * r * t) * np.exp(-t * d)
                  for r, a, d in ((1, 1, 2.6), (2.76, 0.35, 5), (5.4, 0.15, 9), (8.93, 0.06, 14)))
        self.add(start, sig * envelope(len(t), 0.001, 0.1) * vol, bus)

    # Voice

    def voice(self, start, midi, dur, word, vol=1.6, breath=0.0, bus=None):
        """A sung vowel: two detuned saws through three formant filters picked from the word's first vowel."""
        t = t_axis(dur + 0.1)
        vib = 1 + 0.008 * np.sin(2 * np.pi * 5.2 * t) * np.clip(t / 0.3, 0, 1)
        freq = mtof(midi)
        phase = np.cumsum(freq * vib) / SR
        src = (2 * (phase % 1) - 1) + (2 * ((phase * 1.0046) % 1) - 1)
        if breath: src = src + self.rng.standard_normal(len(t)) * breath
        vowel = next((VOWELS[c] for c in word if c in VOWELS), VOWELS["a"])
        sig = sum(g * bandpass(src * 0.5, f, q) for f, q, g in zip(vowel, (5, 8, 10), (1, .55, .3)))
        self.add(start, sig * envelope(len(t), 0.05, 0.12) * vol, bus)

    def sing(self, vol=1.6, breath=0.0, bus=None, legato=0.92):
        """Sings the melody with the voice."""
        for bar, words in self.melody:
            for word, beat, length, midi in words:
                self.voice(self.bar_time(bar) + beat * self.beat, midi, length * self.beat * legato, word, vol, breath, bus)

    # Files

    def write_wav(self, path):
        out = np.tanh(self.mix * 1.2)
        out /= max(1e-6, np.abs(out).max()) / 0.9
        pcm = (out * 32767).astype("<i2")
        with wave.open(str(path), "wb") as w:
            w.setnchannels(1)
            w.setsampwidth(2)
            w.setframerate(SR)
            w.writeframes(pcm.tobytes())

    def write_lrc(self, path):
        lines = [f"[ti:{self.title}]", f"[ar:{self.artist}]"]
        for bar, words in self.melody:
            start = self.bar_time(bar)
            timed = " ".join(f"<{stamp(start + beat * self.beat)}>{word}" for word, beat, _, _ in words)
            lines.append(f"[{stamp(start)}]{timed}")
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")

    def write_notes(self, path):
        """One sung note per line: start time (s), MIDI pitch, length (s). Worlds use it to play in time with the melody."""
        lines = ["# time midi length"]
        for bar, words in self.melody:
            start = self.bar_time(bar)
            for _, beat, length, midi in words:
                lines.append(f"{start + beat * self.beat:.3f} {midi} {length * self.beat:.3f}")
        path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def stamp(seconds):
    return f"{int(seconds // 60):02d}:{seconds % 60:05.2f}"
