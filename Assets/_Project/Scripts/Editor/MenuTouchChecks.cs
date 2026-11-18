using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
        /// <summary>
        /// The menu answers a fingertip as well as a pinch. A fingertip reaching a bubble from the front presses it as it
        /// arrives, once however long it stays; one already through the menu when it opens presses nothing until it
        /// comes back out; a hand farther away still points and pinches; and a touch presses the song page's buttons.
        /// </summary>
        static bool CheckMenuTouch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Eyes").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1.6f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var managerGo = new GameObject("Worlds");
            var worlds = new World[]
            {
                MakePreviewWorld<HeartbeatOrb>(managerGo.transform),
                MakePreviewWorld<Lightning>(managerGo.transform),
                MakePreviewWorld<BubbleNotes>(managerGo.transform),
                MakePreviewWorld<SoundPool>(managerGo.transform)
            };
            var manager = managerGo.AddComponent<WorldManager>();
            var mso = new SerializedObject(manager);
            var list = mso.FindProperty("worlds");
            list.arraySize = worlds.Length;
            for (int i = 0; i < worlds.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = worlds[i];
            mso.ApplyModifiedPropertiesWithoutUndo();
            manager.Switch(0);

            var menu = new GameObject("WorldMenu").AddComponent<WorldMenu>();
            var wso = new SerializedObject(menu);
            wso.FindProperty("bubbleMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.BubbleMaterialPath, "MusicInYourHands/Bubble");
            wso.FindProperty("glowMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.GlowMaterialPath, "MusicInYourHands/Glow");
            wso.FindProperty("lineMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.LineMaterialPath, "MusicInYourHands/Line");
            wso.ApplyModifiedPropertiesWithoutUndo();

            const float Frame = 1f / 72f;
            bool lyricsWere = LyricsOption.On;
            var fingertip = camera.transform.position + new Vector3(0.15f, -0.4f, 0.15f);

            // A pointing right hand: the thumb tucked away from the index fingertip, or touching it in a pinch.
            void Hand(Vector3 index, bool pinch = false)
            {
                fingertip = index;
                var tips = new[]
                {
                    index + (pinch ? new Vector3(0f, -0.008f, 0f) : new Vector3(-0.03f, -0.045f, -0.03f)),
                    index,
                    index + new Vector3(-0.02f, -0.05f, -0.06f),
                    index + new Vector3(-0.03f, -0.055f, -0.065f),
                    index + new Vector3(-0.04f, -0.06f, -0.07f)
                };
                rig.Push(false, true, new Pose(index + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
            }
            void Step(bool pinch = false)
            {
                Hand(fingertip, pinch);
                menu.Step(rig, manager, null, camera, Frame);
            }
            void Run(float seconds)
            {
                for (float t = 0f; t < seconds; t += Frame) Step();
            }
            // Moves the fingertip in a straight line, one menu frame at a time.
            void Move(Vector3 to, int frames)
            {
                var from = fingertip;
                for (int i = 1; i <= frames; i++)
                {
                    Hand(Vector3.Lerp(from, to, i / (float)frames));
                    menu.Step(rig, manager, null, camera, Frame);
                }
            }
            void OpenMenu()
            {
                menu.Open(camera);
                Run(1f);
            }

            Step();
            OpenMenu();
            var root = menu.transform.Find("Menu");
            Vector3 Bubble(string name) => root.Find(name).position;
            // From the viewer into the menu.
            var toward = root.forward;

            // A fingertip reaching a bubble presses it as it arrives, not before.
            var orb = Bubble("Bubble 2");
            Move(orb - toward * 0.12f, 6);
            Move(orb - toward * 0.015f, 10);
            bool early = manager.Current != 0;
            Move(orb + toward * 0.02f, 4);
            bool reached = manager.Current == 2;
            Move(orb - toward * 0.1f, 6);
            Run(0.6f);

            // A fingertip already through the menu when it opens presses nothing until it comes back out.
            var pool = Bubble("Bubble 3");
            Hand(pool + toward * 0.03f);
            OpenMenu();
            Move(pool + toward * 0.01f, 6);
            bool ignored = manager.Current == 2;
            Move(pool - toward * 0.08f, 8);
            Move(pool + toward * 0.01f, 8);
            bool cameBack = manager.Current == 3;
            Move(pool - toward * 0.1f, 6);
            Run(0.6f);

            // A fingertip held through a bubble presses it once; tapping again presses it again.
            OpenMenu();
            var lyrics = Bubble("Bubble Lyrics");
            bool before = LyricsOption.On;
            Move(lyrics - toward * 0.1f, 8);
            Move(lyrics + toward * 0.02f, 8);
            Run(1.2f);
            bool once = LyricsOption.On != before;
            Move(lyrics - toward * 0.06f, 6);
            Run(0.2f);
            Move(lyrics + toward * 0.01f, 6);
            bool twice = LyricsOption.On == before;
            Move(lyrics - toward * 0.1f, 6);
            Run(0.7f);

            // The Less flashing bubble at the end of the song row turns the option on (and is lit while it is on).
            var calm = Bubble("Bubble Less Flashing");
            bool calmWas = ComfortOptions.LessFlashing;
            ComfortOptions.LessFlashing = false;
            Move(calm - toward * 0.1f, 8);
            Move(calm + toward * 0.02f, 8);
            Run(1.2f);
            bool calmed = ComfortOptions.LessFlashing;
            Move(calm - toward * 0.1f, 6);
            ComfortOptions.LessFlashing = calmWas;

            // A hand farther away still points its ray and pinches: put the point between thumb and index on the line
            // from the shoulder through the bubble.
            var heart = Bubble("Bubble 1");
            var ahead = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            var side = Vector3.Cross(Vector3.up, ahead).normalized;
            var shoulder = camera.transform.position + Vector3.down * 0.17f + side * 0.17f;
            var pinchAt = shoulder + (heart - shoulder) * 0.4f;
            Move(pinchAt + new Vector3(0.015f, 0.0225f, 0.015f), 10);
            // Aim for a moment, and let the pause after the last press run out.
            Run(0.7f);
            for (int i = 0; i < 6; i++) Step(pinch: true);
            bool pinched = manager.Current == 1;
            Run(0.6f);

            // A pinch that closes the way fingers do, the index coming down to the thumb while the palm stays put,
            // leaves the cursor where it was, so the bubble aimed at is the one chosen.
            OpenMenu();
            var bubbles = Bubble("Bubble 2");
            var cursor = root.Find("Cursor R");
            var open = shoulder + (bubbles - shoulder) * 0.4f;
            var palmAt = open + new Vector3(-0.02f, -0.04f, -0.09f);
            Vector3 thumbOpen = open + new Vector3(0f, -0.035f, 0f), indexOpen = open + new Vector3(0f, 0.035f, 0f);
            Vector3 thumbShut = open + new Vector3(0f, -0.03f, 0f), indexShut = open + new Vector3(0f, -0.02f, 0f);
            void Fingers(float closed)
            {
                var index = Vector3.Lerp(indexOpen, indexShut, closed);
                var tips = new[]
                {
                    Vector3.Lerp(thumbOpen, thumbShut, closed),
                    index,
                    indexOpen + new Vector3(-0.02f, -0.05f, -0.06f),
                    indexOpen + new Vector3(-0.03f, -0.055f, -0.065f),
                    indexOpen + new Vector3(-0.04f, -0.06f, -0.07f)
                };
                rig.Push(false, true, new Pose(palmAt, Quaternion.identity), Vector3.left, tips);
                menu.Step(rig, manager, null, camera, Frame);
            }
            // Hold the open hand long enough for its aim to settle on this hand shape.
            for (float t = 0f; t < 3f; t += Frame) Fingers(0f);
            var aimedAt = cursor.position;
            float drift = 0f;
            const int Closing = 15;
            for (int i = 1; i <= Closing + 4; i++)
            {
                Fingers(Mathf.Min(1f, i / (float)Closing));
                if (cursor.gameObject.activeSelf) drift = Mathf.Max(drift, Vector3.Distance(cursor.position, aimedAt));
            }
            bool steady = drift < 0.006f && manager.Current == 2;
            Run(0.6f);

            // The song bubble opens the cassette deck and the menu steps aside. A touch presses the song page's buttons:
            // open the page (as the deck's song list button does), then touch its Add button, which shows a message here
            // since the file picker only opens on the headset.
            OpenMenu();
            var library = Bubble("Bubble Library");
            Move(library - toward * 0.1f, 8);
            Move(library + toward * 0.01f, 6);
            Move(library - toward * 0.08f, 8);
            Run(0.7f);
            bool deckOpened = menu.Deck != null && menu.Deck.IsOpen && !root.gameObject.activeSelf;
            menu.ShowSongList(camera);
            Run(0.7f);
            var page = root.Find("Song Library");
            bool pageShown = page.gameObject.activeSelf;
            var add = page.Find("Add").position;
            bool messageBefore = page.Find("Message").gameObject.activeSelf;
            Move(add - toward * 0.06f, 6);
            Move(add + toward * 0.01f, 6);
            Run(0.2f);
            bool added = !messageBefore && page.Find("Message").gameObject.activeSelf;

            menu.Close();
            LyricsOption.On = lyricsWere;
            bool ok = !early && reached && ignored && cameBack && once && twice && calmed && pinched && steady && deckOpened && pageShown && added;
            if (ok) Debug.Log($"[Check] The menu answers touch and pinch: a fingertip presses a bubble as it reaches it, once per tap and never from behind, the Less flashing bubble switches the option, a far hand still points and pinches, a closing pinch moves its cursor only {drift * 1000f:0.0} mm, the song bubble opens the cassette deck, and a touch presses the song page's buttons.");
            else Debug.LogError($"[Check] Menu touch: pressed early {early}, pressed on arrival {reached}, ignored from behind {ignored}, " +
                                $"pressed after coming back out {cameBack}, held press counted once {once}, second tap {twice}, less flashing switched {calmed}, " +
                                $"pinch from afar {pinched}, cursor steady through a closing pinch {steady} ({drift * 1000f:0.0} mm, chose world {manager.Current}), " +
                                $"song bubble opened the deck {deckOpened}, song page opened {pageShown}, page's Add button touched {added}.");
            return ok;
        }
    }
}
