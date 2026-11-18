using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MusicInYourHands.EditorTools
{
    public static partial class EditorChecks
    {
#if META_XR_CORE
        /// <summary>
        /// Screenshots of the cassette deck from a seated player's eyes (1.2 m up): 24a the deck just opened, a song in
        /// the radio and one of your own at the front, the right hand reaching for it; 24b a cassette pinched in the
        /// right hand over the radio's open door with the snap outline; 24c a song playing in the radio, and a close-up
        /// of the radio (24c-close). Made-up songs stand in for your own, beside the ones that come with the app.
        /// </summary>
        static void DeckShots()
        {
            if (!Wanted("24a-deck-open") && !Wanted("24b-deck-held") && !Wanted("24c-deck-playing")) return;
            DeckParts.Forget();
            // Songs, by their place in DeckPreviewSongs: 0-6 come with the app, 7-8 are your own.
            Shoot("24a-deck-open", 0, 7, 1.6f, (deck, rig, camera, hands, songs) =>
            {
                // The right hand comes in over the front cassette, about to pinch it, so the cassette lights up.
                var center = deck.CassetteCenter(songs[7]);
                var toward = Vector3.ProjectOnPlane(camera.transform.position - center, Vector3.up).normalized;
                var right = deck.Root.right;
                var forward = deck.Root.forward;
                var at = deck.PointOnCassette(songs[7], new Vector3(0.05f, 0.01f, 0f)) + right * 0.02f + toward * 0.01f;
                SyntheticPinch(rig, at, 0.04f);
                PoseHand(hands[1], at, (-right * 0.6f + Vector3.down * 0.55f + forward * 0.3f).normalized,
                    (-right * 0.55f + forward * 0.65f + Vector3.down * 0.35f).normalized, 0.55f, 0.035f);
                for (int i = 0; i < 4; i++) deck.Step(rig, null, camera, 1f / 30f);
            }, closeUp: deck => deck.CassetteCenter(DeckFront(deck)) + Vector3.up * 0.03f);
            Shoot("24b-deck-held", -1, 4, 1.6f, (deck, rig, camera, hands, songs) =>
            {
                // Pinch the front cassette by its top right corner, then carry it to the radio's door.
                var song = songs[4];
                var corner = new Vector3(0.026f, 0.029f, 0f);
                var grab = deck.PointOnCassette(song, corner);
                SyntheticPinch(rig, grab + Vector3.up * 0.04f, 0.06f);
                for (int i = 0; i < 6; i++) deck.Step(rig, null, camera, 1f / 30f);
                SyntheticPinch(rig, grab, 0.06f);
                deck.Step(rig, null, camera, 1f / 30f);
                for (int i = 0; i < 4; i++)
                {
                    SyntheticPinch(rig, grab, 0.008f);
                    deck.Step(rig, null, camera, 1f / 30f);
                }
                if (!deck.InHand(song)) Debug.LogWarning($"[Shots] The pinch didn't pick up {song.title}.");
                var right = deck.Root.right;
                var forward = deck.Root.forward;
                // Held a few centimetres out in front of the open door: close enough for the outline in the slot.
                var to = deck.SnapPosition + right * 0.026f + Vector3.up * 0.06f - forward * 0.055f;
                var lift = Vector3.Lerp(grab, to, 0.5f) + Vector3.up * 0.08f;
                for (int i = 1; i <= 30; i++)
                {
                    float t = i / 30f;
                    var p = Vector3.Lerp(Vector3.Lerp(grab, lift, t), Vector3.Lerp(lift, to, t), t);
                    SyntheticPinch(rig, p, 0.008f);
                    deck.Step(rig, null, camera, 1f / 30f);
                }
                for (int i = 0; i < 18; i++)
                {
                    SyntheticPinch(rig, to, 0.008f);
                    deck.Step(rig, null, camera, 1f / 30f);
                }
                var pinch = deck.PointOnCassette(song, corner);
                PoseHand(hands[1], pinch, (-right * 0.55f + Vector3.down * 0.75f + forward * 0.25f).normalized,
                    (-right * 0.55f + forward * 0.65f + Vector3.down * 0.35f).normalized, 0.7f, 0f);
                if (!deck.ShowsSnapPreview) Debug.LogWarning("[Shots] The held cassette didn't reach the snap preview.");
            }, closeUp: deck => deck.SlotPosition + Vector3.up * 0.03f);
            Shoot("24c-deck-playing", 6, 2, 2.2f, (deck, rig, camera, hands, songs) => hands[1].Root.transform.position += Vector3.down * 0.6f,
                closeUp: deck => deck.SlotPosition + Vector3.up * 0.02f);
            Room.Clear();
        }

        static SongAsset DeckFront(CassetteDeck deck) => deck.FrontSong;

        /// <summary>The songs that come with the app and two made-up songs of your own.</summary>
        static List<SongAsset> DeckPreviewSongs(out List<SongAsset> demo, out List<SongAsset> own)
        {
            demo = AssetDatabase.FindAssets("t:SongAsset", new[] { "Assets/_Project/Songs" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SongAsset>(AssetDatabase.GUIDToAssetPath(guid))).Where(s => s != null).OrderBy(s => s.name).ToList();
            SongAsset Own(string title, string artist, bool lyrics)
            {
                var song = ScriptableObject.CreateInstance<SongAsset>();
                song.title = title;
                song.artist = artist;
                song.audioPath = title + ".mp3";
                song.lyricsPath = lyrics ? title + ".lrc" : null;
                return song;
            }
            own = new List<SongAsset> { Own("Sunday Morning", "The Open Windows", true), Own("Harbour Lights", "", false) };
            // Made-up songs live only in this code, so keep them through scene changes.
            foreach (var song in own) song.hideFlags = HideFlags.DontSave;
            return demo.Concat(own).ToList();
        }

        /// <summary>
        /// One deck shot: a stand-in room, a seated player, the deck opened with the made-up songs, <paramref name="playing"/>
        /// in the radio and <paramref name="front"/> at the front, a few seconds of a beat, then <paramref name="pose"/>.
        /// </summary>
        static void Shoot(string name, int playingIndex, int frontIndex, float seconds,
            System.Action<CassetteDeck, HandRig, Camera, List<PreviewHand>, List<SongAsset>> pose, System.Func<CassetteDeck, Vector3> closeUp = null)
        {
            if (!Wanted(name)) return;
            BodyRig.Clear();
            var camera = StageRoom(34f);
            var songs = DeckPreviewSongs(out _, out _);
            var playing = playingIndex >= 0 ? songs[playingIndex] : null;
            var front = songs[frontIndex];
            camera.transform.position = new Vector3(0f, 1.2f, 0f);
            var rig = new GameObject("HandRig").AddComponent<HandRig>();
            var hands = PlaceHands(camera, HandPose.BacksToFace, rig, side => side == "L" ? new Vector3(-0.12f, -0.8f, -0.05f) : new Vector3(0.05f, -0.35f, 0.05f));
            // Only the synthetic right hand drives the deck; the left rests out of the way.
            rig.Left.IsTracked = false;
            rig.Right.IsTracked = false;
            HandSkin.Theme = HandTheme.Veins;

            var managerGo = new GameObject("Worlds");
            var worlds = new World[] { MakePreviewWorld<HeartbeatOrb>(managerGo.transform) };
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
            wso.FindProperty("panelMaterial").objectReferenceValue = ProjectSetup.EnsureMaterial(ProjectSetup.PanelMaterialPath, "MusicInYourHands/Panel");
            wso.ApplyModifiedPropertiesWithoutUndo();
            menu.Step(rig, manager, null, camera, 0f);
            var deck = menu.Deck;
            var clock = new SongClock { Time = ChorusStart - seconds };
            var frame = clock.Advance(0f);
            deck.Preview(songs, playing, frame.Bands, frame.Kick);
            deck.PreviewFront(front);
            menu.OpenDeck(camera);
            const float dt = 1f / 30f;
            while (clock.Time < ChorusStart + 0.09f)
            {
                frame = clock.Advance(dt);
                deck.Preview(songs, playing, frame.Bands, frame.Kick);
                menu.Step(rig, manager, null, camera, dt);
            }
            pose(deck, rig, camera, hands, songs);
            deck.Preview(songs, playing, frame.Bands, frame.Kick);
            deck.Step(rig, null, camera, 0.001f);
            foreach (var hand in hands)
            {
                hand.Root.GetComponent<HandSkin>().Apply();
                SetBeat(hand.Skin, clock.BeatAge, clock.Kick);
            }
            File.WriteAllBytes($"{ShotFolder}/{name}.png", Render(camera, 1600, 900).EncodeToPNG());
            if (closeUp != null)
            {
                camera.fieldOfView = 30f;
                camera.transform.LookAt(closeUp(deck));
                File.WriteAllBytes($"{ShotFolder}/{name}-close.png", Render(camera, 1600, 900).EncodeToPNG());
            }
            Shader.SetGlobalVector("_CassetteDeckClip", Vector4.zero);
        }

        /// <summary>Feeds HandRig a right hand whose thumb and index tips are <paramref name="gap"/> apart round <paramref name="at"/>.</summary>
        static void SyntheticPinch(HandRig rig, Vector3 at, float gap)
        {
            var index = at + Vector3.up * (gap * 0.5f);
            var tips = new[]
            {
                at - Vector3.up * (gap * 0.5f), index, index + new Vector3(-0.02f, -0.05f, -0.06f),
                index + new Vector3(-0.03f, -0.055f, -0.065f), index + new Vector3(-0.04f, -0.06f, -0.07f)
            };
            rig.SetJoints(false, null, new Transform[5]);
            rig.Push(false, true, new Pose(index + new Vector3(-0.02f, -0.04f, -0.09f), Quaternion.identity), Vector3.left, tips);
        }

        /// <summary>
        /// Poses a preview hand: turned so its fingers point along <paramref name="fingers"/> with its palm facing
        /// <paramref name="palm"/>, the fingers curled by <paramref name="curl"/> (0 open, 1 tucked), the thumb brought to
        /// <paramref name="apart"/> from the index fingertip, and moved so the point between them is at <paramref name="at"/>.
        /// </summary>
        static void PoseHand(PreviewHand hand, Vector3 at, Vector3 fingers, Vector3 palm, float curl, float apart)
        {
            var bones = hand.Root.GetComponentsInChildren<Transform>();
            Transform Bone(string n) => bones.First(t => t.name == n);
            var wrist = Bone("XRHand_Wrist");
            var nowFingers = (Bone("XRHand_MiddleProximal").position - wrist.position).normalized;
            var nowPalm = PalmNormal(Bone, hand.Left);
            var turn = Quaternion.LookRotation(fingers, palm) * Quaternion.Inverse(Quaternion.LookRotation(nowFingers, Vector3.ProjectOnPlane(nowPalm, nowFingers).normalized));
            hand.Root.transform.rotation = turn * hand.Root.transform.rotation;

            float Gap() => Vector3.Distance(Bone("XRHand_ThumbTip").position, Bone("XRHand_IndexTip").position) * 100f;
            float openGap = Gap();
            // Curl each finger towards the palm, joint by joint; the index less, so it meets the thumb.
            var palmNormal = PalmNormal(Bone, hand.Left);
            foreach (var (finger, amount) in new[] { ("Index", 0.8f), ("Middle", 1.15f), ("Ring", 1.25f), ("Little", 1.35f) })
            {
                var tip = Bone($"XRHand_{finger}Tip");
                foreach (var (joint, angle) in new[] { ("Proximal", 38f), ("Intermediate", 48f), ("Distal", 26f) })
                {
                    var bone = Bone($"XRHand_{finger}{joint}");
                    var child = bone.childCount > 0 ? bone.GetChild(0) : tip;
                    var axis = Vector3.Cross(child.position - bone.position, palmNormal).normalized;
                    var before = tip.position;
                    float a = angle * amount * curl;
                    bone.Rotate(axis, a, Space.World);
                    if (Vector3.Dot(tip.position - before, palmNormal) < 0f) bone.Rotate(axis, -2f * a, Space.World);
                }
            }

            float curledGap = Gap();
            // Bring the thumb tip and the index fingertip together by small turns of their joints.
            var thumbTip = Bone("XRHand_ThumbTip");
            var indexTip = Bone("XRHand_IndexTip");
            var thumbJoints = new[] { Bone("XRHand_ThumbMetacarpal"), Bone("XRHand_ThumbProximal"), Bone("XRHand_ThumbDistal") };
            var indexJoints = new[] { Bone("XRHand_IndexProximal"), Bone("XRHand_IndexIntermediate") };
            var axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            float Miss() => Mathf.Abs(Vector3.Distance(thumbTip.position, indexTip.position) - Mathf.Max(apart, 0.009f));
            void Try(Transform joint, Vector3 axis, float step)
            {
                float was = Miss();
                joint.Rotate(axis, step, Space.World);
                if (Miss() >= was) joint.Rotate(axis, -step, Space.World);
            }
            for (int pass = 0; pass < 150 && Miss() > 0.002f; pass++)
            {
                foreach (var joint in thumbJoints)
                    foreach (var axis in axes)
                    {
                        Try(joint, axis, 3f);
                        Try(joint, axis, -3f);
                    }
                // The index only bends, about the axis across it.
                foreach (var joint in indexJoints)
                {
                    var along = (joint.GetChild(0).position - joint.position).normalized;
                    var across = Vector3.Cross(along, palmNormal).normalized;
                    Try(joint, across, 2f);
                    Try(joint, across, -2f);
                }
            }

            var pinch = (thumbTip.position + indexTip.position) * 0.5f;
            hand.Root.transform.position += at - pinch;
            hand.Root.GetComponent<HandSkin>().Apply();
            Debug.Log($"[Shots] Posed the {(hand.Left ? "left" : "right")} hand: thumb to index {openGap:0.0} cm open, {curledGap:0.0} cm curled, {Gap():0.0} cm posed; pinch at {at}, mesh at {hand.Skin.bounds.center}.");
        }
#endif
    }
}
