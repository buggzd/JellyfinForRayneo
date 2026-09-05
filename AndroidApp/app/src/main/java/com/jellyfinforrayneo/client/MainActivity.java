package com.jellyfinforrayneo.client;

import android.app.Activity;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageInfo;
import android.graphics.Color;
import android.media.AudioManager;
import android.net.ConnectivityManager;
import android.net.LinkAddress;
import android.net.LinkProperties;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.View;
import android.view.WindowManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.net.Inet4Address;
import java.net.Inet6Address;
import java.net.InetAddress;
import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public final class MainActivity extends Activity
{
    private static final int MAX_PASSWORD_LENGTH = 4_096;
    private static final int MAX_SCREEN_LENGTH = 32;

    private final ArrayList<JellyfinDiscoveryService.Server> discoveredServers =
            new ArrayList<>();
    private final DiagnosticLog diagnosticLog = new DiagnosticLog();
    private final PlaybackSnapshot playback = new PlaybackSnapshot();

    private SessionRepository sessions;
    private JellyfinAuthenticationService authentication;
    private JellyfinDiscoveryService discovery;
    private RemoteCommandRouter remoteCommands;
    private RayNeoDisplayController rayNeoDisplay;
    private GlassesPresentationController glassesPresentation;
    private CompanionWebViewController companionWebView;

    private String state = "login_required";
    private String message = "请选择 Jellyfin 服务器并登录。";
    private String selectedServerUrl = "";
    private String selectedServerName = "";
    private String selectedUserName = "";
    private String quickConnectCode = "";
    private String discoveryMessage = "";
    private String webScreen = "connect";
    private String glassesRuntimeState = "booting";
    private String glassesRuntimeErrorCode = "none";
    private String glassesSearchQuery = "";
    private int glassesCatalogGeneration;
    private boolean error;
    private boolean busy;
    private boolean discoveryScanning;
    private boolean glassesWebReady;
    private boolean glassesSearchActive;
    private boolean destroyed;

    @Override
    protected void onCreate(Bundle savedInstanceState)
    {
        super.onCreate(savedInstanceState);
        diagnosticLog.record(DiagnosticLog.Event.APP_CREATED);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        setVolumeControlStream(AudioManager.STREAM_MUSIC);

        sessions = new SessionRepository(getSharedPreferences(
                SessionRepository.PREFERENCES_NAME,
                Context.MODE_PRIVATE));
        restoreSessionState();

        remoteCommands = new RemoteCommandRouter();
        rayNeoDisplay = new RayNeoDisplayController(
                this,
                sessions.getDisplayMode(),
                this::onDisplayModeStateChanged);
        rayNeoDisplay.start();

        glassesPresentation = new GlassesPresentationController(
                this,
                rayNeoDisplay.getState(),
                new GlassesPresentationController.Callback()
                {
                    @Override
                    public void onDisplayConnectionChanged(boolean connected)
                    {
                        diagnosticLog.record(connected
                                ? DiagnosticLog.Event.GLASSES_CONNECTED
                                : DiagnosticLog.Event.GLASSES_DISCONNECTED);
                        rayNeoDisplay.setConnected(connected);
                        pushCompanionState();
                    }

                    @Override
                    public void onStereoOutputChanged(DisplayOutputGeometry output)
                    {
                        rayNeoDisplay.setSystemDisplayDisabled(glassesPresentation != null
                                && glassesPresentation.isSystemDisplayDisabled());
                        rayNeoDisplay.setOutputGeometry(output);
                        pushCompanionState();
                    }

                    @Override
                    public void onWebReadyChanged(boolean ready)
                    {
                        diagnosticLog.record(ready
                                ? DiagnosticLog.Event.GLASSES_WEB_READY
                                : DiagnosticLog.Event.GLASSES_WEB_NOT_READY);
                        glassesWebReady = ready;
                        if (!ready)
                        {
                            glassesRuntimeState = "booting";
                            glassesRuntimeErrorCode = "none";
                            glassesSearchActive = false;
                            glassesSearchQuery = "";
                            if (companionWebView != null)
                            {
                                companionWebView.hideSearchKeyboard();
                            }
                            if (sessions != null && sessions.hasSession())
                            {
                                state = "session_ready";
                                message = "眼镜端正在启动，Jellyfin 会话仍保存在手机中。";
                                error = false;
                            }
                        }
                        remoteCommands.setReady(ready);
                        pushCompanionState();
                    }

                    @Override
                    public void onGlassesMessage(GlassesMessage message)
                    {
                        handleGlassesMessage(message);
                    }

                    @Override
                    public JSONObject buildBootstrap()
                    {
                        return buildGlassesBootstrap();
                    }
                });
        glassesPresentation.setStereoScreenSettings(sessions.getStereoScreenSettings());
        remoteCommands.setSink(glassesPresentation::dispatchCommand);

        authentication = new JellyfinAuthenticationService(
                sessions,
                Build.MODEL,
                new JellyfinAuthenticationService.Callback()
                {
                    @Override
                    public void onQuickConnectCode(int generation, String code)
                    {
                        runOnUiThread(() ->
                        {
                            if (destroyed || !authentication.isCurrent(generation))
                            {
                                return;
                            }
                            diagnosticLog.record(DiagnosticLog.Event.AUTH_QUICK_CODE_RECEIVED);
                            state = "quick_connect_waiting";
                            message = "请在 Jellyfin App 或网页中确认此登录码。";
                            quickConnectCode = code;
                            error = false;
                            pushCompanionState();
                        });
                    }

                    @Override
                    public void onAuthenticated(
                            int generation,
                            SessionPayload session,
                            boolean persist)
                    {
                        runOnUiThread(() ->
                        {
                            if (!destroyed && authentication.isCurrent(generation))
                            {
                                finishAuthentication(session, persist);
                            }
                        });
                    }

                    @Override
                    public void onError(int generation, String failureMessage)
                    {
                        runOnUiThread(() ->
                        {
                            if (destroyed || !authentication.isCurrent(generation))
                            {
                                return;
                            }
                            diagnosticLog.record(DiagnosticLog.Event.AUTH_FAILED);
                            busy = false;
                            state = "login_required";
                            message = failureMessage;
                            quickConnectCode = "";
                            error = true;
                            pushCompanionState();
                        });
                    }
                });
        discovery = new JellyfinDiscoveryService(
                this,
                (generation, servers, failed) -> runOnUiThread(
                        () ->
                        {
                            if (!destroyed && discovery.isCurrent(generation))
                            {
                                finishDiscovery(servers, failed);
                            }
                        }));

        companionWebView = new CompanionWebViewController(
                this,
                new CompanionBridge(),
                this::buildCompanionStateJson);
        setContentView(companionWebView.getView());
        companionWebView.start();
        glassesPresentation.start();
        updatePhoneSurface();

        if (!sessions.hasSession())
        {
            companionWebView.getView().postDelayed(this::startDiscovery, 500L);
        }
    }

    @Override
    protected void onResume()
    {
        super.onResume();
        diagnosticLog.record(DiagnosticLog.Event.APP_RESUMED);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        if (glassesPresentation != null)
        {
            glassesPresentation.refresh();
        }
        if (rayNeoDisplay != null)
        {
            rayNeoDisplay.onResume();
        }
    }

    @Override
    protected void onPause()
    {
        diagnosticLog.record(DiagnosticLog.Event.APP_PAUSED);
        if (rayNeoDisplay != null)
        {
            rayNeoDisplay.onPause();
        }
        super.onPause();
    }

    @Override
    protected void onDestroy()
    {
        diagnosticLog.record(DiagnosticLog.Event.APP_DESTROYED);
        destroyed = true;
        if (authentication != null)
        {
            authentication.close();
        }
        if (discovery != null)
        {
            discovery.close();
        }
        if (companionWebView != null)
        {
            companionWebView.destroy();
        }
        if (glassesPresentation != null)
        {
            glassesPresentation.stop();
        }
        if (rayNeoDisplay != null)
        {
            rayNeoDisplay.destroy();
        }
        super.onDestroy();
    }

    @Override
    public void onBackPressed()
    {
        if ("touchpad".equals(webScreen))
        {
            remoteCommands.submit("back");
            companionWebView.haptic(true);
            companionWebView.handleBack();
            return;
        }
        if ("settings".equals(webScreen) || "auth".equals(webScreen))
        {
            companionWebView.handleBack();
            return;
        }
        super.onBackPressed();
    }

    @Override
    public boolean dispatchKeyEvent(KeyEvent event)
    {
        if (event != null && event.getAction() == KeyEvent.ACTION_DOWN)
        {
            String command = commandForKey(event.getKeyCode());
            if (command != null && remoteCommands.submit(command))
            {
                return true;
            }
        }

        boolean volumeKey = event != null && isVolumeKey(event.getKeyCode());
        boolean handled = super.dispatchKeyEvent(event);
        if (volumeKey && event.getAction() == KeyEvent.ACTION_DOWN)
        {
            getWindow().getDecorView().post(this::publishVolume);
        }
        return handled;
    }

    private void restoreSessionState()
    {
        SessionPayload session = sessions.getSession();
        if (session != null)
        {
            diagnosticLog.record(DiagnosticLog.Event.SESSION_RESTORED);
            selectedServerUrl = session.getServerUrl();
            selectedServerName = session.getServerName();
            selectedUserName = session.getUserName();
            state = "session_ready";
            message = "Jellyfin 会话已恢复，连接眼镜后会自动同步媒体库。";
        }
        else
        {
            diagnosticLog.record(DiagnosticLog.Event.SESSION_EMPTY);
            selectedServerUrl = normalizedServerInput(sessions.getServerHint());
            selectedUserName = bounded(
                    sessions.getUserNameHint(),
                    SessionPayload.MAX_USER_NAME_LENGTH).trim();
            sessions.setServerHint(selectedServerUrl);
            sessions.setUserNameHint(selectedUserName);
        }
    }

    private void finishAuthentication(SessionPayload session, boolean persist)
    {
        if (destroyed || session == null)
        {
            return;
        }
        sessions.save(session, persist);
        diagnosticLog.record(persist
                ? DiagnosticLog.Event.AUTH_SUCCEEDED_PERSISTED
                : DiagnosticLog.Event.AUTH_SUCCEEDED_EPHEMERAL);
        selectedServerUrl = session.getServerUrl();
        selectedServerName = session.getServerName();
        selectedUserName = session.getUserName();
        state = "session_ready";
        message = persist
                ? "Jellyfin 会话已保存，正在同步眼镜媒体库。"
                : "Jellyfin 已连接；会话仅在本次运行期间保留。";
        quickConnectCode = "";
        glassesRuntimeState = "loading";
        glassesRuntimeErrorCode = "none";
        advanceGlassesCatalogGeneration();
        busy = false;
        error = false;
        discovery.cancel();
        discoveryScanning = false;
        glassesPresentation.refreshBootstrap();
        pushCompanionState();
    }

    private void clearSession(boolean unauthorized)
    {
        glassesPresentation.setStereoTestPattern(false);
        authentication.cancel();
        diagnosticLog.record(unauthorized
                ? DiagnosticLog.Event.SESSION_UNAUTHORIZED
                : DiagnosticLog.Event.SESSION_CLEARED);
        sessions.clear();
        remoteCommands.clear();
        playback.clear();
        glassesSearchActive = false;
        glassesSearchQuery = "";
        companionWebView.hideSearchKeyboard();
        quickConnectCode = "";
        glassesRuntimeState = "no-session";
        glassesRuntimeErrorCode = "none";
        state = "login_required";
        message = unauthorized
                ? "Jellyfin 会话已失效，请在手机端重新登录。"
                : "Jellyfin 会话已清除，请重新选择服务器并登录。";
        busy = false;
        error = unauthorized;
        glassesPresentation.refreshBootstrap();
        companionWebView.openScreen("connect");
        pushCompanionState();
    }

    private void startDiscovery()
    {
        if (destroyed || busy || discoveryScanning || sessions.hasSession())
        {
            return;
        }
        discoveryScanning = true;
        diagnosticLog.record(DiagnosticLog.Event.DISCOVERY_STARTED);
        discoveryMessage = "正在搜索同一 Wi-Fi 中的 Jellyfin 服务器…";
        discoveredServers.clear();
        discovery.scan();
        pushCompanionState();
    }

    private void finishDiscovery(List<JellyfinDiscoveryService.Server> servers, boolean failed)
    {
        if (destroyed)
        {
            return;
        }
        discoveryScanning = false;
        discoveredServers.clear();
        discoveredServers.addAll(servers);
        diagnosticLog.record(failed
                ? DiagnosticLog.Event.DISCOVERY_FAILED
                : servers.isEmpty()
                        ? DiagnosticLog.Event.DISCOVERY_EMPTY
                        : DiagnosticLog.Event.DISCOVERY_FOUND);
        if (servers.isEmpty())
        {
            discoveryMessage = failed
                    ? "自动发现失败，请手动输入服务器地址。"
                    : "未发现服务器，请确认手机与 Jellyfin 在同一 Wi-Fi。";
        }
        else
        {
            discoveryMessage = "发现 " + servers.size() + " 台 Jellyfin 服务器。";
            if (servers.size() == 1 && selectedServerUrl.trim().isEmpty())
            {
                selectedServerUrl = servers.get(0).address;
                selectedServerName = servers.get(0).name;
            }
        }
        pushCompanionState();
    }

    private void onDisplayModeStateChanged(DisplayModeStateMachine.State displayState)
    {
        // Keep the remote usable while this Activity is visible; this flag does not survive leaving the app.
        if (displayState.connected)
        {
            getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        }
        else
        {
            getWindow().clearFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        }
        recordDisplayDiagnostic(displayState);
        if (glassesPresentation != null)
        {
            glassesPresentation.setDisplayState(displayState);
        }
        pushCompanionState();
    }

    private void handleGlassesMessage(GlassesMessage incoming)
    {
        if (incoming == null)
        {
            return;
        }
        switch (incoming.type)
        {
            case MANAGE_LOGIN:
                companionWebView.openScreen(sessions.hasSession() ? "settings" : "connect");
                break;
            case LOGOUT:
                clearSession(false);
                break;
            case UNAUTHORIZED:
                clearSession(true);
                break;
            case PLAYBACK_STATE:
                playback.update(incoming);
                pushCompanionState();
                break;
            case RUNTIME_STATE:
                recordRuntimeDiagnostic(incoming);
                glassesRuntimeState = incoming.state;
                glassesRuntimeErrorCode = incoming.errorCode;
                if ("error".equals(incoming.state))
                {
                    state = "glasses_error";
                    message = glassesRuntimeErrorMessage(incoming.errorCode);
                    error = true;
                }
                else if ("ready".equals(incoming.state))
                {
                    state = "ready";
                    message = "Jellyfin 已连接，媒体库正在眼镜中显示。";
                    error = false;
                }
                else if ("loading".equals(incoming.state))
                {
                    state = sessions.hasSession() ? "session_ready" : "login_required";
                    message = sessions.hasSession()
                            ? "眼镜端正在连接 Jellyfin 并加载媒体库。"
                            : "请先在手机端登录 Jellyfin。";
                    error = false;
                }
                else if ("booting".equals(incoming.state))
                {
                    state = sessions.hasSession() ? "session_ready" : "login_required";
                    message = sessions.hasSession()
                            ? "眼镜端正在启动，Jellyfin 会话仍保存在手机中。"
                            : "请先在手机端登录 Jellyfin。";
                    error = false;
                }
                else if ("no-session".equals(incoming.state))
                {
                    state = sessions.hasSession() ? "session_ready" : "login_required";
                    message = sessions.hasSession()
                            ? "眼镜端正在等待 Jellyfin 会话同步。"
                            : "请先在手机端登录 Jellyfin。";
                    error = false;
                }
                pushCompanionState();
                break;
            case SEARCH_STATE:
            {
                boolean wasSearchActive = glassesSearchActive;
                glassesSearchActive = "active".equals(incoming.state);
                glassesSearchQuery = glassesSearchActive ? incoming.query : "";
                if (!glassesSearchActive)
                {
                    remoteCommands.clear();
                }
                pushCompanionState();
                if (glassesSearchActive && !wasSearchActive)
                {
                    companionWebView.showSearchKeyboard();
                }
                else if (!glassesSearchActive && wasSearchActive)
                {
                    companionWebView.hideSearchKeyboard();
                }
                break;
            }
            default:
                break;
        }
    }

    private JSONObject buildGlassesBootstrap()
    {
        JSONObject result = new JSONObject();
        try
        {
            DisplayModeStateMachine.State displayState = rayNeoDisplay == null
                    ? new DisplayModeStateMachine(DisplayModeStateMachine.MIRROR_2D).snapshot()
                    : rayNeoDisplay.getState();
            result.put("source", "android");
            result.put("displayMode", displayState.requestedMode);
            result.put("glassesConnected", displayState.connected);
            result.put("catalogGeneration", glassesCatalogGeneration);
            SessionPayload session = sessions == null ? null : sessions.getSession();
            result.put("session", session == null ? JSONObject.NULL : session.toJsonObject());
        }
        catch (Exception ignored)
        {
            return new JSONObject();
        }
        return result;
    }

    private String buildCompanionStateJson()
    {
        JSONObject result = new JSONObject();
        try
        {
            SessionPayload session = sessions.getSession();
            DisplayModeStateMachine.State displayState = rayNeoDisplay.getState();
            boolean mediaReady = "ready".equals(glassesRuntimeState) && session != null;
            result.put("state", state);
            result.put("message", message);
            result.put("isError", error);
            result.put("serverUrl", session == null ? selectedServerUrl : session.getServerUrl());
            result.put("serverName", session == null ? selectedServerName : session.getServerName());
            result.put("serverVersion", session == null ? "" : session.getServerVersion());
            result.put("serverId", session == null ? "" : session.getServerId());
            result.put("username", session == null ? selectedUserName : session.getUserName());
            result.put("quickConnectCode", quickConnectCode);
            result.put("sessionAvailable", session != null);
            result.put("sessionSaved", sessions.isPersisted());
            result.put("busy", busy);
            result.put("webHardwareAccelerated",
                    companionWebView != null && companionWebView.isHardwareAccelerated());
            result.put("glassesConnected", displayState.connected);
            result.put("glassesPresentationReady", glassesWebReady);
            result.put("glassesRuntimeState", glassesRuntimeState);
            result.put("glassesRuntimeErrorCode", glassesRuntimeErrorCode);
            result.put("mediaReady", mediaReady);
            result.put("touchpadReady", glassesWebReady && mediaReady);
            result.put("searchInputActive", glassesSearchActive);
            result.put("searchQuery", glassesSearchQuery);
            result.put("displayMode", displayState.requestedMode);
            result.put("activeDisplayMode", displayState.activeMode);
            result.put("displayModeApplied", displayState.displayModeApplied);
            result.put("displayModeTransitioning", displayState.displayModeTransitioning);
            result.put("displayMessage", displayState.message);
            result.put("glassesDisplayDisabled", glassesPresentation != null
                    && glassesPresentation.isSystemDisplayDisabled());
            result.put("stereoScreen", sessions.getStereoScreenSettings().toJson());
            result.put("stereoOutput", glassesPresentation == null
                    ? DisplayOutputGeometry.EMPTY.toJson() : glassesPresentation.getOutputGeometry().toJson());
            result.put("stereoTestPattern", glassesPresentation != null
                    && glassesPresentation.isStereoTestPatternEnabled());
            result.put("discoveryMessage", discoveryMessage);
            result.put("discoveryError", !discoveryScanning && discoveredServers.isEmpty());
            result.put("discoveryScanning", discoveryScanning);
            result.put("playback", playback.toJson());

            JSONArray servers = new JSONArray();
            for (JellyfinDiscoveryService.Server server : discoveredServers)
            {
                JSONObject item = new JSONObject();
                item.put("id", server.id.isEmpty() ? server.address : server.id);
                item.put("name", server.name);
                item.put("host", server.address);
                item.put("detail", "Jellyfin 服务器");
                item.put("latency", "局域网");
                item.put("strength", 3);
                servers.put(item);
            }
            result.put("servers", servers);
        }
        catch (Exception ignored)
        {
            return new JSONObject().toString();
        }
        return result.toString();
    }

    private void pushCompanionState()
    {
        if (companionWebView != null && !destroyed)
        {
            companionWebView.pushState();
        }
    }

    private void updatePhoneSurface()
    {
        boolean oled = "touchpad".equals(webScreen);
        getWindow().setStatusBarColor(oled ? Color.BLACK : Color.rgb(234, 247, 250));
        getWindow().setNavigationBarColor(oled ? Color.BLACK : Color.rgb(229, 245, 249));
        int flags = View.SYSTEM_UI_FLAG_LAYOUT_STABLE;
        if (!oled)
        {
            flags |= View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR
                    | View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
        }
        getWindow().getDecorView().setSystemUiVisibility(flags);
    }

    private void publishVolume()
    {
        AudioManager audio = (AudioManager) getSystemService(Context.AUDIO_SERVICE);
        if (audio == null)
        {
            return;
        }
        int maximum = audio.getStreamMaxVolume(AudioManager.STREAM_MUSIC);
        int current = audio.getStreamVolume(AudioManager.STREAM_MUSIC);
        int percentage = maximum <= 0
                ? 0
                : Math.round(Math.max(0, Math.min(current, maximum)) * 100f / maximum);
        remoteCommands.submitVolume(percentage);
    }

    private void copyQuickConnectCode()
    {
        if (quickConnectCode.isEmpty())
        {
            return;
        }
        ClipboardManager clipboard =
                (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
        if (clipboard != null)
        {
            clipboard.setPrimaryClip(
                    ClipData.newPlainText("Jellyfin Quick Connect", quickConnectCode));
            Toast.makeText(this, "快速登录码已复制", Toast.LENGTH_SHORT).show();
        }
    }

    private void openQuickConnectAuthorization()
    {
        if (selectedServerUrl.isEmpty() || quickConnectCode.isEmpty())
        {
            return;
        }
        String target = selectedServerUrl.replaceAll("/+$", "")
                + "/web/#/quickconnect?code="
                + Uri.encode(quickConnectCode);
        try
        {
            startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(target)));
        }
        catch (RuntimeException exception)
        {
            Toast.makeText(
                    this,
                    "无法打开授权页，请在 Jellyfin App 中手动授权。",
                    Toast.LENGTH_LONG).show();
        }
    }

    private void shareDiagnosticLog()
    {
        diagnosticLog.record(DiagnosticLog.Event.DIAGNOSTICS_SHARED);
        Intent share = new Intent(Intent.ACTION_SEND);
        share.setType("text/plain");
        share.putExtra(Intent.EXTRA_SUBJECT, "Jellyfin for RayNeo 诊断日志");
        share.putExtra(Intent.EXTRA_TEXT, buildDiagnosticReport());
        try
        {
            startActivity(Intent.createChooser(share, "分享已脱敏诊断日志"));
        }
        catch (RuntimeException exception)
        {
            Toast.makeText(this, "没有可接收诊断日志的分享应用。", Toast.LENGTH_LONG).show();
        }
    }

    private String buildDiagnosticReport()
    {
        StringBuilder result = new StringBuilder();
        result.append("Jellyfin for RayNeo diagnostics\n");
        appendDiagnostic(result, "format", "2");
        appendDiagnostic(result, "appVersion", BuildConfig.VERSION_NAME);
        appendDiagnostic(result, "appVersionCode", String.valueOf(BuildConfig.VERSION_CODE));
        appendDiagnostic(result, "androidSdk", String.valueOf(Build.VERSION.SDK_INT));
        appendDiagnostic(result, "androidRelease", Build.VERSION.RELEASE);
        appendDiagnostic(result, "deviceManufacturer", Build.MANUFACTURER);
        appendDiagnostic(result, "deviceModel", Build.MODEL);
        appendWebViewDiagnostics(result);

        SessionPayload session = sessions == null ? null : sessions.getSession();
        appendDiagnostic(result, "sessionPresent", booleanText(session != null));
        appendDiagnostic(result, "sessionPersisted", booleanText(
                sessions != null && sessions.isPersisted()));
        appendServerDiagnostics(result, session);
        appendNetworkDiagnostics(result);

        DisplayModeStateMachine.State display = rayNeoDisplay == null
                ? null
                : rayNeoDisplay.getState();
        appendDiagnostic(result, "glassesConnected", booleanText(
                display != null && display.connected));
        appendDiagnostic(result, "glassesWebReady", booleanText(glassesWebReady));
        appendDiagnostic(result, "glassesRuntimeState", glassesRuntimeState);
        appendDiagnostic(result, "glassesRuntimeError", glassesRuntimeErrorCode);
        appendDiagnostic(result, "catalogGeneration", String.valueOf(glassesCatalogGeneration));
        if (display != null)
        {
            appendDiagnostic(result, "displayRequested", display.requestedMode);
            appendDiagnostic(result, "displayActive", display.activeMode);
            appendDiagnostic(result, "displayApplied", booleanText(display.displayModeApplied));
            appendDiagnostic(
                    result,
                    "displayTransitioning",
                    booleanText(display.displayModeTransitioning));
        }
        DisplayOutputGeometry output = glassesPresentation == null
                ? DisplayOutputGeometry.EMPTY : glassesPresentation.getOutputGeometry();
        appendDiagnostic(result, "stereoOutput", output.toJson().toString());
        if (sessions != null)
        {
            appendDiagnostic(result, "stereoScreen", sessions.getStereoScreenSettings().toJson().toString());
        }
        appendDiagnostic(result, "stereoTestPattern", booleanText(glassesPresentation != null
                && glassesPresentation.isStereoTestPatternEnabled()));
        result.append("privacy=server address, account, media titles and credentials omitted\n");
        result.append("events:\n");
        result.append(diagnosticLog.exportEvents());
        return result.toString();
    }

    private void appendWebViewDiagnostics(StringBuilder result)
    {
        try
        {
            PackageInfo webView = WebView.getCurrentWebViewPackage();
            appendDiagnostic(
                    result,
                    "webViewPackage",
                    webView == null ? "unknown" : webView.packageName);
            appendDiagnostic(
                    result,
                    "webViewVersion",
                    webView == null ? "unknown" : webView.versionName);
        }
        catch (RuntimeException exception)
        {
            appendDiagnostic(result, "webViewPackage", "unknown");
            appendDiagnostic(result, "webViewVersion", "unknown");
        }
    }

    private static void appendServerDiagnostics(
            StringBuilder result,
            SessionPayload session)
    {
        String scheme = "none";
        String hostType = "none";
        boolean subpath = false;
        if (session != null)
        {
            try
            {
                URI uri = new URI(session.getServerUrl());
                scheme = "https".equalsIgnoreCase(uri.getScheme()) ? "https" : "http";
                String host = uri.getHost() == null ? "" : uri.getHost();
                if (host.contains(":"))
                {
                    hostType = "ipv6-literal";
                }
                else if (host.matches("[0-9.]+"))
                {
                    hostType = "ipv4-literal";
                }
                else
                {
                    hostType = "hostname";
                }
                String path = uri.getPath();
                subpath = path != null && !path.isEmpty() && !"/".equals(path);
            }
            catch (Exception ignored)
            {
                scheme = "invalid";
                hostType = "invalid";
            }
        }
        appendDiagnostic(result, "serverScheme", scheme);
        appendDiagnostic(result, "serverHostType", hostType);
        appendDiagnostic(result, "serverSubpath", booleanText(subpath));
    }

    private void appendNetworkDiagnostics(StringBuilder result)
    {
        ConnectivityManager manager =
                (ConnectivityManager) getSystemService(Context.CONNECTIVITY_SERVICE);
        Network active = manager == null ? null : manager.getActiveNetwork();
        NetworkCapabilities capabilities = active == null || manager == null
                ? null
                : manager.getNetworkCapabilities(active);
        LinkProperties links = active == null || manager == null
                ? null
                : manager.getLinkProperties(active);

        appendDiagnostic(result, "networkTransport", networkTransport(capabilities));
        appendDiagnostic(result, "networkInternet", booleanText(capabilities != null
                && capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET)));
        appendDiagnostic(result, "networkValidated", booleanText(capabilities != null
                && capabilities.hasCapability(NetworkCapabilities.NET_CAPABILITY_VALIDATED)));

        boolean ipv4 = false;
        boolean ipv6 = false;
        boolean nonLinkLocalIpv6 = false;
        boolean ipv6Dns = false;
        if (links != null)
        {
            for (LinkAddress link : links.getLinkAddresses())
            {
                InetAddress address = link.getAddress();
                ipv4 |= address instanceof Inet4Address;
                ipv6 |= address instanceof Inet6Address;
                nonLinkLocalIpv6 |= address instanceof Inet6Address
                        && !address.isAnyLocalAddress()
                        && !address.isLinkLocalAddress()
                        && !address.isLoopbackAddress()
                        && !address.isMulticastAddress();
            }
            for (InetAddress dns : links.getDnsServers())
            {
                ipv6Dns |= dns instanceof Inet6Address;
            }
        }
        appendDiagnostic(result, "networkIpv4", booleanText(ipv4));
        appendDiagnostic(result, "networkIpv6", booleanText(ipv6));
        appendDiagnostic(result, "networkNonLinkLocalIpv6", booleanText(nonLinkLocalIpv6));
        appendDiagnostic(result, "networkIpv6Dns", booleanText(ipv6Dns));
    }

    private static String networkTransport(NetworkCapabilities capabilities)
    {
        if (capabilities == null)
        {
            return "none";
        }
        if (capabilities.hasTransport(NetworkCapabilities.TRANSPORT_VPN))
        {
            return "vpn";
        }
        if (capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI))
        {
            return "wifi";
        }
        if (capabilities.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR))
        {
            return "cellular";
        }
        if (capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET))
        {
            return "ethernet";
        }
        if (capabilities.hasTransport(NetworkCapabilities.TRANSPORT_BLUETOOTH))
        {
            return "bluetooth";
        }
        return "other";
    }

    private static void appendDiagnostic(StringBuilder result, String key, String value)
    {
        result.append(key)
                .append('=')
                .append(safeDiagnosticText(value))
                .append('\n');
    }

    private static String safeDiagnosticText(String value)
    {
        if (value == null)
        {
            return "unknown";
        }
        String normalized = value.replace('\n', ' ').replace('\r', ' ').trim();
        String lower = normalized.toLowerCase(Locale.US);
        if (lower.contains("://")
                || lower.contains("password")
                || lower.contains("token="))
        {
            return "[redacted]";
        }
        return bounded(normalized, 120);
    }

    private static String booleanText(boolean value)
    {
        return value ? "true" : "false";
    }

    private void recordDisplayDiagnostic(DisplayModeStateMachine.State display)
    {
        if (display == null || !display.connected)
        {
            return;
        }
        if (display.displayModeTransitioning)
        {
            diagnosticLog.record(DisplayModeStateMachine.STEREO_SCREEN.equals(
                    display.requestedMode)
                    ? DiagnosticLog.Event.DISPLAY_SWITCH_3D
                    : DiagnosticLog.Event.DISPLAY_SWITCH_2D);
        }
        else if (display.displayModeApplied)
        {
            diagnosticLog.record(DisplayModeStateMachine.STEREO_SCREEN.equals(
                    display.activeMode)
                    ? DiagnosticLog.Event.DISPLAY_STEREO_APPLIED
                    : DiagnosticLog.Event.DISPLAY_MIRROR_APPLIED);
        }
        else
        {
            diagnosticLog.record(DiagnosticLog.Event.DISPLAY_SAFE_FALLBACK);
        }
    }

    private void recordRuntimeDiagnostic(GlassesMessage incoming)
    {
        switch (incoming.state)
        {
            case "booting":
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_BOOTING);
                break;
            case "loading":
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_LOADING);
                break;
            case "ready":
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_READY);
                break;
            case "no-session":
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_NO_SESSION);
                break;
            case "error":
                recordRuntimeErrorDiagnostic(incoming.errorCode);
                break;
            default:
                break;
        }
    }

    private void recordRuntimeErrorDiagnostic(String errorCode)
    {
        switch (errorCode)
        {
            case "network":
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_ERROR_NETWORK);
                break;
            case "http":
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_ERROR_HTTP);
                break;
            case "response":
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_ERROR_RESPONSE);
                break;
            default:
                diagnosticLog.record(DiagnosticLog.Event.RUNTIME_ERROR_UNKNOWN);
                break;
        }
    }

    private static String commandForKey(int keyCode)
    {
        switch (keyCode)
        {
            case KeyEvent.KEYCODE_DPAD_UP:
                return "up";
            case KeyEvent.KEYCODE_DPAD_DOWN:
                return "down";
            case KeyEvent.KEYCODE_DPAD_LEFT:
                return "left";
            case KeyEvent.KEYCODE_DPAD_RIGHT:
                return "right";
            case KeyEvent.KEYCODE_DPAD_CENTER:
            case KeyEvent.KEYCODE_ENTER:
                return "enter";
            default:
                return null;
        }
    }

    private static boolean isVolumeKey(int keyCode)
    {
        return keyCode == KeyEvent.KEYCODE_VOLUME_UP
                || keyCode == KeyEvent.KEYCODE_VOLUME_DOWN
                || keyCode == KeyEvent.KEYCODE_VOLUME_MUTE;
    }

    private static String bounded(String value, int maximumLength)
    {
        if (value == null)
        {
            return "";
        }
        return value.length() <= maximumLength ? value : value.substring(0, maximumLength);
    }

    private static String normalizedServerInput(String value)
    {
        String candidate = bounded(value, SessionPayload.MAX_SERVER_URL_LENGTH).trim();
        try
        {
            return SessionPayload.normalizeServerUrl(candidate);
        }
        catch (Exception ignored)
        {
            return "";
        }
    }

    private static String glassesRuntimeErrorMessage(String errorCode)
    {
        switch (errorCode)
        {
            case "network":
                return "眼镜端无法访问 Jellyfin。请检查当前网络和服务器地址；IPv6 带端口时必须使用方括号。";
            case "http":
                return "眼镜端已访问 Jellyfin，但媒体库请求返回了 HTTP 错误。请检查服务状态或重新登录。";
            case "response":
                return "眼镜端已收到服务器响应，但无法解析为 Jellyfin 数据。请确认地址指向 Jellyfin 服务。";
            default:
                return "眼镜端加载媒体库失败。请检查服务器地址和当前网络后重试。";
        }
    }

    private void retryGlassesRuntime()
    {
        if (!sessions.hasSession())
        {
            return;
        }
        glassesRuntimeState = "loading";
        glassesRuntimeErrorCode = "none";
        diagnosticLog.record(DiagnosticLog.Event.CATALOG_RETRY);
        advanceGlassesCatalogGeneration();
        state = "session_ready";
        message = "正在让眼镜重新连接 Jellyfin 并加载媒体库。";
        error = false;
        glassesPresentation.refreshBootstrap();
        pushCompanionState();
    }

    private void advanceGlassesCatalogGeneration()
    {
        glassesCatalogGeneration = glassesCatalogGeneration == Integer.MAX_VALUE
                ? 1
                : glassesCatalogGeneration + 1;
    }

    private void showInvalidServerAddress()
    {
        busy = false;
        state = "login_required";
        message = "服务器地址无效。IPv6 带端口时请使用 http://[IPv6地址]:端口。";
        quickConnectCode = "";
        error = true;
        pushCompanionState();
    }

    private final class CompanionBridge implements CompanionWebViewController.JavascriptBridge
    {
        @JavascriptInterface
        public String getState()
        {
            return buildCompanionStateJson();
        }

        @JavascriptInterface
        public void ready()
        {
            runOnUiThread(() ->
            {
                companionWebView.onJavascriptReady();
                if (glassesSearchActive)
                {
                    companionWebView.showSearchKeyboard();
                }
            });
        }

        @JavascriptInterface
        public void scan()
        {
            runOnUiThread(MainActivity.this::startDiscovery);
        }

        @JavascriptInterface
        public void selectServer(String serverUrl, String serverName)
        {
            String url = normalizedServerInput(serverUrl);
            String name = bounded(serverName, SessionPayload.MAX_SERVER_NAME_LENGTH).trim();
            runOnUiThread(() ->
            {
                selectedServerUrl = url;
                selectedServerName = name;
                pushCompanionState();
            });
        }

        @JavascriptInterface
        public void login(
                String serverUrl,
                String username,
                String password,
                boolean rememberSession)
        {
            if (password != null && password.length() > MAX_PASSWORD_LENGTH)
            {
                runOnUiThread(() ->
                {
                    error = true;
                    message = "密码长度超出允许范围。";
                    pushCompanionState();
                });
                return;
            }
            String url = normalizedServerInput(serverUrl);
            String user = bounded(username, SessionPayload.MAX_USER_NAME_LENGTH).trim();
            String ephemeralPassword = password == null ? "" : password;
            if (url.isEmpty())
            {
                runOnUiThread(MainActivity.this::showInvalidServerAddress);
                return;
            }
            runOnUiThread(() ->
            {
                authentication.cancel();
                selectedServerUrl = url;
                selectedUserName = user;
                sessions.setServerHint(url);
                sessions.setUserNameHint(user);
                diagnosticLog.record(DiagnosticLog.Event.AUTH_PASSWORD_STARTED);
                state = "native_connecting";
                message = "正在验证服务器与账户…";
                quickConnectCode = "";
                busy = true;
                error = false;
                pushCompanionState();
                authentication.login(url, user, ephemeralPassword, rememberSession);
            });
        }

        @JavascriptInterface
        public void startQuickConnect(String serverUrl)
        {
            String url = normalizedServerInput(serverUrl);
            if (url.isEmpty())
            {
                runOnUiThread(MainActivity.this::showInvalidServerAddress);
                return;
            }
            runOnUiThread(() ->
            {
                authentication.cancel();
                selectedServerUrl = url;
                sessions.setServerHint(url);
                diagnosticLog.record(DiagnosticLog.Event.AUTH_QUICK_STARTED);
                state = "native_connecting";
                message = "正在向 Jellyfin 申请快速登录码…";
                quickConnectCode = "";
                busy = true;
                error = false;
                pushCompanionState();
                authentication.quickConnect(url, true);
            });
        }

        @JavascriptInterface
        public void cancelQuickConnect()
        {
            runOnUiThread(() ->
            {
                authentication.cancel();
                busy = false;
                state = "login_required";
                message = "已取消快速登录。";
                quickConnectCode = "";
                error = false;
                pushCompanionState();
            });
        }

        @JavascriptInterface
        public void clearSession()
        {
            runOnUiThread(() -> MainActivity.this.clearSession(false));
        }

        @JavascriptInterface
        public void retryGlasses()
        {
            runOnUiThread(MainActivity.this::retryGlassesRuntime);
        }

        @JavascriptInterface
        public void shareDiagnostics()
        {
            runOnUiThread(MainActivity.this::shareDiagnosticLog);
        }

        @JavascriptInterface
        public void selectDisplayMode(String mode)
        {
            String normalized = DisplayModeStateMachine.normalizeMode(mode);
            runOnUiThread(() ->
            {
                sessions.setDisplayMode(normalized);
                rayNeoDisplay.requestMode(normalized);
            });
        }

        @JavascriptInterface
        public void setStereoScreen(String payload)
        {
            StereoScreenSettings settings = StereoScreenSettings.parse(payload);
            if (settings == null)
            {
                return;
            }
            runOnUiThread(() ->
            {
                if (!destroyed)
                {
                    sessions.setStereoScreenSettings(settings);
                    glassesPresentation.setStereoScreenSettings(settings);
                    pushCompanionState();
                }
            });
        }

        @JavascriptInterface
        public void setStereoTestPattern(String value)
        {
            if (!"on".equals(value) && !"off".equals(value))
            {
                return;
            }
            runOnUiThread(() ->
            {
                if (!destroyed)
                {
                    glassesPresentation.setStereoTestPattern("on".equals(value)
                            && "settings".equals(webScreen) && glassesWebReady);
                    pushCompanionState();
                }
            });
        }

        @JavascriptInterface
        public void copyQuickConnectCode()
        {
            runOnUiThread(MainActivity.this::copyQuickConnectCode);
        }

        @JavascriptInterface
        public void openQuickConnectAuthorization()
        {
            runOnUiThread(MainActivity.this::openQuickConnectAuthorization);
        }

        @JavascriptInterface
        public void remoteCommand(String value, boolean haptic)
        {
            String command = bounded(value, 32).trim().toLowerCase(Locale.US);
            runOnUiThread(() ->
            {
                if (remoteCommands.submit(command) && haptic)
                {
                    companionWebView.haptic("back".equals(command));
                }
            });
        }

        @JavascriptInterface
        public void searchText(String value)
        {
            if (value == null || value.length() > RemoteCommandRouter.MAX_SEARCH_QUERY_LENGTH)
            {
                return;
            }
            String query = value;
            runOnUiThread(() ->
            {
                if (glassesSearchActive)
                {
                    remoteCommands.submitSearchText(query);
                }
            });
        }

        @JavascriptInterface
        public void previewHaptic()
        {
            runOnUiThread(() -> companionWebView.haptic(false));
        }

        @JavascriptInterface
        public void screenChanged(String value)
        {
            String requested = bounded(value, MAX_SCREEN_LENGTH).trim().toLowerCase(Locale.US);
            if (!"connect".equals(requested)
                    && !"auth".equals(requested)
                    && !"home".equals(requested)
                    && !"settings".equals(requested)
                    && !"touchpad".equals(requested))
            {
                return;
            }
            runOnUiThread(() ->
            {
                if (destroyed)
                {
                    return;
                }
                webScreen = requested;
                if (!"settings".equals(requested))
                {
                    glassesPresentation.setStereoTestPattern(false);
                    pushCompanionState();
                }
                updatePhoneSurface();
            });
        }
    }
}
