package com.musicinyourhands.audio;

import android.media.AudioFormat;
import android.media.AudioRecord;
import android.media.MediaRecorder;
import android.media.audiofx.AcousticEchoCanceler;
import android.media.audiofx.AutomaticGainControl;
import android.media.audiofx.NoiseSuppressor;
import android.util.Log;

/**
 * Records the headset microphone for MusicBus's listen mode. Unity's Microphone records with the voice
 * recognition source, which Quest tunes for the wearer's voice and which leaves music in the room almost
 * silent. This asks for sources meant for music instead, and switches off any voice clean-up effects.
 */
public class RoomMicrophone {
    static final String TAG = "RoomMicrophone";
    static final int RING = 1 << 16;

    /**
     * Tried in order. On a Quest 3S, UNPROCESSED hears loud music from a phone speaker at about 0.02 RMS, while the
     * voice recognition input barely hears it and VOICE_PERFORMANCE opens but records silence, so it isn't tried.
     */
    static final int[] PREFERRED = {
        MediaRecorder.AudioSource.UNPROCESSED,
        MediaRecorder.AudioSource.CAMCORDER,
        MediaRecorder.AudioSource.MIC,
    };

    final float[] ring = new float[RING];
    long written, taken;
    AudioRecord record;
    Thread thread;
    volatile boolean running;
    int sampleRate = 48000;
    int source = -1;

    /** Starts with the first preferred source that opens, or the given source when it is 0 or more. */
    public boolean start(int wanted, int rate) {
        stop();
        sampleRate = rate;
        int[] order = wanted >= 0 ? new int[] { wanted } : PREFERRED;
        for (int s : order) {
            if (open(s)) break;
        }
        if (record == null) return false;

        running = true;
        thread = new Thread(this::loop, "RoomMicrophone");
        thread.setPriority(Thread.MAX_PRIORITY);
        thread.start();
        Log.i(TAG, "Recording from " + sourceName(source) + " at " + sampleRate + " Hz");
        return true;
    }

    /** Probes every source on a background thread, then starts with the first preferred one that opens. */
    public void probeThenStart(final int rate) {
        stop();
        new Thread(() -> { probe(rate); start(-1, rate); }, "RoomMicrophoneProbe").start();
    }

    /**
     * Records two seconds from each source in turn and logs how loud each one heard the room, to find the one
     * that hears music on a new headset. Blocks, so call it off the main thread, and only while stopped.
     */
    public String probe(int rate) {
        sampleRate = rate;
        int[] all = {
            MediaRecorder.AudioSource.VOICE_RECOGNITION, MediaRecorder.AudioSource.VOICE_PERFORMANCE,
            MediaRecorder.AudioSource.UNPROCESSED, MediaRecorder.AudioSource.CAMCORDER, MediaRecorder.AudioSource.MIC,
        };
        StringBuilder result = new StringBuilder();
        float[] chunk = new float[1024];
        for (int s : all) {
            if (!open(s)) {
                result.append(sourceName(s)).append("=unavailable ");
                continue;
            }
            // Skip the first half second while the input settles.
            double sum = 0, peak = 0;
            long count = 0, skip = rate / 2, total = rate * 2L;
            for (long got = 0; got < total; ) {
                int n = record.read(chunk, 0, chunk.length, AudioRecord.READ_BLOCKING);
                if (n <= 0) break;
                for (int i = 0; i < n; i++, got++) {
                    if (got < skip) continue;
                    sum += chunk[i] * chunk[i];
                    peak = Math.max(peak, Math.abs(chunk[i]));
                    count++;
                }
            }
            record.stop();
            record.release();
            record = null;
            double rms = count > 0 ? Math.sqrt(sum / count) : 0;
            result.append(String.format(java.util.Locale.US, "%s=%.4f/%.3f ", sourceName(s), rms, peak));
        }
        source = -1;
        Log.i(TAG, "Probe (rms/peak): " + result);
        return result.toString();
    }

    boolean open(int s) {
        try {
            int min = AudioRecord.getMinBufferSize(sampleRate, AudioFormat.CHANNEL_IN_MONO, AudioFormat.ENCODING_PCM_FLOAT);
            if (min <= 0) return false;
            AudioRecord r = new AudioRecord.Builder()
                .setAudioSource(s)
                .setAudioFormat(new AudioFormat.Builder()
                    .setEncoding(AudioFormat.ENCODING_PCM_FLOAT)
                    .setSampleRate(sampleRate)
                    .setChannelMask(AudioFormat.CHANNEL_IN_MONO)
                    .build())
                .setBufferSizeInBytes(Math.max(min, 8192 * 4))
                .build();
            if (r.getState() != AudioRecord.STATE_INITIALIZED) {
                r.release();
                return false;
            }
            int session = r.getAudioSessionId();
            if (NoiseSuppressor.isAvailable()) disable(NoiseSuppressor.create(session));
            if (AutomaticGainControl.isAvailable()) disable(AutomaticGainControl.create(session));
            if (AcousticEchoCanceler.isAvailable()) disable(AcousticEchoCanceler.create(session));
            r.startRecording();
            if (r.getRecordingState() != AudioRecord.RECORDSTATE_RECORDING) {
                r.release();
                return false;
            }
            record = r;
            source = s;
            return true;
        } catch (Exception e) {
            Log.w(TAG, sourceName(s) + " would not open: " + e);
            return false;
        }
    }

    static void disable(android.media.audiofx.AudioEffect effect) {
        if (effect != null) effect.setEnabled(false);
    }

    void loop() {
        float[] chunk = new float[1024];
        while (running) {
            int n = record.read(chunk, 0, chunk.length, AudioRecord.READ_BLOCKING);
            if (n <= 0) continue;
            synchronized (ring) {
                for (int i = 0; i < n; i++) ring[(int) ((written + i) & (RING - 1))] = chunk[i];
                written += n;
            }
        }
    }

    /** The samples recorded since the last call, at most a ring's worth. */
    public float[] take() {
        synchronized (ring) {
            long from = Math.max(taken, written - RING);
            float[] out = new float[(int) (written - from)];
            for (int i = 0; i < out.length; i++) out[i] = ring[(int) ((from + i) & (RING - 1))];
            taken = written;
            return out;
        }
    }

    public void stop() {
        running = false;
        if (thread != null) {
            try { thread.join(500); } catch (InterruptedException ignored) { }
            thread = null;
        }
        if (record != null) {
            record.stop();
            record.release();
            record = null;
        }
        source = -1;
    }

    public boolean isRecording() { return record != null && running; }
    public int sampleRate() { return sampleRate; }
    public String sourceName() { return sourceName(source); }

    static String sourceName(int s) {
        switch (s) {
            case MediaRecorder.AudioSource.MIC: return "MIC";
            case MediaRecorder.AudioSource.CAMCORDER: return "CAMCORDER";
            case MediaRecorder.AudioSource.VOICE_RECOGNITION: return "VOICE_RECOGNITION";
            case MediaRecorder.AudioSource.VOICE_COMMUNICATION: return "VOICE_COMMUNICATION";
            case MediaRecorder.AudioSource.UNPROCESSED: return "UNPROCESSED";
            case MediaRecorder.AudioSource.VOICE_PERFORMANCE: return "VOICE_PERFORMANCE";
            default: return "none";
        }
    }
}
