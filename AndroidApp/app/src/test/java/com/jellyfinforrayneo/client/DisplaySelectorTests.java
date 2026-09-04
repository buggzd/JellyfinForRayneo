package com.jellyfinforrayneo.client;

import org.junit.Test;

import java.util.List;

import static org.junit.Assert.assertEquals;

public final class DisplaySelectorTests
{
    @Test
    public void selectBestIndex_IgnoresDefaultInvalidAndOffDisplays()
    {
        List<DisplaySelector.Candidate> candidates = List.of(
                new DisplaySelector.Candidate(0, true, true, false, "Phone"),
                new DisplaySelector.Candidate(2, false, true, true, "RayNeo"),
                new DisplaySelector.Candidate(3, true, false, true, "RayNeo"),
                new DisplaySelector.Candidate(4, true, true, false, "External"));

        assertEquals(3, DisplaySelector.selectBestIndex(candidates, 0));
    }

    @Test
    public void selectBestIndex_PrefersNamedRayNeoPresentation()
    {
        List<DisplaySelector.Candidate> candidates = List.of(
                new DisplaySelector.Candidate(1, true, true, true, "Generic display"),
                new DisplaySelector.Candidate(2, true, true, false, "RayNeo SmartGlasses"));

        assertEquals(1, DisplaySelector.selectBestIndex(candidates, 0));
    }
}
