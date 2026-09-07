package com.jellyfinforrayneo.client;

import org.junit.Test;

import java.io.ByteArrayInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.Arrays;

import static org.junit.Assert.assertArrayEquals;
import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertThrows;
import static org.junit.Assert.assertTrue;

public final class CompanionSettingsPolicyTests
{
    @Test
    public void glassTransparency_AcceptsOnlyBoundedWholePercentages()
    {
        for (String value : Arrays.asList("0", "1", "50", "88", "99", "100"))
        {
            assertEquals(Integer.valueOf(value), CompanionSettingsPolicy.parseGlassTransparency(value));
        }
        for (String value : Arrays.asList(null, "", "-1", "101", "0.5", "50.0", "+50", " 50", "50\n", "05",
                "1e2", "{}", "NaN", new String(new char[65536])))
        {
            assertNull(CompanionSettingsPolicy.parseGlassTransparency(value));
        }
    }

    @Test
    public void projectLinks_AllowOnlyNamedPublicPages()
    {
        assertEquals("https://github.com/buggzd/JellyfinForRayneo", CompanionSettingsPolicy.projectPage("project"));
        assertEquals("https://github.com/buggzd/JellyfinForRayneo/issues", CompanionSettingsPolicy.projectPage("issues"));
        assertEquals("https://github.com/buggzd/JellyfinForRayneo/blob/main/docs/USER_GUIDE.md",
                CompanionSettingsPolicy.projectPage("guide"));
        for (String page : Arrays.asList(null, "", "Issues", " issues", "javascript:alert(1)",
                "https://example.com", "../issues", "issues?token=test", new String(new char[5000])))
        {
            assertNull(CompanionSettingsPolicy.projectPage(page));
        }
    }

    @Test
    public void backgroundRoute_DoesNotExposeArbitraryLocalFiles()
    {
        String valid = CompanionSettingsPolicy.BACKGROUND_URL + "0123456789abcdef0123456789abcdef";
        assertTrue(CompanionSettingsPolicy.isBackgroundRequest(valid));
        for (String url : Arrays.asList(null, valid + "&path=private", valid + "/../session", valid + "#fragment",
                valid.replace("CompanionUI", "GlassesUI"), valid.replace("phone-background.jpg", "../session.json"),
                valid.replace("v=", "v=%"), valid.substring(0, valid.length() - 1), "content://image/123"))
        {
            assertFalse(CompanionSettingsPolicy.isBackgroundRequest(url));
        }
    }

    @Test
    public void imageDecode_RejectsInvalidOrExcessiveDimensions()
    {
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.sampleSize(-1, 100));
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.sampleSize(100, 0));
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.sampleSize(16001, 1));
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.sampleSize(8001, 8000));
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.sampleSize(Integer.MAX_VALUE, Integer.MAX_VALUE));
    }

    @Test
    public void imageDecode_DownsamplesLargePhotosBeforeAllocatingPixels()
    {
        assertEquals(1, sample(800, 1200));
        assertEquals(2, sample(4000, 3000));
        assertEquals(4, sample(6000, 8000));
        assertEquals(8, sample(16000, 1000));
    }

    @Test
    public void imageImport_RejectsEmptyOrMissingContent()
    {
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.readImage(null));
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.readImage(new ByteArrayInputStream(new byte[0])));
    }

    @Test
    public void imageImport_PreservesTheChosenBytes() throws IOException
    {
        byte[] bytes = new byte[]{1, 2, 3, 4};
        assertArrayEquals(bytes, CompanionSettingsPolicy.readImage(new ByteArrayInputStream(bytes)));
    }

    @Test
    public void imageImport_StopsReadingAtTheByteLimit()
    {
        InputStream source = new InputStream()
        {
            @Override
            public int read()
            {
                return 1;
            }

            @Override
            public int read(byte[] bytes)
            {
                return bytes.length;
            }
        };
        assertThrows(IOException.class, () -> CompanionSettingsPolicy.readImage(source));
    }

    private static int sample(int width, int height)
    {
        try
        {
            return CompanionSettingsPolicy.sampleSize(width, height);
        }
        catch (IOException exception)
        {
            throw new AssertionError(exception);
        }
    }
}
