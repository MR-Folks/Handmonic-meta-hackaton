using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>The smallest a caption's capital letters may look from the eyes, in degrees.</summary>
        const float SmallestCaption = 1f;

        /// <summary>
        /// Every caption is big enough to read: each scene is set up as the player sees it (eyes 1.6 m up, standing;
        /// the deck seated), and every visible label marked as a caption (TypeLabel.IsCaption) has its capital letters
        /// measured as seen from the eyes. Fails if any is under a degree tall. Labels that aren't captions (credits,
        /// key names, small print) are listed when they are smaller, but don't fail. The scenes: a thumb tap's caption,
        /// the open menu, the cassette deck, Earth and water's element, Lightning's weather and Finger frame's view,
        /// under an open window and in front of you while it is closed.
        /// </summary>
        static bool CheckCaptionSizes()
        {
            var measured = new List<(string scene, string label, string text, float degrees, bool caption)>();
            void Measure(string scene, Camera eyes)
            {
                foreach (var label in TypeLabel.All)
                    if (label.Visible) measured.Add((scene, label.GameObject.name, label.Text, label.CapitalAngle(eyes.transform.position), label.IsCaption));
            }
            var errors = new List<string>();
            void Collect(string message, string stack, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception) errors.Add(message);
            }
            Application.logMessageReceived += Collect;
            try
            {
                // A thumb tap's caption.
                var eyes = StageEyes(out var rig);
                var gestures = new GameObject("MusicGestures").AddComponent<MusicGestures>();
                SetSerialized(gestures, "bubbleMaterial", ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble"));
                var music = new StandInMusic();
                var song = ScriptableObject.CreateInstance<SongAsset>();
                song.title = "Glass Room";
                music.Songs.Add(song);
                var hand = rig.Left;
                hand.IsTracked = true;
                hand.Palm = new Pose(new Vector3(-0.15f, 1.2f, 0.35f), Quaternion.identity);
                hand.Pinch = 0.3f;
                hand.Fist = 0.4f;
                for (int i = 0; i < 5; i++) hand.Tips[i] = hand.Palm.position + new Vector3(0f, 0.05f, 0.04f + i * 0.01f);
                for (int i = 0; i < 40; i++) gestures.Step(rig, music, false, eyes, 1f / 72f);
                gestures.Recognize(true, Microgesture.ThumbTap);
                for (int i = 0; i < 20; i++) gestures.Step(rig, music, false, eyes, 1f / 72f);
                Measure("Thumb tap", eyes);
                Object.DestroyImmediate(song);

                // The open menu, then the cassette deck (seated, as the deck checks have it).
                eyes = StageEyes(out rig);
                eyes.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 0f), Quaternion.Euler(20f, 0f, 0f));
                var menu = StageMenu(out var manager, out var bus);
                eyes.transform.rotation = Quaternion.identity;
                menu.Step(rig, manager, bus, eyes, 0f);
                menu.Open(eyes);
                for (int i = 0; i < 30; i++) menu.Step(rig, manager, bus, eyes, 1f / 30f);
                Measure("World menu", eyes);
                eyes.transform.rotation = Quaternion.Euler(20f, 0f, 0f);
                menu.OpenDeck(eyes);
                for (int i = 0; i < 60; i++) menu.Step(rig, manager, bus, eyes, 1f / 30f);
                Measure("Cassette deck", eyes);
                RunMessage(menu, "OnDisable");

                // Earth and water's element, Lightning's weather over a fist, Finger frame's view.
                MeasureWorld<EarthAndWater>("Earth and water", Measure, (world, at) => world.PreviewCaption());
                MeasureWorld<Lightning>("Lightning", Measure, (world, at) => world.PreviewCaption(at + new Vector3(0.15f, -0.35f, 0.4f)));
                MeasureWorld<FingerFrame>("Finger frame, window open", Measure, (world, at) =>
                {
                    world.PreviewWindow(at + new Vector3(0f, -0.14f, 0.46f), Quaternion.identity, new Vector2(0.26f, 0.17f), 2);
                    world.PreviewCaption();
                });
                MeasureWorld<FingerFrame>("Finger frame, window closed", Measure, (world, at) => world.PreviewCaption());
            }
            finally
            {
                Application.logMessageReceived -= Collect;
            }

            bool ok = errors.Count == 0;
            foreach (var e in errors.Take(3)) Debug.LogError($"[Check] Captions: setting up the scenes logged an error: {e}");
            var small = measured.Where(m => m.degrees < SmallestCaption).ToList();
            foreach (var m in small.Where(m => m.caption))
            {
                Debug.LogError($"[Check] Captions: \"{m.text}\" ({m.label}, {m.scene}) is {m.degrees:0.00}° tall from the eyes, under {SmallestCaption:0.0}°.");
                ok = false;
            }
            var captions = measured.Where(m => m.caption).ToList();
            var scenes = measured.Select(m => m.scene).Distinct().ToList();
            foreach (var scene in new[] { "Thumb tap", "World menu", "Cassette deck", "Earth and water", "Lightning", "Finger frame, window open", "Finger frame, window closed" })
                if (!captions.Any(m => m.scene == scene))
                {
                    Debug.LogError($"[Check] Captions: no caption showed in \"{scene}\", so its size went unmeasured.");
                    ok = false;
                }
            string Summary(IEnumerable<(string scene, string label, string text, float degrees, bool caption)> list) =>
                string.Join("; ", list.Select(m => $"{m.scene} \"{Shorten(m.text)}\" {m.degrees:0.00}°"));
            if (ok)
                Debug.Log($"[Check] Captions: every one of the {captions.Count} captions in {scenes.Count} scenes is at least {SmallestCaption:0.0}° tall from the eyes " +
                          $"(smallest: {Summary(captions.OrderBy(m => m.degrees).Take(3))}).");
            var print = small.Where(m => !m.caption).ToList();
            if (print.Count > 0) Debug.Log($"[Check] Captions: small print under {SmallestCaption:0.0}° (not captions): {Summary(print)}.");
            return ok;
        }

        static string Shorten(string text) => text.Length > 24 ? text.Substring(0, 22) + "…" : text;

        /// <summary>A new scene with the eyes 1.6 m up, looking ahead, and a hand rig with no hands yet.</summary>
        static Camera StageEyes(out HandRig rig)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Room.Clear();
            var eyes = new GameObject("Eyes").AddComponent<Camera>();
            eyes.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            rig = new GameObject("HandRig").AddComponent<HandRig>();
            return eyes;
        }

        /// <summary>
        /// The world menu with two worlds and the demo songs (so the cassette deck has cassettes), made
        /// WorldMenu.Instance as on the headset. Undo with RunMessage(menu, "OnDisable").
        /// </summary>
        static WorldMenu StageMenu(out WorldManager manager, out MusicBus bus)
        {
            DeckParts.Forget();
            var managerGo = new GameObject("Worlds");
            var worlds = new World[] { MakePreviewWorld<HeartbeatOrb>(managerGo.transform), MakePreviewWorld<Lightning>(managerGo.transform) };
            manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);

            var songs = AssetDatabase.FindAssets("t:SongAsset", new[] { "Assets/_Project/Songs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SongAsset>(AssetDatabase.GUIDToAssetPath(guid))).Where(s => s != null && s.clip != null).ToList();
            bus = new GameObject("Music").AddComponent<MusicBus>();
            var bso = new SerializedObject(bus);
            bso.FindProperty("startSong").objectReferenceValue = songs[0];
            var songList = bso.FindProperty("songs");
            songList.arraySize = songs.Count - 1;
            for (int i = 1; i < songs.Count; i++) songList.GetArrayElementAtIndex(i - 1).objectReferenceValue = songs[i];
            bso.ApplyModifiedPropertiesWithoutUndo();
            typeof(MusicBus).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bus, null);
            bus.RefreshSongs();

            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.FindProperty("panelMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.PanelMaterialPath, "MusicInYourHands/Panel");
            wso.ApplyModifiedPropertiesWithoutUndo();
            RunMessage(menu, "OnEnable");
            return menu;
        }

        /// <summary>One world with its scene settings, run for a moment, shown its caption by <paramref name="show"/>, run a little more, and measured.</summary>
        static void MeasureWorld<T>(string scene, System.Action<string, Camera> measure, System.Action<T, Vector3> show) where T : World
        {
            var eyes = StageEyes(out var rig);
            var world = new GameObject(typeof(T).Name).AddComponent<T>();
            ProjectSetup.AssignMaterials(world);
            world.Build();
            var frame = new MusicFrame { Bands = PreviewBands, SongTime = 10f };
            for (int i = 0; i < 10; i++) world.Step(rig, frame, eyes, 1f / 30f);
            show(world, eyes.transform.position);
            for (int i = 0; i < 12; i++) world.Step(rig, frame, eyes, 1f / 30f);
            measure(scene, eyes);
        }
    }
}
