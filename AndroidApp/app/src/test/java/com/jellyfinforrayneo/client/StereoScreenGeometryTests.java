package com.jellyfinforrayneo.client;

import org.junit.Test;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertNull;
import static org.junit.Assert.assertTrue;

public final class StereoScreenGeometryTests
{
    private static final float EPSILON = .001f;

    @Test
    public void everySetting_KeepsBothEyesInsideTheirViewportWithStableAverageCenter()
    {
        for (int width : new int[] {1280, 1920, 3840, 7680})
        {
            int height = width * 9 / 32;
            for (int depth = 0; depth <= 3; depth++)
            {
                for (int size = 80; size <= 95; size++)
                {
                    StereoScreenGeometry frame = StereoScreenGeometry.create(
                            width, height, depth * 8f / 1920f, size / 100f);
                    assertNotNull(frame);
                    float margin = frame.eyeWidth * StereoScreenGeometry.EDGE_MARGIN_FRACTION;
                    for (float x : new float[] {frame.leftX, frame.rightX})
                    {
                        assertTrue(x >= margin - EPSILON);
                        assertTrue(x + frame.scale * frame.eyeWidth <= frame.eyeWidth - margin + EPSILON);
                    }
                    assertTrue(frame.top >= height * StereoScreenGeometry.EDGE_MARGIN_FRACTION);
                    assertTrue(frame.top + frame.scale * height <= height * .99f);
                    float averageCenter = (frame.leftX + frame.rightX) * .5f
                            + frame.scale * frame.eyeWidth * .5f;
                    assertEquals(frame.eyeWidth * .5f, averageCenter, EPSILON);
                    assertEquals(height * .5f, frame.top + frame.scale * height * .5f, EPSILON);
                }
            }
        }
    }

    @Test
    public void closerSetting_AddsEqualAndOppositeShiftsIndependentOfSizeAndSourcePoint()
    {
        for (int depth = 0; depth <= 3; depth++)
        {
            for (int size : new int[] {80, 90, 95})
            {
                StereoScreenGeometry frame = StereoScreenGeometry.create(3840, 1080,
                        depth * 8f / 1920f, size / 100f);
                float inset = (1f - frame.scale) * frame.eyeWidth * .5f;
                assertEquals(depth * 4f, frame.leftX - inset, EPSILON);
                assertEquals(-depth * 4f, frame.rightX - inset, EPSILON);
                for (int sourceX : new int[] {0, 300, 960, 1920})
                {
                    float left = frame.leftX + frame.scale * sourceX;
                    float right = frame.rightX + frame.scale * sourceX;
                    assertEquals(depth * 8f, left - right, EPSILON);
                }
                assertEquals(size / 100f, frame.scale, EPSILON);
            }
        }
    }

    @Test
    public void uniformlyScaledViewport_PreservesNormalizedDisparityAndShape()
    {
        StereoScreenGeometry full = StereoScreenGeometry.create(3840, 1080, 16f / 1920f, .9f);
        StereoScreenGeometry half = StereoScreenGeometry.create(1920, 540, 16f / 1920f, .9f);
        assertEquals(full.disparity, half.disparity * 2f, EPSILON);
        assertEquals(full.leftX, half.leftX * 2f, EPSILON);
        assertEquals(full.rightX, half.rightX * 2f, EPSILON);
        assertEquals(full.top, half.top * 2f, EPSILON);
        assertEquals(full.scale, half.scale, EPSILON);
    }

    @Test
    public void outputReadiness_RequiresFullSbsModeAndActualViewport()
    {
        assertTrue(StereoScreenGeometry.supportsOutput(3840, 1080, 3840, 1080));
        assertTrue(StereoScreenGeometry.supportsOutput(3840, 1080, 1920, 540));
        assertFalse(StereoScreenGeometry.supportsOutput(1920, 1080, 3840, 1080));
        assertFalse(StereoScreenGeometry.supportsOutput(3840, 1080, 1920, 1080));
        assertFalse(StereoScreenGeometry.supportsOutput(3840, 1080, 3840, 1040));
        assertFalse(StereoScreenGeometry.supportsOutput(3840, 1080, 0, 0));
        assertFalse(StereoScreenGeometry.hasFullSbsAspect(3839, 1080));
        assertFalse(StereoScreenGeometry.hasFullSbsAspect(Integer.MAX_VALUE, 1080));
        assertFalse(StereoScreenGeometry.hasFullSbsAspect(32, 9));
    }

    @Test
    public void invalidTransforms_NeverProduceStereoGeometry()
    {
        for (float disparity : new float[] {-1f, -.001f, .02f, Float.NaN, Float.POSITIVE_INFINITY})
        {
            assertNull(StereoScreenGeometry.create(3840, 1080, disparity, .9f));
        }
        for (float size : new float[] {0f, .79f, .96f, Float.NaN, Float.NEGATIVE_INFINITY})
        {
            assertNull(StereoScreenGeometry.create(3840, 1080, 0f, size));
        }
        assertNull(StereoScreenGeometry.create(1920, 1080, 0f, .9f));
    }
}
