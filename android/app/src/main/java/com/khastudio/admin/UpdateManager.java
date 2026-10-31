package com.khastudio.admin;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.DownloadManager;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.database.Cursor;
import android.net.Uri;
import android.os.Build;
import android.os.Environment;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.widget.Toast;

import androidx.core.content.FileProvider;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.File;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.URL;

public class UpdateManager {

    private static final String GITHUB_REPO = "ktechzara-dev/kha-studio";
    private static final String API_URL = "https://api.github.com/repos/" + GITHUB_REPO + "/releases";

    private final Activity activity;
    private final Handler mainHandler;
    private long downloadId = -1;

    public UpdateManager(Activity activity) {
        this.activity = activity;
        this.mainHandler = new Handler(Looper.getMainLooper());
    }

    public void checkForUpdates(boolean notifyIfLatest) {
        new Thread(() -> {
            try {
                URL url = new URL(API_URL);
                HttpURLConnection conn = (HttpURLConnection) url.openConnection();
                conn.setRequestMethod("GET");
                conn.setRequestProperty("Accept", "application/vnd.github.v3+json");
                conn.setConnectTimeout(8000);
                conn.setReadTimeout(8000);

                if (conn.getResponseCode() == 200) {
                    BufferedReader reader = new BufferedReader(new InputStreamReader(conn.getInputStream()));
                    StringBuilder sb = new StringBuilder();
                    String line;
                    while ((line = reader.readLine()) != null) {
                        sb.append(line);
                    }
                    reader.close();

                    JSONArray releases = new JSONArray(sb.toString());
                    if (releases.length() > 0) {
                        for (int i = 0; i < releases.length(); i++) {
                            JSONObject rel = releases.getJSONObject(i);
                            String tag = rel.optString("tag_name", "").replace("v", "").trim();
                            JSONArray assets = rel.optJSONArray("assets");

                            String apkUrl = null;
                            String apkName = "KhaStudio-Admin.apk";

                            if (assets != null) {
                                for (int j = 0; j < assets.length(); j++) {
                                    JSONObject asset = assets.getJSONObject(j);
                                    String name = asset.optString("name", "");
                                    if (name.toLowerCase().endsWith(".apk")) {
                                        apkUrl = asset.optString("browser_download_url");
                                        apkName = name;
                                        break;
                                    }
                                }
                            }

                            if (apkUrl != null && isNewerVersion(tag, BuildConfig.VERSION_NAME)) {
                                final String finalApkUrl = apkUrl;
                                final String finalApkName = apkName;
                                final String finalTag = tag;
                                mainHandler.post(() -> showUpdateDialog(finalTag, finalApkUrl, finalApkName));
                                return;
                            }
                        }
                    }

                    if (notifyIfLatest) {
                        mainHandler.post(() -> Toast.makeText(activity, "Bạn đang sử dụng phiên bản mới nhất (" + BuildConfig.VERSION_NAME + ")", Toast.LENGTH_SHORT).show());
                    }
                }
            } catch (Exception e) {
                if (notifyIfLatest) {
                    mainHandler.post(() -> Toast.makeText(activity, "Không thể kiểm tra cập nhật: " + e.getMessage(), Toast.LENGTH_SHORT).show());
                }
            }
        }).start();
    }

    private boolean isNewerVersion(String remote, String current) {
        try {
            String[] rParts = remote.split("\\.");
            String[] cParts = current.split("\\.");
            int len = Math.max(rParts.length, cParts.length);
            for (int i = 0; i < len; i++) {
                int r = i < rParts.length ? Integer.parseInt(rParts[i].replaceAll("\\D+", "")) : 0;
                int c = i < cParts.length ? Integer.parseInt(cParts[i].replaceAll("\\D+", "")) : 0;
                if (r > c) return true;
                if (r < c) return false;
            }
        } catch (Exception ignored) {}
        return false;
    }

    private void showUpdateDialog(String newVersion, String downloadUrl, String fileName) {
        new AlertDialog.Builder(activity)
                .setTitle("Cập nhật Kha Studio Admin")
                .setMessage("Đã có bản cập nhật mới v" + newVersion + "! Bấm \"Cập nhật\" để tự động tải về và nâng cấp ứng dụng.")
                .setPositiveButton("Cập nhật ngay", (dialog, which) -> downloadAndInstallApk(downloadUrl, fileName))
                .setNegativeButton("Để sau", null)
                .setCancelable(false)
                .show();
    }

    private void downloadAndInstallApk(String url, String fileName) {
        Toast.makeText(activity, "Đang tải bản cập nhật mới...", Toast.LENGTH_LONG).show();

        try {
            File destination = new File(activity.getExternalFilesDir(Environment.DIRECTORY_DOWNLOADS), fileName);
            if (destination.exists()) {
                destination.delete();
            }

            DownloadManager.Request request = new DownloadManager.Request(Uri.parse(url));
            request.setTitle("Kha Studio Admin Update");
            request.setDescription("Đang tải bản cập nhật mới...");
            request.setNotificationVisibility(DownloadManager.Request.VISIBILITY_VISIBLE_NOTIFY_COMPLETED);
            request.setDestinationUri(Uri.fromFile(destination));

            DownloadManager dm = (DownloadManager) activity.getSystemService(Context.DOWNLOAD_SERVICE);
            if (dm != null) {
                downloadId = dm.enqueue(request);

                BroadcastReceiver receiver = new BroadcastReceiver() {
                    @Override
                    public void onReceive(Context context, Intent intent) {
                        long id = intent.getLongExtra(DownloadManager.EXTRA_DOWNLOAD_ID, -1);
                        if (id == downloadId) {
                            activity.unregisterReceiver(this);
                            promptInstall(destination);
                        }
                    }
                };

                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                    activity.registerReceiver(receiver, new IntentFilter(DownloadManager.ACTION_DOWNLOAD_COMPLETE), Context.RECEIVER_EXPORTED);
                } else {
                    activity.registerReceiver(receiver, new IntentFilter(DownloadManager.ACTION_DOWNLOAD_COMPLETE));
                }
            }
        } catch (Exception e) {
            Toast.makeText(activity, "Lỗi tải bản cập nhật: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    private void promptInstall(File apkFile) {
        if (!apkFile.exists()) return;

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            if (!activity.getPackageManager().canRequestPackageInstalls()) {
                Toast.makeText(activity, "Vui lòng cho phép cài đặt ứng dụng từ nguồn này để tiếp tục", Toast.LENGTH_LONG).show();
                Intent permissionIntent = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES);
                permissionIntent.setData(Uri.parse("package:" + activity.getPackageName()));
                activity.startActivity(permissionIntent);
                return;
            }
        }

        try {
            Uri contentUri = FileProvider.getUriForFile(
                    activity,
                    activity.getPackageName() + ".provider",
                    apkFile
            );

            Intent install = new Intent(Intent.ACTION_VIEW);
            install.setDataAndType(contentUri, "application/vnd.android.package-archive");
            install.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            install.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(install);
        } catch (Exception e) {
            Toast.makeText(activity, "Không thể mở bộ cài: " + e.getMessage(), Toast.LENGTH_LONG).show();
        }
    }
}
