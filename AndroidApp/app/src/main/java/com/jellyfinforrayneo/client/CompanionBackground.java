package com.jellyfinforrayneo.client;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Matrix;
import android.net.Uri;
import android.util.AtomicFile;
import android.webkit.WebResourceResponse;
import android.widget.Toast;

import androidx.exifinterface.media.ExifInterface;

import java.io.ByteArrayInputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.Collections;
import java.util.UUID;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

/** One local phone wallpaper; neither its source URI nor its bytes enter the bridge. */
final class CompanionBackground
{
    private static final int PICK_IMAGE = 4102;
    private final Activity activity;
    private final Runnable changed;
    private final AtomicFile image;
    private final ThreadPoolExecutor worker = new ThreadPoolExecutor(
            1, 1, 0L, TimeUnit.MILLISECONDS, new ArrayBlockingQueue<>(1));
    private volatile String revision;
    private volatile boolean busy;
    private volatile boolean closed;

    CompanionBackground(Activity activity, Runnable changed)
    {
        this.activity = activity;
        this.changed = changed;
        image = new AtomicFile(new File(activity.getFilesDir(), "companion-background.jpg"));
        revision = image.getBaseFile().isFile() ? newRevision() : "";
    }

    String getUrl()
    {
        String current = revision;
        return current.isEmpty() ? "" : CompanionSettingsPolicy.BACKGROUND_URL + current;
    }

    boolean isBusy()
    {
        return busy;
    }

    void choose()
    {
        if (busy || closed)
        {
            return;
        }
        busy = true;
        changed.run();
        Intent picker = new Intent(Intent.ACTION_GET_CONTENT);
        picker.setType("image/*");
        picker.addCategory(Intent.CATEGORY_OPENABLE);
        picker.putExtra(Intent.EXTRA_MIME_TYPES, new String[]{"image/jpeg", "image/png", "image/webp"});
        try
        {
            activity.startActivityForResult(Intent.createChooser(picker, "选择手机背景"), PICK_IMAGE);
        }
        catch (RuntimeException ignored)
        {
            finish("无法打开图片选择器");
        }
    }

    boolean onActivityResult(int requestCode, int resultCode, Intent data)
    {
        if (requestCode != PICK_IMAGE)
        {
            return false;
        }
        Uri uri = data == null ? null : data.getData();
        if (resultCode != Activity.RESULT_OK || uri == null)
        {
            finish(null);
            return true;
        }
        if (closed || !"content".equals(uri.getScheme()))
        {
            finish("无法读取这张图片，请重新选择");
            return true;
        }
        busy = true;
        changed.run();
        worker.execute(() ->
        {
            String notice = "手机背景已更新";
            try (InputStream source = activity.getContentResolver().openInputStream(uri))
            {
                save(CompanionSettingsPolicy.readImage(source));
            }
            catch (Exception | OutOfMemoryError ignored)
            {
                notice = "图片未能保存，请选择 20 MB 以内的 JPG、PNG 或 WebP 图片";
            }
            String result = notice;
            activity.runOnUiThread(() -> finish(result));
        });
        return true;
    }

    void clear()
    {
        if (busy || closed)
        {
            return;
        }
        busy = true;
        changed.run();
        worker.execute(() ->
        {
            synchronized (image)
            {
                image.delete();
                revision = image.getBaseFile().exists() ? revision : "";
            }
            activity.runOnUiThread(() -> finish(revision.isEmpty() ? null : "背景未能移除，请重试"));
        });
    }

    WebResourceResponse open()
    {
        synchronized (image)
        {
            try
            {
                return new WebResourceResponse("image/jpeg", null, 200, "OK",
                        Collections.singletonMap("Cache-Control", "no-store"), image.openRead());
            }
            catch (IOException ignored)
            {
                return new WebResourceResponse("image/jpeg", null, 404, "Not Found",
                        Collections.singletonMap("Cache-Control", "no-store"),
                        new ByteArrayInputStream(new byte[0]));
            }
        }
    }

    void close()
    {
        closed = true;
        worker.shutdownNow();
    }

    private void finish(String notice)
    {
        busy = false;
        if (!closed)
        {
            changed.run();
            if (notice != null)
            {
                Toast.makeText(activity, notice, Toast.LENGTH_LONG).show();
            }
        }
    }

    private void save(byte[] bytes) throws IOException
    {
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inJustDecodeBounds = true;
        BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options);
        if (!"image/jpeg".equals(options.outMimeType) && !"image/png".equals(options.outMimeType)
                && !"image/webp".equals(options.outMimeType))
        {
            throw new IOException("Unsupported image format");
        }
        options.inSampleSize = CompanionSettingsPolicy.sampleSize(options.outWidth, options.outHeight);
        options.inJustDecodeBounds = false;
        Bitmap decoded = BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options);
        if (decoded == null)
        {
            throw new IOException("Invalid image");
        }
        Bitmap oriented = decoded;
        Bitmap scaled = null;
        Bitmap opaque = null;
        try
        {
            Matrix transform = orientation(bytes);
            oriented = Bitmap.createBitmap(decoded, 0, 0, decoded.getWidth(), decoded.getHeight(), transform, true);
            float scale = Math.min(1f, (float) CompanionSettingsPolicy.MAX_IMAGE_EDGE
                    / Math.max(oriented.getWidth(), oriented.getHeight()));
            scaled = Bitmap.createScaledBitmap(oriented, Math.max(1, Math.round(oriented.getWidth() * scale)),
                    Math.max(1, Math.round(oriented.getHeight() * scale)), true);
            opaque = Bitmap.createBitmap(scaled.getWidth(), scaled.getHeight(), Bitmap.Config.ARGB_8888);
            Canvas canvas = new Canvas(opaque);
            canvas.drawColor(Color.WHITE);
            canvas.drawBitmap(scaled, 0, 0, null);
            synchronized (image)
            {
                if (closed || Thread.currentThread().isInterrupted())
                {
                    throw new IOException("Import cancelled");
                }
                FileOutputStream output = image.startWrite();
                try
                {
                    if (!opaque.compress(Bitmap.CompressFormat.JPEG, 88, output))
                    {
                        throw new IOException("Image encoding failed");
                    }
                    if (closed || Thread.currentThread().isInterrupted())
                    {
                        throw new IOException("Import cancelled");
                    }
                    image.finishWrite(output);
                    revision = newRevision();
                }
                catch (IOException | RuntimeException | OutOfMemoryError failure)
                {
                    image.failWrite(output);
                    throw failure;
                }
            }
        }
        finally
        {
            if (opaque != null)
            {
                opaque.recycle();
            }
            if (scaled != null && scaled != oriented)
            {
                scaled.recycle();
            }
            if (oriented != decoded)
            {
                oriented.recycle();
            }
            decoded.recycle();
        }
    }

    private static Matrix orientation(byte[] bytes)
    {
        Matrix matrix = new Matrix();
        try
        {
            ExifInterface exif = new ExifInterface(new ByteArrayInputStream(bytes));
            switch (exif.getAttributeInt(ExifInterface.TAG_ORIENTATION, ExifInterface.ORIENTATION_NORMAL))
            {
                case ExifInterface.ORIENTATION_FLIP_HORIZONTAL: matrix.setScale(-1, 1); break;
                case ExifInterface.ORIENTATION_ROTATE_180: matrix.setRotate(180); break;
                case ExifInterface.ORIENTATION_FLIP_VERTICAL: matrix.setScale(1, -1); break;
                case ExifInterface.ORIENTATION_TRANSPOSE: matrix.setRotate(90); matrix.postScale(-1, 1); break;
                case ExifInterface.ORIENTATION_ROTATE_90: matrix.setRotate(90); break;
                case ExifInterface.ORIENTATION_TRANSVERSE: matrix.setRotate(-90); matrix.postScale(-1, 1); break;
                case ExifInterface.ORIENTATION_ROTATE_270: matrix.setRotate(-90); break;
                default: break;
            }
        }
        catch (IOException ignored)
        {
            // Images without EXIF retain their encoded orientation.
        }
        return matrix;
    }

    private static String newRevision()
    {
        return UUID.randomUUID().toString().replace("-", "");
    }
}
