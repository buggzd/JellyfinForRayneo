package com.jellyfinforrayneo.client;

import org.junit.Test;

import static org.junit.Assert.assertFalse;
import static org.junit.Assert.assertTrue;

public final class WebNavigationPolicyTests
{
    @Test
    public void companionPolicy_AllowsOnlyOwnAssetRoot()
    {
        assertTrue(WebNavigationPolicy.isCompanionAsset(
                "file:///android_asset/CompanionUI/index.html"));
        assertFalse(WebNavigationPolicy.isCompanionAsset("https://example.com"));
        assertFalse(WebNavigationPolicy.isCompanionAsset(
                "file:///android_asset/GlassesUI/index.html"));
        assertFalse(WebNavigationPolicy.isCompanionAsset(
                "file:///android_asset/CompanionUI/../GlassesUI/index.html"));
        assertFalse(WebNavigationPolicy.isCompanionAsset(
                "file:///android_asset/CompanionUI/%2e%2e/GlassesUI/index.html"));
        assertFalse(WebNavigationPolicy.isCompanionAsset(
                "file:///android_asset/CompanionUI/%252e%252e/GlassesUI/index.html"));
    }

    @Test
    public void glassesPolicy_AllowsOnlyOwnAssetRoot()
    {
        assertTrue(WebNavigationPolicy.isGlassesAsset(
                "file:///android_asset/GlassesUI/assets/app.js"));
        assertFalse(WebNavigationPolicy.isGlassesAsset("javascript:alert(1)"));
        assertFalse(WebNavigationPolicy.isGlassesAsset(
                "file:///android_asset/CompanionUI/index.html"));
    }
}
