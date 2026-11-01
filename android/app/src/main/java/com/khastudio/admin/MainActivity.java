package com.khastudio.admin;

import android.annotation.SuppressLint;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Environment;
import android.provider.MediaStore;
import android.util.Base64;
import android.view.View;
import android.webkit.GeolocationPermissions;
import android.webkit.PermissionRequest;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.ProgressBar;
import android.widget.Toast;

import androidx.activity.OnBackPressedCallback;
import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.content.FileProvider;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.text.SimpleDateFormat;
import java.util.Date;
import java.util.Locale;

public class MainActivity extends AppCompatActivity {

    private WebView webView;
    private SwipeRefreshLayout swipeRefresh;
    private ProgressBar progressBar;
    private UpdateManager updateManager;
    private ValueCallback<Uri[]> uploadMessage;
    private Uri cameraPhotoUri;

    public final static int FILECHOOSER_RESULTCODE = 1002;
    public final static int CAMERA_DIRECT_REQUEST_CODE = 1004;
    public final static int PERMISSION_CAMERA_REQUEST_CODE = 1005;

    @SuppressLint("SetJavaScriptEnabled")
    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        webView = findViewById(R.id.webView);
        swipeRefresh = findViewById(R.id.swipeRefresh);
        progressBar = findViewById(R.id.progressBar);

        updateManager = new UpdateManager(this);

        // Khởi tạo Kênh thông báo chuông & rung
        NotificationHelper.createNotificationChannel(this);

        // Yêu cầu quyền gửi thông báo trên Android 13+ (API 33+)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (checkSelfPermission(android.Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                requestPermissions(new String[]{android.Manifest.permission.POST_NOTIFICATIONS}, 1001);
            }
        }

        // Yêu cầu quyền Camera & Location cho chức năng chụp ảnh có Timemark
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            String[] permissions = new String[]{
                    android.Manifest.permission.CAMERA,
                    android.Manifest.permission.ACCESS_FINE_LOCATION,
                    android.Manifest.permission.ACCESS_COARSE_LOCATION
            };
            boolean needPerm = false;
            for (String p : permissions) {
                if (checkSelfPermission(p) != PackageManager.PERMISSION_GRANTED) {
                    needPerm = true;
                    break;
                }
            }
            if (needPerm) {
                requestPermissions(permissions, 1003);
            }
        }

        // Khởi động dịch vụ kiểm tra nền nhận thông báo tức thời
        try {
            Intent serviceIntent = new Intent(this, BackgroundCheckService.class);
            startService(serviceIntent);
        } catch (Exception ignored) {}

        // Cấu hình WebView
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(true);
        settings.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
        settings.setUseWideViewPort(true);
        settings.setLoadWithOverviewMode(true);
        settings.setSupportZoom(false);
        settings.setDisplayZoomControls(false);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setGeolocationEnabled(true);

        // Bridge giao tiếp Android <-> JavaScript
        webView.addJavascriptInterface(new WebAppInterface(this, updateManager), "Android");

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onProgressChanged(WebView view, int newProgress) {
                if (newProgress < 100) {
                    progressBar.setVisibility(View.VISIBLE);
                    progressBar.setProgress(newProgress);
                } else {
                    progressBar.setVisibility(View.GONE);
                }
            }

            // Cấp quyền WebRTC Camera & Audio nếu gọi getUserMedia
            @Override
            public void onPermissionRequest(final PermissionRequest request) {
                runOnUiThread(() -> {
                    try {
                        request.grant(request.getResources());
                    } catch (Exception ignored) {}
                });
            }

            // Cấp quyền định vị Geolocation
            @Override
            public void onGeolocationPermissionsShowPrompt(String origin, GeolocationPermissions.Callback callback) {
                callback.invoke(origin, true, false);
            }

            // Mở hộp thoại chọn file hoặc chụp ảnh Camera trên điện thoại
            @Override
            public boolean onShowFileChooser(WebView webView, ValueCallback<Uri[]> filePathCallback, FileChooserParams fileChooserParams) {
                if (uploadMessage != null) {
                    uploadMessage.onReceiveValue(null);
                    uploadMessage = null;
                }
                uploadMessage = filePathCallback;

                boolean isCapture = fileChooserParams.isCaptureEnabled();
                Intent takePictureIntent = null;
                try {
                    takePictureIntent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
                    File photoFile = createImageFile();
                    cameraPhotoUri = FileProvider.getUriForFile(
                            MainActivity.this,
                            getPackageName() + ".provider",
                            photoFile
                    );
                    takePictureIntent.putExtra(MediaStore.EXTRA_OUTPUT, cameraPhotoUri);
                    takePictureIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                } catch (Exception e) {
                    takePictureIntent = null;
                }

                // Nếu người dùng chọn chụp camera trực tiếp từ thẻ input capture
                if (isCapture && takePictureIntent != null) {
                    try {
                        startActivityForResult(takePictureIntent, FILECHOOSER_RESULTCODE);
                        return true;
                    } catch (Exception ignored) {}
                }

                // Hộp thoại tổng hợp: Cho phép chụp camera hoặc chọn từ Thư viện ảnh
                Intent contentSelectionIntent = new Intent(Intent.ACTION_GET_CONTENT);
                contentSelectionIntent.addCategory(Intent.CATEGORY_OPENABLE);
                contentSelectionIntent.setType("image/*");

                Intent[] intentArray;
                if (takePictureIntent != null) {
                    intentArray = new Intent[]{takePictureIntent};
                } else {
                    intentArray = new Intent[0];
                }

                Intent chooserIntent = new Intent(Intent.ACTION_CHOOSER);
                chooserIntent.putExtra(Intent.EXTRA_INTENT, contentSelectionIntent);
                chooserIntent.putExtra(Intent.EXTRA_TITLE, "Chụp ảnh camera hoặc Chọn ảnh");
                chooserIntent.putExtra(Intent.EXTRA_INITIAL_INTENTS, intentArray);

                try {
                    startActivityForResult(chooserIntent, FILECHOOSER_RESULTCODE);
                } catch (Exception e) {
                    uploadMessage = null;
                    return false;
                }
                return true;
            }
        });

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageStarted(WebView view, String url, Bitmap favicon) {
                super.onPageStarted(view, url, favicon);
            }

            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                swipeRefresh.setRefreshing(false);
            }

            @Override
            public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
                super.onReceivedError(view, request, error);
                swipeRefresh.setRefreshing(false);
            }
        });

        // Kéo xuống để tải lại
        swipeRefresh.setColorSchemeResources(R.color.accent, R.color.primary);
        swipeRefresh.setOnRefreshListener(() -> {
            webView.reload();
        });

        // Xử lý nút Back của Android
        getOnBackPressedDispatcher().addCallback(this, new OnBackPressedCallback(true) {
            @Override
            public void handleOnBackPressed() {
                if (webView.canGoBack()) {
                    webView.goBack();
                } else {
                    finish();
                }
            }
        });

        // Mở giao diện Admin Mobile từ assets
        webView.loadUrl("file:///android_asset/admin-mobile.html");

        // Tự động kiểm tra bản cập nhật mới từ xa (GitHub Releases OTA)
        updateManager.checkForUpdates(false);
    }

    private File createImageFile() {
        String timeStamp = new SimpleDateFormat("yyyyMMdd_HHmmss", Locale.getDefault()).format(new Date());
        String imageFileName = "IMG_" + timeStamp + "_";
        File storageDir = getExternalFilesDir(Environment.DIRECTORY_PICTURES);
        if (storageDir == null) {
            storageDir = getCacheDir();
        }
        try {
            return File.createTempFile(imageFileName, ".jpg", storageDir);
        } catch (IOException e) {
            return new File(storageDir, imageFileName + ".jpg");
        }
    }

    public void openNativeCameraDirect() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            if (checkSelfPermission(android.Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
                requestPermissions(new String[]{android.Manifest.permission.CAMERA}, PERMISSION_CAMERA_REQUEST_CODE);
                return;
            }
        }
        try {
            Intent takePictureIntent = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
            File photoFile = createImageFile();
            cameraPhotoUri = FileProvider.getUriForFile(
                    this,
                    getPackageName() + ".provider",
                    photoFile
            );
            takePictureIntent.putExtra(MediaStore.EXTRA_OUTPUT, cameraPhotoUri);
            takePictureIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
            startActivityForResult(takePictureIntent, CAMERA_DIRECT_REQUEST_CODE);
        } catch (Exception e) {
            Toast.makeText(this, "Không thể mở máy ảnh: " + e.getMessage(), Toast.LENGTH_SHORT).show();
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == FILECHOOSER_RESULTCODE) {
            if (uploadMessage == null) return;
            Uri[] results = null;
            if (resultCode == RESULT_OK) {
                if (data == null || data.getData() == null) {
                    if (cameraPhotoUri != null) {
                        results = new Uri[]{cameraPhotoUri};
                    }
                } else {
                    String dataString = data.getDataString();
                    if (dataString != null) {
                        results = new Uri[]{Uri.parse(dataString)};
                    } else if (data.getClipData() != null) {
                        int count = data.getClipData().getItemCount();
                        results = new Uri[count];
                        for (int i = 0; i < count; i++) {
                            results[i] = data.getClipData().getItemAt(i).getUri();
                        }
                    }
                }
            }
            uploadMessage.onReceiveValue(results);
            uploadMessage = null;
        } else if (requestCode == CAMERA_DIRECT_REQUEST_CODE) {
            if (resultCode == RESULT_OK && cameraPhotoUri != null) {
                try {
                    InputStream inputStream = getContentResolver().openInputStream(cameraPhotoUri);
                    Bitmap bitmap = BitmapFactory.decodeStream(inputStream);
                    if (inputStream != null) inputStream.close();
                    if (bitmap != null) {
                        int maxDim = 1600;
                        int w = bitmap.getWidth();
                        int h = bitmap.getHeight();
                        if (w > maxDim || h > maxDim) {
                            float ratio = Math.min((float) maxDim / w, (float) maxDim / h);
                            bitmap = Bitmap.createScaledBitmap(bitmap, Math.round(w * ratio), Math.round(h * ratio), true);
                        }
                        ByteArrayOutputStream baos = new ByteArrayOutputStream();
                        bitmap.compress(Bitmap.CompressFormat.JPEG, 85, baos);
                        byte[] imageBytes = baos.toByteArray();
                        String base64Image = "data:image/jpeg;base64," + Base64.encodeToString(imageBytes, Base64.NO_WRAP);

                        webView.post(() -> {
                            webView.evaluateJavascript("window.handleNativePhotoCaptured && window.handleNativePhotoCaptured('" + base64Image + "');", null);
                        });
                    }
                } catch (Exception e) {
                    Toast.makeText(this, "Lỗi đọc ảnh camera: " + e.getMessage(), Toast.LENGTH_SHORT).show();
                }
            }
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == 1001) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                Toast.makeText(this, "✓ Đã bật thông báo duyệt tài khoản tức thì", Toast.LENGTH_SHORT).show();
            }
        } else if (requestCode == PERMISSION_CAMERA_REQUEST_CODE) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                openNativeCameraDirect();
            } else {
                Toast.makeText(this, "Ứng dụng cần quyền Máy ảnh để chụp ảnh hiện trường", Toast.LENGTH_SHORT).show();
            }
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (webView != null) webView.onResume();
    }

    @Override
    protected void onPause() {
        super.onPause();
        if (webView != null) webView.onPause();
    }
}
