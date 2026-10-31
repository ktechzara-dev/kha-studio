package com.khastudio.admin;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.webkit.JavascriptInterface;
import android.widget.Toast;

public class WebAppInterface {

    private final Activity activity;
    private final UpdateManager updateManager;

    public WebAppInterface(Activity activity, UpdateManager updateManager) {
        this.activity = activity;
        this.updateManager = updateManager;
    }

    @JavascriptInterface
    public void checkForUpdate() {
        if (updateManager != null) {
            updateManager.checkForUpdates(true);
        }
    }

    @JavascriptInterface
    public String getAppVersion() {
        return BuildConfig.VERSION_NAME;
    }

    @JavascriptInterface
    public void showToast(String message) {
        activity.runOnUiThread(() -> Toast.makeText(activity, message, Toast.LENGTH_SHORT).show());
    }

    @JavascriptInterface
    public void vibrate(long milliseconds) {
        try {
            Vibrator v = (Vibrator) activity.getSystemService(Context.VIBRATOR_SERVICE);
            if (v != null && v.hasVibrator()) {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    v.vibrate(VibrationEffect.createOneShot(milliseconds, VibrationEffect.DEFAULT_AMPLITUDE));
                } else {
                    v.vibrate(milliseconds);
                }
            }
        } catch (Exception ignored) {}
    }

    @JavascriptInterface
    public void openExternal(String url) {
        try {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
            activity.startActivity(intent);
        } catch (Exception ignored) {}
    }
}
