package com.musicinyourhands.app;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.util.Base64;

import java.io.ByteArrayOutputStream;

/**
 * Draws an emoji with the headset's own colour emoji font, as a square PNG (see EmojiPictures.cs). The app ships no
 * emoji images: the picture is the system's, drawn when the song maker's page first needs it.
 */
public final class EmojiPicture {
    private EmojiPicture() {
    }

    /** The emoji, centred on a transparent square of size pixels, as a base64 PNG; empty if the font has no such emoji. */
    public static String png(String text, int size) {
        Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        paint.setTextAlign(Paint.Align.CENTER);
        paint.setTextSize(size * 0.8f);
        if (text == null || text.isEmpty() || !paint.hasGlyph(text)) return "";

        Bitmap bitmap = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(bitmap);
        Rect bounds = new Rect();
        paint.getTextBounds(text, 0, text.length(), bounds);
        canvas.drawText(text, size / 2f, size / 2f - bounds.exactCenterY(), paint);

        ByteArrayOutputStream out = new ByteArrayOutputStream();
        bitmap.compress(Bitmap.CompressFormat.PNG, 100, out);
        bitmap.recycle();
        return Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP);
    }
}
