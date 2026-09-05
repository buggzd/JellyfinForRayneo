# JavaScript bridge entry points are invoked by Chromium.
-keepclassmembers class * {
    @android.webkit.JavascriptInterface <methods>;
}
