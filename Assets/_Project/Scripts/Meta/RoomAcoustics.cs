#if META_XR_AUDIO
using UnityEngine;
using Preset = MetaXRAudioRoomAcousticProperties.MaterialPreset;

namespace MusicInYourHands.Meta
{
    /// <summary>
    /// The reverb's room is your room. Meta XR Audio's Shoebox Room model gives every spatialized sound early
    /// reflections off six walls and a short reverb tail; this sizes that box from the room scan (the walls' extent,
    /// floor to ceiling) each time the scan loads or changes, and makes it more cluttered, so softer and more diffuse,
    /// the more of the floor the scanned furniture covers. Without a scan it is an ordinary living room. The box stays
    /// centred on your head: Meta's shoebox can only lie along the world's axes, so pinned to the real walls it would
    /// be off in a room at an angle; its long side follows the room's. Only the small sounds go through it
    /// (SpatialAudio); the song stays dry.
    /// It sets the room through Meta's native interface, as MetaXRAudioRoomAcousticProperties does, but only when the
    /// room changes: that component sends it every frame and allocates while doing so.
    /// </summary>
    public class RoomAcoustics : MonoBehaviour
    {
        [Tooltip("The room without a scan, in metres: width (x), height, depth (z). Keep the sides unequal; a cube rings.")]
        [SerializeField] Vector3 standInRoom = new Vector3(4.2f, 2.6f, 5.4f);
        [Tooltip("Clutter without a scan: 0 an empty room, 1 a very full one. Clutter diffuses and shortens the reverb.")]
        [SerializeField, Range(0f, 1f)] float standInClutter = 0.5f;
        [Tooltip("Clutter of a scanned room with no furniture, and of one whose furniture covers 40 % of the floor or more.")]
        [SerializeField] Vector2 clutterRange = new Vector2(0.35f, 0.8f);
        [Tooltip("The walls: gypsum board reflects the mids and highs and soaks up the boomy lows.")]
        [SerializeField] Preset walls = Preset.GypsumBoard;
        [Tooltip("The ceiling. Acoustic tile stands in for the soft things a shoebox can't model, keeping the tail short.")]
        [SerializeField] Preset ceiling = Preset.AcousticTile;
        [Tooltip("The floor: carpet takes the edge off the highs.")]
        [SerializeField] Preset floor = Preset.Carpet;

        // Six walls of four bands each, in Meta's order: right, left, ceiling, floor, front, back.
        readonly float[] wallBands = new float[6 * 4];
        readonly float[] clutterBands = new float[4];
        int appliedVersion = int.MinValue;
        bool dirty = true;

        /// <summary>The room as last sent to Meta XR Audio.</summary>
        public SpatialAudio.RoomShape Applied { get; private set; }

        void OnEnable()
        {
            AudioSettings.OnAudioConfigurationChanged += OnAudioReset;
            dirty = true;
        }

        void OnDisable() => AudioSettings.OnAudioConfigurationChanged -= OnAudioReset;

        void Start()
        {
            AudioSettings.GetDSPBufferSize(out int buffer, out int buffers);
            var config = AudioSettings.GetConfiguration();
            Debug.Log($"[Audio] Spatializer \"{AudioSettings.GetSpatializerPluginName()}\", {config.sampleRate} Hz {config.speakerMode}, " +
                      $"DSP buffer {buffer} x {buffers}, {config.numRealVoices} real voices.");
            Apply();
        }

        // Back from the headset being taken off, or the audio device changing: send the room again.
        void OnApplicationPause(bool paused)
        {
            if (!paused) dirty = true;
        }

        void OnAudioReset(bool deviceChanged) => dirty = true;

        void Update()
        {
            if (dirty || Room.Version != appliedVersion) Apply();
        }

        void Apply()
        {
            dirty = false;
            appliedVersion = Room.Version;
            var shape = SpatialAudio.ShapeRoom(standInRoom, standInClutter, clutterRange);
            Applied = shape;

            SetBands(0, walls);
            SetBands(1, walls);
            SetBands(2, ceiling);
            SetBands(3, floor);
            SetBands(4, walls);
            SetBands(5, walls);
            // As Meta's own room component spreads clutter: full at the top band, halving towards the lows.
            float factor = shape.Clutter;
            for (int band = clutterBands.Length - 1; band >= 0; band--)
            {
                clutterBands[band] = factor;
                factor *= 0.5f;
            }

            var native = MetaXRAudioNativeInterface.Interface;
            int room = native.SetAdvancedBoxRoomParameters(shape.Size.x, shape.Size.y, shape.Size.z, true, Vector3.zero, wallBands);
            int clutter = native.SetRoomClutterFactor(clutterBands);
            if (room != 0 || clutter != 0) Debug.LogWarning($"[Audio] Meta XR Audio refused the room (codes {room}, {clutter}).");
            Debug.Log($"[Audio] Reverb room {shape.Size.x:F1} x {shape.Size.y:F1} x {shape.Size.z:F1} m " +
                      $"({(shape.Scanned ? "from the scan" : "stand-in")}), clutter {shape.Clutter:F2}.");
        }

        void SetBands(int wall, Preset preset)
        {
            // Meta's table of reflection coefficients; it makes a new array per call, which is fine this rarely.
            var bands = MetaXRAudioRoomAcousticProperties.GetMaterialPresetBands(preset);
            for (int i = 0; i < 4; i++) wallBands[wall * 4 + i] = bands[i];
        }
    }
}
#endif
