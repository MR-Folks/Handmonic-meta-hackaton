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
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            r = go.GetComponent<Renderer>();
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

            palms[side].position = hand.Palm.position;
            palms[side].localScale = Vector3.one * palmSize * (1f + kick * 0.6f);
            Paint(palmRenderers[side], hand.PalmUp ? palmUp : idle);

            var tipColor = Color.Lerp(idle, pinch, hand.Pinch);
            for (int f = 0; f < 5; f++)
            {
                var t = tips[side * 5 + f];
                t.position = hand.Tips[f];
                t.localScale = Vector3.one * tipSize;
                Paint(tipRenderers[side * 5 + f], tipColor);
            }
        }

        void Paint(Renderer r, Color c)
        {
            block.SetColor(BaseColor, c);
            r.SetPropertyBlock(block);
        }
    }
}
