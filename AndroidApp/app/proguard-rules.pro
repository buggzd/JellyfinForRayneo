# RayNeo's client binds to the vendor USB service through AIDL and reflection.
-keep class com.tcl.xr.** { *; }
-keep class com.tcl.ar.usbservice.** { *; }

# JavaScript bridge entry points are invoked by Chromium.
-keepclassmembers class * {
    @android.webkit.JavascriptInterface <methods>;
}
