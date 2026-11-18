"""
Writes the section files of the built-in songs: Assets/_Project/Audio/<File>.sections.txt, which Journey mode reads to
change worlds at the song's own sections.

Each song's sections are kept here in bars on the song's own beat grid, so every section starts on a downbeat:

- The four demo songs (Tools/songs/*.py) are rendered from a bar list, so their sections are exact: the bar ranges
  each script uses for its intro, verse, chorus (Neon Tide's build and drop) and outro.
- The three sung songs were composed by Tools/elevenlabs_songs.py from section plans with durations, but ElevenLabs
  laid the sections out differently (Turn It Up to Light's intro runs 16 s, not 10). Their sections here come from the
  audio itself: the beat grid was fitted to the songs' kicks (tempo as asked for, first downbeat measured), the section
  starts sit where the energy changes on that grid, and every one agrees with the word-timed lyrics (a chorus starts
  on the downbeat its first line leads into, as pickups do). The keys were estimated from the audio's chroma.

Each file has the tempo, beats per bar, key and where the music ends, then one line per section: its start (m:ss.xx)
and its label. Run it after changing a song's structure:

    python Tools/song_sections.py           # writes the files
    python Tools/song_sections.py --check   # also lists each section's sung lines from the .lrc, to compare
"""

import argparse
import pathlib
import re

AUDIO = pathlib.Path(__file__).resolve().parent.parent / "Assets" / "_Project" / "Audio"

# file, title, bpm, beats per bar, first downbeat (s), key, end of the music (s), sections as (bar, label).
SONGS = [
    # Tools/songs/glass_room.py: intro bars 0-1, verse 2-9, chorus 10-17, outro 18-19.
    ("GlassRoom", "Glass Room", 96, 4, 0.0, "A minor", 51.0,
     [(0, "intro"), (2, "verse"), (10, "chorus"), (18, "outro")]),
    # Tools/songs/neon_tide.py: intro 0-3, verse 4-11, build 12-13 (snare roll and riser), drop 14-23, outro 24-27.
    ("NeonTide", "Neon Tide", 124, 4, 0.0, "F minor", 55.5,
     [(0, "intro"), (4, "verse"), (12, "build"), (14, "drop"), (24, "outro")]),
    # Tools/songs/rainy_window.py: two bars of keys, verse 2-9, chorus 10-17, two closing bars and the last chord.
    ("RainyWindow", "Rainy Window", 80, 4, 0.0, "C major", 63.0,
     [(0, "intro"), (2, "verse"), (10, "chorus"), (18, "outro")]),
    # Tools/songs/paper_boat.py (3/4): intro 0-3 with the glockenspiel tune, verse 4-19, chorus 20-35, outro 36-39.
    ("PaperBoat", "Paper Boat", 144, 3, 0.0, "G major", 52.5,
     [(0, "intro"), (4, "verse"), (20, "chorus"), (36, "outro")]),
    # Plan: intro 10, verse 24, chorus 20, verse 24, chorus 20, bridge 16, final chorus 22, outro 12 (148 s).
    # Audio: an 8-bar intro, 12-bar choruses (the last line, then four bars of synths), a one-bar drum break before
    # each chorus (bars 17, 38, 58) and the vocals leading into each chorus a beat early. The outro is the four bars
    # after the final chorus's last line and the fade.
    ("TurnItUpToLight", "Turn It Up to Light", 118, 4, 0.02, "Ab major", 148.0,
     [(0, "intro"), (8, "verse 1"), (18, "chorus"), (30, "verse 2"), (39, "chorus"), (51, "bridge"),
      (59, "final chorus"), (67, "outro")]),
    # Plan: intro 12, verse 30, chorus 26, verse 30, chorus 28, outro 14 (140 s).
    # Audio: an 8-bar guitar intro, then after the first chorus five quiet bars of guitar alone (an interlude the
    # plan doesn't have) before the bass and drums come in with the second verse. The music has faded by 137 s.
    ("TheWorldIsHumming", "The World Is Humming", 92, 4, 0.025, "D major", 137.0,
     [(0, "intro"), (8, "verse 1"), (16, "chorus"), (24, "interlude"), (29, "verse 2"), (38, "final chorus"),
      (46, "outro")]),
    # Plan: intro 12, verse 26, hook 20, interlude 14, verse 26, hook 22, outro 16 (136 s).
    # Audio: the hi-hats come in with each rap verse (the rap starts on beat three of its first bar), the hooks are
    # five bars, and the Rhodes interlude runs nine bars. The crackle has faded by 131.5 s.
    ("SlowMotionGlow", "Slow Motion Glow", 80, 4, 1.505, "C major", 131.5,
     [(0, "intro"), (7, "verse 1"), (13, "hook"), (18, "interlude"), (27, "verse 2"), (33, "final hook"),
      (38, "outro")]),
]


def stamp(t):
    return f"{int(t // 60)}:{t % 60:05.2f}"


def starts(song):
    _, _, bpm, beats, first, _, _, sections = song
    bar = 60.0 / bpm * beats
    # The first section always starts with the song, even when the first downbeat comes a moment later.
    return [(0.0 if i == 0 else first + b * bar, label) for i, (b, label) in enumerate(sections)]


def write(song):
    file, title, bpm, beats, first, key, end, _ = song
    lines = [
        f"# {title}: where each section starts (m:ss.xx) and what it is. Journey mode changes world at each one.",
        "# Written by Tools/song_sections.py; edit the song there.",
        f"bpm {bpm}",
        f"beats {beats}",
        f"key {key}",
    ]
    lines += [f"{stamp(t)} {label}" for t, label in starts(song)]
    lines.append(f"{stamp(end)} end")
    path = AUDIO / f"{file}.sections.txt"
    path.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"Wrote {path.name}: " + ", ".join(f"{label} {stamp(t)}" for t, label in starts(song)))


def lrc_lines(file):
    path = AUDIO / f"{file}.lrc"
    if not path.exists():
        return []
    out = []
    for raw in path.read_text(encoding="utf-8").splitlines():
        m = re.match(r"\[(\d+):(\d+(?:\.\d+)?)\](.*)", raw)
        if not m:
            continue
        text = re.sub(r"<[^>]*>", "", m.group(3)).strip()
        if text:
            out.append((int(m.group(1)) * 60 + float(m.group(2)), text))
    return out


def check(song):
    file, title, *_ = song
    marks = starts(song)
    print(f"\n{title}")
    lines = lrc_lines(file)
    for i, (t, label) in enumerate(marks):
        nxt = marks[i + 1][0] if i + 1 < len(marks) else song[6]
        # A line sung up to a beat and a half before the section's downbeat leads into it (a pickup).
        lead = 1.5 * 60.0 / song[2]
        sung = [text for lt, text in lines if t - lead <= lt < nxt - lead]
        print(f"  {stamp(t)}-{stamp(nxt)} {label:13s} " + (" / ".join(s[:28] for s in sung) or "(instrumental)"))


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--check", action="store_true", help="list each section's sung lines")
    args = parser.parse_args()
    for song in SONGS:
        write(song)
        if args.check:
            check(song)


if __name__ == "__main__":
    main()
