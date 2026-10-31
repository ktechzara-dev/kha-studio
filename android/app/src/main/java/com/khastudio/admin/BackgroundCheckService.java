package com.khastudio.admin;

import android.app.Service;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;

import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;
import java.util.HashSet;
import java.util.Iterator;
import java.util.Set;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public class BackgroundCheckService extends Service {

    public static final String DEFAULT_FIREBASE_URL = "https://k-core-studio-default-rtdb.asia-southeast1.firebasedatabase.app";
    public static final String PREF_NAME = "ks_admin_prefs";
    public static final String KEY_FIREBASE_URL = "firebase_url";
    public static final String KEY_KNOWN_PENDING_IDS = "known_pending_ids";

    private ScheduledExecutorService scheduler;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    @Override
    public void onCreate() {
        super.onCreate();
        NotificationHelper.createNotificationChannel(this);
        startPolling();
    }

    @Override
    public int onStartCommand(Intent intent, int flags, int startId) {
        if (scheduler == null || scheduler.isShutdown()) {
            startPolling();
        }
        return START_STICKY;
    }

    private void startPolling() {
        if (scheduler != null && !scheduler.isShutdown()) return;
        scheduler = Executors.newSingleThreadScheduledExecutor();
        // Quét chu kỳ mỗi 20 giây một lần
        scheduler.scheduleWithFixedDelay(this::checkPendingRegistrations, 2, 20, TimeUnit.SECONDS);
    }

    private void checkPendingRegistrations() {
        try {
            SharedPreferences prefs = getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
            String firebaseUrl = prefs.getString(KEY_FIREBASE_URL, DEFAULT_FIREBASE_URL);
            if (firebaseUrl == null || firebaseUrl.trim().isEmpty()) {
                firebaseUrl = DEFAULT_FIREBASE_URL;
            }
            firebaseUrl = firebaseUrl.replaceAll("/+$", "");

            URL url = new URL(firebaseUrl + "/registrations.json");
            HttpURLConnection conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("GET");
            conn.setConnectTimeout(8000);
            conn.setReadTimeout(8000);

            if (conn.getResponseCode() == 200) {
                BufferedReader reader = new BufferedReader(new InputStreamReader(conn.getInputStream()));
                StringBuilder sb = new StringBuilder();
                String line;
                while ((line = reader.readLine()) != null) sb.append(line);
                reader.close();

                String jsonStr = sb.toString().trim();
                if (jsonStr.startsWith("{")) {
                    JSONObject root = new JSONObject(jsonStr);
                    Set<String> knownIds = new HashSet<>(prefs.getStringSet(KEY_KNOWN_PENDING_IDS, new HashSet<>()));
                    Set<String> currentPendingIds = new HashSet<>();

                    int pendingCount = 0;
                    String latestPendingName = null;
                    String latestPendingUsername = null;

                    Iterator<String> keys = root.keys();
                    while (keys.hasNext()) {
                        String key = keys.next();
                        JSONObject item = root.optJSONObject(key);
                        if (item != null && "pending".equalsIgnoreCase(item.optString("status"))) {
                            pendingCount++;
                            currentPendingIds.add(key);
                            if (latestPendingName == null) {
                                latestPendingName = item.optString("name", item.optString("username", "Kỹ sư"));
                                latestPendingUsername = item.optString("username", "");
                            }
                        }
                    }

                    // Tìm các ID mới xuất hiện mà chưa thông báo
                    Set<String> newlyArrived = new HashSet<>(currentPendingIds);
                    newlyArrived.removeAll(knownIds);

                    if (!newlyArrived.isEmpty()) {
                        // Có yêu cầu mới! Bắn thông báo ngay lên điện thoại sếp!
                        String notifTitle = "Kha Studio — Yêu cầu duyệt mới! (" + pendingCount + ")";
                        String displayName = (latestPendingName != null && !latestPendingName.isEmpty())
                                ? latestPendingName : "@" + latestPendingUsername;
                        String notifBody = "Kỹ sư " + displayName + " vừa gửi yêu cầu đăng ký tài khoản cần Sếp phê duyệt.";
                        final int finalCount = pendingCount;

                        mainHandler.post(() -> {
                            NotificationHelper.showNotification(getApplicationContext(), notifTitle, notifBody, finalCount);
                        });

                        // Cập nhật lại danh sách đã biết
                        prefs.edit().putStringSet(KEY_KNOWN_PENDING_IDS, currentPendingIds).apply();
                    } else if (currentPendingIds.size() != knownIds.size()) {
                        // Sếp đã duyệt bớt hoặc từ chối -> cập nhật lại danh sách ID
                        prefs.edit().putStringSet(KEY_KNOWN_PENDING_IDS, currentPendingIds).apply();
                    }
                }
            }
        } catch (Exception ignored) {
        }
    }

    @Override
    public void onDestroy() {
        if (scheduler != null) {
            scheduler.shutdownNow();
        }
        super.onDestroy();
    }

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }
}
