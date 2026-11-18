"""
Generates the ElevenLabs songs (music with sung lyrics) and their word-timed LRC lyrics.

For each song: Eleven Music composes it from a composition plan (one chunk per section, with its lyrics and styles),
then ElevenLabs' forced alignment times every sung word against the lyrics, which become the .lrc file.

The API key is read from the ELEVENLABS_API_KEY environment variable only; it is never written anywhere.

    python Tools/elevenlabs_songs.py --out <folder> [--only TurnItUpToLight ...] [--align-only]

Writes <File>.mp3, <File>.lrc and <File>.alignment.json per song into the folder. Copy the .mp3 and .lrc into
Assets/_Project/Audio once you have listened to them.
"""

import argparse
import difflib
import json
import os
import re
import sys
import urllib.error
import urllib.request
import uuid

API = "https://api.elevenlabs.io/v1"
MODEL = "music_v2_5"
OUTPUT_FORMAT = "mp3_44100_192"
ARTIST = "Handmonic"


def chunk(section, duration_s, styles, lines=(), negative=()):
    return {"section": section, "duration_ms": int(duration_s * 1000), "styles": list(styles),
            "lines": list(lines), "negative": list(negative)}


# --- The songs -------------------------------------------------------------------------------------------------

T1_STYLE = ["synth-pop", "118 bpm", "bright analog synths", "punchy electronic drums", "clear female lead vocal",
            "euphoric", "uplifting major key"]
T1_NEG = ["rock guitar", "heavy distortion", "male vocal", "rap"]
T1_CHORUS = ["Turn it up, turn it up to light", "Every sound becomes a star tonight",
             "I can feel it, I can feel it, I can feel it in my hands", "Turn it up, turn it up to light"]

T2_STYLE = ["indie folk", "92 bpm", "fingerpicked acoustic guitar", "soft kick drum", "warm male lead vocal",
            "vocal harmonies", "intimate and hopeful"]
T2_NEG = ["electronic drums", "synthesizer", "female lead vocal", "distorted guitar"]
T2_CHORUS = ["Oh, the world is humming if you hold still long enough", "Every stone a drum, every breath a song of us",
             "Oh, the world is humming, can you feel it in your chest?",
             "Put your hand against the earth and let it do the rest"]

T3_STYLE = ["lo-fi hip-hop", "neo-soul", "80 bpm", "dusty boom bap drums", "Rhodes electric piano", "vinyl crackle",
            "mellow late-night mood"]
T3_NEG = ["trap hi-hats", "aggressive", "distorted bass", "edm"]
T3_HOOK = ["Slow motion, let it glow", "Feel the rhythm, let it go", "Every note's a little light",
           "Slow motion through the night"]

SONGS = [
    {
        "file": "TurnItUpToLight", "title": "Turn It Up to Light",
        "chunks": [
            chunk("Intro", 10, T1_STYLE + ["instrumental", "arpeggiated synth build"], negative=T1_NEG),
            chunk("Verse 1", 24, T1_STYLE + ["driving verse"], [
                "Hands up in the dark, I can feel it start", "A low hum rising underneath my heart",
                "Every beat's a spark running through my skin", "Open up the room and let the colour in"], T1_NEG),
            chunk("Chorus", 20, T1_STYLE + ["big anthemic chorus", "layered vocals"], T1_CHORUS, T1_NEG),
            chunk("Verse 2", 24, T1_STYLE + ["pulsing bass"], [
                "Bass like a wave rolling through the floor", "Every note I touch opens up a door",
                "Nothing left to hide, nothing left to fight", "I'm just a frequency dissolving into light"], T1_NEG),
            chunk("Chorus", 20, T1_STYLE + ["big anthemic chorus", "layered vocals"], T1_CHORUS, T1_NEG),
            chunk("Bridge", 16, T1_STYLE + ["stripped back", "soft pads", "breathy vocal"], [
                "Close your eyes, you don't need to see", "The music's moving, moving through me"], T1_NEG),
            chunk("Final Chorus", 22, T1_STYLE + ["biggest chorus", "layered vocals", "full energy"], T1_CHORUS, T1_NEG),
            chunk("Outro", 12, T1_STYLE + ["instrumental", "fading synths"], negative=T1_NEG),
        ],
    },
    {
        "file": "TheWorldIsHumming", "title": "The World Is Humming",
        "chunks": [
            chunk("Intro", 12, T2_STYLE + ["instrumental", "solo fingerpicked guitar"], negative=T2_NEG),
            chunk("Verse 1", 30, T2_STYLE + ["gentle verse"], [
                "Morning on the water, not a word around",
                "I laid my hand upon the river and it hummed back from the ground",
                "The wind was keeping rhythm in the branches overhead",
                "And I swear I heard a melody in everything it said"], T2_NEG),
            chunk("Chorus", 26, T2_STYLE + ["warm singalong chorus", "harmonies swell"], T2_CHORUS, T2_NEG),
            chunk("Verse 2", 30, T2_STYLE + ["gentle verse", "light tambourine"], [
                "Fire in the evening, crackle like a snare",
                "Lightning in the distance playing chords out in the air",
                "I used to think that silence was the only place to rest",
                "Now I hear the music when I'm quiet at my best"], T2_NEG),
            chunk("Chorus", 28, T2_STYLE + ["fullest chorus", "harmonies swell", "soft strings"], T2_CHORUS, T2_NEG),
            chunk("Outro", 14, T2_STYLE + ["instrumental", "guitar fades out"], negative=T2_NEG),
        ],
    },
    {
        "file": "SlowMotionGlow", "title": "Slow Motion Glow",
        "chunks": [
            chunk("Intro", 12, T3_STYLE + ["instrumental", "filtered Rhodes chords"], negative=T3_NEG),
            chunk("Verse 1", 26, T3_STYLE + ["laid-back male rap verse"], [
                "Fingers on the beat like I'm reading it in braille", "Every kick in my chest, every snare's a comet tail",
                "Turn the volume low so the colours come alive", "Shadows on the wall dancing in a 4/4 vibe"], T3_NEG),
            chunk("Hook", 20, T3_STYLE + ["smooth sung hook", "soulful female vocal"], T3_HOOK, T3_NEG),
            chunk("Interlude", 14, T3_STYLE + ["instrumental", "Rhodes solo"], negative=T3_NEG),
            chunk("Verse 2", 26, T3_STYLE + ["laid-back male rap verse"], [
                "Bassline moving like the tide inside my bones", "Never felt a song till I felt it on my own",
                "Touch the air and it ripples like a pool", "Lo-fi heartbeat keeping everything cool"], T3_NEG),
            chunk("Hook", 22, T3_STYLE + ["smooth sung hook", "soulful female vocal", "harmonies"], T3_HOOK, T3_NEG),
            chunk("Outro", 16, T3_STYLE + ["instrumental", "vinyl crackle fades out"], negative=T3_NEG),
        ],
    },
]


# --- API -------------------------------------------------------------------------------------------------------

def key():
    k = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not k:
        sys.exit("Set ELEVENLABS_API_KEY first.")
    return k


def request(url, body, content_type, timeout):
    req = urllib.request.Request(url, data=body, method="POST",
                                 headers={"xi-api-key": key(), "Content-Type": content_type})
    try:
        with urllib.request.urlopen(req, timeout=timeout) as r:
            return r.read(), dict(r.headers)
    except urllib.error.HTTPError as e:
        sys.exit(f"{url} failed with HTTP {e.code}: {e.read().decode('utf-8', 'replace')[:2000]}")


def compose(song):
    plan = {"chunks": []}
    for c in song["chunks"]:
        text = f"[{c['section']}]"
        if c["lines"]:
            text += "\n" + "\n".join(c["lines"])
        entry = {"text": text, "duration_ms": c["duration_ms"], "positive_styles": c["styles"]}
        if c["negative"]:
            entry["negative_styles"] = c["negative"]
        plan["chunks"].append(entry)
    body = json.dumps({"model_id": MODEL, "composition_plan": plan}).encode("utf-8")
    audio, headers = request(f"{API}/music?output_format={OUTPUT_FORMAT}", body, "application/json", 600)
    song_id = next((v for k, v in headers.items() if k.lower() == "song-id"), None)
    return audio, song_id


def align(audio, text):
    boundary = uuid.uuid4().hex
    parts = [
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"song.mp3\"\r\n"
        f"Content-Type: audio/mpeg\r\n\r\n".encode() + audio + b"\r\n",
        f"--{boundary}\r\nContent-Disposition: form-data; name=\"text\"\r\n\r\n".encode() + text.encode("utf-8") + b"\r\n",
        f"--{boundary}--\r\n".encode(),
    ]
    body, _ = request(f"{API}/forced-alignment", b"".join(parts), f"multipart/form-data; boundary={boundary}", 600)
    return json.loads(body)


# --- LRC -------------------------------------------------------------------------------------------------------

def norm(word):
    return re.sub(r"[^a-z0-9']", "", word.lower())


def stamp(t):
    t = max(0.0, t)
    return f"{int(t // 60):02d}:{t % 60:05.2f}"


def lrc(song, alignment):
    """One LRC line per sung line, every word with its own time, matched against what the alignment heard."""
    lines = [line for c in song["chunks"] for line in c["lines"]]
    tokens = [(i, w) for i, line in enumerate(lines) for w in line.split()]
    heard = [w for w in alignment.get("words", []) if w.get("text", "").strip()]
    times = [None] * len(tokens)
    matcher = difflib.SequenceMatcher(a=[norm(w) for _, w in tokens], b=[norm(w["text"]) for w in heard], autojunk=False)
    for block in matcher.get_matching_blocks():
        for k in range(block.size):
            times[block.a + k] = float(heard[block.b + k]["start"])
    # Words the alignment didn't match take a time between their neighbours.
    known = [i for i, t in enumerate(times) if t is not None]
    if not known:
        sys.exit(f"The alignment of {song['title']} matched no words.")
    for i in range(len(times)):
        if times[i] is not None:
            continue
        before = max((k for k in known if k < i), default=None)
        after = min((k for k in known if k > i), default=None)
        if before is None:
            times[i] = times[after]
        elif after is None:
            times[i] = times[before] + 0.3 * (i - before)
        else:
            times[i] = times[before] + (times[after] - times[before]) * (i - before) / (after - before)
    for i in range(1, len(times)):
        times[i] = max(times[i], times[i - 1])

    out = [f"[ti:{song['title']}]", f"[ar:{ARTIST}]"]
    for li in range(len(lines)):
        words = [(times[k], w) for k, (i, w) in enumerate(tokens) if i == li]
        out.append(f"[{stamp(words[0][0])}]" + " ".join(f"<{stamp(t)}>{w}" for t, w in words))
    matched = sum(b.size for b in matcher.get_matching_blocks())
    return "\n".join(out) + "\n", matched, len(tokens)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", required=True)
    ap.add_argument("--only", nargs="*")
    ap.add_argument("--align-only", action="store_true", help="re-time lyrics for mp3s already in --out")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    for song in SONGS:
        if args.only and song["file"] not in args.only:
            continue
        base = os.path.join(args.out, song["file"])
        if args.align_only:
            with open(base + ".mp3", "rb") as f:
                audio = f.read()
        else:
            print(f"Composing {song['title']}...", flush=True)
            audio, song_id = compose(song)
            with open(base + ".mp3", "wb") as f:
                f.write(audio)
            print(f"  {len(audio) / 1e6:.1f} MB, song id {song_id}", flush=True)
        print(f"Timing the lyrics of {song['title']}...", flush=True)
        text = "\n".join(line for c in song["chunks"] for line in c["lines"])
        alignment = align(audio, text)
        with open(base + ".alignment.json", "w", encoding="utf-8") as f:
            json.dump(alignment, f, indent=1)
        text, matched, total = lrc(song, alignment)
        with open(base + ".lrc", "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print(f"  {matched}/{total} words matched, alignment loss {alignment.get('loss')}", flush=True)
    print("Done.")


if __name__ == "__main__":
    main()
