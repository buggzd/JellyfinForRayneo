package com.jellyfinforrayneo.client;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.graphics.Color;
import android.view.HapticFeedbackConstants;
import android.view.View;
import android.view.ViewGroup;
import android.view.inputmethod.InputMethodManager;
import android.webkit.RenderProcessGoneDetail;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;

import org.json.JSONObject;

final class CompanionWebViewController
{
    private static final String COMPANION_URL = WebNavigationPolicy.COMPANION_ROOT + "index.html";
    private static final long RECREATE_DELAY_MS = 400L;

    interface StateProvider
    {
        String buildStateJson();
    }

    interface JavascriptBridge
    {
        @android.webkit.JavascriptInterface
        String getState();

        @android.webkit.JavascriptInterface
        void ready();

        @android.webkit.JavascriptInterface
        void scan();

        @android.webkit.JavascriptInterface
        void selectServer(String serverUrl, String serverName);

        @android.webkit.JavascriptInterface
        void login(String serverUrl, String username, String password, boolean rememberSession);

        @android.webkit.JavascriptInterface
        void startQuickConnect(String serverUrl);

        @android.webkit.JavascriptInterface
        void cancelQuickConnect();

        @android.webkit.JavascriptInterface
        void clearSession();

        @android.webkit.JavascriptInterface
        void retryGlasses();

        @android.webkit.JavascriptInterface
        void shareDiagnostics();

        @android.webkit.JavascriptInterface
        void selectDisplayMode(String mode);

        @android.webkit.JavascriptInterface
        void copyQuickConnectCode();

        @android.webkit.JavascriptInterface
        void openQuickConnectAuthorization();

        @android.webkit.JavascriptInterface
        void remoteCommand(String value, boolean haptic);

        @android.webkit.JavascriptInterface
        void searchText(String value);

        @android.webkit.JavascriptInterface
        void previewHaptic();

        @android.webkit.JavascriptInterface
        void screenChanged(String value);
    }

    private final Activity activity;
    private final JavascriptBridge javascriptBridge;
    private final StateProvider stateProvider;
    private final FrameLayout root;
    private final Runnable recreate = new Runnable()
    {
        @Override
        public void run()
        {
            if (!destroyed && !activity.isFinishing())
            {
                createWebView();
            }
        }
    };

    private WebView webView;
    private boolean javascriptReady;
    private boolean destroyed;
    private String lastState;

    CompanionWebViewController(
            Activity activity,
            JavascriptBridge javascriptBridge,
            StateProvider stateProvider)
    {
        this.activity = activity;
        this.javascriptBridge = javascriptBridge;
        this.stateProvider = stateProvider;
        root = new FrameLayout(activity);
        root.setBackgroundColor(Color.BLACK);
    }

    View getView()
    {
        return root;
    }

    void start()
    {
        destroyed = false;
        createWebView();
    }

    void onJavascriptReady()
    {
        javascriptReady = true;
        lastState = null;
        pushState();
    }

    void pushState()
    {
        if (!javascriptReady || webView == null)
        {
            return;
        }
        String state = stateProvider.buildStateJson();
        if (state.equals(lastState))
        {
            return;
        }
        lastState = state;
        evaluateJavascript(
                "window.LumaNative && window.LumaNative.receiveState && "
                        + "window.LumaNative.receiveState("
                        + JSONObject.quote(state)
                        + ");");
    }

    void openScreen(String screen)
    {
        String safeScreen = "settings".equals(screen) ? "settings" : "connect";
        evaluateJavascript(
                "window.LumaNative && window.LumaNative.openScreen && "
                        + "window.LumaNative.openScreen("
                        + JSONObject.quote(safeScreen)
                        + ");");
    }

    void handleBack()
    {
        evaluateJavascript(
                "window.LumaNative && window.LumaNative.handleBack && "
                        + "window.LumaNative.handleBack();");
    }

    void haptic(boolean strong)
    {
        WebView current = webView;
        if (current != null)
        {
            current.performHapticFeedback(strong
                    ? HapticFeedbackConstants.LONG_PRESS
                    : HapticFeedbackConstants.KEYBOARD_TAP);
        }
    }

    void showSearchKeyboard()
    {
        WebView current = webView;
        if (current == null)
        {
            return;
        }
        current.requestFocus();
        current.postDelayed(() ->
        {
            if (current != webView || destroyed)
            {
                return;
            }
            InputMethodManager keyboard = (InputMethodManager) activity.getSystemService(
                    Context.INPUT_METHOD_SERVICE);
            if (keyboard != null)
            {
                keyboard.showSoftInput(current, InputMethodManager.SHOW_IMPLICIT);
            }
        }, 240L);
    }

    void hideSearchKeyboard()
    {
        WebView current = webView;
        if (current == null)
        {
            return;
        }
        InputMethodManager keyboard = (InputMethodManager) activity.getSystemService(
                Context.INPUT_METHOD_SERVICE);
        if (keyboard != null)
        {
            keyboard.hideSoftInputFromWindow(current.getWindowToken(), 0);
        }
    }

    boolean isHardwareAccelerated()
    {
        return webView != null && webView.isHardwareAccelerated();
    }

    void destroy()
    {
        hideSearchKeyboard();
        destroyed = true;
        root.removeCallbacks(recreate);
        destroyWebView();
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void createWebView()
    {
        destroyWebView();
        webView = new WebView(activity);
        webView.setBackgroundColor(Color.rgb(234, 247, 250));
        webView.setLayerType(View.LAYER_TYPE_HARDWARE, null);
        webView.setSaveEnabled(false);
        webView.setOverScrollMode(View.OVER_SCROLL_NEVER);
        webView.setVerticalScrollBarEnabled(false);
        webView.setHorizontalScrollBarEnabled(false);
        webView.setFocusable(true);
        webView.setFocusableInTouchMode(true);
        webView.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO_EXCLUDE_DESCENDANTS);

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(false);
        settings.setAllowFileAccessFromFileURLs(true);
        settings.setAllowUniversalAccessFromFileURLs(false);
        settings.setSupportMultipleWindows(false);
        settings.setBuiltInZoomControls(false);
        settings.setDisplayZoomControls(false);
        settings.setTextZoom(100);
        settings.setDefaultTextEncodingName("utf-8");
        settings.setSaveFormData(false);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);
        webView.setRendererPriorityPolicy(WebView.RENDERER_PRIORITY_IMPORTANT, false);

        if ((activity.getApplicationInfo().flags & ApplicationInfo.FLAG_DEBUGGABLE) != 0)
        {
            WebView.setWebContentsDebuggingEnabled(true);
        }

        webView.addJavascriptInterface(javascriptBridge, "JellyfinNative");
        webView.setWebViewClient(new WebViewClient()
        {
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, WebResourceRequest request)
            {
                return request == null
                        || request.getUrl() == null
                        || !WebNavigationPolicy.isCompanionAsset(request.getUrl().toString());
            }

            @SuppressWarnings("deprecation")
            @Override
            public boolean shouldOverrideUrlLoading(WebView view, String url)
            {
                return !WebNavigationPolicy.isCompanionAsset(url);
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
        root.addView(webView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));
        webView.loadUrl(COMPANION_URL);
    }

    private void scheduleRecreate()
    {
        javascriptReady = false;
        lastState = null;
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

    private void evaluateJavascript(String script)
    {
        WebView current = webView;
        if (javascriptReady && current != null && !destroyed)
        {
            current.post(() ->
            {
                if (current == webView && javascriptReady && !destroyed)
                {
                    current.evaluateJavascript(script, null);
                }
            });
        }
    }

    private void destroyWebView()
    {
        javascriptReady = false;
        lastState = null;
        WebView current = webView;
        webView = null;
        if (current != null)
        {
            current.removeJavascriptInterface("JellyfinNative");
            current.stopLoading();
            root.removeView(current);
            current.destroy();
        }
    }
}
