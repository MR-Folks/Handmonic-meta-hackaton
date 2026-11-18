using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// Flashing is kept in check. Every world, driven hard (a beat on every frame, both hands striking, pinching and
        /// letting go ten times a second) with Less flashing off and on, never makes more than three flashes in any one
        /// second (World.Flash), and the limiter really holds some back. With Less flashing on, each flash shows at a
        /// quarter of its strength, the kick reaches the worlds at a quarter, the hands' beat glow and pulse drop to a
        /// quarter, and Lightning's room flash becomes a gentle glow (under a third as bright, rising slowly).
        /// </summary>
        static bool CheckFlashes()
        {
            var failures = new List<string>();
            bool was = ComfortOptions.LessFlashing;
            var strongest = new Dictionary<System.Type, float[]>();
            int held = 0, worldsFlashing = 0;
            float worst = 0f;
            string worstWorld = "";
            try
            {
                foreach (bool calm in new[] { false, true })
                {
                    ComfortOptions.LessFlashing = calm;
                    foreach (var type in WorldCatalog.Types().OrderBy(t => t.Name))
                    {
                        var (log, heldBack) = Hammer(type, 4f);
                        held += heldBack;
                        int most = MostInASecond(log);
                        if (most > World.MostFlashesPerSecond)
                            failures.Add($"{type.Name} flashed {most} times within a second{(calm ? " with Less flashing" : "")}.");
                        if (log.Count == 0) continue;
                        if (!calm) worldsFlashing++;
                        if (!strongest.TryGetValue(type, out var pair)) strongest[type] = pair = new float[2];
                        pair[calm ? 1 : 0] = log.Max(f => f.y);
                    }
                }
                foreach (var entry in strongest)
                {
                    float off = entry.Value[0], on = entry.Value[1];
                    if (off <= 0f) continue;
                    float ratio = on / off;
                    if (ratio > worst) { worst = ratio; worstWorld = entry.Key.Name; }
                    if (ratio > 0.3f) failures.Add($"{entry.Key.Name}'s strongest flash with Less flashing is {ratio:0.00} of its usual (want about a quarter).");
                }
                if (held == 0) failures.Add("no world was ever held back by the flash limiter, so it went untested.");
                if (worldsFlashing < 4) failures.Add($"only {worldsFlashing} worlds flashed at all while hammered; the check no longer reaches them.");

                // The kick reaches a world at a quarter with Less flashing.
                var eyes = StageEyes(out var rig);
                var probe = new GameObject("Probe").AddComponent<HookProbe>();
                ProjectSetup.AssignMaterials(probe);
                ComfortOptions.LessFlashing = false;
                probe.Step(rig, new MusicFrame { Kick = 1f }, eyes, 0.02f);
                float kickOff = probe.Kick;
                ComfortOptions.LessFlashing = true;
                probe.Step(rig, new MusicFrame { Kick = 1f }, eyes, 0.02f);
                float kickOn = probe.Kick;
                if (Mathf.Abs(kickOff - 1f) > 1e-4f || Mathf.Abs(kickOn - 0.25f) > 1e-4f) failures.Add($"the kick reached a world at {kickOff:0.00} and, with Less flashing, {kickOn:0.00} (want 1 and 0.25).");

                // Lightning's strike: a full flash of the room, or a gentle glow.
                float Peak(bool calm, out float rise)
                {
                    ComfortOptions.LessFlashing = calm;
                    var (world, handRig, view) = StageWorld<Lightning>();
                    for (int i = 0; i < 30; i++) world.Step(handRig, Music(PreviewBands, 0.1f, false), view, 1f / 60f);
                    world.PreviewStorm();
                    float peak = 0f;
                    rise = -1f;
                    for (int i = 0; i < 60; i++)
                    {
                        world.Step(handRig, Music(PreviewBands, 0.1f, false), view, 1f / 60f);
                        if (world.RoomLight > peak) { peak = world.RoomLight; rise = i / 60f; }
                    }
                    return peak;
                }
                float full = Peak(false, out float fullRise), gentle = Peak(true, out float gentleRise);
                if (full < 0.8f || gentle > 0.3f || gentleRise < 0.05f)
                    failures.Add($"Lightning's strike lit the room to {full:0.00} (at {fullRise:0.00} s), and with Less flashing to {gentle:0.00} (at {gentleRise:0.00} s): want a full flash, then under 0.3 rising over a moment.");

#if META_XR_CORE
                // The hands' beat glow and pulse.
                var skin = StageFeedbackHand(out var hand);
                var block = new MaterialPropertyBlock();
                ComfortOptions.LessFlashing = false;
                skin.Apply();
                hand.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(block);
                float pulseOff = block.GetFloat("_PulseScale");
                ComfortOptions.LessFlashing = true;
                skin.Apply();
                hand.GetComponentInChildren<SkinnedMeshRenderer>().GetPropertyBlock(block);
                float pulseOn = block.GetFloat("_PulseScale");
                Object.DestroyImmediate(hand);
                if (Mathf.Abs(pulseOff - 1f) > 1e-4f || Mathf.Abs(pulseOn - 0.25f) > 1e-4f) failures.Add($"the hands' beat pulse showed at {pulseOff:0.00} and, with Less flashing, {pulseOn:0.00} (want 1 and 0.25).");
#endif
            }
            finally
            {
                ComfortOptions.LessFlashing = was;
            }
            foreach (var failure in failures) Debug.LogError($"[Check] Flashes: {failure}");
            if (failures.Count == 0)
                Debug.Log($"[Check] Flashes: hammered with a beat every frame and both hands striking and pinching, no world flashed more than " +
                          $"{World.MostFlashesPerSecond} times in a second ({worldsFlashing} worlds flashed; the limiter held back {held} flashes); with Less flashing " +
                          $"each flash showed at a quarter (at most {worst:0.00} of usual, {worstWorld}), the kick and the hands' pulse at a quarter, " +
                          "and Lightning's strike became a gentle glow.");
            return failures.Count == 0;
        }

        /// <summary>
        /// One world driven as hard as hands and music can: a beat every frame (with a full kick), and both hands going
        /// from open to a fist, open again and a pinch, a new shape every 0.1 s, for <paramref name="seconds"/>. Returns its flashes (time,
        /// strength) and how many it held back.
        /// </summary>
        static (List<Vector2> log, int held) Hammer(System.Type type, float seconds)
        {
            var eyes = StageEyes(out var rig);
            var world = (World)new GameObject(type.Name).AddComponent(type);
            ProjectSetup.AssignMaterials(world);
            world.Build();
            const float dt = 1f / 30f;
            var shapes = new[] { HandShape.Open, HandShape.Fist, HandShape.Open, HandShape.Pinch };
            for (float t = 0f; t < seconds; t += dt)
            {
                var shape = shapes[(int)(t / 0.1f) % shapes.Length];
                PutHand(rig, true, new Vector3(-0.15f, 1.3f, 0.38f), Vector3.right, Vector3.up, shape, Vector3.zero);
                PutHand(rig, false, new Vector3(0.15f, 1.3f, 0.38f), Vector3.left, Vector3.up, shape, Vector3.zero);
                world.Step(rig, new MusicFrame { Bands = PreviewBands, Kick = 1f, Beat = true, BeatAge = 0f, BeatInterval = 0.3f, SongTime = 10f + t }, eyes, dt);
            }
            var log = new List<Vector2>(world.FlashLog);
            int held = world.FlashesHeld;
            Object.DestroyImmediate(world.gameObject);
            return (log, held);
        }

        /// <summary>The most flashes in any one second of a log of (time, strength).</summary>
        static int MostInASecond(List<Vector2> log)
        {
            int most = 0;
            for (int i = 0; i < log.Count; i++)
            {
                int n = 0;
                for (int j = i; j < log.Count && log[j].x - log[i].x <= 1f; j++) n++;
                most = Mathf.Max(most, n);
            }
            return most;
        }
    }
}
