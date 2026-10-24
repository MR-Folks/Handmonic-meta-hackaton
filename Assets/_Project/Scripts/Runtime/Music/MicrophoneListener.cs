using UnityEngine;

namespace MusicInYourHands
{
    /// <summary>
    /// Records the headset microphone and turns the latest samples into a spectrum shaped like
    /// AudioSource.GetSpectrumData, so MusicBus can follow music playing in the room from any source.
    /// Nothing is played back: the samples are read straight from the recording clip.
    /// </summary>
    public class MicrophoneListener
    {
        const int WindowSize = 2048;
        const int PreferredRate = 48000;

        readonly float[] samples = new float[WindowSize];
        readonly float[] re = new float[WindowSize];
        readonly float[] im = new float[WindowSize];
        readonly float[] window = new float[WindowSize];
        readonly float windowSum;
        AudioClip clip;
        string device;
        float waveformPeak = 0.05f;

        public bool IsRecording => clip != null && Microphone.IsRecording(device);
        public int SampleRate { get; private set; } = PreferredRate;

        /// <summary>Loudness of the latest samples (RMS, 0-1).</summary>
        public float Level { get; private set; }

        public MicrophoneListener()
        {
            // Blackman-Harris, the same window MusicBus asks Unity for on songs.
            for (int i = 0; i < WindowSize; i++)
            {
                float x = 2f * Mathf.PI * i / (WindowSize - 1);
                window[i] = 0.35875f - 0.48829f * Mathf.Cos(x) + 0.14128f * Mathf.Cos(2f * x) - 0.01168f * Mathf.Cos(3f * x);
                windowSum += window[i];
            }
        }

        /// <summary>Starts recording from the default microphone. False when there is none.</summary>
        public bool Start()
        {
            if (IsRecording) return true;
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[MusicBus] No microphone found.");
                return false;
            }
            device = null;
            Microphone.GetDeviceCaps(device, out int min, out int max);
            SampleRate = max == 0 ? PreferredRate : Mathf.Clamp(PreferredRate, min, max);
            clip = Microphone.Start(device, true, 1, SampleRate);
            if (clip == null)
            {
                Debug.LogWarning("[MusicBus] The microphone would not start.");
                return false;
            }
            System.Array.Clear(samples, 0, samples.Length);
            return true;
        }

        public void Stop()
        {
            if (clip != null) Microphone.End(device);
            clip = null;
            Level = 0f;
        }

        /// <summary>
        /// Reads the latest samples into <paramref name="spectrum"/> (magnitudes from 0 Hz up to half the sample rate)
        /// and <paramref name="waveform"/> (auto-levelled, so a quiet room still draws a full wave).
        /// </summary>
        public bool Read(float[] spectrum, float[] waveform, float dt)
        {
            if (!IsRecording) return false;
            int position = Microphone.GetPosition(device);
            int start = position - WindowSize;
            if (start < 0) start += clip.samples;
            // Reads past the end of the looping clip wrap around to its start.
            clip.GetData(samples, start);

            float sum = 0f, peak = 0f;
            for (int i = 0; i < WindowSize; i++)
            {
                float s = samples[i];
                sum += s * s;
                peak = Mathf.Max(peak, Mathf.Abs(s));
                re[i] = s * window[i];
                im[i] = 0f;
            }
            Level = Mathf.Sqrt(sum / WindowSize);

            Fft(re, im);
            float scale = 2f / windowSum;
            int bins = Mathf.Min(spectrum.Length, WindowSize / 2);
            for (int k = 0; k < bins; k++) spectrum[k] = Mathf.Sqrt(re[k] * re[k] + im[k] * im[k]) * scale;

            waveformPeak = Mathf.Max(waveformPeak * Mathf.Pow(0.5f, dt), peak, 0.05f);
            float gain = 0.8f / waveformPeak;
            int offset = WindowSize - waveform.Length;
            for (int i = 0; i < waveform.Length; i++) waveform[i] = Mathf.Clamp(samples[offset + i] * gain, -1f, 1f);
            return true;
        }

        /// <summary>In-place radix-2 FFT. The length must be a power of two.</summary>
        static void Fft(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j)
                {
                    (re[i], re[j]) = (re[j], re[i]);
                    (im[i], im[j]) = (im[j], im[i]);
                }
            }
            for (int size = 2; size <= n; size <<= 1)
            {
                float angle = -2f * Mathf.PI / size;
                float stepRe = Mathf.Cos(angle), stepIm = Mathf.Sin(angle);
                int half = size >> 1;
                for (int i = 0; i < n; i += size)
                {
                    float wRe = 1f, wIm = 0f;
                    for (int k = 0; k < half; k++)
                    {
                        int a = i + k, b = a + half;
                        float tRe = re[b] * wRe - im[b] * wIm;
                        float tIm = re[b] * wIm + im[b] * wRe;
                        re[b] = re[a] - tRe;
                        im[b] = im[a] - tIm;
                        re[a] += tRe;
                        im[a] += tIm;
                        float next = wRe * stepRe - wIm * stepIm;
                        wIm = wRe * stepIm + wIm * stepRe;
                        wRe = next;
                    }
                }
            }
        }
    }
}
