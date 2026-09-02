package com.jellyfinforrayneo.companion;

import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.net.wifi.WifiManager;
import android.os.Bundle;
import android.os.SystemClock;
import android.text.InputType;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.view.WindowManager;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import com.tcl.xr.api.AirApi;
import com.tcl.unity.unityadapter.UnityXRSupportActivity;

import org.json.JSONObject;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.InterfaceAddress;
import java.net.NetworkInterface;
import java.net.SocketTimeoutException;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Enumeration;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class JellyfinRayNeoActivity extends UnityXRSupportActivity {
    private static final int LOGIN_MESSAGE_TYPE = 1000;
    private static final int QUICK_CONNECT_MESSAGE_TYPE = 1001;
    private static final int CANCEL_QUICK_CONNECT_MESSAGE_TYPE = 1002;
    private static final int DISCOVERY_PORT = 7359;
    private static final int DISCOVERY_DURATION_MS = 3000;
    private static final byte[] DISCOVERY_MESSAGE =
            "who is JellyfinServer?".getBytes(StandardCharsets.UTF_8);

    private static final int COLOR_BACKGROUND = Color.rgb(8, 10, 18);
    private static final int COLOR_SURFACE = Color.rgb(20, 23, 36);
    private static final int COLOR_FIELD = Color.rgb(34, 38, 55);
    private static final int COLOR_PRIMARY = Color.rgb(244, 246, 255);
    private static final int COLOR_SECONDARY = Color.rgb(166, 174, 199);
    private static final int COLOR_ACCENT = Color.rgb(91, 104, 255);
    private static final int COLOR_ACCENT_BRIGHT = Color.rgb(130, 174, 255);
    private static final int COLOR_ERROR = Color.rgb(255, 126, 145);

    private FrameLayout companionOverlay;
    private EditText serverInput;
    private EditText usernameInput;
    private EditText passwordInput;
    private Button discoverButton;
    private Button quickConnectButton;
    private Button connectButton;
    private LinearLayout discoveredServersContainer;
    private LinearLayout quickConnectPanel;
    private LinearLayout manualLoginContainer;
    private TextView discoveryStatusText;
    private TextView quickConnectCodeText;
    private TextView statusText;

    private String latestState = "initializing";
    private String latestMessage = "正在启动 Jellyfin 客户端…";
    private boolean latestIsError;
    private String latestServerUrl = "http://";
    private String latestUsername = "";
    private String latestQuickConnectCode = "";
    private boolean automaticDiscoveryStarted;
    private volatile int discoveryGeneration;
    private volatile DatagramSocket discoverySocket;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        installCompanionUi();
    }

    @Override
    public void onDestroy() {
        cancelDiscovery();
        super.onDestroy();
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
                latestState = TextUtils.isEmpty(state) ? "offline" : state;
                latestMessage = message == null ? "" : message;
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
        companionOverlay.setBackgroundColor(COLOR_BACKGROUND);
        companionOverlay.setClickable(true);
        companionOverlay.setFocusable(true);
        companionOverlay.setElevation(dp(24));

        ScrollView scrollView = new ScrollView(this);
        scrollView.setFillViewport(true);
        scrollView.setClipToPadding(false);

        LinearLayout page = new LinearLayout(this);
        page.setOrientation(LinearLayout.VERTICAL);
        page.setGravity(Gravity.CENTER_HORIZONTAL);
        page.setPadding(dp(24), dp(28), dp(24), dp(40));

        TextView eyebrow = createText(
                "RAYNEO AIR  ·  JELLYFIN COMPANION",
                13,
                COLOR_ACCENT_BRIGHT,
                Typeface.BOLD,
                Gravity.CENTER);
        page.addView(eyebrow, matchWrap(dp(8)));

        TextView title = createText(
                "连接 Jellyfin",
                30,
                COLOR_PRIMARY,
                Typeface.BOLD,
                Gravity.CENTER);
        page.addView(title, matchWrap(dp(8)));

        TextView subtitle = createText(
                "自动发现同一局域网内的服务器，并在手机上完成安全登录。",
                15,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        subtitle.setLineSpacing(0f, 1.15f);
        page.addView(subtitle, matchWrap(dp(22)));

        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(20), dp(22), dp(20), dp(22));
        card.setBackground(rounded(COLOR_SURFACE, 22));
        page.addView(card, matchWrap(dp(18)));

        card.addView(createLabel("Jellyfin 服务器"), matchWrap(dp(7)));

        LinearLayout serverRow = new LinearLayout(this);
        serverRow.setOrientation(LinearLayout.HORIZONTAL);
        serverRow.setGravity(Gravity.CENTER_VERTICAL);
        card.addView(serverRow, matchHeight(52, 8));

        serverInput = createInput("http://192.168.1.20:8096");
        serverInput.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
        LinearLayout.LayoutParams serverInputParams = new LinearLayout.LayoutParams(
                0,
                ViewGroup.LayoutParams.MATCH_PARENT,
                1f);
        serverInputParams.rightMargin = dp(10);
        serverRow.addView(serverInput, serverInputParams);

        discoverButton = createButton("发现", COLOR_FIELD);
        serverRow.addView(discoverButton, new LinearLayout.LayoutParams(
                dp(88),
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
        card.addView(discoveryStatusText, matchWrap(dp(6)));

        discoveredServersContainer = new LinearLayout(this);
        discoveredServersContainer.setOrientation(LinearLayout.VERTICAL);
        discoveredServersContainer.setVisibility(View.GONE);
        card.addView(discoveredServersContainer, matchWrap(dp(10)));

        quickConnectButton = createButton("使用 Jellyfin 快速登录", COLOR_ACCENT);
        quickConnectButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                submitQuickConnect();
            }
        });
        card.addView(quickConnectButton, matchHeight(54, 8));

        TextView quickHint = createText(
                "无需在此输入密码，使用已登录的 Jellyfin App 或网页授权一次即可。",
                12,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        quickHint.setLineSpacing(0f, 1.12f);
        card.addView(quickHint, matchWrap(dp(14)));

        quickConnectPanel = createQuickConnectPanel();
        quickConnectPanel.setVisibility(View.GONE);
        card.addView(quickConnectPanel, matchWrap(dp(14)));

        manualLoginContainer = new LinearLayout(this);
        manualLoginContainer.setOrientation(LinearLayout.VERTICAL);
        card.addView(manualLoginContainer, matchWrap(dp(4)));

        TextView alternative = createText(
                "或使用帐号密码",
                13,
                COLOR_SECONDARY,
                Typeface.BOLD,
                Gravity.CENTER);
        manualLoginContainer.addView(alternative, matchWrap(dp(14)));

        manualLoginContainer.addView(createLabel("用户名"), matchWrap(dp(7)));
        usernameInput = createInput("Jellyfin 用户名");
        usernameInput.setInputType(
                InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_NORMAL);
        manualLoginContainer.addView(usernameInput, matchHeight(52, 14));

        manualLoginContainer.addView(createLabel("密码"), matchWrap(dp(7)));
        passwordInput = createInput("密码仅用于本次登录");
        passwordInput.setInputType(
                InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        passwordInput.setSaveEnabled(false);
        passwordInput.setImportantForAutofill(
                View.IMPORTANT_FOR_AUTOFILL_NO_EXCLUDE_DESCENDANTS);
        manualLoginContainer.addView(passwordInput, matchHeight(52, 18));

        connectButton = createButton("连接并在眼镜中打开", COLOR_ACCENT);
        connectButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                submitLogin();
            }
        });
        manualLoginContainer.addView(connectButton, matchHeight(54, 10));

        statusText = createText(
                latestMessage,
                14,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        statusText.setLineSpacing(0f, 1.12f);
        card.addView(statusText, matchWrap(0));

        TextView privacy = createText(
                "密码与快速登录 secret 不会保存或显示；登录后仅保留 Jellyfin 会话令牌。",
                12,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        page.addView(privacy, matchWrap(0));

        scrollView.addView(page, new ScrollView.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.WRAP_CONTENT));
        companionOverlay.addView(scrollView, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));
        content.addView(companionOverlay, new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));

        serverInput.setText(latestServerUrl);
        usernameInput.setText(latestUsername);
        applyCompanionState();
    }

    private LinearLayout createQuickConnectPanel() {
        LinearLayout panel = new LinearLayout(this);
        panel.setOrientation(LinearLayout.VERTICAL);
        panel.setGravity(Gravity.CENTER_HORIZONTAL);
        panel.setPadding(dp(16), dp(16), dp(16), dp(16));
        panel.setBackground(rounded(COLOR_FIELD, 16));

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
        String serverUrl = validatedServerUrl();
        if (serverUrl == null) {
            return;
        }

        String username = usernameInput.getText().toString().trim();
        String password = passwordInput.getText().toString();
        if (TextUtils.isEmpty(username)) {
            showLocalError("请输入 Jellyfin 用户名。");
            usernameInput.requestFocus();
            return;
        }

        try {
            JSONObject payload = new JSONObject();
            payload.put("serverUrl", serverUrl);
            payload.put("username", username);
            payload.put("password", password);
            AirApi.ins().onCommandRespToUnity(LOGIN_MESSAGE_TYPE, payload.toString());

            latestServerUrl = serverUrl;
            latestUsername = username;
            latestState = "connecting";
            latestMessage = "正在验证服务器与用户…";
            latestIsError = false;
            latestQuickConnectCode = "";
            applyCompanionState();
        } catch (Exception ignored) {
            showLocalError("登录信息发送失败，请确认 Unity 与眼镜连接后重试。");
        } finally {
            passwordInput.getText().clear();
        }
    }

    private void submitQuickConnect() {
        String serverUrl = validatedServerUrl();
        if (serverUrl == null) {
            return;
        }

        try {
            JSONObject payload = new JSONObject();
            payload.put("serverUrl", serverUrl);
            AirApi.ins().onCommandRespToUnity(
                    QUICK_CONNECT_MESSAGE_TYPE,
                    payload.toString());

            latestServerUrl = serverUrl;
            latestState = "connecting";
            latestMessage = "正在向 Jellyfin 申请快速登录码…";
            latestIsError = false;
            latestQuickConnectCode = "";
            applyCompanionState();
        } catch (Exception ignored) {
            showLocalError("快速登录请求发送失败，请确认 Unity 与眼镜连接后重试。");
        }
    }

    private void cancelQuickConnect() {
        try {
            AirApi.ins().onCommandRespToUnity(
                    CANCEL_QUICK_CONNECT_MESSAGE_TYPE,
                    "{}");
            latestState = "connecting";
            latestMessage = "正在取消快速登录…";
            latestIsError = false;
            applyCompanionState();
        } catch (Exception ignored) {
            showLocalError("无法取消快速登录，请重新启动应用。");
        }
    }

    private String validatedServerUrl() {
        String serverUrl = serverInput.getText().toString().trim();
        if (TextUtils.isEmpty(serverUrl)) {
            showLocalError("请先选择或输入 Jellyfin 服务器地址。");
            serverInput.requestFocus();
            return null;
        }
        if (!serverUrl.startsWith("http://") && !serverUrl.startsWith("https://")) {
            showLocalError("服务器地址需要以 http:// 或 https:// 开头。");
            serverInput.requestFocus();
            return null;
        }
        return serverUrl;
    }

    private void discoverServers() {
        if (!"login_required".equals(latestState)) {
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
                discoverButton.setEnabled("login_required".equals(latestState));
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

        boolean ready = "ready".equals(latestState);
        boolean canEdit = "login_required".equals(latestState);
        boolean waitingForQuickConnect = "quick_connect_waiting".equals(latestState)
                && !TextUtils.isEmpty(latestQuickConnectCode);

        companionOverlay.setVisibility(ready ? View.GONE : View.VISIBLE);
        if (ready) {
            cancelDiscovery();
            passwordInput.getText().clear();
            hideKeyboard();
            return;
        }

        setControlsEnabled(canEdit);
        quickConnectPanel.setVisibility(waitingForQuickConnect ? View.VISIBLE : View.GONE);
        quickConnectButton.setVisibility(waitingForQuickConnect ? View.GONE : View.VISIBLE);
        manualLoginContainer.setVisibility(waitingForQuickConnect ? View.GONE : View.VISIBLE);
        if (waitingForQuickConnect) {
            quickConnectCodeText.setText(formatQuickConnectCode(latestQuickConnectCode));
        }

        if (!canEdit) {
            discoveredServersContainer.setVisibility(View.GONE);
            discoveryStatusText.setVisibility(View.GONE);
        }

        if (canEdit && !TextUtils.isEmpty(latestServerUrl)) {
            serverInput.setText(latestServerUrl);
            serverInput.setSelection(serverInput.length());
        }
        if (canEdit && latestUsername != null) {
            usernameInput.setText(latestUsername);
            usernameInput.setSelection(usernameInput.length());
        }

        if ("connecting".equals(latestState)) {
            connectButton.setText("正在连接…");
            quickConnectButton.setText("正在申请登录码…");
        } else if ("initializing".equals(latestState)) {
            connectButton.setText("正在启动…");
            quickConnectButton.setText("正在启动…");
        } else if ("offline".equals(latestState)) {
            connectButton.setText("Unity 尚未运行");
            quickConnectButton.setText("Unity 尚未运行");
        } else {
            connectButton.setText("连接并在眼镜中打开");
            quickConnectButton.setText("使用 Jellyfin 快速登录");
        }

        statusText.setText(TextUtils.isEmpty(latestMessage)
                ? defaultMessageForState(latestState)
                : latestMessage);
        statusText.setTextColor(latestIsError ? COLOR_ERROR : COLOR_SECONDARY);

        if (canEdit && !automaticDiscoveryStarted) {
            automaticDiscoveryStarted = true;
            serverInput.post(new Runnable() {
                @Override
                public void run() {
                    if ("login_required".equals(latestState)) {
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
        float alpha = enabled ? 1f : 0.62f;
        serverInput.setAlpha(alpha);
        discoverButton.setAlpha(alpha);
        quickConnectButton.setAlpha(alpha);
        usernameInput.setAlpha(alpha);
        passwordInput.setAlpha(alpha);
        connectButton.setAlpha(alpha);
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
    }

    private String defaultMessageForState(String state) {
        if ("quick_connect_waiting".equals(state)) {
            return "等待 Jellyfin 授权快速登录码…";
        }
        if ("connecting".equals(state)) {
            return "正在连接 Jellyfin…";
        }
        if ("initializing".equals(state)) {
            return "正在启动 Jellyfin 客户端…";
        }
        if ("offline".equals(state)) {
            return "Unity 尚未运行，请重新启动应用。";
        }
        return "请选择服务器，然后使用快速登录或帐号密码。";
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
        input.setHint(hint);
        input.setHintTextColor(Color.rgb(115, 123, 150));
        input.setTextColor(COLOR_PRIMARY);
        input.setTextSize(16);
        input.setPadding(dp(16), 0, dp(16), 0);
        input.setBackground(rounded(COLOR_FIELD, 13));
        return input;
    }

    private Button createButton(String text, int color) {
        Button button = new Button(this);
        button.setAllCaps(false);
        button.setText(text);
        button.setTextColor(Color.WHITE);
        button.setTextSize(15);
        button.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        button.setGravity(Gravity.CENTER);
        button.setPadding(dp(10), 0, dp(10), 0);
        button.setBackground(rounded(color, 14));
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
        return view;
    }

    private GradientDrawable rounded(int color, int radiusDp) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setColor(color);
        drawable.setCornerRadius(dp(radiusDp));
        return drawable;
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
}
