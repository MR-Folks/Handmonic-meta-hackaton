"""Neon Tide: a house song in F minor at 124 BPM. Four on the floor, offbeat bass and stabs pumping under the
kick, a two-bar build with a snare roll, then the drop."""
from songkit import Song, echo, reverb

NAME = "NeonTide"

BARS = 28
LOOP = ["Fm", "Db", "Ab", "Eb"]
CHORD_NOTES = {"Fm": [53, 56, 60], "Db": [53, 56, 61], "Ab": [51, 56, 60], "Eb": [51, 55, 58]}
ROOT = {"Fm": 41, "Db": 37, "Ab": 44, "Eb": 39}

LINE = [("we", 0, .5, 72), ("ride", .5, 1.5, 77), ("the", 2, .5, 75), ("neon", 2.5, 1.5, 72), ("tide", 4, 3, 73)]
MELODY = [
    (4, [("neon", 0, 1, 72), ("on", 1, .5, 72), ("the", 1.5, .5, 70), ("water", 2, 1.5, 68), ("line", 4, 3, 65)]),
    (6, [("every", 0, 1, 72), ("color", 1, 1, 70), ("falls", 2, 1, 68), ("in", 3, .5, 67), ("time", 3.5, 3.5, 67)]),
    (8, [("lift", 0, .5, 65), ("your", .5, .5, 68), ("hands", 1, 1, 72), ("and", 2, .5, 70), ("catch", 2.5, 1.5, 72), ("the", 4, .5, 70), ("tide", 4.5, 2.5, 68)]),
    (10, [("let", 0, .5, 72), ("the", .5, .5, 72), ("bass", 1, 1, 75), ("light", 2, 1, 72), ("up", 3, .5, 70), ("the", 3.5, .5, 68), ("night", 4, 3, 70)]),
    (14, LINE),
    (16, [("we", 0, .5, 72), ("glow,", .5, 1.5, 75), ("we", 2, .5, 72), ("come", 2.5, 1.5, 70), ("alive", 4, 3, 75)]),
    (18, LINE),
    (20, [("hands", 0, 1, 77), ("up", 1, 1, 75), ("into", 2, 1, 72), ("the", 3, 1, 70), ("light", 4, 3, 70)]),
    (22, [("ride", 0, 1.5, 77), ("the", 1.5, .5, 75), ("neon", 2, 2, 72), ("tide", 4, 3, 73)]),
]


def section(bar):
    return "intro" if bar < 4 else "verse" if bar < 12 else "build" if bar < 14 else "drop" if bar < 24 else "outro"


def render():
    song = Song("Neon Tide", bpm=124, bars=BARS)
    song.melody = MELODY
    beat = song.beat
    kicks = []
    for bar in range(BARS):
        chord = LOOP[bar % 4]
        part = section(bar)
        t0 = song.bar_time(bar)
        notes, root = CHORD_NOTES[chord], ROOT[chord]

        song.pad(t0, notes, 4 * beat, 0.02, 2000 if part == "drop" else 800 + 60 * bar if part == "build" else 900, bus="pump")
        for q in range(4):
            t = t0 + q * beat
            if part != "build" and (bar >= 2 and bar < 26):
                song.kick(t, 0.95, low=45, punch=110, decay=9)
                kicks.append(t)
            if part in ("verse", "drop") and q % 2 == 1: song.clap(t, 0.3)
            if part in ("verse", "drop") or (part == "intro" and bar >= 2) or (part == "outro" and bar < 26):
                song.hat(t + beat / 2, 0.11, decay=22, length=0.14)
            if part == "drop":
                for s in (1, 3): song.hat(t + s * beat / 4, 0.04)
            if part in ("verse", "drop"):
                song.sub(t + beat / 2, root, beat * 0.45, 0.3, bus="pump")
            if part == "drop":
                song.bass(t + beat / 2, root + 12, beat * 0.4, 0.1, 900, bus="pump")
                if q in (0, 1, 3): song.stab(t + beat / 2, [n + 12 for n in notes], 0.045, bus="pump")
                if q == 2: song.stab(t + beat * 0.75, [n + 12 for n in notes], 0.045, bus="pump")

        if part == "build":
            steps = 8 if bar == 12 else 16
            for s in range(steps):
                song.snare(t0 + s * 4 * beat / steps, 0.35 + 0.5 * s / steps)
            if bar == 12: song.riser(t0, 8 * beat)
        if part == "intro" and bar < 2:
            for s in range(8): song.hat(t0 + s * beat / 2, 0.03)

    song.sing(vol=2.0, bus="vox")
    pump = song.bus("pump") * song.duck(kicks, depth=0.7, release=0.09)
    vox = echo(song.bus("vox"), delay=beat * 0.75, feedback=0.3)
    song.mix += reverb(pump, 1.2, 0.25) + reverb(vox, 1.8, 0.35)
    return song
