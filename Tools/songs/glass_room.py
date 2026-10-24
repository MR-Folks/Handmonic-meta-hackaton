"""Glass Room: the original demo song. Synth pop in A minor at 96 BPM."""
from songkit import Song

NAME = "GlassRoom"

CHORDS = ["Am", "F", "Am", "F", "C", "G", "Am", "F", "C", "G", "F", "G", "Am", "C", "F", "G", "C", "C", "Am", "F"]
CHORD_NOTES = {"Am": [57, 60, 64], "F": [53, 57, 60], "C": [52, 55, 60], "G": [55, 59, 62]}
ROOT = {"Am": 45, "F": 41, "C": 48, "G": 43}

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


def render():
    song = Song("Glass Room", bpm=96, bars=len(CHORDS))
    song.melody = MELODY
    beat = song.beat
    e8 = beat / 2
    for bar, chord in enumerate(CHORDS):
        section = "intro" if bar < 2 else "verse" if bar < 10 else "chorus" if bar < 18 else "outro"
        t0 = song.bar_time(bar)
        song.pad(t0, CHORD_NOTES[chord], 4 * beat)
        for e in range(8):
            t, on, q = t0 + e * e8, e % 2 == 0, e // 2
            if section in ("verse", "chorus"):
                if on and (section == "chorus" or q % 2 == 0): song.kick(t)
                if on and q % 2 == 1: song.snare(t)
                song.hat(t, 0.05 if on else 0.08)
            if bar == 1: song.hat(t, 0.05)
            if section == "verse" and on: song.bass(t, ROOT[chord], beat * 0.9, 0.2, 500)
            if section == "chorus": song.bass(t, ROOT[chord] + (12 if e % 2 else 0), e8 * 0.9, 0.18, 700)
        if bar == 18:
            song.kick(t0)
            song.bass(t0, ROOT[chord], beat * 4, 0.2, 400)
    song.sing()
    return song
