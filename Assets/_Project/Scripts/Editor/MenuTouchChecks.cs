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
        /// The caption says what a world bubble will do and then what it did; with two hands it speaks of the bubble a
        /// fingertip touches; and after the menu reopens it doesn't repeat an old choice. The menu stays open while
        /// worlds are added or turned off, and closes after a swap that leaves a single world on.
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
                MakePreviewWorld<NoteRain>(managerGo.transform),
                MakePreviewWorld<FireflyChoir>(managerGo.transform),
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

            // A fingertip reaching a bubble presses it as it arrives, not before. Close to it, the caption says what
            // choosing it will do, and once pressed, what it did; adding a world leaves the menu open.
            var orb = Bubble("Bubble 2");
            Move(orb - toward * 0.12f, 6);
            Move(orb - toward * 0.015f, 10);
            bool early = manager.Current != 0;
            string captionBefore = menu.CaptionText;
            Move(orb + toward * 0.02f, 4);
            bool reached = manager.Current == 2;
            string captionAfter = menu.CaptionText;
            Move(orb - toward * 0.1f, 6);
            Run(0.6f);
            bool stayedOpen = menu.IsOpen && manager.Active.Count == 2;
            bool captions = captionBefore == "Add Firefly choir" && captionAfter == "Added Firefly choir";

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

            // A touch presses the song page's buttons: open the page from its bubble, then touch the Audius tab, which
            // shows "Try again" here since there is no MusicBus to ask Audius.
            OpenMenu();
            var library = Bubble("Bubble Library");
            Move(library - toward * 0.1f, 8);
            Move(library + toward * 0.01f, 6);
            Move(library - toward * 0.08f, 8);
            Run(0.7f);
            var page = root.Find("Song Library");
            bool pageShown = page.gameObject.activeSelf;
            var tab = page.Find("Audius Tab").position;
            bool retryBefore = page.Find("Try Again").gameObject.activeSelf;
            Move(tab - toward * 0.06f, 6);
            Move(tab + toward * 0.01f, 6);
            Run(0.2f);
            bool tabbed = !retryBefore && page.Find("Try Again").gameObject.activeSelf;

            // Two hands: the left points at Sound pool from afar while the right fingertip is at Firefly choir. The caption
            // speaks of the bubble the fingertip touches (Note rain and Heartbeat orb are on now).
            menu.Close();
            OpenMenu();
            var leftSide = Vector3.Cross(Vector3.up, ahead).normalized;
            var leftShoulder = camera.transform.position + Vector3.down * 0.17f - leftSide * 0.17f;
            var leftPinch = leftShoulder + (Bubble("Bubble 3") - leftShoulder) * 0.4f;
            var leftIndex = leftPinch + new Vector3(0.015f, 0.0225f, 0.015f);
            var leftTips = new[]
            {
                leftIndex + new Vector3(-0.03f, -0.045f, -0.03f), leftIndex, leftIndex + new Vector3(-0.02f, -0.05f, -0.06f),
                leftIndex + new Vector3(-0.03f, -0.055f, -0.065f), leftIndex + new Vector3(-0.04f, -0.06f, -0.07f)
            };
            rig.Push(true, true, new Pose(leftIndex + new Vector3(0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.right, leftTips);
            Move(orb - toward * 0.1f, 8);
            Move(orb - toward * 0.015f, 8);
            string twoHands = menu.CaptionText;
            bool focused = twoHands == "Swap Note rain for Firefly choir";
            rig.Push(true, false, default, Vector3.zero, leftTips);
            Move(orb - toward * 0.1f, 6);

            // Turning a world off leaves the menu open; a swap that leaves a single world on closes it a moment later.
            var heartBubble = Bubble("Bubble 0");
            Move(heartBubble - toward * 0.1f, 8);
            Move(heartBubble + toward * 0.01f, 6);
            Move(heartBubble - toward * 0.1f, 6);
            Run(0.6f);
            bool openAfterOff = menu.IsOpen && manager.Active.Count == 1 && manager.Current == 1;
            Move(orb - toward * 0.1f, 8);
            Move(orb + toward * 0.01f, 6);
            Move(orb - toward * 0.1f, 6);
            Run(0.6f);
            bool closedAfterSwap = !menu.IsOpen && manager.Active.Count == 1 && manager.Current == 2;

            // Reopened with a fingertip on the play bubble, the caption names it rather than repeating the last choice.
            Hand(Bubble("Bubble Now Playing") - toward * 0.03f);
            OpenMenu();
            string reopened = menu.CaptionText;
            bool fresh = reopened == "Music";

            menu.Close();
            LyricsOption.On = lyricsWere;
            bool ok = !early && reached && ignored && cameBack && once && twice && pinched && pageShown && tabbed
                      && captions && stayedOpen && focused && openAfterOff && closedAfterSwap && fresh;
            if (ok) Debug.Log("[Check] The menu answers touch and pinch: a fingertip presses a bubble as it reaches it, once per tap and never from behind, a far hand still points and pinches, and a touch presses the song page's buttons. " +
                              "The caption says what a world bubble will do and did, speaks of the touched bubble with two hands and starts afresh when reopened; the menu stays open while adding and turning off, and closes after a swap to one world.");
            else Debug.LogError($"[Check] Menu touch: pressed early {early}, pressed on arrival {reached}, ignored from behind {ignored}, " +
                                $"pressed after coming back out {cameBack}, held press counted once {once}, second tap {twice}, " +
                                $"pinch from afar {pinched}, song page opened {pageShown}, page tab touched {tabbed}, " +
                                $"captions {captions} (\"{captionBefore}\", then \"{captionAfter}\"), open after adding {stayedOpen}, two hands {focused} (\"{twoHands}\"), " +
                                $"open after turning off {openAfterOff}, closed after a swap to one world {closedAfterSwap}, reopened caption {fresh} (\"{reopened}\").");
            return ok;
        }
    }
}
