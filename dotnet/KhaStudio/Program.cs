using Velopack;

namespace KhaStudio;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Phải chạy đầu tiên: xử lý các hook cài đặt/gỡ/cập nhật của Velopack
        VelopackApp.Build().Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
