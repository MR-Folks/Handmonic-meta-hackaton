using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if META_XR_AUDIO
using Meta.XR.Acoustics;
using MusicInYourHands.Meta;
#endif

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// Spatial audio is set up: Meta XR Audio spatializes (and is loaded) and decodes ambisonics, the output is
        /// stereo with the Best latency buffer and the acoustic model is the Shoebox Room; SpatialMixer carries Meta's
        /// reflection effect on its Master group; every sound source in every world plays spatialized through it, at
        /// full level within reach and still clearly audible a few metres away, with Air strings' drums and cymbals
        /// each sounding from where it stands and its strings and Bubble notes' pops from the pool; the song stays a
        /// plain stereo bed; and the reverb room follows a scanned room at an angle and falls back without a scan.
        /// </summary>
        static bool CheckSpatialAudio()
        {
            var failures = new List<string>();

            // Project settings.
            if (AudioSettings.GetSpatializerPluginName() != SpatialAudioSetup.PluginName)
                failures.Add($"The spatializer plugin is \"{AudioSettings.GetSpatializerPluginName()}\", not \"{SpatialAudioSetup.PluginName}\". Run Music In Your Hands > Set Up Spatial Audio.");
            if (!AudioSettings.GetSpatializerPluginNames().Contains(SpatialAudioSetup.PluginName))
                failures.Add($"The {SpatialAudioSetup.PluginName} plugin isn't loaded (is com.meta.xr.sdk.audio installed?).");
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath(SpatialAudioSetup.AudioManagerPath)[0]);
            if (manager.FindProperty("m_AmbisonicDecoderPlugin").stringValue != SpatialAudioSetup.PluginName)
                failures.Add($"The ambisonic decoder plugin isn't {SpatialAudioSetup.PluginName}.");
            if (manager.FindProperty("Default Speaker Mode").intValue != (int)AudioSpeakerMode.Stereo)
                failures.Add("The default speaker mode isn't Stereo.");
            if (manager.FindProperty("m_DSPBufferSize").intValue != SpatialAudioSetup.BestLatency)
                failures.Add($"The DSP buffer size is {manager.FindProperty("m_DSPBufferSize").intValue}, not Best latency ({SpatialAudioSetup.BestLatency}).");
#if META_XR_AUDIO
            var acoustics = AssetDatabase.LoadAssetAtPath<MetaXRAcousticSettings>(SpatialAudioSetup.AcousticSettingsPath);
            if (acoustics == null || acoustics.AcousticModel != AcousticModel.ShoeboxRoom)
                failures.Add($"{SpatialAudioSetup.AcousticSettingsPath} should choose the Shoebox Room acoustic model.");
#endif

            // The mixer: Meta's reflection effect on the group every spatial sound plays through.
            var output = SpatialAudio.Output;
            Object reflection = null, master = null;
            foreach (var part in AssetDatabase.LoadAllAssetsAtPath(SpatialAudioSetup.MixerAssetPath))
            {
                if (part == null) continue;
                if (part == output) master = part;
                if (new SerializedObject(part).FindProperty("m_EffectName")?.stringValue == "Meta XR Audio Reflection") reflection = part;
            }
            bool onMaster = false;
            if (master != null && reflection != null)
            {
                var effects = new SerializedObject(master).FindProperty("m_Effects");
                for (int i = 0; i < effects.arraySize; i++) onMaster |= effects.GetArrayElementAtIndex(i).objectReferenceValue == reflection;
            }
            if (output == null || !onMaster)
                failures.Add($"{SpatialAudioSetup.MixerAssetPath} should load from Resources with the Meta XR Audio Reflection effect on its Master group.");

            // Every world's sound sources, built as on the headset.
            var holder = new GameObject("Spatial Audio Check");
            int sources = 0, worlds = 0;
            var counts = new Dictionary<string, int>();
            int Count(string world) => counts.TryGetValue(world, out int n) ? n : 0;
            try
            {
                foreach (var type in WorldCatalog.Types().OrderBy(t => t.Name))
                {
                    var go = new GameObject(type.Name);
                    go.transform.SetParent(holder.transform, false);
                    var world = (World)go.AddComponent(type);
                    ProjectSetup.AssignMaterials(world);
                    world.Build();
                    var found = go.GetComponentsInChildren<AudioSource>(true);
                    if (found.Length > 0) worlds++;
                    counts[type.Name] = found.Length;
                    foreach (var source in found)
                    {
                        sources++;
                        CheckSpatialSource(source, $"{type.Name}/{source.name}", failures);
                    }
                }
                CheckAirStringsVoices(holder.transform.Find(nameof(AirStrings)).GetComponent<AirStrings>(), failures);
                if (Count(nameof(BubbleNotes)) == 0) failures.Add("Bubble notes has no sound sources.");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }

            // The song stays a stereo bed, and the scene has the room's acoustics.
            EditorSceneManager.OpenScene(ProjectSetup.MainScenePath, OpenSceneMode.Single);
            var bus = Object.FindAnyObjectByType<MusicBus>(FindObjectsInactive.Include);
            var song = bus != null ? bus.GetComponent<AudioSource>() : null;
            if (song == null || song.spatialize || song.spatialBlend > 0f || song.outputAudioMixerGroup != null)
                failures.Add("MusicBus's AudioSource should be a plain 2D source (no spatialize, spatial blend 0, no mixer), so the song is unchanged.");
#if META_XR_AUDIO
            if (Object.FindAnyObjectByType<RoomAcoustics>(FindObjectsInactive.Include) == null)
                failures.Add("Main.unity has no RoomAcoustics; run Update Worlds in Main Scene.");
#endif
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            string room = CheckRoomShape(failures);

            foreach (var failure in failures) Debug.LogError($"[Check] {failure}");
            if (failures.Count == 0)
            {
                Debug.Log($"[Check] Spatial audio: {AudioSettings.GetSpatializerPluginName()} spatializes and decodes ambisonics, stereo output, " +
                          $"DSP buffer {SpatialAudioSetup.BestLatency}, Shoebox Room acoustics, SpatialMixer with Meta's reflection effect. " +
                          $"All {sources} sound sources in {worlds} worlds are spatialized through it (Air strings {Count(nameof(AirStrings))}, " +
                          $"Bubble notes {Count(nameof(BubbleNotes))}): full level within {SpatialAudio.Near} m, " +
                          $"{20f * Mathf.Log10(SpatialAudio.Gain(1f)):F0} dB at 1 m, {20f * Mathf.Log10(SpatialAudio.Gain(3f)):F0} dB at 3 m. " +
                          $"The song stays a stereo bed. {room}");
            }
            return failures.Count == 0;
        }

        /// <summary>One sound source: spatialized through SpatialMixer, mono, with distances that suit a room.</summary>
        static void CheckSpatialSource(AudioSource source, string label, List<string> failures)
        {
            var problems = new List<string>();
            if (!source.spatialize) problems.Add("Spatialize is off");
            else if (!source.GetSpatializerFloat(0, out _)) problems.Add("Meta XR Audio didn't take it on");
            if (source.spatialBlend < 1f) problems.Add($"spatial blend is {source.spatialBlend}");
            if (source.outputAudioMixerGroup == null || source.outputAudioMixerGroup != SpatialAudio.Output) problems.Add("it doesn't play through SpatialMixer");
            if (source.dopplerLevel > 0f) problems.Add("it has Doppler");
            if (source.minDistance < 0.25f || source.minDistance > 1f || source.maxDistance < 6f)
                problems.Add($"its distances are {source.minDistance}-{source.maxDistance} m");
            float GainAt(float metres) => source.rolloffMode == AudioRolloffMode.Custom
                ? source.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(metres / source.maxDistance)
                : source.rolloffMode == AudioRolloffMode.Logarithmic ? Mathf.Min(1f, source.minDistance / metres)
                : Mathf.Clamp01(1f - (metres - source.minDistance) / (source.maxDistance - source.minDistance));
            if (GainAt(0.25f) < 0.95f) problems.Add($"it is {GainAt(0.25f):F2} loud within reach");
            // A few metres away it still carries over the song: no quieter than -12 dB at 3 m.
            if (GainAt(3f) < 0.25f || GainAt(3f) > GainAt(1f)) problems.Add($"its level at 3 m is {GainAt(3f):F2}");
            if (source.clip != null && source.clip.channels != 1) problems.Add($"its clip has {source.clip.channels} channels (Meta XR Audio needs mono)");
            if (problems.Count > 0) failures.Add($"{label}: {string.Join(", ", problems)}.");
        }

        /// <summary>
        /// Air strings: each drum and cymbal has its own voice on the piece itself, so once the kit stands round you
        /// every piece sounds from a place of its own within reach; the strings play from the pool where they are plucked.
        /// </summary>
        static void CheckAirStringsVoices(AirStrings world, List<string> failures)
        {
            int pieces = ((System.Array)typeof(AirStrings).GetField("Kit", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Length;
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.5f, 0f), Quaternion.identity);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            Room.Clear();
            try
            {
                world.Step(rig, Music(PreviewBands, 0.2f, false), camera, 0.02f);
                var kit = world.GetComponentsInChildren<AudioSource>(true).Where(s => s.GetComponent<MeshRenderer>() != null).ToList();
                if (kit.Count != pieces)
                {
                    failures.Add($"Air strings has {kit.Count} voices on its {pieces} drums and cymbals; each piece should have its own.");
                    return;
                }
                var eye = camera.transform.position;
                for (int i = 0; i < kit.Count; i++)
                {
                    var at = kit[i].transform.position;
                    if (Vector3.Distance(at, eye) > 1.2f) failures.Add($"Air strings' {kit[i].name} sounds from {Vector3.Distance(at, eye):F2} m away, not from the kit round you.");
                    for (int j = 0; j < i; j++)
                        if (Vector3.Distance(at, kit[j].transform.position) < 0.05f)
                            failures.Add($"Air strings' {kit[i].name} and {kit[j].name} sound from the same place.");
                }
                if (world.GetComponentsInChildren<AudioSource>(true).Length - kit.Count < 4) failures.Add("Air strings has too few pooled voices for its strings.");
            }
            finally
            {
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(rig.gameObject);
            }
        }

        /// <summary>
        /// The reverb room: a scanned 5 x 3 m room turned 30° from the world's axes gives a 5 m wide, 3 m deep box (its
        /// long side along the nearer world axis) as tall as the scan, more cluttered with more furniture; without a
        /// scan, the stand-in room. Returns a line for the log.
        /// </summary>
        static string CheckRoomShape(List<string> failures)
        {
            var standIn = new Vector3(4.2f, 2.6f, 5.4f);
            var clutterRange = new Vector2(0.35f, 0.8f);
            var turn = Quaternion.AngleAxis(30f, Vector3.up);
            var centre = new Vector3(0.4f, 0f, 1.1f);
            var walls = new List<Room.Surface>();
            for (int i = 0; i < 4; i++)
            {
                var outward = turn * (Quaternion.AngleAxis(90f * i, Vector3.up) * Vector3.forward);
                float reach = i % 2 == 0 ? 1.5f : 2.5f, span = i % 2 == 0 ? 5f : 3f;
                walls.Add(new Room.Surface(centre + outward * reach + Vector3.up * 1.3f, -outward, Vector3.Cross(Vector3.up, outward), new Vector2(span, 2.6f)));
            }
            var none = new List<Room.Surface>();
            var sofa = new List<Room.Surface> { new Room.Surface(centre + Vector3.up * 0.45f, Vector3.up, turn * Vector3.right, new Vector2(2f, 0.9f)) };
            try
            {
                Room.Set(0f, 2.6f, walls, none, none);
                var bare = SpatialAudio.ShapeRoom(standIn, 0.5f, clutterRange);
                Room.Set(0f, 2.6f, walls, none, sofa);
                var furnished = SpatialAudio.ShapeRoom(standIn, 0.5f, clutterRange);
                Room.Clear();
                var fallback = SpatialAudio.ShapeRoom(standIn, 0.5f, clutterRange);
                if (!bare.Scanned || Mathf.Abs(bare.Size.x - 5f) > 0.05f || Mathf.Abs(bare.Size.y - 2.6f) > 0.05f || Mathf.Abs(bare.Size.z - 3f) > 0.05f)
                    failures.Add($"A scanned 5 x 2.6 x 3 m room turned 30° made a {bare.Size} m reverb room.");
                if (Mathf.Abs(bare.Clutter - clutterRange.x) > 0.01f || furnished.Clutter <= bare.Clutter)
                    failures.Add($"Clutter should grow with furniture: {bare.Clutter:F2} bare, {furnished.Clutter:F2} with a sofa.");
                if (fallback.Scanned || fallback.Size != standIn || Mathf.Abs(fallback.Clutter - 0.5f) > 0.001f)
                    failures.Add($"Without a scan the reverb room should be the stand-in {standIn}, not {fallback.Size}.");
                return $"Reverb room: a scanned 5 x 3 m room at 30° gives {bare.Size.x:F1} x {bare.Size.y:F1} x {bare.Size.z:F1} m " +
                       $"(clutter {bare.Clutter:F2} bare, {furnished.Clutter:F2} with a sofa); no scan gives {fallback.Size.x:F1} x {fallback.Size.y:F1} x {fallback.Size.z:F1} m.";
            }
            finally
            {
                Room.Clear();
            }
        }
    }
}
