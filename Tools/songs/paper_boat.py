"""Paper Boat: a folk waltz in G major, 3/4 at 144 BPM. A fingerpicked nylon guitar going oom-pah-pah, an upright
bass, a soft foot stomp and brushes, and a glockenspiel answering the voice in the chorus."""
from songkit import Song, reverb

NAME = "PaperBoat"

CHORDS = (["G", "C", "G", "D"]
          + ["G", "G", "C", "G", "Em", "Em", "C", "D", "G", "G", "C", "G", "Em", "C", "D", "D"]
          + ["C", "C", "G", "G", "Em", "Em", "D", "D", "C", "C", "G", "Em", "Am", "D", "G", "G"]
          + ["C", "D", "G", "G"])
GUITAR = {"G": (43, [59, 62, 67]), "C": (48, [55, 60, 64]), "D": (50, [57, 62, 66]), "Em": (40, [55, 59, 64]), "Am": (45, [57, 60, 64])}
UPRIGHT = {"G": 31, "C": 36, "D": 38, "Em": 40, "Am": 33}

# The glockenspiel's little tune in the intro: (bar, beat, midi).
INTRO_BELLS = [(0, 0, 79), (0, 2, 76), (1, 0, 76), (1, 2, 72), (2, 0, 74), (2, 2, 71), (3, 0, 69)]

SAIL = [("sail", 0, 2, 76), ("away,", 2, 4, 72), ("sail", 6, 2, 74), ("away", 8, 3.5, 71)]
MELODY = [
    (4, [("fold", 0, 2, 71), ("a", 2, 1, 72), ("paper", 3, 2, 74), ("boat", 5, 1, 71), ("for", 6, 1, 72), ("me", 7, 4, 76)]),
    (8, [("set", 0, 2, 71), ("it", 2, 1, 69), ("on", 3, 2, 67), ("the", 5, 1, 66), ("river", 6, 2, 64), ("slow", 9, 3, 66)]),
    (12, [("ripples", 0, 2, 71), ("hum", 2, 1, 74), ("a", 3, 1, 72), ("little", 4, 2, 71), ("song", 6, 5, 67)]),
    (16, [("carry", 0, 2, 71), ("it", 2, 1, 72), ("where", 3, 2, 72), ("waters", 5, 1, 71), ("go", 6, 5.5, 69)]),
    (20, SAIL),
    (24, [("down", 0, 2, 71), ("the", 2, 1, 72), ("river", 3, 2, 74), ("into", 5, 1, 76), ("light", 6, 5.5, 74)]),
    (28, SAIL),
    (32, [("we'll", 0, 1, 72), ("be", 1, 1, 72), ("home", 2, 1, 76), ("before", 3, 2, 74), ("the", 5, 1, 72), ("night", 6, 5.5, 67)]),
]


def render():
    song = Song("Paper Boat", bpm=144, bars=len(CHORDS), beats_per_bar=3, tail=2.5)
    song.melody = MELODY
    beat = song.beat
    for bar, chord in enumerate(CHORDS):
        t0 = song.bar_time(bar)
        band = 4 <= bar < 36
        chorus = 20 <= bar < 36
        last = bar == len(CHORDS) - 1
        low, upper = GUITAR[chord]

        # Oom-pah-pah: a bass string, then the chord picked twice, each a little rolled.
        song.pluck(t0, low, beat * (6 if last else 3), 0.16, bus="band")
        for b in ((1,) if last else (1, 2)):
            for i, n in enumerate(upper):
                song.pluck(t0 + b * beat + i * 0.012, n, beat * (5 if last else 2), 0.07 if b == 1 else 0.06, bus="band")

        if band:
            song.kick(t0, 0.55, low=50, punch=60, decay=12)
            song.sub(t0, UPRIGHT[chord], beat * 2.6, 0.28, bus="band")
            for b in (1, 2): song.brush(t0 + b * beat, 0.05)
            if chorus:
                for e in range(6): song.hat(t0 + e * beat / 2, 0.025 if e % 2 else 0.015, decay=90)
        elif last:
            song.kick(t0, 0.5, low=50, punch=60, decay=12)
            song.sub(t0, UPRIGHT[chord], beat * 6, 0.25, bus="band")

        # The glockenspiel answers each chorus line in its last bar.
        if chorus and bar % 4 == 3:
            for b, n in enumerate(sorted(upper)):
                song.bell(t0 + b * beat, n + 12, 0.05, bus="band")
    for bar, b, n in INTRO_BELLS:
        song.bell(song.bar_time(bar) + b * beat, n, 0.06, bus="band")
    song.bell(song.bar_time(len(CHORDS) - 1) + beat, 79, 0.05, bus="band")

    song.sing(vol=1.65, breath=0.12, bus="vox", legato=0.95)
    song.mix += reverb(song.bus("band"), 1.6, 0.3) + reverb(song.bus("vox"), 2.0, 0.35)
    return song
