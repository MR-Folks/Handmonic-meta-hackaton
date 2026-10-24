using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// The world picker. Hold one palm up for a moment and the worlds rise above it as glowing orbs; poke an orb with
    /// the other hand's index finger to switch. A poke, not a pinch: with the palm facing the headset, Meta shows its
    /// menu button, and a pinch there would open Meta's menu.
    /// </summary>
    [DefaultExecutionOrder(160)]
    public class WorldWheel : MonoBehaviour
    {
        [SerializeField] Material glowMaterial;
        [Tooltip("Seconds the palm must stay up before the wheel opens.")]
        [SerializeField] float openHold = 0.35f;
        [Tooltip("Seconds the palm may turn away before the wheel closes, so it does not flicker.")]
        [SerializeField] float closeGrace = 0.25f;
        [SerializeField] float height = 0.09f;
        [SerializeField] float radius = 0.1f;
        [SerializeField] float orbSize = 0.05f;
        [SerializeField] float hoverDistance = 0.05f;
        [SerializeField] float pokeDistance = 0.022f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        Transform root;
        Transform[] orbs = new Transform[0];
        Renderer[] orbRenderers;
        TextMesh[] labels;
        float[] hover;
        readonly float[] palmUpTime = new float[2];
        MaterialPropertyBlock block;
        bool open, anchorLeft;
        float openAmount, lostTime, flash, cooldown;
        int flashIndex = -1;

        public bool IsOpen => open;

        void LateUpdate() => Step(HandRig.Instance, WorldManager.Instance, Camera.main, Time.deltaTime);

        /// <summary>
        /// One frame of the wheel. Public so the editor preview can drive it; forceOpenLeft opens it on that hand
        /// without waiting for the palm-up hold.
        /// </summary>
        public void Step(HandRig rig, WorldManager manager, Camera view, float dt, bool? forceOpenLeft = null)
        {
            if (rig == null || manager == null || manager.Worlds.Count == 0) return;
            Build(manager);
            cooldown -= dt;

            if (forceOpenLeft.HasValue)
            {
                open = true;
                anchorLeft = forceOpenLeft.Value;
            }
            else UpdateOpenState(rig, dt);

            openAmount = Mathf.MoveTowards(openAmount, open ? 1f : 0f, dt * (open ? 5f : 7f));
            bool visible = openAmount > 0.001f;
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
            if (!visible) return;

            var anchor = rig.Get(anchorLeft);
            var palm = anchor.PalmJoint != null ? anchor.PalmJoint.position : anchor.Palm.position;
            var up = anchor.PalmNormal.sqrMagnitude > 0.5f ? anchor.PalmNormal : Vector3.up;
            float rise = Mathf.SmoothStep(0f, 1f, openAmount);
            root.position = palm + up * (height * rise);
            if (view != null)
            {
                var away = root.position - view.transform.position;
                if (away.sqrMagnitude > 1e-6f) root.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            var pointerHand = rig.Get(!anchorLeft);
            bool pointing = pointerHand.IsTracked;
            var pointer = pointerHand.TipJoints[1] != null ? pointerHand.TipJoints[1].position : pointerHand.Tip(HandRig.Finger.Index);

            flash = Mathf.MoveTowards(flash, 0f, dt * 2.5f);
            int count = orbs.Length;
            for (int i = 0; i < count; i++)
            {
                var world = manager.Worlds[i];
                // Spread the orbs on an arc that widens with the number of worlds, up to 55 degrees each side.
                float spread = Mathf.Min(55f, 22f * (count - 1));
                float a = count == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (float)(count - 1)) * Mathf.Deg2Rad;
                var local = new Vector3(Mathf.Sin(a) * radius, (Mathf.Cos(a) - 1f) * radius * 0.6f, 0f) * rise;
                orbs[i].localPosition = local;

                float distance = pointing ? Vector3.Distance(pointer, orbs[i].position) : 99f;
                hover[i] = Mathf.MoveTowards(hover[i], distance < hoverDistance ? 1f : 0f, dt * 6f);
                if (open && pointing && distance < pokeDistance && cooldown <= 0f && !forceOpenLeft.HasValue)
                {
                    manager.Switch(i);
                    flashIndex = i;
                    flash = 1f;
                    cooldown = 0.6f;
                }

                bool current = i == manager.Current;
                float f = i == flashIndex ? flash : 0f;
                float size = orbSize * Mathf.SmoothStep(0f, 1f, openAmount) * (1f + hover[i] * 0.45f + (current ? 0.2f : 0f) + f * 0.8f);
                orbs[i].localScale = Vector3.one * size;

                block.Clear();
                block.SetColor(ColorId, Color.Lerp(world.Color, Color.white, f * 0.6f));
                block.SetFloat(IntensityId, (0.9f + hover[i] * 0.8f + (current ? 0.5f : 0f) + f * 2f) * openAmount);
                orbRenderers[i].SetPropertyBlock(block);

                var label = labels[i];
                label.text = world.DisplayName;
                label.transform.localPosition = local + Vector3.down * (orbSize * 0.85f);
                var c = current ? Color.Lerp(world.Color, Color.white, 0.5f) : new Color(1f, 1f, 1f, 0.75f);
                c.a *= openAmount * (0.55f + hover[i] * 0.45f);
                label.color = c;
            }
        }

        void UpdateOpenState(HandRig rig, float dt)
        {
            for (int side = 0; side < 2; side++)
            {
                var hand = rig.Get(side == 0);
                bool up = hand.IsTracked && hand.PalmUp && hand.Fist < 0.4f;
                palmUpTime[side] = up ? palmUpTime[side] + dt : 0f;
            }

            if (!open)
            {
                for (int side = 0; side < 2; side++)
                {
                    if (palmUpTime[side] < openHold) continue;
                    open = true;
                    anchorLeft = side == 0;
                    lostTime = 0f;
                    break;
                }
                return;
            }

            // Close once the anchor palm has been down (or lost) for a moment, or shortly after a world is picked.
            lostTime = palmUpTime[anchorLeft ? 0 : 1] > 0f ? 0f : lostTime + dt;
            if (lostTime > closeGrace || (flashIndex >= 0 && flash > 0f && flash < 0.35f)) Close();
        }

        void Close()
        {
            open = false;
            palmUpTime[0] = palmUpTime[1] = 0f;
            flashIndex = -1;
        }

        void Build(WorldManager manager)
        {
            if (root != null && orbs.Length == manager.Worlds.Count) return;
            if (root != null) DestroyNow(root.gameObject);

            block = new MaterialPropertyBlock();
            root = new GameObject("Wheel").transform;
            root.SetParent(transform, false);
            int count = manager.Worlds.Count;
            orbs = new Transform[count];
            orbRenderers = new Renderer[count];
            labels = new TextMesh[count];
            hover = new float[count];

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            for (int i = 0; i < count; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = $"Orb {i}";
                DebugShapes.RemoveCollider(quad);
                quad.transform.SetParent(root, false);
                orbRenderers[i] = quad.GetComponent<Renderer>();
                orbRenderers[i].sharedMaterial = glowMaterial;
                orbs[i] = quad.transform;

                var labelGo = new GameObject($"Label {i}");
                labelGo.transform.SetParent(root, false);
                var label = labelGo.AddComponent<TextMesh>();
                label.font = font;
                labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                label.fontSize = 48;
                label.characterSize = 0.0022f;
                label.anchor = TextAnchor.UpperCenter;
                label.alignment = TextAlignment.Center;
                labels[i] = label;
            }
            root.gameObject.SetActive(false);
        }

        static void DestroyNow(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
