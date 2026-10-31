package com.musicinyourhands.app;

import android.app.Activity;
import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.database.Cursor;
import android.media.MediaMetadataRetriever;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;
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
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

/**
 * Opens the system file picker for one file and copies it into the app's Songs folder (see SongPicker.cs): a song, or
 * lyrics for a chosen song, saved under that song's name so they always match. Plain text lyrics become an .lrc with the
 * lines spread evenly over the song. The outcome is left in a small result file, as the app beside this window keeps
 * running and has no other way to know the pick has finished.
 */
public class SongPickerActivity extends Activity {
    private static final String TAG = "SongPicker";
    private static final String MODE = "mode";
    private static final String FOLDER = "folder";
    private static final String SONG = "song";
    private static final String RESULT = "result";
    private static final String DOWNLOADS = "content://com.android.externalstorage.documents/document/primary%3ADownload";
    private static final int PICK = 1;

    /** Called from SongPicker.cs. */
    public static void open(Activity from, String mode, String folder, String song, String result) {
        Intent intent = new Intent(from, SongPickerActivity.class);
        intent.putExtra(MODE, mode);
        intent.putExtra(FOLDER, folder);
        intent.putExtra(SONG, song);
        intent.putExtra(RESULT, result);
        from.startActivity(intent);
    }

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        if (saved != null) return;
        Intent pick = new Intent(Intent.ACTION_OPEN_DOCUMENT);
        pick.addCategory(Intent.CATEGORY_OPENABLE);
        // Lyrics files have no type of their own, so anything may be picked there and the name is checked afterwards.
        pick.setType(SONG.equals(getIntent().getStringExtra(MODE)) ? "audio/*" : "*/*");
        pick.putExtra(DocumentsContract.EXTRA_INITIAL_URI, Uri.parse(DOWNLOADS));
        try {
            startActivityForResult(pick, PICK);
        } catch (ActivityNotFoundException e) {
            Log.w(TAG, "This headset has no file picker.", e);
            finishWith("failed", "");
        }
    }

    @Override
    protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != PICK) return;
        if (result != RESULT_OK || data == null || data.getData() == null) {
            finishWith("cancelled", "");
            return;
        }
        Uri picked = data.getData();
        // Copied away from the main thread, as songs can be large.
        new Thread(() -> {
            String name = "";
            try {
                name = fileName(picked);
                File folder = new File(getIntent().getStringExtra(FOLDER));
                folder.mkdirs();
                String added = SONG.equals(getIntent().getStringExtra(MODE)) ? addSong(picked, name, folder) : addLyrics(picked, name, folder);
                finishWith(added != null ? "added" : "unsupported", added != null ? added : name);
            } catch (IOException | RuntimeException e) {
                Log.w(TAG, "Couldn't add " + name, e);
                finishWith("failed", name);
            }
        }).start();
    }

    private String addSong(Uri uri, String name, File folder) throws IOException {
        String extension = extension(name);
        if (!extension.equals("mp3") && !extension.equals("ogg") && !extension.equals("wav")) return null;
        copy(uri, new File(folder, name));
        return name;
    }

    private String addLyrics(Uri uri, String name, File folder) throws IOException {
        String extension = extension(name);
        File song = new File(getIntent().getStringExtra(SONG));
        File target = new File(folder, base(song.getName()) + ".lrc");
        if (extension.equals("lrc")) copy(uri, target);
        else if (extension.equals("txt")) timeLines(uri, song, target);
        else return null;
        return target.getName();
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

    /** Leaves the outcome for the app, written whole so it never reads half of it. */
    private void finishWith(String outcome, String file) {
        String path = getIntent().getStringExtra(RESULT);
        try {
            File result = new File(path), written = new File(path + ".new");
            try (PrintWriter writer = new PrintWriter(written, "UTF-8")) {
                writer.println(outcome);
                writer.println(file);
            }
            Files.move(written.toPath(), result.toPath(), StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
        } catch (IOException e) {
            Log.w(TAG, "Couldn't report the result.", e);
        }
        runOnUiThread(this::finish);
    }

    private void copy(Uri uri, File target) throws IOException {
        try (InputStream in = getContentResolver().openInputStream(uri); OutputStream out = new FileOutputStream(target)) {
            byte[] buffer = new byte[64 * 1024];
            for (int read = in.read(buffer); read > 0; read = in.read(buffer)) out.write(buffer, 0, read);
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
}
