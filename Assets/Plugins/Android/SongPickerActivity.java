package com.musicinyourhands.app;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.database.Cursor;
import android.media.MediaMetadataRetriever;
import android.net.Uri;
import android.os.Bundle;
import android.provider.OpenableColumns;
import android.util.Log;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Opens the system file picker for songs and copies what is chosen into the app's Songs folder (see SongPicker.cs).
 * A lyrics file picked together with one song is named after that song, so they match whatever they were called. Plain
 * text lyrics become an .lrc with the lines spread evenly over the song.
 */
public class SongPickerActivity extends Activity {
    private static final String TAG = "SongPicker";
    private static final String FOLDER = "folder";
    private static final int PICK = 1;

    /** Called from SongPicker.cs. */
    public static void open(Activity from, String folder) {
        Intent intent = new Intent(from, SongPickerActivity.class);
        intent.putExtra(FOLDER, folder);
        from.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        if (saved != null) return;
        Intent pick = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        pick.addCategory(Intent.CATEGORY_OPENABLE);
        // Not "audio/*": .lrc files have no type of their own and would be greyed out.
        pick.setType("*/*");
        pick.putExtra(Intent.EXTRA_ALLOW_MULTIPLE, true);
        try {
            startActivityForResult(pick, PICK);
        } catch (ActivityNotFoundException e) {
            Log.w(TAG, "This headset has no file picker.", e);
            finish();
        }
    }

    @Override
    protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != PICK || result != RESULT_OK || data == null) {
            finish();
            return;
        }
        List<Uri> picked = new ArrayList<>();
        if (data.getClipData() != null) {
            for (int i = 0; i < data.getClipData().getItemCount(); i++) picked.add(data.getClipData().getItemAt(i).getUri());
        } else if (data.getData() != null) {
            picked.add(data.getData());
        }
        File folder = new File(getIntent().getStringExtra(FOLDER));
        // Copied away from the main thread, as songs can be large; the app gets focus back when this activity ends.
        new Thread(() -> {
            try {
                add(picked, folder);
            } catch (IOException e) {
                Log.w(TAG, "Couldn't add the song.", e);
            }
            runOnUiThread(this::finish);
        }).start();
    }

    private void add(List<Uri> picked, File folder) throws IOException {
        folder.mkdirs();
        List<File> songs = new ArrayList<>();
        List<Uri> lyrics = new ArrayList<>();
        for (Uri uri : picked) {
            String name = fileName(uri);
            String extension = extension(name);
            if (extension.equals("mp3") || extension.equals("ogg") || extension.equals("wav")) {
                File song = new File(folder, name);
                try (InputStream in = getContentResolver().openInputStream(uri); OutputStream out = new FileOutputStream(song)) {
                    copy(in, out);
                }
                songs.add(song);
            } else if (extension.equals("lrc") || extension.equals("txt")) {
                lyrics.add(uri);
            } else {
                Log.w(TAG, "Skipped " + name + ": songs can be .mp3, .ogg or .wav, and lyrics .lrc or .txt.");
            }
        }
        for (Uri uri : lyrics) {
            String name = fileName(uri);
            File song = songs.size() == 1 ? songs.get(0) : new File(folder, base(name));
            File target = new File(folder, base(song.getName()) + ".lrc");
            if (extension(name).equals("lrc")) {
                try (InputStream in = getContentResolver().openInputStream(uri); OutputStream out = new FileOutputStream(target)) {
                    copy(in, out);
                }
            } else {
                timeLines(uri, song, target);
            }
        }
    }

    /** Writes plain lyrics as an .lrc, the lines evenly spread between just after the start and just before the end. */
    private void timeLines(Uri uri, File song, File target) throws IOException {
        List<String> lines = new ArrayList<>();
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(getContentResolver().openInputStream(uri), StandardCharsets.UTF_8))) {
            for (String line = reader.readLine(); line != null; line = reader.readLine()) {
                if (!line.trim().isEmpty()) lines.add(line.trim());
            }
        }
        double seconds = duration(song);
        double start = seconds * 0.05, step = seconds * 0.9 / Math.max(1, lines.size());
        try (PrintWriter writer = new PrintWriter(target, "UTF-8")) {
            for (int i = 0; i < lines.size(); i++) {
                double time = start + step * i;
                writer.println(String.format(Locale.ROOT, "[%02d:%05.2f]%s", (int) (time / 60), time % 60, lines.get(i)));
            }
        }
    }

    private static double duration(File song) {
        MediaMetadataRetriever retriever = new MediaMetadataRetriever();
        try {
            retriever.setDataSource(song.getPath());
            return Double.parseDouble(retriever.extractMetadata(MediaMetadataRetriever.METADATA_KEY_DURATION)) / 1000;
        } catch (RuntimeException e) {
            return 180;
        } finally {
            try {
                retriever.release();
            } catch (IOException | RuntimeException ignored) {
            }
        }
    }

    private String fileName(Uri uri) {
        try (Cursor cursor = getContentResolver().query(uri, new String[] { OpenableColumns.DISPLAY_NAME }, null, null, null)) {
            if (cursor != null && cursor.moveToFirst() && !cursor.isNull(0)) return new File(cursor.getString(0)).getName();
        }
        return new File(uri.getPath()).getName();
    }

    private static String base(String name) {
        int dot = name.lastIndexOf('.');
        return dot > 0 ? name.substring(0, dot) : name;
    }

    private static String extension(String name) {
        int dot = name.lastIndexOf('.');
        return dot > 0 ? name.substring(dot + 1).toLowerCase(Locale.ROOT) : "";
    }

    private static void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buffer = new byte[64 * 1024];
        for (int read = in.read(buffer); read > 0; read = in.read(buffer)) out.write(buffer, 0, read);
    }
}
