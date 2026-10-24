"""Rainy Window: a lo-fi hip hop song in C major at 80 BPM. Jazzy electric piano chords, a swung boom-bap beat,
a round bass and a soft breathy voice, all a little dusty and dark like an old record."""
from songkit import Song, echo, one_pole_lowpass, reverb

NAME = "RainyWindow"

LOOP = ["Dm9", "G13", "Cmaj9", "Am7"]
CHORDS = ["Dm9", "G13"] + LOOP * 4 + ["Dm9", "Cmaj9"]
EP_NOTES = {"Dm9": [53, 57, 60, 64], "G13": [53, 59, 64], "Cmaj9": [52, 55, 59, 62], "Am7": [55, 60, 64, 67]}
ROOT = {"Dm9": 38, "G13": 43, "Cmaj9": 36, "Am7": 45}

KICK_STEPS = (0, 7, 10)
SNARE_STEPS = (4, 12)

STAY = [("stay", 0, 1, 76), ("a", 1, .5, 74), ("while,", 1.5, 1.5, 72), ("stay", 4, 1, 74), ("a", 5, .5, 72), ("while", 5.5, 1.5, 71)]
MELODY = [
    (2, [("rain", 0, 1, 69), ("keeps", 1, .5, 69), ("tapping", 1.5, 1, 72), ("on", 2.5, .5, 71), ("my", 3, 1, 69), ("window", 4, 2.5, 67)]),
    (4, [("slow", 0, 1, 67), ("light", 1, 1, 71), ("melting", 2, 1.5, 72), ("into", 3.5, .5, 71), ("blue", 4, 3, 69)]),
    (6, [("coffee", 0, 1, 69), ("cooling", 1, 1.5, 72), ("by", 2.5, .5, 74), ("the", 3, 1, 72), ("record", 4, 2.5, 71)]),
    (8, [("every", 0, 1, 71), ("quiet", 1, 1, 72), ("song", 2, 1, 74), ("is", 3, 1, 72), ("you", 4, 3, 69)]),
    (10, STAY),
    (12, [("let", 0, .5, 71), ("the", .5, .5, 72), ("gray", 1, 1, 74), ("day", 2, 1, 76), ("drift", 3, 1, 74), ("on", 4, 1, 72), ("by", 5, 2, 69)]),
    (14, STAY),
    (16, [("hum", 0, 1, 71), ("along", 1, 1, 72), ("until", 2, 1.5, 74), ("it's", 3.5, .5, 72), ("light", 4, 3, 72)]),
]


def render():
    song = Song("Rainy Window", bpm=80, bars=len(CHORDS), tail=3)
    song.melody = MELODY
    beat = song.beat
    step = beat / 4

    def at(t0, s):
        # Swung sixteenths: every second one leans late.
        return t0 + s * step + (step * 0.3 if s % 2 else 0)

    for bar, chord in enumerate(CHORDS):
        t0 = song.bar_time(bar)
        drums = 2 <= bar < 18
        chorus = 10 <= bar < 18
        last = bar == len(CHORDS) - 1
        notes = EP_NOTES[chord]

        song.ep(t0, notes, beat * (6 if last else 2.4), 0.05, bus="keys")
        if not last: song.ep(at(t0, 10), notes[1:], beat * 1.4, 0.032, roll=0.015, bus="keys")

        if drums:
            for s in KICK_STEPS: song.kick(at(t0, s), 0.8 if s else 0.9, low=40, punch=70, decay=7)
            for s in SNARE_STEPS: song.snare(at(t0, s), 0.8, decay=16)
            for s in range(0, 16, 2): song.hat(at(t0, s), 0.05 if s % 4 else 0.035, decay=55)
            for s in ((3, 7, 11, 15) if chorus else (7, 15)): song.hat(at(t0, s), 0.02)
            root = ROOT[chord]
            song.sub(t0, root, beat * 1.7, 0.42)
            song.sub(at(t0, 10), root if chord != "Am7" else 43, beat * 1.3, 0.35)
        elif last:
            song.sub(t0, ROOT[chord], beat * 4, 0.35)

    song.sing(vol=1.55, breath=0.35, bus="vox")
    keys = reverb(song.bus("keys"), 2.2, 0.3)
    vox = reverb(echo(song.bus("vox"), delay=beat, feedback=0.22, repeats=3), 2.4, 0.4)
    song.mix += keys + vox
    # An old record: darker highs, dust and hiss.
    song.mix = one_pole_lowpass(song.mix, 5200)
    song.crackle(vol=0.05)
    return song
