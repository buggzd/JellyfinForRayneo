package com.jellyfinforrayneo.client;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;

final class CompanionSettingsPolicy
{
    static final int MAX_IMAGE_BYTES = 20 * 1024 * 1024;
    static final int MAX_IMAGE_EDGE = 1600;
    static final String BACKGROUND_URL = "https://appassets.androidplatform.net/CompanionUI/phone-background.jpg?v=";
    private static final String PROJECT_URL = "https://github.com/buggzd/JellyfinForRayneo";

    private CompanionSettingsPolicy()
    {
    }

    static boolean isTouchpadBackground(String value)
    {
        return "texture".equals(value) || "black".equals(value);
    }

    static String touchpadBackground(String value, String theme)
    {
        return isTouchpadBackground(value) ? value : UiTheme.SIMPLE.equals(theme) ? "black" : "texture";
    }

    static String projectPage(String page)
    {
        if (page == null || page.length() > 16)
        {
            return null;
        }
        switch (page)
        {
            case "project": return PROJECT_URL;
            case "issues": return PROJECT_URL + "/issues";
            case "guide": return PROJECT_URL + "/blob/main/docs/USER_GUIDE.md";
            default: return null;
        }
    }

    static boolean isBackgroundRequest(String url)
    {
        return url != null && url.startsWith(BACKGROUND_URL)
                && url.length() == BACKGROUND_URL.length() + 32
                && url.substring(BACKGROUND_URL.length()).matches("[a-f0-9]{32}");
    }

    static int sampleSize(int width, int height) throws IOException
    {
        if (width <= 0 || height <= 0 || width > 16000 || height > 16000
                || (long) width * height > 64_000_000L)
        {
            throw new IOException("Unsupported image dimensions");
        }
        int sample = 1;
        while (Math.max(width, height) / (sample * 2) >= MAX_IMAGE_EDGE)
        {
            sample *= 2;
        }
        return sample;
    }

    static byte[] readImage(InputStream source) throws IOException
    {
        if (source == null)
        {
            throw new IOException("Missing image");
        }
        ByteArrayOutputStream output = new ByteArrayOutputStream();
        byte[] buffer = new byte[8192];
        int count;
        while ((count = source.read(buffer)) != -1)
        {
            if (Thread.currentThread().isInterrupted() || count > MAX_IMAGE_BYTES - output.size())
            {
                throw new IOException("Image import limit exceeded");
            }
            output.write(buffer, 0, count);
        }
        if (output.size() == 0)
        {
            throw new IOException("Empty image");
        }
        return output.toByteArray();
    }
}
