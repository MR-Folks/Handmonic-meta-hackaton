using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Draws small spheres on each fingertip and palm so you can check HandRig on the device.
    /// Tips turn pink while pinching, palms grow with the beat and turn yellow when facing up.
    /// </summary>
    public class HandDebugView : MonoBehaviour
    {
        [SerializeField] float tipSize = 0.012f;
        [SerializeField] float palmSize = 0.03f;
        [SerializeField] Color idle = new Color(0.56f, 0.53f, 0.85f);
        [SerializeField] Color pinch = new Color(1f, 0.37f, 0.68f);
        [SerializeField] Color palmUp = new Color(1f, 0.83f, 0.42f);
        [Tooltip("A URP Unlit material. Primitives keep Unity's built-in material otherwise, which URP draws magenta.")]
        [SerializeField] Material material;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        readonly Transform[] tips = new Transform[10];
        readonly Renderer[] tipRenderers = new Renderer[10];
        readonly Transform[] palms = new Transform[2];
        readonly Renderer[] palmRenderers = new Renderer[2];
        MaterialPropertyBlock block;

        void Start()
        {
            block = new MaterialPropertyBlock();
            for (int i = 0; i < tips.Length; i++) tips[i] = MakeSphere($"Tip {i}", out tipRenderers[i]);
            for (int i = 0; i < palms.Length; i++) palms[i] = MakeSphere($"Palm {i}", out palmRenderers[i]);
        }

        Transform MakeSphere(string objectName, out Renderer r)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = objectName;
            DebugShapes.RemoveCollider(go);
            go.transform.SetParent(transform, false);
            r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            return go.transform;
        }

        void Update()
        {
            var rig = HandRig.Instance;
            if (rig == null) return;
            float kick = MusicBus.Instance != null ? MusicBus.Instance.Kick : 0f;
            Show(rig.Left, 0, kick);
            Show(rig.Right, 1, kick);
        }

        void Show(HandRig.Hand hand, int side, float kick)
        {
            palms[side].gameObject.SetActive(hand.IsTracked);
            for (int f = 0; f < 5; f++) tips[side * 5 + f].gameObject.SetActive(hand.IsTracked);
            if (!hand.IsTracked) return;

            Place(palms[side], hand.PalmJoint, hand.Palm.position, palmSize * (1f + kick * 0.6f));
            Paint(palmRenderers[side], hand.PalmUp ? palmUp : idle);

            var tipColor = Color.Lerp(idle, pinch, hand.Pinch);
            for (int f = 0; f < 5; f++)
            {
                Place(tips[side * 5 + f], hand.TipJoints[f], hand.Tips[f], tipSize);
                Paint(tipRenderers[side * 5 + f], tipColor);
            }
        }

        /// <summary>
        /// Attaches the marker to the joint when there is one, so it is drawn exactly where the hand mesh is.
        /// Otherwise places it at the sampled position.
        /// </summary>
        void Place(Transform marker, Transform joint, Vector3 position, float size)
        {
            if (joint != null)
            {
                if (marker.parent != joint)
                {
                    marker.SetParent(joint, false);
                    marker.localPosition = Vector3.zero;
                }
                float parentScale = Mathf.Max(joint.lossyScale.x, 1e-4f);
                marker.localScale = Vector3.one * (size / parentScale);
                return;
            }

            if (marker.parent != transform) marker.SetParent(transform, false);
            marker.position = position;
            marker.localScale = Vector3.one * size;
        }

        void Paint(Renderer r, Color c)
        {
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }
    }
}
