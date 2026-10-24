using System.Text;
using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// A small in-headset panel that shows what MusicBus hears: six band bars, a beat light and the
    /// current lyric line with the sung word highlighted. Use it to check sync on the device.
    /// </summary>
    public class MusicDebugView : MonoBehaviour
    {
        [SerializeField] float barWidth = 0.04f;
        [SerializeField] float maxBarHeight = 0.25f;
        [SerializeField] Color barColor = new Color(0.37f, 0.83f, 1f);
        [SerializeField] Color wordColor = new Color(1f, 0.37f, 0.68f);
        [Tooltip("A URP Unlit material. Primitives keep Unity's built-in material otherwise, which URP draws magenta.")]
        [SerializeField] Material material;

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        Transform[] bars;
        Transform beatLight;
        TextMesh lyricText;
        MaterialPropertyBlock block;
        readonly StringBuilder sb = new StringBuilder();

        void Start() => Build();

        /// <summary>Creates the bars, beat light and lyric text. Also called by the editor preview check.</summary>
        public void Build()
        {
            block = new MaterialPropertyBlock();
            bars = new Transform[MusicBus.BandCount];
            for (int i = 0; i < bars.Length; i++)
            {
                bars[i] = MakePrimitive(PrimitiveType.Cube, $"Band {i}", barColor).transform;
                bars[i].localPosition = new Vector3((i - (bars.Length - 1) * 0.5f) * barWidth * 1.5f, 0f, 0f);
            }

            beatLight = MakePrimitive(PrimitiveType.Sphere, "Beat", wordColor).transform;
            beatLight.localPosition = new Vector3(0f, maxBarHeight + 0.06f, 0f);

            var textGo = new GameObject("Lyric");
            textGo.transform.SetParent(transform, false);
            textGo.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            lyricText = textGo.AddComponent<TextMesh>();
            lyricText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textGo.GetComponent<MeshRenderer>().sharedMaterial = lyricText.font.material;
            lyricText.fontSize = 64;
            lyricText.characterSize = 0.004f;
            lyricText.anchor = TextAnchor.UpperCenter;
            lyricText.alignment = TextAlignment.Center;
            lyricText.richText = true;
        }

        GameObject MakePrimitive(PrimitiveType type, string objectName, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = objectName;
            DebugShapes.RemoveCollider(go);
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * barWidth;
            var r = go.GetComponent<Renderer>();
            if (material != null) r.sharedMaterial = material;
            block.SetColor(BaseColor, color);
            r.SetPropertyBlock(block);
            return go;
        }

        void Update()
        {
            var bus = MusicBus.Instance;
            if (bus == null || bars == null) return;

            for (int i = 0; i < bars.Length; i++)
            {
                float h = Mathf.Max(0.005f, bus.Bands[i] * maxBarHeight);
                bars[i].localScale = new Vector3(barWidth, h, barWidth);
                var p = bars[i].localPosition;
                bars[i].localPosition = new Vector3(p.x, h * 0.5f, 0f);
            }

            beatLight.localScale = Vector3.one * barWidth * (1f + bus.Kick);

            sb.Clear();
            var words = bus.CurrentLineWords;
            string hex = ColorUtility.ToHtmlStringRGB(wordColor);
            for (int k = 0; k < words.Length; k++)
            {
                if (k > 0) sb.Append(' ');
                if (k == bus.Lyric.Word) sb.Append("<color=#").Append(hex).Append('>').Append(words[k].Text).Append("</color>");
                else if (k < bus.Lyric.Word) sb.Append(words[k].Text);
                else sb.Append("<color=#ffffff40>").Append(words[k].Text).Append("</color>");
            }
            sb.Append('\n').Append("<size=40>").Append(bus.SongTime.ToString("0.0")).Append(" s");
            if (bus.IsListening) sb.Append("  listening, mic ").Append(bus.InputLevel.ToString("0.000"));
            sb.Append("</size>");
            lyricText.text = sb.ToString();
        }
    }
}
