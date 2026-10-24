"""Render the demo songs to WAV files plus word-timed LRC lyrics and .notes melodies.

The songs, melodies and lyrics were written for this project, so they are safe to ship.
Usage: python3 Tools/render_songs.py [song ...]  (all songs by default; writes into Assets/_Project/Audio)
Song names are the modules in Tools/songs, e.g. glass_room. --out DIR writes somewhere else.
"""
import argparse
import importlib
import pathlib
import sys

TOOLS = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(TOOLS))


def main():
    names = sorted(p.stem for p in (TOOLS / "songs").glob("*.py") if p.stem != "__init__")
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("songs", nargs="*", metavar="song", help=f"one of: {', '.join(names)}")
    parser.add_argument("--out", type=pathlib.Path, default=TOOLS.parent / "Assets" / "_Project" / "Audio")
    args = parser.parse_args()
    unknown = [n for n in args.songs if n not in names]
    if unknown: parser.error(f"unknown song {', '.join(unknown)}; choose from {', '.join(names)}")
    args.out.mkdir(parents=True, exist_ok=True)

    for name in args.songs or names:
        module = importlib.import_module(f"songs.{name}")
        song = module.render()
        song.write_wav(args.out / f"{module.NAME}.wav")
        song.write_lrc(args.out / f"{module.NAME}.lrc")
        song.write_notes(args.out / f"{module.NAME}.notes")
        print(f"Wrote {song.title}: {module.NAME}.wav, .lrc and .notes ({song.length:.0f} s) to {args.out}")


if __name__ == "__main__":
    main()
