package com.jellyfinforrayneo.client;

final class WebNavigationPolicy
{
    static final String COMPANION_ROOT = "file:///android_asset/CompanionUI/";
    static final String GLASSES_ROOT = "file:///android_asset/GlassesUI/";

    private WebNavigationPolicy()
    {
    }

    static boolean isCompanionAsset(String url)
    {
        return isWithinRoot(url, COMPANION_ROOT);
    }

    static boolean isGlassesAsset(String url)
    {
        return isWithinRoot(url, GLASSES_ROOT);
    }

    private static boolean isWithinRoot(String value, String root)
    {
        if (value == null || value.length() > 4_096 || !value.startsWith(root))
        {
            return false;
        }
        String tail = value.substring(root.length());
        return !tail.contains("..")
                && !tail.contains("\\")
                && !tail.contains("\u0000")
                && !tail.contains("%");
    }
}
