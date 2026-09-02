package com.jellyfinforrayneo.companion;

import android.app.Presentation;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.content.res.ColorStateList;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Typeface;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.hardware.display.DisplayManager;
import android.net.Uri;
import android.net.wifi.WifiManager;
import android.os.Build;
import android.os.Bundle;
import android.os.SystemClock;
import android.text.InputType;
import android.text.TextUtils;
import android.view.Display;
import android.view.Gravity;
import android.view.HapticFeedbackConstants;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.view.ViewParent;
import android.view.WindowManager;
import android.view.inputmethod.EditorInfo;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import com.tcl.unity.unityadapter.UnityXRSupportActivity;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.ConnectException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.InterfaceAddress;
import java.net.NetworkInterface;
import java.net.SocketTimeoutException;
import java.net.URI;
import java.net.URL;
import java.net.UnknownHostException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.Enumeration;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.UUID;

import javax.net.ssl.SSLException;

public final class JellyfinRayNeoActivity extends UnityXRSupportActivity {
    private static final int DISCOVERY_PORT = 7359;
    private static final int DISCOVERY_DURATION_MS = 3000;
    private static final int HTTP_TIMEOUT_MS = 15000;
    private static final int QUICK_CONNECT_TIMEOUT_MS = 300000;
    private static final int QUICK_CONNECT_POLL_MS = 1500;
    private static final long PRESENTATION_FALLBACK_DELAY_MS = 2600L;
    private static final int MAX_REMOTE_COMMANDS = 32;
    private static final long DOUBLE_TAP_WINDOW_MS = 280L;
    private static final long DISPLAY_MODE_LONG_PRESS_MS = 550L;
    private static final byte[] DISCOVERY_MESSAGE =
            "who is JellyfinServer?".getBytes(StandardCharsets.UTF_8);

    private static final String PREFS_NAME = "jellyfin_companion";
    private static final String PREF_DEVICE_ID = "device_id";
    private static final String PREF_SERVER_URL = "server_url";
    private static final String PREF_USERNAME = "username";
    private static final String PREF_SESSION_JSON = "session_json";
    private static final String PREF_DISPLAY_MODE = "display_mode";
    private static final String DISPLAY_MODE_MIRROR_2D = "mirror_2d";
    private static final String DISPLAY_MODE_STEREO_SCREEN = "stereo_screen";

    private static final int COLOR_BACKGROUND_TOP = Color.rgb(17, 17, 20);
    private static final int COLOR_BACKGROUND_BOTTOM = Color.rgb(9, 10, 12);
    private static final int COLOR_SURFACE = Color.rgb(28, 29, 33);
    private static final int COLOR_SURFACE_SOFT = Color.rgb(35, 36, 41);
    private static final int COLOR_FIELD = Color.rgb(20, 21, 24);
    private static final int COLOR_BORDER = Color.rgb(55, 57, 64);
    private static final int COLOR_PRIMARY = Color.rgb(248, 248, 250);
    private static final int COLOR_SECONDARY = Color.rgb(183, 184, 191);
    private static final int COLOR_TERTIARY = Color.rgb(128, 130, 139);
    private static final int COLOR_ACCENT = Color.rgb(93, 224, 210);
    private static final int COLOR_ACCENT_END = Color.rgb(171, 143, 255);
    private static final int COLOR_ACCENT_BRIGHT = Color.rgb(115, 232, 220);
    private static final int COLOR_SUCCESS = Color.rgb(105, 226, 174);
    private static final int COLOR_ERROR = Color.rgb(255, 126, 151);

    private FrameLayout companionOverlay;
    private ScrollView configurationScrollView;
    private TouchpadView touchpadView;
    private EditText serverInput;
    private EditText usernameInput;
    private EditText passwordInput;
    private Button discoverButton;
    private Button quickConnectButton;
    private Button connectButton;
    private LinearLayout discoveredServersContainer;
    private LinearLayout quickConnectPanel;
    private LinearLayout loginForm;
    private LinearLayout sessionPanel;
    private LinearLayout manualLoginContainer;
    private LinearLayout glassesConnectionCard;
    private TextView discoveryStatusText;
    private TextView quickConnectCodeText;
    private TextView statusText;
    private TextView connectionBadge;
    private TextView glassesStatusText;
    private TextView glassesDescriptionText;
    private TextView glassesActionHint;
    private Button mirror2DModeButton;
    private Button stereoScreenModeButton;
    private TextView displayModeDescriptionText;
    private GlassesIconView glassesIconView;
    private TextView sessionTitleText;
    private TextView sessionDetailText;

    private String latestState = "login_required";
    private String latestMessage = "Jellyfin 配置可先完成；浏览和播放需要连接 RayNeo Air。";
    private boolean latestIsError;
    private String latestServerUrl = "";
    private String latestUsername = "";
    private String latestQuickConnectCode = "";
    private String requestedDisplayMode = DISPLAY_MODE_MIRROR_2D;
    private String activeDisplayMode = DISPLAY_MODE_MIRROR_2D;
    private boolean requestedDisplayModeApplied;
    private String displayModeMessage = "默认使用镜像 2D，连接眼镜后自动应用。";
    private boolean automaticDiscoveryStarted;
    private volatile boolean nativeOperationRunning;
    private volatile int authenticationGeneration;
    private volatile int discoveryGeneration;
    private volatile DatagramSocket discoverySocket;
    private DisplayManager companionDisplayManager;
    private boolean glassesConnected;
    private boolean glassesPresentationReady;
    private long glassesDetectedAtMs;
    private long fallbackPresentationStartedAtMs;
    private CompanionUnityPresentation fallbackPresentation;
    private final ArrayDeque<String> remoteCommands = new ArrayDeque<>();
    private final DisplayManager.DisplayListener companionDisplayListener =
            new DisplayManager.DisplayListener() {
                @Override
                public void onDisplayAdded(int displayId) {
                    refreshGlassesConnectionState();
                }

                @Override
                public void onDisplayRemoved(int displayId) {
                    refreshGlassesConnectionState();
                }

                @Override
                public void onDisplayChanged(int displayId) {
                    refreshGlassesConnectionState();
                }
            };
    private final Runnable presentationProbe = new Runnable() {
        @Override
        public void run() {
            refreshGlassesConnectionState();
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().clearFlags(WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        getWindow().setStatusBarColor(COLOR_BACKGROUND_TOP);
        getWindow().setNavigationBarColor(COLOR_BACKGROUND_BOTTOM);
        companionDisplayManager =
                (DisplayManager) getSystemService(Context.DISPLAY_SERVICE);
        if (companionDisplayManager != null) {
            companionDisplayManager.registerDisplayListener(companionDisplayListener, null);
        }
        glassesConnected = hasConnectedRayNeoDisplay();
        glassesDetectedAtMs = glassesConnected ? SystemClock.uptimeMillis() : 0L;
        glassesPresentationReady = isUnityPresentationActive();
        restoreNativeState();
        installCompanionUi();
        schedulePresentationProbe();
    }

    @Override
    protected void onResume() {
        super.onResume();
        getWindow().clearFlags(WindowManager.LayoutParams.FLAG_ALT_FOCUSABLE_IM);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        refreshGlassesConnectionState();
    }

    @Override
    public void onDestroy() {
        authenticationGeneration++;
        nativeOperationRunning = false;
        cancelDiscovery();
        if (companionOverlay != null) {
            companionOverlay.removeCallbacks(presentationProbe);
        }
        if (companionDisplayManager != null) {
            companionDisplayManager.unregisterDisplayListener(companionDisplayListener);
            companionDisplayManager = null;
        }
        dismissFallbackPresentation();
        super.onDestroy();
    }

    @Override
    public void onBackPressed() {
        if (touchpadView != null && touchpadView.getVisibility() == View.VISIBLE) {
            enqueueRemoteCommand("back");
            touchpadView.performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
            return;
        }
        super.onBackPressed();
    }

    public void setGlassesPresentationState(final boolean ready) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                glassesPresentationReady = ready;
                if (ready) {
                    glassesConnected = true;
                    if (glassesDetectedAtMs == 0L) {
                        glassesDetectedAtMs = SystemClock.uptimeMillis();
                    }
                } else {
                    glassesConnected = hasConnectedRayNeoDisplay();
                    if (glassesConnected) {
                        glassesDetectedAtMs = SystemClock.uptimeMillis();
                    } else {
                        glassesDetectedAtMs = 0L;
                        dismissFallbackPresentation();
                    }
                }
                if (companionOverlay != null && !isFinishing()) {
                    applyCompanionState();
                    schedulePresentationProbe();
                }
            }
        });
    }

    public void setCompanionState(
            final String state,
            final String message,
            final boolean isError,
            final String serverUrl,
            final String username,
            final String quickConnectCode) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                String incomingState = TextUtils.isEmpty(state) ? "offline" : state;
                if (nativeOperationRunning && !"ready".equals(incomingState)) {
                    return;
                }

                boolean hasNativeSession = hasNativeSession();
                if (("initializing".equals(incomingState)
                        || "offline".equals(incomingState))
                        && !hasNativeSession) {
                    incomingState = "login_required";
                    latestMessage = "尚未检测到 RayNeo Air，请连接眼镜；你可以先填写 Jellyfin 信息。";
                } else if (("initializing".equals(incomingState)
                        || "offline".equals(incomingState)
                        || "login_required".equals(incomingState))
                        && hasNativeSession) {
                    incomingState = "session_ready";
                    latestMessage = isError && !TextUtils.isEmpty(message)
                            ? message
                            : "Jellyfin 配置已保存。连接 RayNeo Air 后会自动同步媒体库。";
                } else {
                    latestMessage = message == null ? "" : message;
                }

                latestState = incomingState;
                latestIsError = isError;
                if (!TextUtils.isEmpty(serverUrl)) {
                    latestServerUrl = serverUrl;
                }
                if (username != null) {
                    latestUsername = username;
                }
                latestQuickConnectCode = quickConnectCode == null
                        ? ""
                        : quickConnectCode.trim();
                applyCompanionState();
            }
        });
    }

    private void installCompanionUi() {
        ViewGroup content = findViewById(android.R.id.content);
        if (content == null) {
            return;
        }

        companionOverlay = new FrameLayout(this);
        companionOverlay.setBackground(backgroundGradient());
        companionOverlay.setClickable(true);
        companionOverlay.setFocusable(true);
        companionOverlay.setElevation(dp(24));

        configurationScrollView = new ScrollView(this);
        configurationScrollView.setFillViewport(true);
        configurationScrollView.setClipToPadding(false);
        configurationScrollView.setOverScrollMode(View.OVER_SCROLL_NEVER);

        LinearLayout page = new LinearLayout(this);
        page.setOrientation(LinearLayout.VERTICAL);
        page.setGravity(Gravity.START);
        page.setPadding(dp(22), dp(24), dp(22), dp(40));

        LinearLayout brandRow = new LinearLayout(this);
        brandRow.setOrientation(LinearLayout.HORIZONTAL);
        brandRow.setGravity(Gravity.CENTER_VERTICAL);
        page.addView(brandRow, matchWrap(dp(28)));

        TextView brand = createText(
                "J",
                18,
                Color.rgb(12, 20, 22),
                Typeface.BOLD,
                Gravity.CENTER);
        brand.setIncludeFontPadding(false);
        brand.setBackground(accentGradient(13));
        brandRow.addView(brand, new LinearLayout.LayoutParams(dp(42), dp(42)));

        LinearLayout brandIdentity = new LinearLayout(this);
        brandIdentity.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams brandIdentityParams = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT,
                ViewGroup.LayoutParams.WRAP_CONTENT);
        brandIdentityParams.leftMargin = dp(12);
        brandRow.addView(brandIdentity, brandIdentityParams);

        TextView appName = createText(
                "Jellyfin for RayNeo",
                14,
                COLOR_PRIMARY,
                Typeface.BOLD,
                Gravity.START);
        brandIdentity.addView(appName, matchWrap(dp(2)));

        TextView companionLabel = createText(
                "手机伴侣",
                11,
                COLOR_TERTIARY,
                Typeface.NORMAL,
                Gravity.START);
        brandIdentity.addView(companionLabel, matchWrap(0));

        View brandSpacer = new View(this);
        brandRow.addView(brandSpacer, new LinearLayout.LayoutParams(
                0,
                1,
                1f));

        connectionBadge = createText(
                "等待眼镜",
                12,
                COLOR_SECONDARY,
                Typeface.BOLD,
                Gravity.CENTER);
        connectionBadge.setIncludeFontPadding(false);
        connectionBadge.setPadding(dp(11), dp(8), dp(11), dp(8));
        connectionBadge.setBackground(statusChipBackground(COLOR_SURFACE_SOFT, COLOR_BORDER));
        brandRow.addView(connectionBadge, wrapWrap());

        TextView title = createText(
                "连接与配置",
                30,
                COLOR_PRIMARY,
                Typeface.BOLD,
                Gravity.START);
        title.setLetterSpacing(-0.015f);
        page.addView(title, matchWrap(dp(8)));

        TextView subtitle = createText(
                "在手机上发现并登录 Jellyfin。海报墙和视频播放会在眼镜中自动打开。",
                14,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.START);
        subtitle.setLineSpacing(dp(2), 1f);
        page.addView(subtitle, matchWrap(dp(22)));

        glassesConnectionCard = createGlassesConnectionCard();
        page.addView(glassesConnectionCard, matchWrap(dp(16)));

        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(18), dp(20), dp(18), dp(18));
        card.setBackground(roundedWithStroke(COLOR_SURFACE, COLOR_BORDER, 22));
        page.addView(card, matchWrap(dp(16)));

        TextView configurationEyebrow = createText(
                "JELLYFIN",
                11,
                COLOR_ACCENT_BRIGHT,
                Typeface.BOLD,
                Gravity.START);
        configurationEyebrow.setLetterSpacing(0.12f);
        card.addView(configurationEyebrow, matchWrap(dp(7)));

        TextView configurationTitle = createText(
                "连接媒体服务器",
                21,
                COLOR_PRIMARY,
                Typeface.BOLD,
                Gravity.START);
        card.addView(configurationTitle, matchWrap(dp(6)));

        TextView configurationHint = createText(
                "仅服务器发现、登录和账户切换会留在手机端。",
                13,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.START);
        configurationHint.setLineSpacing(dp(1), 1f);
        card.addView(configurationHint, matchWrap(dp(18)));

        loginForm = new LinearLayout(this);
        loginForm.setOrientation(LinearLayout.VERTICAL);
        card.addView(loginForm, matchWrap(0));

        loginForm.addView(createLabel("服务器地址"), matchWrap(dp(8)));

        LinearLayout serverRow = new LinearLayout(this);
        serverRow.setOrientation(LinearLayout.HORIZONTAL);
        serverRow.setGravity(Gravity.CENTER_VERTICAL);
        loginForm.addView(serverRow, matchHeight(54, 8));

        serverInput = createInput("例如 192.168.1.20:8096");
        serverInput.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
        serverInput.setImeOptions(EditorInfo.IME_ACTION_NEXT);
        LinearLayout.LayoutParams serverInputParams = new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.MATCH_PARENT,
                1f);
        serverInputParams.rightMargin = dp(8);
        serverRow.addView(serverInput, serverInputParams);

        discoverButton = createButton("发现", COLOR_SURFACE_SOFT);
        discoverButton.setTextColor(COLOR_PRIMARY);
        discoverButton.setBackground(outlineButtonBackground(15));
        serverRow.addView(discoverButton, new LinearLayout.LayoutParams(
                dp(82),
                ViewGroup.LayoutParams.MATCH_PARENT));
        discoverButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                discoverServers();
            }
        });

        discoveryStatusText = createText(
                "",
                13,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.START);
        discoveryStatusText.setVisibility(View.GONE);
        loginForm.addView(discoveryStatusText, matchWrap(dp(6)));

        discoveredServersContainer = new LinearLayout(this);
        discoveredServersContainer.setOrientation(LinearLayout.VERTICAL);
        discoveredServersContainer.setVisibility(View.GONE);
        loginForm.addView(discoveredServersContainer, matchWrap(dp(10)));

        quickConnectButton = createButton("使用快速登录", COLOR_ACCENT);
        quickConnectButton.setTextColor(Color.rgb(11, 22, 23));
        quickConnectButton.setBackground(accentButtonBackground(16));
        quickConnectButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                submitQuickConnect();
            }
        });
        loginForm.addView(quickConnectButton, matchHeight(54, 7));

        TextView quickHint = createText(
                "推荐 · 无需在此输入密码",
                12,
                COLOR_TERTIARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        quickHint.setLineSpacing(0f, 1.12f);
        loginForm.addView(quickHint, matchWrap(dp(14)));

        quickConnectPanel = createQuickConnectPanel();
        quickConnectPanel.setVisibility(View.GONE);
        loginForm.addView(quickConnectPanel, matchWrap(dp(14)));

        manualLoginContainer = new LinearLayout(this);
        manualLoginContainer.setOrientation(LinearLayout.VERTICAL);
        loginForm.addView(manualLoginContainer, matchWrap(dp(2)));

        TextView alternative = createText(
                "账户密码",
                13,
                COLOR_SECONDARY,
                Typeface.BOLD,
                Gravity.START);
        manualLoginContainer.addView(alternative, matchWrap(dp(10)));

        usernameInput = createInput("用户名");
        usernameInput.setInputType(
                InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_NORMAL);
        usernameInput.setImeOptions(EditorInfo.IME_ACTION_NEXT);
        manualLoginContainer.addView(usernameInput, matchHeight(54, 10));

        passwordInput = createInput("密码（仅用于本次登录）");
        passwordInput.setInputType(
                InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        passwordInput.setImeOptions(EditorInfo.IME_ACTION_DONE);
        passwordInput.setSaveEnabled(false);
        passwordInput.setImportantForAutofill(
                View.IMPORTANT_FOR_AUTOFILL_NO_EXCLUDE_DESCENDANTS);
        manualLoginContainer.addView(passwordInput, matchHeight(54, 12));

        connectButton = createButton("登录 Jellyfin", COLOR_ACCENT);
        connectButton.setTextColor(COLOR_PRIMARY);
        connectButton.setBackground(outlineButtonBackground(16));
        connectButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                submitLogin();
            }
        });
        manualLoginContainer.addView(connectButton, matchHeight(54, 0));

        sessionPanel = createSessionPanel();
        sessionPanel.setVisibility(View.GONE);
        card.addView(sessionPanel, matchWrap(dp(12)));

        statusText = createText(
                latestMessage,
                13,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.START | Gravity.CENTER_VERTICAL);
        statusText.setLineSpacing(0f, 1.15f);
        statusText.setPadding(dp(14), dp(12), dp(14), dp(12));
        statusText.setBackground(rounded(COLOR_SURFACE_SOFT, 14));
        card.addView(statusText, matchWrap(0));

        TextView privacy = createText(
                "隐私提示  ·  密码不会保存，手机仅保留 Jellyfin 会话令牌。",
                12,
                COLOR_TERTIARY,
                Typeface.NORMAL,
                Gravity.START);
        privacy.setLineSpacing(0f, 1.18f);
        page.addView(privacy, matchWrap(0));

        int availableWidth = getResources().getDisplayMetrics().widthPixels;
        int pageWidth = Math.min(availableWidth, dp(560));
        ScrollView.LayoutParams pageParams = new ScrollView.LayoutParams(
                pageWidth,
                ViewGroup.LayoutParams.WRAP_CONTENT);
        pageParams.gravity = Gravity.CENTER_HORIZONTAL;
        configurationScrollView.addView(page, pageParams);
        companionOverlay.addView(configurationScrollView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));

        touchpadView = new TouchpadView(this);
        touchpadView.setVisibility(View.GONE);
        companionOverlay.addView(touchpadView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));
        content.addView(companionOverlay, new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));

        if (isUsableServerValue(latestServerUrl)) {
            serverInput.setText(latestServerUrl);
        }
        usernameInput.setText(latestUsername);
        applyCompanionState();
    }

    private LinearLayout createGlassesConnectionCard() {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setPadding(dp(18), dp(17), dp(18), dp(16));
        panel.setBackground(connectionCardBackground(false, false));

        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        panel.addView(header, matchWrap(dp(12)));

        glassesIconView = new GlassesIconView(this);
        glassesIconView.setBackground(rounded(Color.argb(24, 255, 255, 255), 16));
        header.addView(glassesIconView, new LinearLayout.LayoutParams(dp(54), dp(54)));

        LinearLayout identity = new LinearLayout(this);
        identity.setOrientation(LinearLayout.VERTICAL);
        LinearLayout.LayoutParams identityParams = new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.WRAP_CONTENT,
                1f);
        identityParams.leftMargin = dp(14);
        header.addView(identity, identityParams);

        TextView heading = createText(
                "RayNeo Air",
                19,
                COLOR_PRIMARY,
                Typeface.BOLD,
                Gravity.START);
        identity.addView(heading, matchWrap(dp(5)));

        glassesStatusText = createText(
                "未检测到眼镜",
                12,
                COLOR_ERROR,
                Typeface.BOLD,
                Gravity.START);
        identity.addView(glassesStatusText, matchWrap(0));

        glassesDescriptionText = createText(
                "连接眼镜后，应用会自动建立外接显示并把 Unity 画面送到眼镜。",
                13,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.START);
        glassesDescriptionText.setLineSpacing(dp(2), 1f);
        panel.addView(glassesDescriptionText, matchWrap(dp(12)));

        panel.addView(createDisplayModeSelector(), matchWrap(dp(12)));

        glassesActionHint = createText(
                "插入手机接口即可  ·  无需点击任何按钮",
                12,
                COLOR_TERTIARY,
                Typeface.NORMAL,
                Gravity.START | Gravity.CENTER_VERTICAL);
        glassesActionHint.setCompoundDrawablePadding(dp(7));
        panel.addView(glassesActionHint, matchWrap(0));
        return panel;
    }

    private LinearLayout createDisplayModeSelector() {
        LinearLayout selector = new LinearLayout(this);
        selector.setOrientation(LinearLayout.VERTICAL);
        selector.setPadding(dp(12), dp(12), dp(12), dp(11));
        selector.setBackground(roundedWithStroke(COLOR_FIELD, COLOR_BORDER, 16));

        TextView label = createText(
                "眼镜显示模式",
                12,
                COLOR_SECONDARY,
                Typeface.BOLD,
                Gravity.START);
        selector.addView(label, matchWrap(dp(9)));

        LinearLayout choices = new LinearLayout(this);
        choices.setOrientation(LinearLayout.HORIZONTAL);
        choices.setGravity(Gravity.CENTER_VERTICAL);
        selector.addView(choices, matchHeight(44, 9));

        mirror2DModeButton = createButton("镜像 2D", COLOR_SURFACE_SOFT);
        mirror2DModeButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                selectDisplayMode(DISPLAY_MODE_MIRROR_2D);
            }
        });
        LinearLayout.LayoutParams mirrorParams = new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.MATCH_PARENT,
                1f);
        mirrorParams.rightMargin = dp(7);
        choices.addView(mirror2DModeButton, mirrorParams);

        stereoScreenModeButton = createButton("立体屏幕", COLOR_SURFACE_SOFT);
        stereoScreenModeButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                selectDisplayMode(DISPLAY_MODE_STEREO_SCREEN);
            }
        });
        choices.addView(stereoScreenModeButton, new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.MATCH_PARENT,
                1f));

        displayModeDescriptionText = createText(
                "",
                12,
                COLOR_TERTIARY,
                Typeface.NORMAL,
                Gravity.START);
        displayModeDescriptionText.setLineSpacing(dp(1), 1f);
        selector.addView(displayModeDescriptionText, matchWrap(0));
        updateDisplayModeUi();
        return selector;
    }

    private void selectDisplayMode(String mode) {
        String normalized = normalizeDisplayMode(mode);
        if (normalized.equals(requestedDisplayMode) && requestedDisplayModeApplied) {
            return;
        }

        requestedDisplayMode = normalized;
        requestedDisplayModeApplied = false;
        displayModeMessage = glassesConnected
                ? "正在同步 Unity 双相机与眼镜硬件模式…"
                : (isStereoDisplayMode(normalized)
                        ? "已保存。连接眼镜后启用固定立体虚拟屏幕。"
                        : "已保存。连接眼镜后双眼显示同一幅完整画面。");
        getCompanionPreferences()
                .edit()
                .putString(PREF_DISPLAY_MODE, requestedDisplayMode)
                .apply();
        updateDisplayModeUi();
        if (touchpadView != null) {
            touchpadView.invalidate();
        }
    }

    private void toggleDisplayMode() {
        selectDisplayMode(isStereoDisplayMode(requestedDisplayMode)
                ? DISPLAY_MODE_MIRROR_2D
                : DISPLAY_MODE_STEREO_SCREEN);
    }

    private String normalizeDisplayMode(String mode) {
        if (DISPLAY_MODE_STEREO_SCREEN.equals(mode)
                || "stereo".equalsIgnoreCase(mode)
                || "3d".equalsIgnoreCase(mode)) {
            return DISPLAY_MODE_STEREO_SCREEN;
        }
        return DISPLAY_MODE_MIRROR_2D;
    }

    private boolean isStereoDisplayMode(String mode) {
        return DISPLAY_MODE_STEREO_SCREEN.equals(normalizeDisplayMode(mode));
    }

    private String displayModeLabel(String mode) {
        return isStereoDisplayMode(mode) ? "立体屏幕" : "镜像 2D";
    }

    private void updateDisplayModeUi() {
        boolean stereoSelected = isStereoDisplayMode(requestedDisplayMode);
        if (mirror2DModeButton != null) {
            mirror2DModeButton.setTextColor(stereoSelected
                    ? COLOR_SECONDARY
                    : Color.rgb(11, 22, 23));
            mirror2DModeButton.setBackground(stereoSelected
                    ? outlineButtonBackground(13)
                    : accentButtonBackground(13));
        }
        if (stereoScreenModeButton != null) {
            stereoScreenModeButton.setTextColor(stereoSelected
                    ? Color.rgb(11, 22, 23)
                    : COLOR_SECONDARY);
            stereoScreenModeButton.setBackground(stereoSelected
                    ? accentButtonBackground(13)
                    : outlineButtonBackground(13));
        }
        if (displayModeDescriptionText != null) {
            String explanation;
            if (!TextUtils.isEmpty(displayModeMessage)) {
                explanation = displayModeMessage;
            } else if (stereoSelected) {
                explanation = "左右眼各自渲染一个 16:9 视图，64 mm 固定视差；Air 3S 不使用头部追踪。";
            } else {
                explanation = "双眼接收同一幅完整 1920×1080 画面，不生成左右视差。";
            }
            displayModeDescriptionText.setText(explanation);
            displayModeDescriptionText.setTextColor(
                    requestedDisplayModeApplied ? COLOR_SUCCESS : COLOR_TERTIARY);
        }
    }

    private LinearLayout createSessionPanel() {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setGravity(Gravity.START);
        panel.setPadding(0, dp(2), 0, 0);

        TextView badge = createText(
                "JELLYFIN  ·  已连接",
                11,
                COLOR_SUCCESS,
                Typeface.BOLD,
                Gravity.START);
        badge.setLetterSpacing(0.08f);
        panel.addView(badge, matchWrap(dp(10)));

        sessionTitleText = createText(
                "Jellyfin 已配置",
                20,
                COLOR_PRIMARY,
                Typeface.BOLD,
                Gravity.START);
        panel.addView(sessionTitleText, matchWrap(dp(8)));

        sessionDetailText = createText(
                "连接 RayNeo Air 后，媒体库会自动同步到眼镜。",
                13,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.START);
        sessionDetailText.setLineSpacing(0f, 1.18f);
        panel.addView(sessionDetailText, matchWrap(dp(16)));

        Button switchAccountButton = createButton("更换账户", COLOR_SURFACE_SOFT);
        switchAccountButton.setTextColor(COLOR_SECONDARY);
        switchAccountButton.setBackground(outlineButtonBackground(14));
        switchAccountButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                clearNativeSession();
            }
        });
        panel.addView(switchAccountButton, matchHeight(48, 0));
        return panel;
    }

    private LinearLayout createQuickConnectPanel() {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setGravity(Gravity.CENTER_HORIZONTAL);
        panel.setPadding(dp(16), dp(16), dp(16), dp(16));
        panel.setBackground(roundedWithStroke(COLOR_FIELD, COLOR_BORDER, 16));

        TextView label = createText(
                "JELLYFIN 快速登录码",
                12,
                COLOR_ACCENT_BRIGHT,
                Typeface.BOLD,
                Gravity.CENTER);
        panel.addView(label, matchWrap(dp(6)));

        quickConnectCodeText = createText(
                "",
                32,
                COLOR_PRIMARY,
                Typeface.BOLD,
                Gravity.CENTER);
        quickConnectCodeText.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        quickConnectCodeText.setLetterSpacing(0.08f);
        quickConnectCodeText.setTextIsSelectable(true);
        panel.addView(quickConnectCodeText, matchWrap(dp(8)));

        TextView instructions = createText(
                "在已登录的 Jellyfin 中进入“快速连接”并确认此代码。",
                13,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        instructions.setLineSpacing(0f, 1.12f);
        panel.addView(instructions, matchWrap(dp(12)));

        LinearLayout actions = new LinearLayout(this);
        actions.setOrientation(LinearLayout.HORIZONTAL);
        actions.setGravity(Gravity.CENTER);
        panel.addView(actions, matchHeight(46, 8));

        Button copyButton = createButton("复制代码", COLOR_SURFACE);
        copyButton.setTextColor(COLOR_PRIMARY);
        copyButton.setBackground(outlineButtonBackground(14));
        copyButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                copyQuickConnectCode();
            }
        });
        LinearLayout.LayoutParams copyParams = new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.MATCH_PARENT,
                1f);
        copyParams.rightMargin = dp(8);
        actions.addView(copyButton, copyParams);

        Button openButton = createButton("打开授权页", COLOR_ACCENT);
        openButton.setTextColor(Color.rgb(11, 22, 23));
        openButton.setBackground(accentButtonBackground(14));
        openButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                openQuickConnectAuthorization();
            }
        });
        actions.addView(openButton, new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.MATCH_PARENT,
                1f));

        Button cancelButton = createButton("取消快速登录", COLOR_SURFACE);
        cancelButton.setTextColor(COLOR_TERTIARY);
        cancelButton.setBackground(transparentButtonBackground(14));
        cancelButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                cancelQuickConnect();
            }
        });
        panel.addView(cancelButton, matchHeight(44, 0));
        return panel;
    }

    private void submitLogin() {
        final String serverUrl = validatedServerUrl();
        if (serverUrl == null) {
            return;
        }

        final String username = usernameInput.getText().toString().trim();
        final String password = passwordInput.getText().toString();
        if (TextUtils.isEmpty(username)) {
            showLocalError("请输入 Jellyfin 用户名。");
            usernameInput.requestFocus();
            return;
        }

        final int generation = beginNativeOperation(
                "native_connecting",
                "正在验证服务器与账户…",
                serverUrl,
                username);
        passwordInput.getText().clear();
        hideKeyboard();

        Thread worker = new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    JSONObject session = authenticateByPassword(
                            serverUrl,
                            username,
                            password);
                    finishNativeAuthentication(generation, session);
                } catch (Exception exception) {
                    finishNativeAuthenticationError(generation, exception);
                }
            }
        }, "Jellyfin-Native-Login");
        worker.start();
    }

    private void submitQuickConnect() {
        final String serverUrl = validatedServerUrl();
        if (serverUrl == null) {
            return;
        }

        final int generation = beginNativeOperation(
                "native_connecting",
                "正在向 Jellyfin 申请快速登录码…",
                serverUrl,
                "");
        hideKeyboard();

        Thread worker = new Thread(new Runnable() {
            @Override
            public void run() {
                try {
                    runNativeQuickConnect(generation, serverUrl);
                } catch (Exception exception) {
                    finishNativeAuthenticationError(generation, exception);
                }
            }
        }, "Jellyfin-Native-QuickConnect");
        worker.start();
    }

    private void cancelQuickConnect() {
        authenticationGeneration++;
        nativeOperationRunning = false;
        latestState = "login_required";
        latestMessage = "已取消快速登录，你可以重新申请或使用账户密码。";
        latestIsError = false;
        latestQuickConnectCode = "";
        applyCompanionState();
    }

    private String validatedServerUrl() {
        String serverUrl = serverInput.getText().toString().trim();
        if (TextUtils.isEmpty(serverUrl)) {
            showLocalError("请先选择或输入 Jellyfin 服务器地址。");
            serverInput.requestFocus();
            return null;
        }
        try {
            serverUrl = normalizeServerUrl(serverUrl);
        } catch (Exception exception) {
            showLocalError("请输入有效的 Jellyfin 地址，例如 192.168.1.20:8096。");
            serverInput.requestFocus();
            return null;
        }
        serverInput.setText(serverUrl);
        serverInput.setSelection(serverInput.length());
        return serverUrl;
    }

    private int beginNativeOperation(
            String state,
            String message,
            String serverUrl,
            String username) {
        int generation = ++authenticationGeneration;
        nativeOperationRunning = true;
        latestState = state;
        latestMessage = message;
        latestIsError = false;
        latestServerUrl = serverUrl;
        latestUsername = username == null ? "" : username;
        latestQuickConnectCode = "";
        getCompanionPreferences().edit()
                .putString(PREF_SERVER_URL, latestServerUrl)
                .putString(PREF_USERNAME, latestUsername)
                .apply();
        applyCompanionState();
        return generation;
    }

    private JSONObject authenticateByPassword(
            String serverUrl,
            String username,
            String password) throws Exception {
        JSONObject publicInfo = requestJson(
                "GET",
                serverUrl + "/System/Info/Public",
                null);
        JSONObject request = new JSONObject();
        request.put("Username", username);
        request.put("Pw", password == null ? "" : password);
        JSONObject authentication = requestJson(
                "POST",
                serverUrl + "/Users/AuthenticateByName",
                request);
        return createSessionJson(serverUrl, publicInfo, authentication);
    }

    private void runNativeQuickConnect(int generation, String serverUrl) throws Exception {
        String enabled = requestText(
                "GET",
                serverUrl + "/QuickConnect/Enabled",
                null);
        if (!"true".equalsIgnoreCase(enabled.trim())) {
            throw new CompanionHttpException(
                    400,
                    "此 Jellyfin 服务器未启用快速连接，请使用账户密码登录。");
        }

        JSONObject publicInfo = requestJson(
                "GET",
                serverUrl + "/System/Info/Public",
                null);
        JSONObject quickConnect = requestJson(
                "POST",
                serverUrl + "/QuickConnect/Initiate",
                null);
        final String secret = quickConnect.optString("Secret", "").trim();
        final String code = quickConnect.optString("Code", "").trim();
        if (TextUtils.isEmpty(secret) || TextUtils.isEmpty(code)) {
            throw new CompanionHttpException(0, "服务器没有返回有效的快速登录码。");
        }

        postQuickConnectCode(generation, code);
        long deadline = SystemClock.elapsedRealtime() + QUICK_CONNECT_TIMEOUT_MS;
        boolean authenticated = quickConnect.optBoolean("Authenticated", false);
        while (generation == authenticationGeneration
                && !authenticated
                && SystemClock.elapsedRealtime() < deadline) {
            SystemClock.sleep(QUICK_CONNECT_POLL_MS);
            if (generation != authenticationGeneration) {
                return;
            }
            JSONObject state = requestJson(
                    "GET",
                    serverUrl + "/QuickConnect/Connect?secret=" + Uri.encode(secret),
                    null);
            authenticated = state.optBoolean("Authenticated", false);
        }

        if (generation != authenticationGeneration) {
            return;
        }
        if (!authenticated) {
            throw new CompanionHttpException(408, "快速登录码已过期，请重新申请。");
        }

        JSONObject request = new JSONObject();
        request.put("Secret", secret);
        JSONObject authentication = requestJson(
                "POST",
                serverUrl + "/Users/AuthenticateWithQuickConnect",
                request);
        JSONObject session = createSessionJson(serverUrl, publicInfo, authentication);
        finishNativeAuthentication(generation, session);
    }

    private void postQuickConnectCode(final int generation, final String code) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (generation != authenticationGeneration || isFinishing()) {
                    return;
                }
                latestState = "quick_connect_waiting";
                latestMessage = "请在 Jellyfin App 或网页中确认此登录码。";
                latestQuickConnectCode = code;
                latestIsError = false;
                applyCompanionState();
            }
        });
    }

    private JSONObject createSessionJson(
            String serverUrl,
            JSONObject publicInfo,
            JSONObject authentication) throws Exception {
        JSONObject user = authentication.optJSONObject("User");
        String accessToken = authentication.optString("AccessToken", "").trim();
        String userId = user == null ? "" : user.optString("Id", "").trim();
        if (TextUtils.isEmpty(accessToken) || TextUtils.isEmpty(userId)) {
            throw new CompanionHttpException(0, "服务器没有返回有效的 Jellyfin 会话。");
        }

        JSONObject session = new JSONObject();
        session.put("serverUrl", serverUrl);
        session.put("serverName", publicInfo.optString("ServerName", ""));
        session.put("serverVersion", publicInfo.optString("Version", ""));
        session.put(
                "serverId",
                firstNonEmpty(
                        authentication.optString("ServerId", ""),
                        publicInfo.optString("Id", "")));
        session.put("accessToken", accessToken);
        session.put("userId", userId);
        session.put("userName", user == null ? "" : user.optString("Name", ""));
        session.put("deviceId", getOrCreateDeviceId());
        session.put("createdAt", System.currentTimeMillis());
        return session;
    }

    private void finishNativeAuthentication(
            final int generation,
            final JSONObject session) {
        if (generation != authenticationGeneration || session == null) {
            return;
        }

        getCompanionPreferences().edit()
                .putString(PREF_SESSION_JSON, session.toString())
                .putString(PREF_SERVER_URL, session.optString("serverUrl", ""))
                .putString(PREF_USERNAME, session.optString("userName", ""))
                .apply();

        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (generation != authenticationGeneration || isFinishing()) {
                    return;
                }
                nativeOperationRunning = false;
                latestState = "session_ready";
                latestServerUrl = session.optString("serverUrl", "");
                latestUsername = session.optString("userName", "");
                latestQuickConnectCode = "";
                latestIsError = false;
                latestMessage = "Jellyfin 配置已保存。请连接 RayNeo Air 以浏览和播放。";
                applyCompanionState();
            }
        });
    }

    private void finishNativeAuthenticationError(
            final int generation,
            final Exception exception) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (generation != authenticationGeneration || isFinishing()) {
                    return;
                }
                nativeOperationRunning = false;
                latestState = "login_required";
                latestQuickConnectCode = "";
                latestIsError = true;
                latestMessage = friendlyAuthenticationError(exception);
                applyCompanionState();
            }
        });
    }

    private String requestText(
            String method,
            String endpoint,
            JSONObject body) throws Exception {
        HttpURLConnection connection = null;
        try {
            connection = (HttpURLConnection) new URL(endpoint).openConnection();
            connection.setRequestMethod(method);
            connection.setConnectTimeout(HTTP_TIMEOUT_MS);
            connection.setReadTimeout(HTTP_TIMEOUT_MS);
            connection.setUseCaches(false);
            connection.setRequestProperty("Accept", "application/json");
            String authorization = buildAuthorizationHeader();
            connection.setRequestProperty("Authorization", authorization);
            connection.setRequestProperty("X-Emby-Authorization", authorization);

            if (body != null) {
                byte[] payload = body.toString().getBytes(StandardCharsets.UTF_8);
                connection.setDoOutput(true);
                connection.setFixedLengthStreamingMode(payload.length);
                connection.setRequestProperty("Content-Type", "application/json; charset=utf-8");
                OutputStream output = connection.getOutputStream();
                try {
                    output.write(payload);
                    output.flush();
                } finally {
                    output.close();
                }
            }

            int statusCode = connection.getResponseCode();
            InputStream stream = statusCode >= 200 && statusCode < 300
                    ? connection.getInputStream()
                    : connection.getErrorStream();
            String response = readStream(stream);
            if (statusCode < 200 || statusCode >= 300) {
                throw new CompanionHttpException(statusCode, response);
            }
            return response;
        } finally {
            if (connection != null) {
                connection.disconnect();
            }
        }
    }

    private JSONObject requestJson(
            String method,
            String endpoint,
            JSONObject body) throws Exception {
        String response = requestText(method, endpoint, body);
        if (TextUtils.isEmpty(response)) {
            return new JSONObject();
        }
        return new JSONObject(response);
    }

    private String readStream(InputStream stream) throws Exception {
        if (stream == null) {
            return "";
        }
        BufferedReader reader = new BufferedReader(
                new InputStreamReader(stream, StandardCharsets.UTF_8));
        try {
            StringBuilder builder = new StringBuilder();
            String line;
            while ((line = reader.readLine()) != null) {
                builder.append(line);
            }
            return builder.toString();
        } finally {
            reader.close();
        }
    }

    private String buildAuthorizationHeader() {
        return "MediaBrowser Client=\"Jellyfin for RayNeo\", Device=\""
                + headerSafe(Build.MODEL)
                + "\", DeviceId=\""
                + headerSafe(getOrCreateDeviceId())
                + "\", Version=\"0.1.0\"";
    }

    private String headerSafe(String value) {
        if (value == null) {
            return "";
        }
        return value.replace("\\", "").replace("\"", "").replace("\n", "").replace("\r", "");
    }

    private String normalizeServerUrl(String value) throws Exception {
        String candidate = value.trim();
        if (!candidate.regionMatches(true, 0, "http://", 0, 7)
                && !candidate.regionMatches(true, 0, "https://", 0, 8)) {
            candidate = "http://" + candidate;
        }

        URI uri = new URI(candidate);
        String scheme = uri.getScheme();
        if (TextUtils.isEmpty(scheme)
                || (!"http".equalsIgnoreCase(scheme) && !"https".equalsIgnoreCase(scheme))
                || TextUtils.isEmpty(uri.getHost())
                || !TextUtils.isEmpty(uri.getUserInfo())
                || !TextUtils.isEmpty(uri.getRawQuery())
                || !TextUtils.isEmpty(uri.getRawFragment())) {
            throw new IllegalArgumentException("Invalid Jellyfin server URL");
        }

        String path = uri.getRawPath();
        path = path == null ? "" : path;
        while (path.endsWith("/") && path.length() > 0) {
            path = path.substring(0, path.length() - 1);
        }
        return scheme.toLowerCase(Locale.US) + "://" + uri.getRawAuthority() + path;
    }

    private String friendlyAuthenticationError(Exception exception) {
        if (exception instanceof CompanionHttpException) {
            int statusCode = ((CompanionHttpException) exception).statusCode;
            String detail = ((CompanionHttpException) exception).response;
            if (statusCode == 401 || statusCode == 403) {
                return "用户名或密码不正确，请检查后重试。";
            }
            if (statusCode == 404) {
                return "服务器不支持此登录方式，请检查地址或改用账户密码。";
            }
            if (statusCode == 408) {
                return TextUtils.isEmpty(detail) ? "请求已超时，请重试。" : detail;
            }
            if (!TextUtils.isEmpty(detail) && detail.length() < 120) {
                return detail;
            }
            return "Jellyfin 请求失败（HTTP " + statusCode + "），请检查服务器。";
        }
        if (exception instanceof SocketTimeoutException) {
            return "连接 Jellyfin 超时，请确认手机与服务器在同一网络。";
        }
        if (exception instanceof UnknownHostException) {
            return "找不到 Jellyfin 服务器，请检查地址。";
        }
        if (exception instanceof ConnectException) {
            return "无法连接 Jellyfin，请检查地址、端口和局域网。";
        }
        if (exception instanceof SSLException) {
            return "HTTPS 证书验证失败，请检查服务器证书。";
        }
        return "Jellyfin 登录失败，请检查服务器地址后重试。";
    }

    private String getOrCreateDeviceId() {
        SharedPreferences preferences = getCompanionPreferences();
        String existing = preferences.getString(PREF_DEVICE_ID, "");
        if (!TextUtils.isEmpty(existing)) {
            return existing;
        }
        String created = UUID.randomUUID().toString().replace("-", "");
        preferences.edit().putString(PREF_DEVICE_ID, created).apply();
        return created;
    }

    private SharedPreferences getCompanionPreferences() {
        return getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
    }

    public String getPendingSessionJson() {
        return getCompanionPreferences().getString(PREF_SESSION_JSON, "");
    }

    public String getRayNeoDisplayMode() {
        return normalizeDisplayMode(getCompanionPreferences().getString(
                PREF_DISPLAY_MODE,
                DISPLAY_MODE_MIRROR_2D));
    }

    public boolean isRayNeoDisplayConnected() {
        return hasConnectedRayNeoDisplay();
    }

    public void setRayNeoDisplayModeState(
            final String requestedMode,
            final String activeMode,
            final boolean requestedModeApplied,
            final String message) {
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                JellyfinRayNeoActivity.this.requestedDisplayMode =
                        normalizeDisplayMode(requestedMode);
                JellyfinRayNeoActivity.this.activeDisplayMode =
                        normalizeDisplayMode(activeMode);
                JellyfinRayNeoActivity.this.requestedDisplayModeApplied =
                        requestedModeApplied;
                displayModeMessage = message == null ? "" : message.trim();
                updateDisplayModeUi();
                if (touchpadView != null) {
                    touchpadView.invalidate();
                }
                if (companionOverlay != null && !isFinishing()) {
                    applyCompanionState();
                }
            }
        });
    }

    public String pollRemoteCommand() {
        synchronized (remoteCommands) {
            return remoteCommands.isEmpty() ? "" : remoteCommands.removeFirst();
        }
    }

    private void enqueueRemoteCommand(String command) {
        if (TextUtils.isEmpty(command)) {
            return;
        }
        synchronized (remoteCommands) {
            while (remoteCommands.size() >= MAX_REMOTE_COMMANDS) {
                remoteCommands.removeFirst();
            }
            remoteCommands.addLast(command);
        }
    }

    private void clearRemoteCommands() {
        synchronized (remoteCommands) {
            remoteCommands.clear();
        }
    }

    public void clearNativeSession() {
        authenticationGeneration++;
        nativeOperationRunning = false;
        getCompanionPreferences().edit().remove(PREF_SESSION_JSON).apply();
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                latestState = "login_required";
                latestMessage = "Jellyfin 配置已清除，请重新选择服务器并登录。";
                latestIsError = false;
                latestQuickConnectCode = "";
                automaticDiscoveryStarted = false;
                if (companionOverlay != null) {
                    applyCompanionState();
                }
            }
        });
    }

    private void restoreNativeState() {
        SharedPreferences preferences = getCompanionPreferences();
        requestedDisplayMode = normalizeDisplayMode(preferences.getString(
                PREF_DISPLAY_MODE,
                DISPLAY_MODE_MIRROR_2D));
        activeDisplayMode = DISPLAY_MODE_MIRROR_2D;
        requestedDisplayModeApplied = false;
        displayModeMessage = isStereoDisplayMode(requestedDisplayMode)
                ? "立体屏幕已保存，连接眼镜后自动启用。"
                : "镜像 2D 已保存，连接眼镜后自动启用。";
        latestServerUrl = preferences.getString(PREF_SERVER_URL, "");
        latestUsername = preferences.getString(PREF_USERNAME, "");
        String sessionText = preferences.getString(PREF_SESSION_JSON, "");
        if (isValidNativeSession(sessionText)) {
            try {
                JSONObject session = new JSONObject(sessionText);
                latestServerUrl = session.optString("serverUrl", latestServerUrl);
                latestUsername = session.optString("userName", latestUsername);
            } catch (Exception ignored) {
            }
            latestState = "session_ready";
            latestMessage = "Jellyfin 配置已保存。请连接 RayNeo Air 以浏览和播放。";
        } else if (!TextUtils.isEmpty(sessionText)) {
            preferences.edit().remove(PREF_SESSION_JSON).apply();
        }
    }

    private boolean hasNativeSession() {
        return isValidNativeSession(getPendingSessionJson());
    }

    private boolean isValidNativeSession(String value) {
        if (TextUtils.isEmpty(value)) {
            return false;
        }
        try {
            JSONObject session = new JSONObject(value);
            return isUsableServerValue(session.optString("serverUrl", ""))
                    && !TextUtils.isEmpty(session.optString("accessToken", ""))
                    && !TextUtils.isEmpty(session.optString("userId", ""))
                    && !TextUtils.isEmpty(session.optString("deviceId", ""));
        } catch (Exception ignored) {
            return false;
        }
    }

    private boolean isUsableServerValue(String value) {
        return !TextUtils.isEmpty(value)
                && !"http://".equalsIgnoreCase(value.trim())
                && !"https://".equalsIgnoreCase(value.trim());
    }

    private void discoverServers() {
        if (nativeOperationRunning || hasNativeSession()) {
            return;
        }

        discoveryGeneration++;
        closeDiscoverySocket();
        final int generation = discoveryGeneration;
        discoverButton.setEnabled(false);
        discoverButton.setText("扫描中");
        discoveryStatusText.setText("正在搜索同一 Wi-Fi 中的 Jellyfin 服务器…");
        discoveryStatusText.setTextColor(COLOR_SECONDARY);
        discoveryStatusText.setVisibility(View.VISIBLE);
        discoveredServersContainer.removeAllViews();
        discoveredServersContainer.setVisibility(View.GONE);
        hideKeyboard();

        Thread worker = new Thread(new Runnable() {
            @Override
            public void run() {
                scanForServers(generation);
            }
        }, "Jellyfin-LAN-Discovery");
        worker.start();
    }

    private void scanForServers(final int generation) {
        final Map<String, DiscoveredServer> found =
                new LinkedHashMap<String, DiscoveredServer>();
        DatagramSocket socket = null;
        WifiManager.MulticastLock multicastLock = null;
        String failure = null;

        try {
            WifiManager wifiManager = (WifiManager) getApplicationContext()
                    .getSystemService(Context.WIFI_SERVICE);
            if (wifiManager != null) {
                multicastLock = wifiManager.createMulticastLock("jellyfin-rayneo-discovery");
                multicastLock.setReferenceCounted(false);
                multicastLock.acquire();
            }

            socket = new DatagramSocket();
            socket.setBroadcast(true);
            socket.setSoTimeout(350);
            if (generation != discoveryGeneration) {
                return;
            }
            discoverySocket = socket;

            List<InetAddress> broadcasts = findBroadcastAddresses();
            for (InetAddress broadcast : broadcasts) {
                try {
                    DatagramPacket request = new DatagramPacket(
                            DISCOVERY_MESSAGE,
                            DISCOVERY_MESSAGE.length,
                            broadcast,
                            DISCOVERY_PORT);
                    socket.send(request);
                } catch (Exception ignored) {
                }
            }

            long deadline = SystemClock.elapsedRealtime() + DISCOVERY_DURATION_MS;
            byte[] buffer = new byte[4096];
            while (generation == discoveryGeneration
                    && SystemClock.elapsedRealtime() < deadline) {
                DatagramPacket response = new DatagramPacket(buffer, buffer.length);
                try {
                    socket.receive(response);
                    DiscoveredServer server = parseDiscoveryResponse(response);
                    if (server != null) {
                        String key = !TextUtils.isEmpty(server.id)
                                ? server.id
                                : server.address.toLowerCase();
                        found.put(key, server);
                    }
                } catch (SocketTimeoutException ignored) {
                }
            }
        } catch (Exception exception) {
            failure = exception.getMessage();
        } finally {
            if (discoverySocket == socket) {
                discoverySocket = null;
            }
            if (socket != null) {
                socket.close();
            }
            if (multicastLock != null && multicastLock.isHeld()) {
                multicastLock.release();
            }
        }

        final ArrayList<DiscoveredServer> results =
                new ArrayList<DiscoveredServer>(found.values());
        final String scanFailure = failure;
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                if (generation != discoveryGeneration || isFinishing()) {
                    return;
                }
                discoverButton.setEnabled(!nativeOperationRunning && !hasNativeSession());
                discoverButton.setText("重新发现");
                renderDiscoveredServers(results, scanFailure);
            }
        });
    }

    private List<InetAddress> findBroadcastAddresses() {
        LinkedHashMap<String, InetAddress> addresses =
                new LinkedHashMap<String, InetAddress>();
        try {
            InetAddress global = InetAddress.getByName("255.255.255.255");
            addresses.put(global.getHostAddress(), global);
        } catch (Exception ignored) {
        }

        try {
            Enumeration<NetworkInterface> interfaces = NetworkInterface.getNetworkInterfaces();
            while (interfaces != null && interfaces.hasMoreElements()) {
                NetworkInterface network = interfaces.nextElement();
                if (!network.isUp() || network.isLoopback()) {
                    continue;
                }
                for (InterfaceAddress address : network.getInterfaceAddresses()) {
                    InetAddress broadcast = address.getBroadcast();
                    if (broadcast != null) {
                        addresses.put(broadcast.getHostAddress(), broadcast);
                    }
                }
            }
        } catch (Exception ignored) {
        }
        return new ArrayList<InetAddress>(addresses.values());
    }

    private DiscoveredServer parseDiscoveryResponse(DatagramPacket packet) {
        try {
            String text = new String(
                    packet.getData(),
                    packet.getOffset(),
                    packet.getLength(),
                    StandardCharsets.UTF_8).trim();
            JSONObject json = new JSONObject(text);
            String address = firstNonEmpty(
                    json.optString("Address", ""),
                    json.optString("address", ""));
            address = sanitizeDiscoveredAddress(address, packet.getAddress());
            if (TextUtils.isEmpty(address)) {
                return null;
            }

            String name = firstNonEmpty(
                    json.optString("Name", ""),
                    json.optString("name", ""));
            String id = firstNonEmpty(
                    json.optString("Id", ""),
                    json.optString("id", ""));
            if (TextUtils.isEmpty(name)) {
                name = packet.getAddress().getHostAddress();
            }
            return new DiscoveredServer(name, address, id);
        } catch (Exception ignored) {
            return null;
        }
    }

    private String sanitizeDiscoveredAddress(String value, InetAddress source) {
        if (TextUtils.isEmpty(value)) {
            return null;
        }

        String candidate = value.trim();
        if (!candidate.startsWith("http://") && !candidate.startsWith("https://")) {
            return null;
        }

        try {
            java.net.URI uri = new java.net.URI(candidate);
            String host = uri.getHost();
            if (TextUtils.isEmpty(host)
                    || "0.0.0.0".equals(host)
                    || "::".equals(host)
                    || "[::]".equals(host)) {
                host = source.getHostAddress();
                if (host.contains(":")) {
                    host = "[" + host + "]";
                }
                StringBuilder rebuilt = new StringBuilder();
                rebuilt.append(uri.getScheme()).append("://").append(host);
                if (uri.getPort() >= 0) {
                    rebuilt.append(":").append(uri.getPort());
                }
                if (!TextUtils.isEmpty(uri.getRawPath()) && !"/".equals(uri.getRawPath())) {
                    rebuilt.append(uri.getRawPath());
                }
                candidate = rebuilt.toString();
            }
        } catch (Exception ignored) {
            return null;
        }

        while (candidate.endsWith("/")) {
            candidate = candidate.substring(0, candidate.length() - 1);
        }
        return candidate;
    }

    private void renderDiscoveredServers(
            List<DiscoveredServer> servers,
            String failure) {
        discoveredServersContainer.removeAllViews();
        if (servers.isEmpty()) {
            discoveryStatusText.setText(TextUtils.isEmpty(failure)
                    ? "未发现服务器。请确认手机与 Jellyfin 在同一 Wi-Fi，或手动输入地址。"
                    : "自动发现失败，请手动输入服务器地址。");
            discoveryStatusText.setTextColor(TextUtils.isEmpty(failure)
                    ? COLOR_SECONDARY
                    : COLOR_ERROR);
            discoveredServersContainer.setVisibility(View.GONE);
            return;
        }

        discoveryStatusText.setText("发现 " + servers.size() + " 台 Jellyfin 服务器，点击选择：");
        discoveryStatusText.setTextColor(COLOR_ACCENT_BRIGHT);
        discoveredServersContainer.setVisibility(View.VISIBLE);

        for (int index = 0; index < servers.size(); index++) {
            final DiscoveredServer server = servers.get(index);
            Button button = createButton(
                    server.name + "\n" + server.address,
                    COLOR_FIELD);
            button.setGravity(Gravity.START | Gravity.CENTER_VERTICAL);
            button.setTextSize(14);
            button.setPadding(dp(14), 0, dp(14), 0);
            button.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View view) {
                    serverInput.setText(server.address);
                    serverInput.setSelection(serverInput.length());
                    latestServerUrl = server.address;
                    discoveryStatusText.setText("已选择 " + server.name);
                    discoveryStatusText.setTextColor(COLOR_ACCENT_BRIGHT);
                }
            });
            discoveredServersContainer.addView(button, matchHeight(62, 8));
        }

        String current = serverInput.getText().toString().trim();
        if (servers.size() == 1
                && (TextUtils.isEmpty(current) || "http://".equals(current))) {
            DiscoveredServer server = servers.get(0);
            serverInput.setText(server.address);
            serverInput.setSelection(serverInput.length());
            latestServerUrl = server.address;
            discoveryStatusText.setText("已自动选择 " + server.name);
        }
    }

    private void applyCompanionState() {
        if (companionOverlay == null) {
            return;
        }

        boolean libraryReady = "ready".equals(latestState);
        boolean sessionAvailable = libraryReady
                || "session_ready".equals(latestState)
                || hasNativeSession();
        boolean waitingForQuickConnect = "quick_connect_waiting".equals(latestState)
                && !TextUtils.isEmpty(latestQuickConnectCode);
        boolean busy = nativeOperationRunning
                || "native_connecting".equals(latestState)
                || "connecting".equals(latestState);
        boolean touchpadActive = glassesPresentationReady && libraryReady;

        companionOverlay.setVisibility(View.VISIBLE);
        configurationScrollView.setVisibility(touchpadActive ? View.GONE : View.VISIBLE);
        touchpadView.setVisibility(touchpadActive ? View.VISIBLE : View.GONE);
        touchpadView.setTouchpadActive(touchpadActive);
        loginForm.setVisibility(sessionAvailable ? View.GONE : View.VISIBLE);
        sessionPanel.setVisibility(sessionAvailable ? View.VISIBLE : View.GONE);
        updateDisplayModeUi();

        if (glassesPresentationReady) {
            connectionBadge.setText("眼镜显示中");
            connectionBadge.setTextColor(COLOR_SUCCESS);
            connectionBadge.setBackground(statusChipBackground(
                    Color.rgb(27, 55, 45),
                    Color.rgb(54, 105, 83)));
            glassesStatusText.setText("画面已在眼镜中显示");
            glassesStatusText.setTextColor(COLOR_SUCCESS);
            glassesDescriptionText.setText(isStereoDisplayMode(activeDisplayMode)
                    ? "立体屏幕：左眼仅显示左视图，右眼仅显示右视图；画面固定，不启用头部追踪。"
                    : "镜像 2D：双眼显示同一幅完整 1920×1080 媒体界面。");
            glassesActionHint.setText(requestedDisplayModeApplied
                    ? "手机已切换为全屏盲操触控板"
                    : displayModeMessage);
            glassesActionHint.setTextColor(
                    requestedDisplayModeApplied ? COLOR_SUCCESS : COLOR_ACCENT_BRIGHT);
            glassesConnectionCard.setBackground(connectionCardBackground(true, true));
            glassesIconView.setConnectionState(true, true);
        } else if (glassesConnected) {
            connectionBadge.setText("眼镜已连接");
            connectionBadge.setTextColor(COLOR_ACCENT_BRIGHT);
            connectionBadge.setBackground(statusChipBackground(
                    Color.rgb(31, 54, 54),
                    Color.rgb(55, 101, 98)));
            glassesStatusText.setText("已连接 · 正在准备显示");
            glassesStatusText.setTextColor(COLOR_ACCENT_BRIGHT);
            glassesDescriptionText.setText(
                    "已检测到外接眼镜，正在应用“"
                            + displayModeLabel(requestedDisplayMode)
                            + "”并创建眼镜画面。");
            glassesActionHint.setText(TextUtils.isEmpty(displayModeMessage)
                    ? "连接已识别，画面会自动出现"
                    : displayModeMessage);
            glassesActionHint.setTextColor(COLOR_ACCENT_BRIGHT);
            glassesConnectionCard.setBackground(connectionCardBackground(true, false));
            glassesIconView.setConnectionState(true, false);
        } else {
            connectionBadge.setText("眼镜未连接");
            connectionBadge.setTextColor(COLOR_SECONDARY);
            connectionBadge.setBackground(statusChipBackground(COLOR_SURFACE_SOFT, COLOR_BORDER));
            glassesStatusText.setText("未检测到眼镜");
            glassesStatusText.setTextColor(COLOR_ERROR);
            glassesDescriptionText.setText(
                    "将 RayNeo Air 插入手机。检测到外接显示后，应用会自动把画面送到眼镜。");
            glassesActionHint.setText(
                    "已选择 " + displayModeLabel(requestedDisplayMode) + "  ·  插入手机接口即可");
            glassesActionHint.setTextColor(COLOR_TERTIARY);
            glassesConnectionCard.setBackground(connectionCardBackground(false, false));
            glassesIconView.setConnectionState(false, false);
        }

        if (sessionAvailable) {
            cancelDiscovery();
            passwordInput.getText().clear();
            hideKeyboard();
            boolean mediaVisible = glassesPresentationReady && libraryReady;
            sessionTitleText.setText(mediaVisible ? "媒体库已在眼镜中打开" : "Jellyfin 已配置");
            String user = TextUtils.isEmpty(latestUsername) ? "Jellyfin 用户" : latestUsername;
            String server = TextUtils.isEmpty(latestServerUrl) ? "Jellyfin 服务器" : latestServerUrl;
            if (mediaVisible) {
                sessionDetailText.setText(user + " · " + server);
            } else if (glassesPresentationReady) {
                sessionDetailText.setText(
                        user + " · " + server + "\n眼镜已连接，媒体库正在同步。");
            } else if (glassesConnected) {
                sessionDetailText.setText(
                        user + " · " + server + "\n眼镜画面正在启动，媒体库随后自动同步。");
            } else {
                sessionDetailText.setText(
                        user + " · " + server + "\n连接 RayNeo Air 后会自动同步媒体库。");
            }
        } else {
            setControlsEnabled(!busy && !waitingForQuickConnect);
            quickConnectPanel.setVisibility(waitingForQuickConnect ? View.VISIBLE : View.GONE);
            quickConnectButton.setVisibility(waitingForQuickConnect ? View.GONE : View.VISIBLE);
            manualLoginContainer.setVisibility(waitingForQuickConnect ? View.GONE : View.VISIBLE);
            if (waitingForQuickConnect) {
                quickConnectCodeText.setText(formatQuickConnectCode(latestQuickConnectCode));
            }

            if (isUsableServerValue(latestServerUrl)
                    && !latestServerUrl.equals(serverInput.getText().toString().trim())) {
                serverInput.setText(latestServerUrl);
                serverInput.setSelection(serverInput.length());
            }
            if (!TextUtils.isEmpty(latestUsername)
                    && !latestUsername.equals(usernameInput.getText().toString())) {
                usernameInput.setText(latestUsername);
                usernameInput.setSelection(usernameInput.length());
            }

            if (busy) {
                connectButton.setText("正在登录…");
                quickConnectButton.setText("正在申请登录码…");
            } else {
                connectButton.setText("登录 Jellyfin");
                quickConnectButton.setText("使用快速登录");
            }
        }

        String visibleMessage = TextUtils.isEmpty(latestMessage)
                ? defaultMessageForState(latestState)
                : latestMessage;
        if (libraryReady) {
            if (glassesPresentationReady) {
                visibleMessage = "Jellyfin 已连接，媒体库正在眼镜中显示。";
            } else if (glassesConnected) {
                visibleMessage = "Jellyfin 已连接，眼镜画面正在启动。";
            } else {
                visibleMessage = "Jellyfin 已配置。连接 RayNeo Air 后即可浏览和播放。";
            }
        }
        statusText.setText(visibleMessage);
        statusText.setTextColor(latestIsError ? COLOR_ERROR : COLOR_SECONDARY);
        statusText.setVisibility(
                latestIsError || busy || waitingForQuickConnect
                        ? View.VISIBLE
                        : View.GONE);

        if (!sessionAvailable && !busy && !automaticDiscoveryStarted) {
            automaticDiscoveryStarted = true;
            serverInput.post(new Runnable() {
                @Override
                public void run() {
                    if (!nativeOperationRunning && !hasNativeSession()) {
                        discoverServers();
                    }
                }
            });
        }
    }

    private void setControlsEnabled(boolean enabled) {
        serverInput.setEnabled(enabled);
        discoverButton.setEnabled(enabled);
        quickConnectButton.setEnabled(enabled);
        usernameInput.setEnabled(enabled);
        passwordInput.setEnabled(enabled);
        connectButton.setEnabled(enabled);
        float alpha = enabled ? 1f : 0.72f;
        serverInput.setAlpha(alpha);
        discoverButton.setAlpha(alpha);
        quickConnectButton.setAlpha(alpha);
        usernameInput.setAlpha(alpha);
        passwordInput.setAlpha(alpha);
        connectButton.setAlpha(alpha);
    }

    private boolean hasConnectedRayNeoDisplay() {
        return findExternalDisplay() != null;
    }

    private Display findExternalDisplay() {
        DisplayManager manager = companionDisplayManager;
        if (manager == null) {
            manager = (DisplayManager) getSystemService(Context.DISPLAY_SERVICE);
        }
        if (manager == null) {
            return null;
        }

        Display display = findBestExternalDisplay(
                manager.getDisplays(DisplayManager.DISPLAY_CATEGORY_PRESENTATION));
        if (display != null) {
            return display;
        }
        return findBestExternalDisplay(manager.getDisplays());
    }

    private Display findBestExternalDisplay(Display[] displays) {
        if (displays == null) {
            return null;
        }

        Display bestDisplay = null;
        int bestScore = Integer.MIN_VALUE;
        for (Display display : displays) {
            if (display == null
                    || !display.isValid()
                    || display.getDisplayId() == Display.DEFAULT_DISPLAY
                    || display.getState() != Display.STATE_ON) {
                continue;
            }

            int score = 0;
            String name = display.getName();
            String normalizedName = name == null ? "" : name.toLowerCase(Locale.ROOT);
            if (normalizedName.contains("smartglasses")
                    || normalizedName.contains("rayneo")
                    || normalizedName.contains("tcl")
                    || normalizedName.contains("hdmi")) {
                score += 200;
            }

            if (bestDisplay == null || score > bestScore) {
                bestDisplay = display;
                bestScore = score;
            }
        }
        return bestDisplay;
    }

    private boolean isUnityPresentationActive() {
        try {
            View unityView = mUnityPlayer == null ? null : mUnityPlayer.getView();
            Display display = unityView == null ? null : unityView.getDisplay();
            return display != null && display.getDisplayId() != Display.DEFAULT_DISPLAY;
        } catch (Exception ignored) {
            return false;
        }
    }

    private void refreshGlassesConnectionState() {
        final boolean connected = hasConnectedRayNeoDisplay();
        final boolean presentationActive = connected && isUnityPresentationActive();
        runOnUiThread(new Runnable() {
            @Override
            public void run() {
                boolean wasConnected = glassesConnected;
                glassesConnected = connected;
                if (!connected) {
                    glassesPresentationReady = false;
                    glassesDetectedAtMs = 0L;
                    dismissFallbackPresentation();
                } else if (presentationActive) {
                    glassesPresentationReady = true;
                    if (fallbackPresentation != null
                            && !fallbackPresentation.ownsUnityPlayer()) {
                        dismissFallbackPresentation();
                    }
                } else if (!wasConnected || glassesDetectedAtMs == 0L) {
                    glassesDetectedAtMs = SystemClock.uptimeMillis();
                }

                if (connected
                        && !glassesPresentationReady
                        && SystemClock.uptimeMillis() - glassesDetectedAtMs
                                >= PRESENTATION_FALLBACK_DELAY_MS) {
                    startFallbackPresentation();
                }
                if (companionOverlay != null && !isFinishing()) {
                    applyCompanionState();
                    schedulePresentationProbe();
                }
            }
        });
    }

    private void schedulePresentationProbe() {
        if (companionOverlay == null) {
            return;
        }
        companionOverlay.removeCallbacks(presentationProbe);
        boolean verifyingFallback = fallbackPresentation != null
                && SystemClock.uptimeMillis() - fallbackPresentationStartedAtMs < 5000L;
        if (glassesConnected
                && (!glassesPresentationReady || verifyingFallback)
                && !isFinishing()) {
            companionOverlay.postDelayed(presentationProbe, 500L);
        }
    }

    private void startFallbackPresentation() {
        if (isFinishing()
                || isUnityPresentationActive()
                || (fallbackPresentation != null && fallbackPresentation.isShowing())) {
            return;
        }

        Display display = findExternalDisplay();
        if (display == null || mUnityPlayer == null) {
            return;
        }

        try {
            fallbackPresentation = new CompanionUnityPresentation(display);
            fallbackPresentationStartedAtMs = SystemClock.uptimeMillis();
            fallbackPresentation.show();
        } catch (Exception ignored) {
            fallbackPresentation = null;
            fallbackPresentationStartedAtMs = 0L;
        }
    }

    private void onFallbackPresentationShown() {
        glassesConnected = true;
        glassesPresentationReady = true;
        if (companionOverlay != null && !isFinishing()) {
            applyCompanionState();
        }
    }

    private void dismissFallbackPresentation() {
        CompanionUnityPresentation presentation = fallbackPresentation;
        fallbackPresentation = null;
        fallbackPresentationStartedAtMs = 0L;
        if (presentation != null) {
            presentation.releaseSafely();
        }
    }

    private void copyQuickConnectCode() {
        if (TextUtils.isEmpty(latestQuickConnectCode)) {
            return;
        }
        ClipboardManager clipboard =
                (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
        if (clipboard != null) {
            clipboard.setPrimaryClip(
                    ClipData.newPlainText("Jellyfin Quick Connect", latestQuickConnectCode));
            Toast.makeText(this, "快速登录码已复制", Toast.LENGTH_SHORT).show();
        }
    }

    private void openQuickConnectAuthorization() {
        if (TextUtils.isEmpty(latestServerUrl)
                || TextUtils.isEmpty(latestQuickConnectCode)) {
            showLocalError("快速登录码尚未准备好。");
            return;
        }

        String target = latestServerUrl.replaceAll("/+$", "")
                + "/web/#/quickconnect?code="
                + Uri.encode(latestQuickConnectCode);
        try {
            startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(target)));
        } catch (Exception ignored) {
            showLocalError("无法打开授权页，请复制代码后在 Jellyfin App 中手动授权。");
        }
    }

    private void showLocalError(String message) {
        latestIsError = true;
        latestMessage = message;
        statusText.setText(message);
        statusText.setTextColor(COLOR_ERROR);
        statusText.setVisibility(View.VISIBLE);
    }

    private String defaultMessageForState(String state) {
        if ("quick_connect_waiting".equals(state)) {
            return "等待 Jellyfin 授权快速登录码…";
        }
        if ("connecting".equals(state) || "native_connecting".equals(state)) {
            return "正在连接 Jellyfin…";
        }
        if ("session_ready".equals(state)) {
            return "Jellyfin 配置已保存，请连接 RayNeo Air。";
        }
        if ("initializing".equals(state) || "offline".equals(state)) {
            return "未连接眼镜；你可以先完成 Jellyfin 配置。";
        }
        return "请选择服务器，然后使用快速登录或账户密码。";
    }

    private String formatQuickConnectCode(String code) {
        String compact = code == null ? "" : code.replace(" ", "").trim();
        if (compact.length() == 6) {
            return compact.substring(0, 3) + "  " + compact.substring(3);
        }
        return compact;
    }

    private String firstNonEmpty(String first, String second) {
        return TextUtils.isEmpty(first) ? second : first;
    }

    private void cancelDiscovery() {
        discoveryGeneration++;
        closeDiscoverySocket();
    }

    private void closeDiscoverySocket() {
        DatagramSocket socket = discoverySocket;
        discoverySocket = null;
        if (socket != null) {
            socket.close();
        }
    }

    private EditText createInput(String hint) {
        EditText input = new EditText(this);
        input.setSingleLine(true);
        input.setFocusable(true);
        input.setFocusableInTouchMode(true);
        input.setCursorVisible(true);
        input.setIncludeFontPadding(false);
        input.setMinHeight(0);
        input.setMinWidth(0);
        input.setHint(hint);
        input.setHintTextColor(COLOR_TERTIARY);
        input.setTextColor(COLOR_PRIMARY);
        input.setTextSize(15);
        input.setPadding(dp(16), 0, dp(16), 0);
        input.setBackground(roundedWithStroke(COLOR_FIELD, COLOR_BORDER, 14));
        return input;
    }

    private Button createButton(String text, int color) {
        Button button = new Button(this);
        button.setAllCaps(false);
        button.setText(text);
        button.setTextColor(Color.WHITE);
        button.setTextSize(14);
        button.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        button.setGravity(Gravity.CENTER);
        button.setIncludeFontPadding(false);
        button.setMinHeight(0);
        button.setMinWidth(0);
        button.setStateListAnimator(null);
        button.setBackgroundTintList(null);
        button.setPadding(dp(10), 0, dp(10), 0);
        button.setBackground(rippleBackground(rounded(color, 14), Color.WHITE));
        return button;
    }

    private TextView createLabel(String text) {
        return createText(text, 14, COLOR_SECONDARY, Typeface.BOLD, Gravity.START);
    }

    private TextView createText(String text, int sizeSp, int color, int style, int gravity) {
        TextView view = new TextView(this);
        view.setText(text);
        view.setTextSize(sizeSp);
        view.setTextColor(color);
        view.setTypeface(Typeface.DEFAULT, style);
        view.setGravity(gravity);
        view.setIncludeFontPadding(false);
        return view;
    }

    private GradientDrawable rounded(int color, int radiusDp) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(color);
        drawable.setCornerRadius(dp(radiusDp));
        return drawable;
    }

    private GradientDrawable roundedWithStroke(
            int color,
            int strokeColor,
            int radiusDp) {
        GradientDrawable drawable = rounded(color, radiusDp);
        drawable.setStroke(dp(1), strokeColor);
        return drawable;
    }

    private GradientDrawable backgroundGradient() {
        GradientDrawable drawable = new GradientDrawable(
                GradientDrawable.Orientation.TOP_BOTTOM,
                new int[] { COLOR_BACKGROUND_TOP, COLOR_BACKGROUND_BOTTOM });
        drawable.setGradientType(GradientDrawable.LINEAR_GRADIENT);
        return drawable;
    }

    private GradientDrawable accentGradient(int radiusDp) {
        GradientDrawable drawable = new GradientDrawable(
                GradientDrawable.Orientation.LEFT_RIGHT,
                new int[] { COLOR_ACCENT, COLOR_ACCENT_END });
        drawable.setCornerRadius(dp(radiusDp));
        return drawable;
    }

    private Drawable accentButtonBackground(int radiusDp) {
        return rippleBackground(accentGradient(radiusDp), Color.BLACK);
    }

    private Drawable outlineButtonBackground(int radiusDp) {
        return rippleBackground(
                roundedWithStroke(COLOR_SURFACE_SOFT, COLOR_BORDER, radiusDp),
                Color.WHITE);
    }

    private Drawable transparentButtonBackground(int radiusDp) {
        return rippleBackground(rounded(Color.TRANSPARENT, radiusDp), Color.WHITE);
    }

    private Drawable rippleBackground(Drawable content, int rippleColor) {
        return new RippleDrawable(
                ColorStateList.valueOf(Color.argb(
                        42,
                        Color.red(rippleColor),
                        Color.green(rippleColor),
                        Color.blue(rippleColor))),
                content,
                null);
    }

    private GradientDrawable statusChipBackground(int color, int strokeColor) {
        return roundedWithStroke(color, strokeColor, 20);
    }

    private GradientDrawable connectionCardBackground(boolean connected, boolean ready) {
        int[] colors;
        int stroke;
        if (ready) {
            colors = new int[] { Color.rgb(27, 61, 51), Color.rgb(28, 30, 34) };
            stroke = Color.rgb(57, 112, 89);
        } else if (connected) {
            colors = new int[] { Color.rgb(29, 57, 57), Color.rgb(28, 30, 35) };
            stroke = Color.rgb(57, 104, 101);
        } else {
            colors = new int[] { COLOR_SURFACE_SOFT, Color.rgb(27, 28, 32) };
            stroke = COLOR_BORDER;
        }

        GradientDrawable drawable = new GradientDrawable(
                GradientDrawable.Orientation.TL_BR,
                colors);
        drawable.setCornerRadius(dp(22));
        drawable.setStroke(dp(1), stroke);
        return drawable;
    }

    private LinearLayout.LayoutParams wrapWrap() {
        return new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT,
                ViewGroup.LayoutParams.WRAP_CONTENT);
    }

    private LinearLayout.LayoutParams matchWrap(int bottomMargin) {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT);
        params.bottomMargin = bottomMargin;
        return params;
    }

    private LinearLayout.LayoutParams matchHeight(int heightDp, int bottomMarginDp) {
        LinearLayout.LayoutParams params = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                dp(heightDp));
        params.bottomMargin = dp(bottomMarginDp);
        return params;
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private void hideKeyboard() {
        View focused = getCurrentFocus();
        if (focused == null) {
            return;
        }
        InputMethodManager manager =
                (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        if (manager != null) {
            manager.hideSoftInputFromWindow(focused.getWindowToken(), 0);
        }
        focused.clearFocus();
    }

    private final class TouchpadView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Runnable pendingSubmit = new Runnable() {
            @Override
            public void run() {
                lastTapAtMs = 0L;
                emit("submit", HapticFeedbackConstants.KEYBOARD_TAP);
            }
        };
        private boolean touchpadActive;
        private boolean tracking;
        private float downX;
        private float downY;
        private float touchX;
        private float touchY;
        private long downAtMs;
        private long lastTapAtMs;
        private boolean modeChipGesture;

        TouchpadView(Context context) {
            super(context);
            setBackground(backgroundGradient());
            setClickable(true);
            setFocusable(true);
            setFocusableInTouchMode(true);
            setContentDescription(
                    "RayNeo 盲操触控板。上下左右滑动移动焦点，单击确认，双击返回。");
            paint.setStrokeCap(Paint.Cap.ROUND);
            paint.setStrokeJoin(Paint.Join.ROUND);
        }

        void setTouchpadActive(boolean active) {
            if (touchpadActive == active) {
                return;
            }
            touchpadActive = active;
            tracking = false;
            modeChipGesture = false;
            removeCallbacks(pendingSubmit);
            lastTapAtMs = 0L;
            if (active) {
                requestFocus();
            } else {
                clearRemoteCommands();
            }
            invalidate();
        }

        @Override
        public boolean onTouchEvent(MotionEvent event) {
            if (!touchpadActive || event == null) {
                return false;
            }

            switch (event.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    tracking = true;
                    downX = event.getX();
                    downY = event.getY();
                    touchX = downX;
                    touchY = downY;
                    downAtMs = SystemClock.uptimeMillis();
                    modeChipGesture = isInsideModeChip(downX, downY);
                    if (modeChipGesture) {
                        removeCallbacks(pendingSubmit);
                        lastTapAtMs = 0L;
                    }
                    setPressed(true);
                    invalidate();
                    return true;
                case MotionEvent.ACTION_MOVE:
                    if (tracking) {
                        touchX = event.getX();
                        touchY = event.getY();
                        if (modeChipGesture
                                && Math.max(
                                        Math.abs(touchX - downX),
                                        Math.abs(touchY - downY)) > dp(16)) {
                            modeChipGesture = false;
                        }
                        invalidate();
                    }
                    return true;
                case MotionEvent.ACTION_UP:
                    if (tracking && modeChipGesture) {
                        boolean longPress = SystemClock.uptimeMillis() - downAtMs
                                >= DISPLAY_MODE_LONG_PRESS_MS;
                        modeChipGesture = false;
                        tracking = false;
                        setPressed(false);
                        if (longPress) {
                            toggleDisplayMode();
                            performHapticFeedback(HapticFeedbackConstants.LONG_PRESS);
                        } else {
                            Toast.makeText(
                                    JellyfinRayNeoActivity.this,
                                    "长按此处切换眼镜显示模式",
                                    Toast.LENGTH_SHORT).show();
                        }
                        invalidate();
                        return true;
                    }
                    if (tracking) {
                        handleGesture(event.getX(), event.getY());
                    }
                    tracking = false;
                    setPressed(false);
                    performClick();
                    invalidate();
                    return true;
                case MotionEvent.ACTION_CANCEL:
                    tracking = false;
                    modeChipGesture = false;
                    setPressed(false);
                    invalidate();
                    return true;
                default:
                    return true;
            }
        }

        @Override
        public boolean performClick() {
            super.performClick();
            return true;
        }

        @Override
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float width = getWidth();
            float height = getHeight();
            float centerX = width * 0.5f;
            float centerY = height * 0.47f;
            float padRadius = Math.min(width, height) * 0.27f;

            paint.setStyle(Paint.Style.FILL);
            paint.setColor(Color.argb(36, 255, 255, 255));
            canvas.drawRoundRect(
                    dp(22),
                    dp(22),
                    width - dp(22),
                    height - dp(22),
                    dp(30),
                    dp(30),
                    paint);

            paint.setColor(Color.argb(30, 93, 224, 210));
            canvas.drawCircle(centerX, centerY, padRadius, paint);
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(dp(2));
            paint.setColor(Color.argb(82, 115, 232, 220));
            canvas.drawCircle(centerX, centerY, padRadius, paint);
            canvas.drawLine(centerX - padRadius * 0.52f, centerY, centerX + padRadius * 0.52f, centerY, paint);
            canvas.drawLine(centerX, centerY - padRadius * 0.52f, centerX, centerY + padRadius * 0.52f, paint);

            paint.setStyle(Paint.Style.FILL);
            paint.setTextAlign(Paint.Align.LEFT);
            paint.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
            paint.setTextSize(sp(13));
            paint.setColor(COLOR_ACCENT_BRIGHT);
            canvas.drawText("JELLYFIN  ·  RAYNEO AIR 3S", dp(34), dp(58), paint);

            float modeChipRight = width - dp(34);
            float modeChipLeft = modeChipRight - dp(150);
            float modeChipTop = dp(34);
            float modeChipBottom = dp(76);
            paint.setStyle(Paint.Style.FILL);
            paint.setColor(modeChipGesture
                    ? Color.argb(66, 115, 232, 220)
                    : Color.argb(38, 255, 255, 255));
            canvas.drawRoundRect(
                    modeChipLeft,
                    modeChipTop,
                    modeChipRight,
                    modeChipBottom,
                    dp(18),
                    dp(18),
                    paint);
            paint.setTextAlign(Paint.Align.CENTER);
            paint.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.BOLD));
            paint.setTextSize(sp(11));
            paint.setColor(requestedDisplayModeApplied ? COLOR_SUCCESS : COLOR_ACCENT_BRIGHT);
            canvas.drawText(
                    displayModeLabel(requestedDisplayMode) + "  ·  长按切换",
                    (modeChipLeft + modeChipRight) * 0.5f,
                    modeChipTop + dp(26),
                    paint);

            paint.setTextAlign(Paint.Align.CENTER);
            paint.setTextSize(sp(28));
            paint.setColor(COLOR_PRIMARY);
            canvas.drawText("盲操触控板", centerX, centerY - dp(12), paint);
            paint.setTypeface(Typeface.create(Typeface.DEFAULT, Typeface.NORMAL));
            paint.setTextSize(sp(15));
            paint.setColor(COLOR_SECONDARY);
            canvas.drawText("在任意位置滑动", centerX, centerY + dp(24), paint);

            paint.setTextSize(sp(14));
            paint.setColor(COLOR_PRIMARY);
            canvas.drawText("上下左右滑动  移动焦点", centerX, height - dp(112), paint);
            paint.setTextSize(sp(13));
            paint.setColor(COLOR_TERTIARY);
            canvas.drawText("单击确认  ·  双击返回", centerX, height - dp(78), paint);

            if (tracking) {
                paint.setStyle(Paint.Style.FILL);
                paint.setColor(Color.argb(76, 255, 255, 255));
                canvas.drawCircle(touchX, touchY, dp(34), paint);
                paint.setStyle(Paint.Style.STROKE);
                paint.setStrokeWidth(dp(2));
                paint.setColor(COLOR_ACCENT_BRIGHT);
                canvas.drawCircle(touchX, touchY, dp(34), paint);
            }
        }

        private void handleGesture(float upX, float upY) {
            float deltaX = upX - downX;
            float deltaY = upY - downY;
            float absoluteX = Math.abs(deltaX);
            float absoluteY = Math.abs(deltaY);
            float swipeThreshold = dp(44);
            if (Math.max(absoluteX, absoluteY) >= swipeThreshold) {
                if (absoluteX > absoluteY) {
                    emit(deltaX > 0f ? "right" : "left", HapticFeedbackConstants.KEYBOARD_TAP);
                } else {
                    emit(deltaY > 0f ? "down" : "up", HapticFeedbackConstants.KEYBOARD_TAP);
                }
                return;
            }

            if (Math.max(absoluteX, absoluteY) > dp(16)) {
                return;
            }

            long now = SystemClock.uptimeMillis();
            if (lastTapAtMs > 0L && now - lastTapAtMs <= DOUBLE_TAP_WINDOW_MS) {
                removeCallbacks(pendingSubmit);
                lastTapAtMs = 0L;
                emit("back", HapticFeedbackConstants.LONG_PRESS);
                return;
            }

            lastTapAtMs = now;
            removeCallbacks(pendingSubmit);
            postDelayed(pendingSubmit, DOUBLE_TAP_WINDOW_MS);
        }

        private void emit(String command, int hapticConstant) {
            enqueueRemoteCommand(command);
            performHapticFeedback(hapticConstant);
        }

        private boolean isInsideModeChip(float x, float y) {
            float right = getWidth() - dp(34);
            float left = right - dp(150);
            return x >= left && x <= right && y >= dp(34) && y <= dp(76);
        }

        private float sp(int value) {
            return value * getResources().getDisplayMetrics().scaledDensity;
        }
    }

    private final class GlassesIconView extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private boolean connected;
        private boolean ready;

        GlassesIconView(Context context) {
            super(context);
            paint.setStrokeCap(Paint.Cap.ROUND);
            paint.setStrokeJoin(Paint.Join.ROUND);
            setConnectionState(false, false);
        }

        void setConnectionState(boolean isConnected, boolean isReady) {
            connected = isConnected;
            ready = isReady;
            invalidate();
        }

        @Override
        protected void onDraw(Canvas canvas) {
            super.onDraw(canvas);
            float width = getWidth();
            float height = getHeight();
            float stroke = Math.max(dp(2), width * 0.045f);
            float lensWidth = width * 0.27f;
            float lensHeight = height * 0.22f;
            float top = height * 0.38f;
            float left = width * 0.15f;
            float right = width - left - lensWidth;
            float radius = lensHeight * 0.45f;
            int color = ready
                    ? COLOR_SUCCESS
                    : (connected ? COLOR_ACCENT_BRIGHT : COLOR_SECONDARY);

            paint.setColor(color);
            paint.setStyle(Paint.Style.STROKE);
            paint.setStrokeWidth(stroke);
            canvas.drawRoundRect(
                    left,
                    top,
                    left + lensWidth,
                    top + lensHeight,
                    radius,
                    radius,
                    paint);
            canvas.drawRoundRect(
                    right,
                    top,
                    right + lensWidth,
                    top + lensHeight,
                    radius,
                    radius,
                    paint);
            canvas.drawLine(
                    left + lensWidth,
                    top + lensHeight * 0.46f,
                    right,
                    top + lensHeight * 0.46f,
                    paint);
            canvas.drawLine(
                    left,
                    top + lensHeight * 0.32f,
                    width * 0.07f,
                    top + lensHeight * 0.16f,
                    paint);
            canvas.drawLine(
                    right + lensWidth,
                    top + lensHeight * 0.32f,
                    width * 0.93f,
                    top + lensHeight * 0.16f,
                    paint);
        }
    }

    private final class CompanionUnityPresentation extends Presentation {
        private FrameLayout unityContainer;

        CompanionUnityPresentation(Display display) {
            super(JellyfinRayNeoActivity.this, display);
            setCancelable(false);
        }

        @Override
        protected void onCreate(Bundle savedInstanceState) {
            super.onCreate(savedInstanceState);
            unityContainer = new FrameLayout(getContext());
            unityContainer.setBackgroundColor(Color.BLACK);
            setContentView(unityContainer);

            if (mUnityPlayer == null) {
                return;
            }

            ViewParent parent = mUnityPlayer.getParent();
            if (parent instanceof ViewGroup) {
                ((ViewGroup) parent).removeView(mUnityPlayer);
            }
            unityContainer.addView(mUnityPlayer, new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT));
            onFallbackPresentationShown();
        }

        boolean ownsUnityPlayer() {
            return unityContainer != null
                    && mUnityPlayer != null
                    && mUnityPlayer.getParent() == unityContainer;
        }

        void releaseSafely() {
            try {
                if (ownsUnityPlayer()) {
                    unityContainer.removeView(mUnityPlayer);
                }
                if (isShowing()) {
                    super.dismiss();
                }
            } catch (Exception ignored) {
            }
        }
    }

    private static final class DiscoveredServer {
        final String name;
        final String address;
        final String id;

        DiscoveredServer(String name, String address, String id) {
            this.name = name;
            this.address = address;
            this.id = id;
        }
    }

    private static final class CompanionHttpException extends Exception {
        final int statusCode;
        final String response;

        CompanionHttpException(int statusCode, String response) {
            super("HTTP " + statusCode);
            this.statusCode = statusCode;
            this.response = response == null ? "" : response.trim();
        }
    }
}
