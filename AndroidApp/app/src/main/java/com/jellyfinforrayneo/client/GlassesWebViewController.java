package com.jellyfinforrayneo.client;

import android.annotation.SuppressLint;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.graphics.Canvas;
import android.graphics.Color;
import android.media.MediaCodecInfo;
import android.media.MediaCodecList;
import android.os.Build;
import android.text.TextUtils;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.JavascriptInterface;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.LinkedHashSet;
import java.util.Locale;
import java.util.Set;

final class GlassesWebViewController
{
    private static final String GLASSES_URL = WebNavigationPolicy.GLASSES_ROOT + "index.html";
    private static final long RECREATE_DELAY_MS = 400L;

    interface BootstrapProvider
    {
        JSONObject buildBootstrap();
    }

    interface Callback
    {
        void onReadyChanged(boolean ready);

        void onMessage(GlassesMessage message);
    }

    private final FrameLayout root;
    private final BootstrapProvider bootstrapProvider;
    private final Callback callback;
    private final Runnable recreate = new Runnable()
    {
        @Override
        public void run()
        {
            if (!destroyed && root.isAttachedToWindow())
            {
                createWebView();
            }
        }
    };

    private StereoMirrorLayout webContainer;
    private View blackTransition;
    private WebView webView;
    private DisplayModeStateMachine.State displayState;
    private String lastBootstrap;
    private boolean ready;
    private boolean destroyed;

    GlassesWebViewController(
            FrameLayout root,
            BootstrapProvider bootstrapProvider,
            Callback callback)
    {
        this.root = root;
        this.bootstrapProvider = bootstrapProvider;
        this.callback = callback;
    }

    void start(DisplayModeStateMachine.State state)
    {
        displayState = state;
        destroyed = false;
        createWebView();
    }

    void setDisplayState(DisplayModeStateMachine.State state)
    {
        displayState = state;
        applyDisplayState();
        refreshBootstrap();
    }

    boolean dispatchCommand(String command)
    {
        if (!ready || webView == null || command == null || command.length() > 32)
        {
            return false;
        }
        String quoted = JSONObject.quote(command);
        evaluateJavascript(
                "(function(command){"
                        + "window.dispatchEvent(new CustomEvent('rayneo-remote-command',"
                        + "{detail:command}));"
                        + "var keys={up:'ArrowUp',down:'ArrowDown',left:'ArrowLeft',"
                        + "right:'ArrowRight',enter:'Enter',back:'Escape'};"
                        + "var key=keys[command];"
                        + "var target=document.activeElement instanceof Element"
                        + "?document.activeElement:document.body;"
                        + "if(key&&target instanceof Element){target.dispatchEvent("
                        + "new KeyboardEvent('keydown',{key:key,bubbles:true,cancelable:true}));}"
                        + "})(" + quoted + ");");
        return true;
    }

    void refreshBootstrap()
    {
        if (!ready)
        {
            return;
        }
        String state = bootstrapProvider.buildBootstrap().toString();
        if (state.equals(lastBootstrap))
        {
            return;
        }
        lastBootstrap = state;
        evaluateJavascript(
                "window.LucentNative && window.LucentNative.receiveBootstrapState && "
                        + "window.LucentNative.receiveBootstrapState("
                        + JSONObject.quote(state)
                        + ");");
    }

    void destroy()
    {
        destroyed = true;
        root.removeCallbacks(recreate);
        destroyWebView();
        if (blackTransition != null)
        {
            root.removeView(blackTransition);
            blackTransition = null;
        }
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void createWebView()
    {
        destroyWebView();
        Context context = root.getContext();
        webContainer = new StereoMirrorLayout(context);
        webContainer.setBackgroundColor(Color.BLACK);
        webContainer.setClipChildren(false);

        webView = new WebView(context);
        webView.setBackgroundColor(Color.rgb(2, 7, 13));
        webView.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        webView.setSaveEnabled(false);
        webView.setOverScrollMode(View.OVER_SCROLL_NEVER);
        webView.setVerticalScrollBarEnabled(false);
        webView.setHorizontalScrollBarEnabled(false);
        webView.setFocusable(true);
        webView.setFocusableInTouchMode(true);

        WebSettings settings = webView.getSettings();
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
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        webView.setRendererPriorityPolicy(WebView.RENDERER_PRIORITY_IMPORTANT, false);

        if ((context.getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0)
        {
            WebView.setWebContentsDebuggingEnabled(true);
        }

        webView.addJavascriptInterface(new GlassesBridge(webView), "RayNeoGlasses");
        webView.setWebChromeClient(new WebChromeClient());
        webView.setWebViewClient(new WebViewClient()
        {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request)
            {
                return request == null
                        || request.getUrl() == null
                        || !WebNavigationPolicy.isGlassesAsset(request.getUrl().toString());
            }

            @SuppressWarnings("deprecation")
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url)
            {
                return !WebNavigationPolicy.isGlassesAsset(url);
            }

            @Override
            public void onReceivedError(
                    WebView view,
                    WebResourceRequest request,
                    WebResourceError error)
            {
                super.onReceivedError(view, request, error);
                if (request != null && request.isForMainFrame())
                {
                    scheduleRecreate();
                }
            }

            @Override
            public boolean onRenderProcessGone(WebView view, RenderProcessGoneDetail detail)
            {
                scheduleRecreate();
                return true;
            }
        });

        webContainer.addView(webView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));
        root.addView(webContainer, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));

        if (blackTransition == null)
        {
            blackTransition = new View(context);
            blackTransition.setBackgroundColor(Color.BLACK);
            root.addView(blackTransition, new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT));
        }
        applyDisplayState();
        webView.loadUrl(GLASSES_URL);
    }

    private void scheduleRecreate()
    {
        setReady(false);
        root.removeCallbacks(recreate);
        root.post(() ->
        {
            destroyWebView();
            if (!destroyed)
            {
                root.postDelayed(recreate, RECREATE_DELAY_MS);
            }
        });
    }

    private void applyDisplayState()
    {
        if (webContainer == null || blackTransition == null)
        {
            return;
        }
        boolean transitioning = displayState != null && displayState.displayModeTransitioning;
        boolean stereo = displayState != null
                && displayState.displayModeApplied
                && DisplayModeStateMachine.STEREO_SCREEN.equals(displayState.activeMode);
        webContainer.setStereo(!transitioning && stereo);
        webContainer.setVisibility(transitioning ? View.INVISIBLE : View.VISIBLE);
        blackTransition.setVisibility(transitioning ? View.VISIBLE : View.GONE);
        if (transitioning)
        {
            blackTransition.bringToFront();
        }
        else
        {
            webContainer.bringToFront();
        }
    }

    private void evaluateJavascript(String script)
    {
        WebView current = webView;
        if (current != null && !destroyed && !TextUtils.isEmpty(script))
        {
            current.post(() ->
            {
                if (current == webView && !destroyed)
                {
                    current.evaluateJavascript(script, null);
                }
            });
        }
    }

    private void setReady(boolean next)
    {
        if (ready == next)
        {
            return;
        }
        ready = next;
        callback.onReadyChanged(next);
    }

    private void destroyWebView()
    {
        setReady(false);
        lastBootstrap = null;
        WebView current = webView;
        StereoMirrorLayout container = webContainer;
        webView = null;
        webContainer = null;
        if (current != null)
        {
            current.removeJavascriptInterface("RayNeoGlasses");
            current.stopLoading();
            if (container != null)
            {
                container.removeView(current);
            }
            current.destroy();
        }
        if (container != null)
        {
            root.removeView(container);
        }
    }

    private final class GlassesBridge
    {
        private final WebView source;

        GlassesBridge(WebView source)
        {
            this.source = source;
        }

        @JavascriptInterface
        public String getBootstrapState()
        {
            return bootstrapProvider.buildBootstrap().toString();
        }

        @JavascriptInterface
        public String getHardwareVideoCodecs()
        {
            Set<String> codecs = new LinkedHashSet<>();
            try
            {
                MediaCodecInfo[] codecInfos = new MediaCodecList(
                        MediaCodecList.ALL_CODECS).getCodecInfos();
                for (MediaCodecInfo codecInfo : codecInfos)
                {
                    if (codecInfo == null
                            || codecInfo.isEncoder()
                            || !isHardwareAccelerated(codecInfo))
                    {
                        continue;
                    }
                    for (String mimeType : codecInfo.getSupportedTypes())
                    {
                        String codec = videoCodecForMimeType(mimeType);
                        if (!codec.isEmpty())
                        {
                            codecs.add(codec);
                        }
                    }
                }
            }
            catch (RuntimeException ignored)
            {
                codecs.clear();
            }

            JSONArray result = new JSONArray();
            for (String codec : codecs)
            {
                result.put(codec);
            }
            return result.toString();
        }

        @JavascriptInterface
        public void ready()
        {
            root.post(() ->
            {
                if (!destroyed && source == webView)
                {
                    setReady(true);
                    refreshBootstrap();
                }
            });
        }

        @JavascriptInterface
        public void postMessage(String payload)
        {
            GlassesMessage message = GlassesMessage.parse(payload);
            if (message != null)
            {
                root.post(() ->
                {
                    if (!destroyed && source == webView)
                    {
                        callback.onMessage(message);
                    }
                });
            }
        }
    }

    private static boolean isHardwareAccelerated(MediaCodecInfo codecInfo)
    {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q)
        {
            return codecInfo.isHardwareAccelerated();
        }
        String name = codecInfo.getName().toLowerCase(Locale.ROOT);
        return !name.startsWith("omx.google.")
                && !name.startsWith("c2.android.")
                && !name.startsWith("c2.google.")
                && !name.contains(".sw.")
                && !name.endsWith(".sw");
    }

    private static String videoCodecForMimeType(String mimeType)
    {
        if (mimeType == null)
        {
            return "";
        }
        switch (mimeType.toLowerCase(Locale.ROOT))
        {
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

    static final class StereoMirrorLayout extends FrameLayout
    {
        private boolean stereo;
        private final Runnable invalidator = new Runnable()
        {
            @Override
            public void run()
            {
                if (!stereo || getVisibility() != View.VISIBLE || !isAttachedToWindow())
                {
                    return;
                }
                invalidate();
                postOnAnimation(this);
            }
        };

        StereoMirrorLayout(Context context)
        {
            super(context);
            setWillNotDraw(false);
        }

        void setStereo(boolean enabled)
        {
            stereo = enabled;
            removeCallbacks(invalidator);
            requestLayout();
            invalidate();
            if (stereo && getVisibility() == View.VISIBLE && isAttachedToWindow())
            {
                postOnAnimation(invalidator);
            }
        }

        @Override
        protected void onAttachedToWindow()
        {
            super.onAttachedToWindow();
            if (stereo)
            {
                postOnAnimation(invalidator);
            }
        }

        @Override
        protected void onDetachedFromWindow()
        {
            removeCallbacks(invalidator);
            super.onDetachedFromWindow();
        }

        @Override
        protected void onMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            super.onMeasure(widthMeasureSpec, heightMeasureSpec);
            int childWidth = stereo ? Math.max(1, getMeasuredWidth() / 2) : getMeasuredWidth();
            int childWidthSpec = MeasureSpec.makeMeasureSpec(childWidth, MeasureSpec.EXACTLY);
            int childHeightSpec = MeasureSpec.makeMeasureSpec(getMeasuredHeight(), MeasureSpec.EXACTLY);
            for (int index = 0; index < getChildCount(); index++)
            {
                getChildAt(index).measure(childWidthSpec, childHeightSpec);
            }
        }

        @Override
        protected void onLayout(boolean changed, int left, int top, int right, int bottom)
        {
            int childWidth = stereo ? Math.max(1, (right - left) / 2) : right - left;
            int childHeight = bottom - top;
            for (int index = 0; index < getChildCount(); index++)
            {
                getChildAt(index).layout(0, 0, childWidth, childHeight);
            }
        }

        @Override
        protected void dispatchDraw(Canvas canvas)
        {
            if (!stereo || getChildCount() == 0)
            {
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
}
