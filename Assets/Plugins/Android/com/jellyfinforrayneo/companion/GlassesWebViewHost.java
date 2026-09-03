package com.jellyfinforrayneo.companion;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.graphics.Canvas;
import android.graphics.Color;
import android.media.MediaCodecInfo;
import android.media.MediaCodecList;
import android.os.Build;
import android.text.TextUtils;
import android.view.Display;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewParent;
import android.webkit.JavascriptInterface;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;

import com.unity3d.player.UnityPlayer;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.LinkedHashSet;
import java.util.Locale;
import java.util.Set;

final class GlassesWebViewHost {
    private static final String GLASSES_UI_ROOT =
            "file:///android_asset/GlassesUI/";
    private static final String GLASSES_UI_URL = GLASSES_UI_ROOT + "index.html";
    private static final long PRESENTATION_ATTACH_RETRY_MS = 160L;
    private static final String UNITY_RECEIVER = "Jellyfin for RayNeo Application";

    private final JellyfinRayNeoActivity activity;
    private final UnityPlayer unityPlayer;
    private final Runnable attachRunnable = new Runnable() {
        @Override
        public void run() {
            attachToGlassesPresentation();
        }
    };

    private ViewGroup webParent;
    private StereoMirrorLayout webContainer;
    private WebView webView;
    private volatile boolean requested;
    private volatile boolean ready;

    GlassesWebViewHost(
            JellyfinRayNeoActivity activity,
            UnityPlayer unityPlayer) {
        this.activity = activity;
        this.unityPlayer = unityPlayer;
    }

    boolean show() {
        if (activity == null || unityPlayer == null || activity.isFinishing()) {
            return false;
        }
        requested = true;
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                scheduleAttach(0L);
            }
        });
        return true;
    }

    void hide() {
        requested = false;
        ready = false;
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                View decor = activity.getWindow() == null
                        ? null
                        : activity.getWindow().getDecorView();
                if (decor != null) {
                    decor.removeCallbacks(attachRunnable);
                }
                if (webView != null) {
                    webView.setVisibility(View.GONE);
                }
                if (webContainer != null) {
                    webContainer.setVisibility(View.GONE);
                }
            }
        });
    }

    void destroy() {
        requested = false;
        ready = false;
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                View decor = activity.getWindow() == null
                        ? null
                        : activity.getWindow().getDecorView();
                if (decor != null) {
                    decor.removeCallbacks(attachRunnable);
                }
                destroyWebView();
            }
        });
    }

    void onPresentationChanged() {
        if (!requested || activity.isFinishing()) {
            return;
        }
        scheduleAttach(0L);
    }

    void onDisplayModeChanged() {
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                applyDisplayModeLayout();
            }
        });
    }

    boolean dispatchCommand(final String command) {
        if (!requested || !ready || webView == null || TextUtils.isEmpty(command)) {
            return false;
        }

        final String quotedCommand = JSONObject.quote(command.trim().toLowerCase());
        evaluateJavascript(
                "(function(command){"
                        + "window.dispatchEvent(new CustomEvent('rayneo-remote-command',"
                        + "{detail:command}));"
                        + "var keys={up:'ArrowUp',down:'ArrowDown',left:'ArrowLeft',"
                        + "right:'ArrowRight',enter:'Enter',back:'Escape'};"
                        + "var key=keys[command];"
                        + "var target=document.activeElement||document.body;"
                        + "if(key&&target){target.dispatchEvent(new KeyboardEvent('keydown',"
                        + "{key:key,bubbles:true,cancelable:true}));}"
                        + "})("
                        + quotedCommand
                        + ");");
        return true;
    }

    void refreshBootstrapState() {
        if (ready) {
            pushBootstrapState();
        }
    }

    private void scheduleAttach(long delayMs) {
        if (!requested || activity.isFinishing() || activity.getWindow() == null) {
            return;
        }
        View decor = activity.getWindow().getDecorView();
        decor.removeCallbacks(attachRunnable);
        decor.postDelayed(attachRunnable, Math.max(0L, delayMs));
    }

    private void attachToGlassesPresentation() {
        if (!requested || activity.isFinishing()) {
            return;
        }

        View unityView = unityPlayer.getView();
        Display display = unityView == null ? null : unityView.getDisplay();
        boolean externalDisplay = display != null
                && display.isValid()
                && display.getDisplayId() != Display.DEFAULT_DISPLAY
                && display.getState() == Display.STATE_ON;
        ViewParent parent = unityView == null ? null : unityView.getParent();
        if (!externalDisplay || !(parent instanceof ViewGroup)) {
            if (webParent != null) {
                destroyWebView();
            }
            if (activity.isRayNeoDisplayConnected()) {
                scheduleAttach(PRESENTATION_ATTACH_RETRY_MS);
            }
            return;
        }

        ViewGroup targetParent = (ViewGroup) parent;
        if (webView == null || webParent != targetParent) {
            destroyWebView();
            createWebView(targetParent);
        }

        if (webView == null) {
            scheduleAttach(PRESENTATION_ATTACH_RETRY_MS);
            return;
        }
        webView.setVisibility(View.VISIBLE);
        applyDisplayModeLayout();
        if (TextUtils.isEmpty(webView.getUrl())) {
            webView.loadUrl(GLASSES_UI_URL);
        }
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void createWebView(ViewGroup targetParent) {
        Context context = targetParent.getContext();
        StereoMirrorLayout container = new StereoMirrorLayout(context);
        container.setBackgroundColor(Color.BLACK);
        container.setClipChildren(false);

        WebView created = new WebView(context);
        created.setBackgroundColor(Color.rgb(2, 7, 13));
        created.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        created.setSaveEnabled(false);
        created.setOverScrollMode(View.OVER_SCROLL_NEVER);
        created.setVerticalScrollBarEnabled(false);
        created.setHorizontalScrollBarEnabled(false);
        created.setFocusable(true);
        created.setFocusableInTouchMode(true);

        WebSettings settings = created.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(false);
        settings.setAllowFileAccessFromFileURLs(true);
        settings.setAllowUniversalAccessFromFileURLs(true);
        settings.setSupportMultipleWindows(false);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setTextZoom(100);
        settings.setDefaultTextEncodingName("utf-8");
        settings.setSaveFormData(false);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            created.setRendererPriorityPolicy(WebView.RENDERER_PRIORITY_IMPORTANT, false);
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.KITKAT
                && (activity.getApplicationInfo().flags
                        & ApplicationInfo.FLAG_DEBUGGABLE) != 0) {
            WebView.setWebContentsDebuggingEnabled(true);
        }

        created.addJavascriptInterface(new GlassesJavascriptBridge(), "RayNeoGlasses");
        created.setWebChromeClient(new WebChromeClient());
        created.setWebViewClient(new WebViewClient() {
            @Override
            public boolean shouldOverrideUrlLoading(
                    WebView view,
                    WebResourceRequest request) {
                return request == null
                        || request.getUrl() == null
                        || !isGlassesAssetUrl(request.getUrl().toString());
            }

            @SuppressWarnings("deprecation")
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url) {
                return !isGlassesAssetUrl(url);
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                if (!isGlassesAssetUrl(url)) {
                    return;
                }
                ready = true;
                pushBootstrapState();
            }

            @Override
            public void onReceivedError(
                    WebView view,
                    WebResourceRequest request,
                    WebResourceError error) {
                super.onReceivedError(view, request, error);
                if (request != null && request.isForMainFrame()) {
                    ready = false;
                    view.setVisibility(View.GONE);
                }
            }

            @Override
            public boolean onRenderProcessGone(
                    WebView view,
                    RenderProcessGoneDetail detail) {
                ready = false;
                destroyWebView();
                if (requested) {
                    scheduleAttach(PRESENTATION_ATTACH_RETRY_MS);
                }
                return true;
            }
        });

        webParent = targetParent;
        webContainer = container;
        webView = created;
        container.addView(
                created,
                new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT,
                        ViewGroup.LayoutParams.MATCH_PARENT));
        targetParent.addView(
                container,
                new FrameLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT,
                        ViewGroup.LayoutParams.MATCH_PARENT));
        applyDisplayModeLayout();
        created.loadUrl(GLASSES_UI_URL);
    }

    private void applyDisplayModeLayout() {
        StereoMirrorLayout container = webContainer;
        if (container == null) {
            return;
        }

        boolean transitioning = activity.isRayNeoDisplayModeTransitioning();
        container.setVisibility(requested && !transitioning ? View.VISIBLE : View.INVISIBLE);
        container.setStereo(
                !transitioning && activity.isRayNeoStereoDisplayActive());
        if (requested && !transitioning) {
            container.bringToFront();
        }
    }

    private boolean isGlassesAssetUrl(String url) {
        return !TextUtils.isEmpty(url) && url.startsWith(GLASSES_UI_ROOT);
    }

    private void evaluateJavascript(final String script) {
        if (TextUtils.isEmpty(script)) {
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (requested && ready && webView != null) {
                    webView.evaluateJavascript(script, null);
                }
            }
        });
    }

    private void pushBootstrapState() {
        final String payload = JSONObject.quote(buildBootstrapState().toString());
        evaluateJavascript(
                "window.LucentNative && window.LucentNative.receiveBootstrapState && "
                        + "window.LucentNative.receiveBootstrapState("
                        + payload
                        + ");");
    }

    private JSONObject buildBootstrapState() {
        JSONObject state = new JSONObject();
        try {
            state.put("source", "android");
            state.put("displayMode", activity.getRayNeoDisplayMode());
            state.put("glassesConnected", activity.isRayNeoDisplayConnected());
            String session = activity.getPendingSessionJson();
            if (TextUtils.isEmpty(session)) {
                state.put("session", JSONObject.NULL);
            } else {
                try {
                    state.put("session", new JSONObject(session));
                } catch (Exception ignored) {
                    state.put("session", JSONObject.NULL);
                }
            }
        } catch (Exception ignored) {
        }
        return state;
    }

    private void destroyWebView() {
        ready = false;
        WebView current = webView;
        StereoMirrorLayout container = webContainer;
        ViewGroup parent = webParent;
        webView = null;
        webContainer = null;
        webParent = null;
        if (current == null) {
            return;
        }
        current.removeJavascriptInterface("RayNeoGlasses");
        current.stopLoading();
        if (container != null) {
            container.removeView(current);
        }
        if (parent != null && container != null) {
            parent.removeView(container);
        }
        current.destroy();
    }

    private static final class StereoMirrorLayout extends FrameLayout {
        private boolean stereo;
        private final Runnable stereoInvalidator = new Runnable() {
            @Override
            public void run() {
                if (!stereo
                        || getVisibility() != View.VISIBLE
                        || !isAttachedToWindow()) {
                    return;
                }
                invalidate();
                postOnAnimation(this);
            }
        };

        StereoMirrorLayout(Context context) {
            super(context);
            setWillNotDraw(false);
        }

        void setStereo(boolean enabled) {
            if (stereo == enabled) {
                removeCallbacks(stereoInvalidator);
                if (stereo
                        && getVisibility() == View.VISIBLE
                        && isAttachedToWindow()) {
                    postOnAnimation(stereoInvalidator);
                }
                return;
            }
            stereo = enabled;
            removeCallbacks(stereoInvalidator);
            requestLayout();
            invalidate();
            if (stereo && isAttachedToWindow()) {
                postOnAnimation(stereoInvalidator);
            }
        }

        @Override
        protected void onAttachedToWindow() {
            super.onAttachedToWindow();
            if (stereo) {
                postOnAnimation(stereoInvalidator);
            }
        }

        @Override
        protected void onDetachedFromWindow() {
            removeCallbacks(stereoInvalidator);
            super.onDetachedFromWindow();
        }

        @Override
        protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec) {
            super.onMeasure(widthMeasureSpec, heightMeasureSpec);
            int childWidth = stereo
                    ? Math.max(1, getMeasuredWidth() / 2)
                    : getMeasuredWidth();
            int childHeight = getMeasuredHeight();
            int childWidthSpec = MeasureSpec.makeMeasureSpec(
                    childWidth,
                    MeasureSpec.EXACTLY);
            int childHeightSpec = MeasureSpec.makeMeasureSpec(
                    childHeight,
                    MeasureSpec.EXACTLY);
            for (int index = 0; index < getChildCount(); index++) {
                getChildAt(index).measure(childWidthSpec, childHeightSpec);
            }
        }

        @Override
        protected void onLayout(
                boolean changed,
                int left,
                int top,
                int right,
                int bottom) {
            int childWidth = stereo
                    ? Math.max(1, (right - left) / 2)
                    : right - left;
            int childHeight = bottom - top;
            for (int index = 0; index < getChildCount(); index++) {
                getChildAt(index).layout(0, 0, childWidth, childHeight);
            }
        }

        @Override
        protected void dispatchDraw(Canvas canvas) {
            if (!stereo || getChildCount() == 0) {
                super.dispatchDraw(canvas);
                return;
            }

            View child = getChildAt(0);
            int eyeWidth = Math.max(1, getWidth() / 2);
            int height = getHeight();
            long drawingTime = getDrawingTime();

            int leftSave = canvas.save();
            canvas.clipRect(0, 0, eyeWidth, height);
            drawChild(canvas, child, drawingTime);
            canvas.restoreToCount(leftSave);

            int rightSave = canvas.save();
            canvas.translate(eyeWidth, 0f);
            canvas.clipRect(0, 0, eyeWidth, height);
            drawChild(canvas, child, drawingTime);
            canvas.restoreToCount(rightSave);
        }
    }

    private final class GlassesJavascriptBridge {
        @JavascriptInterface
        public String getBootstrapState() {
            return buildBootstrapState().toString();
        }

        @JavascriptInterface
        public String getHardwareVideoCodecs() {
            Set<String> codecs = new LinkedHashSet<>();
            try {
                MediaCodecInfo[] codecInfos = new MediaCodecList(
                        MediaCodecList.ALL_CODECS).getCodecInfos();
                for (MediaCodecInfo codecInfo : codecInfos) {
                    if (codecInfo == null
                            || codecInfo.isEncoder()
                            || !isHardwareAccelerated(codecInfo)) {
                        continue;
                    }
                    for (String mimeType : codecInfo.getSupportedTypes()) {
                        String codec = videoCodecForMimeType(mimeType);
                        if (!TextUtils.isEmpty(codec)) {
                            codecs.add(codec);
                        }
                    }
                }
            } catch (RuntimeException ignored) {
                codecs.clear();
            }

            JSONArray result = new JSONArray();
            for (String codec : codecs) {
                result.put(codec);
            }
            return result.toString();
        }

        @JavascriptInterface
        public void ready() {
            activity.runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    ready = true;
                    pushBootstrapState();
                }
            });
        }

        @JavascriptInterface
        public void postMessage(String message) {
            if (TextUtils.isEmpty(message)) {
                return;
            }
            UnityPlayer.UnitySendMessage(
                    UNITY_RECEIVER,
                    "OnGlassesWebMessage",
                    message);
        }
    }

    private static boolean isHardwareAccelerated(MediaCodecInfo codecInfo) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            return codecInfo.isHardwareAccelerated();
        }

        String name = codecInfo.getName().toLowerCase(Locale.ROOT);
        return !name.startsWith("omx.google.")
                && !name.startsWith("c2.android.")
                && !name.startsWith("c2.google.")
                && !name.contains(".sw.")
                && !name.endsWith(".sw");
    }

    private static String videoCodecForMimeType(String mimeType) {
        if (mimeType == null) {
            return "";
        }
        switch (mimeType.toLowerCase(Locale.ROOT)) {
            case "video/avc":
                return "h264";
            case "video/hevc":
                return "hevc";
            case "video/x-vnd.on2.vp8":
                return "vp8";
            case "video/x-vnd.on2.vp9":
                return "vp9";
            case "video/av01":
                return "av1";
            default:
                return "";
        }
    }
}
