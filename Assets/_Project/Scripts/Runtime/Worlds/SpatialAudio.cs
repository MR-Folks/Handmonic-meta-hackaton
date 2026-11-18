using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace MusicInYourHands
{
    /// <summary>
    /// Sounds that come from where they happen. Every world sound with a place in the room (a plucked string, a struck
    /// drum, a popped bubble, a chime in the menu) is spatialized by Meta XR Audio, the project's spatializer plugin: you
    /// hear it to your left or right, in front or behind, above or below and near or far, and it stays put as you turn
    /// your head. Each one plays through Resources/Audio/SpatialMixer, whose Meta XR Audio Reflection effect adds the
    /// room's early reflections and a modest reverb; RoomAcoustics sizes that room from the scan (ShapeRoom). The song
    /// is left alone: MusicBus plays it as a plain stereo bed, straight to the speakers.
    /// </summary>
    public static class SpatialAudio
    {
        /// <summary>Metres within which a sound plays at full level: about a hand's reach from the eyes.</summary>
        public const float Near = 0.5f;

        /// <summary>
        /// How fast the level falls past Near, per metre: the gain is 1 / (1 + Falloff * (distance - Near)), about -3 dB
        /// at 1 m, -7 dB at 2 m and -10 dB at 3 m. That is gentler than the real 1/distance, so a sound across the room
        /// still carries over the song while near and far are easy to tell apart.
        /// </summary>
        public const float Falloff = 0.8f;

        /// <summary>Metres past which the level stops falling (the end of the rolloff curve).</summary>
        public const float Far = 10f;

        /// <summary>The mixer every spatialized sound plays through, in a Resources folder.</summary>
        public const string MixerPath = "Audio/SpatialMixer";

        static AudioMixerGroup output;
        static bool lookedForOutput;
        static readonly Dictionary<float, AnimationCurve> Curves = new Dictionary<float, AnimationCurve>();

        /// <summary>SpatialMixer's Master group, with Meta XR Audio's reflections and reverb; null if it is missing.</summary>
        public static AudioMixerGroup Output
        {
            get
            {
                if (lookedForOutput) return output;
                lookedForOutput = true;
                var mixer = Resources.Load<AudioMixer>(MixerPath);
                var groups = mixer != null ? mixer.FindMatchingGroups("Master") : null;
                output = groups != null && groups.Length > 0 ? groups[0] : null;
                if (output == null) Debug.LogError($"[Audio] Resources/{MixerPath} is missing; spatial sounds will play without the room's reverb.");
                return output;
            }
        }

        /// <summary>
        /// Makes <paramref name="source"/> a sound in the room: spatialized by Meta XR Audio at full spatial blend,
        /// level falling gently with distance past <paramref name="near"/> metres (see Falloff), no Doppler, through
        /// SpatialMixer for the room's reverb. Call it once, when the source is made; its clips must be mono.
        /// </summary>
        public static void Spatialize(AudioSource source, float near = Near)
        {
            source.spatialBlend = 1f;
            source.spatialize = true;
            source.spatializePostEffects = false;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.minDistance = near;
            source.maxDistance = Far;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, Rolloff(near));
            source.outputAudioMixerGroup = Output;
        }

        /// <summary>The level (0-1) a spatialized sound plays at, <paramref name="distance"/> metres from your head.</summary>
        public static float Gain(float distance, float near = Near) =>
            distance <= near ? 1f : 1f / (1f + Falloff * (Mathf.Min(distance, Far) - near));

        /// <summary>
        /// The rolloff curve for <paramref name="near"/>, made once and shared. Unity reads a custom rolloff curve over
        /// distance / maxDistance, so the keys sit at distance / Far, with tangents that follow Gain exactly.
        /// </summary>
        static AnimationCurve Rolloff(float near)
        {
            if (Curves.TryGetValue(near, out var curve)) return curve;
            float[] past = { 0f, 0.25f, 0.5f, 1f, 1.5f, 2.5f, 3.5f, 5f, 7f };
            var keys = new List<Keyframe> { new Keyframe(0f, 1f, 0f, 0f) };
            foreach (float extra in past)
            {
                float d = near + extra;
                if (d >= Far) break;
                float slope = -Falloff / Mathf.Pow(1f + Falloff * extra, 2f) * Far;
                keys.Add(new Keyframe(d / Far, Gain(d, near), extra == 0f ? 0f : slope, slope));
            }
            float end = -Falloff / Mathf.Pow(1f + Falloff * (Far - near), 2f) * Far;
            keys.Add(new Keyframe(1f, Gain(Far, near), end, 0f));
            curve = new AnimationCurve(keys.ToArray());
            Curves[near] = curve;
            return curve;
        }

        /// <summary>The room the reverb is shaped by: its size along the world's axes, how cluttered, and whether it was scanned.</summary>
        public struct RoomShape
        {
            /// <summary>Metres: width along the world's x axis, height, depth along z.</summary>
            public Vector3 Size;

            /// <summary>0 an empty room to 1 a very full one: furniture softens and diffuses the reverb.</summary>
            public float Clutter;

            public bool Scanned;
        }

        /// <summary>
        /// The room for the reverb, from the scan (Room): the extent of its walls, floor to ceiling, and clutter from
        /// how much of the floor the furniture's tops cover (<paramref name="clutterRange"/> from none to 40 % or more).
        /// Meta's shoebox room lies along the world's axes, so each of the room's two sides goes to the axis nearest to
        /// the way it runs. Sides are kept to a sensible range and never equal (a cube rings). Without a scan, or
        /// with fewer than three walls, it is <paramref name="standIn"/> with <paramref name="standInClutter"/>.
        /// </summary>
        public static RoomShape ShapeRoom(Vector3 standIn, float standInClutter, Vector2 clutterRange)
        {
            var shape = new RoomShape { Size = standIn, Clutter = standInClutter };
            var walls = Room.Walls;
            if (Room.Scanned && walls.Count >= 3)
            {
                // The longest wall sets the room's own axes, as Room.Around does for the floor.
                int longest = 0;
                for (int i = 1; i < walls.Count; i++)
                    if (walls[i].Size.x > walls[longest].Size.x) longest = i;
                var along = Vector3.ProjectOnPlane(walls[longest].Right, Vector3.up);
                if (along.sqrMagnitude > 1e-6f)
                {
                    along.Normalize();
                    var across = Vector3.Cross(along, Vector3.up);
                    Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
                    foreach (var w in walls)
                        for (int end = -1; end <= 1; end += 2)
                        {
                            var p = w.Center + w.Right * (w.Size.x * 0.5f * end);
                            var q = new Vector2(Vector3.Dot(p, along), Vector3.Dot(p, across));
                            min = Vector2.Min(min, q);
                            max = Vector2.Max(max, q);
                        }
                    var extent = max - min;
                    bool alongIsX = Mathf.Abs(along.x) >= Mathf.Abs(along.z);
                    float height = Room.CeilingY - Room.FloorY;
                    shape.Size = new Vector3(alongIsX ? extent.x : extent.y, height > 1.6f ? height : standIn.y, alongIsX ? extent.y : extent.x);

                    float covered = 0f;
                    foreach (var top in Room.Tops) covered += top.Size.x * top.Size.y;
                    float floorArea = Mathf.Max(1f, extent.x * extent.y);
                    shape.Clutter = Mathf.Lerp(clutterRange.x, clutterRange.y, Mathf.Clamp01(covered / floorArea / 0.4f));
                    shape.Scanned = true;
                }
            }

            var size = shape.Size;
            size.x = Mathf.Clamp(size.x, 1.5f, 12f);
            size.y = Mathf.Clamp(size.y, 2.1f, 4.5f);
            size.z = Mathf.Clamp(size.z, 1.5f, 12f);
            // No two sides within 4 % of each other: equal sides reinforce the same echoes.
            if (Mathf.Abs(size.x - size.z) < 0.04f * size.x) size.z = size.x * 1.07f;
            if (Mathf.Abs(size.y - size.x) < 0.04f * size.x) size.y *= 0.93f;
            if (Mathf.Abs(size.y - size.z) < 0.04f * size.z) size.y *= 0.93f;
            shape.Size = size;
            shape.Clutter = Mathf.Clamp01(shape.Clutter);
            return shape;
        }
    }
}
