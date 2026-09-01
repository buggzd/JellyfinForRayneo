package com.jellyfinforrayneo.companion;

import android.content.Context;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
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

import com.tcl.xr.api.AirApi;
import com.tcl.unity.unityadapter.UnityXRSupportActivity;

import org.json.JSONObject;

public final class JellyfinRayNeoActivity extends UnityXRSupportActivity {
    private static final int LOGIN_MESSAGE_TYPE = 1000;
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
    private Button connectButton;
    private TextView statusText;

    private String latestState = "initializing";
    private String latestMessage = "正在启动 Jellyfin 客户端…";
    private boolean latestIsError;
    private String latestServerUrl = "http://";
    private String latestUsername = "";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);
        installCompanionUi();
    }

    public void setCompanionState(
            final String state,
            final String message,
            final boolean isError,
            final String serverUrl,
            final String username) {
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
        page.setPadding(dp(24), dp(32), dp(24), dp(40));

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
                "在手机上完成地址与帐号输入，连接成功后媒体库会出现在眼镜中。",
                15,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        subtitle.setLineSpacing(0f, 1.15f);
        page.addView(subtitle, matchWrap(dp(24)));

        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(20), dp(22), dp(20), dp(22));
        card.setBackground(rounded(COLOR_SURFACE, 22));
        page.addView(card, matchWrap(dp(18)));

        card.addView(createLabel("Jellyfin 服务器地址"), matchWrap(dp(7)));
        serverInput = createInput("例如：http://192.168.1.20:8096");
        serverInput.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
        card.addView(serverInput, matchHeight(52, 18));

        card.addView(createLabel("用户名"), matchWrap(dp(7)));
        usernameInput = createInput("Jellyfin 用户名");
        usernameInput.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_NORMAL);
        card.addView(usernameInput, matchHeight(52, 18));

        card.addView(createLabel("密码"), matchWrap(dp(7)));
        passwordInput = createInput("密码仅用于本次登录");
        passwordInput.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_PASSWORD);
        passwordInput.setSaveEnabled(false);
        passwordInput.setImportantForAutofill(View.IMPORTANT_FOR_AUTOFILL_NO_EXCLUDE_DESCENDANTS);
        card.addView(passwordInput, matchHeight(52, 22));

        connectButton = new Button(this);
        connectButton.setAllCaps(false);
        connectButton.setText("连接并在眼镜中打开");
        connectButton.setTextColor(Color.WHITE);
        connectButton.setTextSize(16);
        connectButton.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        connectButton.setGravity(Gravity.CENTER);
        connectButton.setBackground(rounded(COLOR_ACCENT, 16));
        connectButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                submitLogin();
            }
        });
        card.addView(connectButton, matchHeight(54, 16));

        statusText = createText(
                latestMessage,
                14,
                COLOR_SECONDARY,
                Typeface.NORMAL,
                Gravity.CENTER);
        statusText.setLineSpacing(0f, 1.12f);
        card.addView(statusText, matchWrap(0));

        TextView privacy = createText(
                "密码不会写入本地配置；登录后仅保存 Jellyfin 会话令牌。",
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

    private void submitLogin() {
        String serverUrl = serverInput.getText().toString().trim();
        String username = usernameInput.getText().toString().trim();
        String password = passwordInput.getText().toString();

        if (TextUtils.isEmpty(serverUrl)) {
            showLocalError("请输入 Jellyfin 服务器地址。");
            serverInput.requestFocus();
            return;
        }
        if (!serverUrl.startsWith("http://") && !serverUrl.startsWith("https://")) {
            showLocalError("服务器地址需要以 http:// 或 https:// 开头。");
            serverInput.requestFocus();
            return;
        }
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
            applyCompanionState();
        } catch (Exception ignored) {
            showLocalError("登录信息发送失败，请确认 Unity 与眼镜连接后重试。");
        } finally {
            passwordInput.getText().clear();
        }
    }

    private void applyCompanionState() {
        if (companionOverlay == null) {
            return;
        }

        boolean ready = "ready".equals(latestState);
        companionOverlay.setVisibility(ready ? View.GONE : View.VISIBLE);
        if (ready) {
            passwordInput.getText().clear();
            hideKeyboard();
            return;
        }

        boolean canEdit = "login_required".equals(latestState);
        setControlsEnabled(canEdit);

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
        } else if ("initializing".equals(latestState)) {
            connectButton.setText("正在启动…");
        } else if ("offline".equals(latestState)) {
            connectButton.setText("Unity 尚未运行");
        } else {
            connectButton.setText("连接并在眼镜中打开");
        }

        statusText.setText(TextUtils.isEmpty(latestMessage)
                ? defaultMessageForState(latestState)
                : latestMessage);
        statusText.setTextColor(latestIsError ? COLOR_ERROR : COLOR_SECONDARY);
    }

    private void setControlsEnabled(boolean enabled) {
        serverInput.setEnabled(enabled);
        usernameInput.setEnabled(enabled);
        passwordInput.setEnabled(enabled);
        connectButton.setEnabled(enabled);
        float alpha = enabled ? 1f : 0.62f;
        serverInput.setAlpha(alpha);
        usernameInput.setAlpha(alpha);
        passwordInput.setAlpha(alpha);
        connectButton.setAlpha(alpha);
    }

    private void showLocalError(String message) {
        latestIsError = true;
        latestMessage = message;
        statusText.setText(message);
        statusText.setTextColor(COLOR_ERROR);
    }

    private String defaultMessageForState(String state) {
        if ("connecting".equals(state)) {
            return "正在连接 Jellyfin…";
        }
        if ("initializing".equals(state)) {
            return "正在启动 Jellyfin 客户端…";
        }
        if ("offline".equals(state)) {
            return "Unity 尚未运行，请重新启动应用。";
        }
        return "请输入服务器地址与 Jellyfin 帐号。";
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
}
