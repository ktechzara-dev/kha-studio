package com.khastudio.admin;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.net.Uri;
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
        NotificationHelper.triggerVibration(activity, milliseconds);
    }

    @JavascriptInterface
    public void showNotification(String title, String message, int count) {
        activity.runOnUiThread(() -> NotificationHelper.showNotification(activity, title, message, count));
    }

    @JavascriptInterface
    public void playNotificationSound() {
        activity.runOnUiThread(() -> NotificationHelper.playSystemSound(activity));
    }

    @JavascriptInterface
    public void saveCloudConfig(String firebaseUrl) {
        try {
            SharedPreferences prefs = activity.getSharedPreferences(BackgroundCheckService.PREF_NAME, Context.MODE_PRIVATE);
            prefs.edit().putString(BackgroundCheckService.KEY_FIREBASE_URL, firebaseUrl).apply();
        } catch (Exception ignored) {}
    }

    @JavascriptInterface
    public String getStoredFirebaseUrl() {
        try {
            SharedPreferences prefs = activity.getSharedPreferences(BackgroundCheckService.PREF_NAME, Context.MODE_PRIVATE);
            return prefs.getString(BackgroundCheckService.KEY_FIREBASE_URL, BackgroundCheckService.DEFAULT_FIREBASE_URL);
        } catch (Exception ignored) {
            return BackgroundCheckService.DEFAULT_FIREBASE_URL;
        }
    }

    @JavascriptInterface
    public void openExternal(String url) {
        try {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
            activity.startActivity(intent);
        } catch (Exception ignored) {}
    }
}
