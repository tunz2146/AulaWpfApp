using System.Windows;
using System.Windows.Media;

namespace AulaWpfApp
{
    public partial class MainWindow : Window
    {
        private readonly AulaHidService _hidService = new AulaHidService();

        private readonly AudioReactiveService _audioService = new AudioReactiveService();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ScanKeyboard_Click(object sender, RoutedEventArgs e)
        {
            bool connected = _hidService.Connect();

            if (connected)
            {
                ConnectionStatusText.Text = "CONNECTED";
                ConnectionStatusText.Foreground = Brushes.LimeGreen;
                DevicePathText.Text = $"DevicePath: {_hidService.ConnectedDevicePath}";
            }
            else
            {
                ConnectionStatusText.Text = "DISCONNECTED";
                ConnectionStatusText.Foreground = Brushes.OrangeRed;
                DevicePathText.Text = "Không tìm thấy bàn phím AULA 60 HE. Kiểm tra lại kết nối USB.";
            }
        }

        private void TestRgb_Click(object sender, RoutedEventArgs e)
        {
            if (!_hidService.IsConnected)
            {
                MessageBox.Show("Chưa kết nối. Nhấn SCAN KEYBOARD trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            byte[] report = AulaHidService.BuildColorReport(0x01);
            bool success = _hidService.SendRgbReport(report);

            if (success)
            {
                MessageBox.Show("Đã gửi lệnh RGB thành công!", "Test RGB", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Gửi lệnh thất bại.", "Test RGB", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TestCustomColor_Click(object sender, RoutedEventArgs e)
        {
            if (!_hidService.IsConnected)
            {
                MessageBox.Show("Chưa kết nối. Nhấn SCAN KEYBOARD trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _hidService.SendRgbReport(AulaHidService.BuildEnableCustomColorReport());

            byte[] purpleReport = AulaHidService.BuildCustomColorReport(128, 0, 255);
            bool success = _hidService.SendRgbReport(purpleReport);

            if (success)
            {
                MessageBox.Show("Đã gửi màu tím tùy chỉnh (R=128,G=0,B=255)! Kiểm tra đèn bàn phím.",
                    "Test Custom Color", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Gửi lệnh thất bại.", "Test Custom Color", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private DateTime _lastRgbSendTime = DateTime.MinValue;
private static readonly TimeSpan RgbSendInterval = TimeSpan.FromMilliseconds(33); // ~30 lần/giây

private void TestAudio_Click(object sender, RoutedEventArgs e)
{
    if (_audioService.IsRunning)
    {
        _audioService.Stop();
        _audioService.LevelChanged -= OnAudioLevelChanged;
        AudioLevelText.Text = "Audio Level: (stopped)";
        return;
    }

    if (!_hidService.IsConnected)
    {
        MessageBox.Show("Chưa kết nối bàn phím. Nhấn SCAN KEYBOARD trước.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
    }

    // Bật chế độ custom color 1 lần trước khi bắt đầu stream
    _hidService.SendRgbReport(AulaHidService.BuildEnableCustomColorReport());

    _audioService.LevelChanged += OnAudioLevelChanged;
    _audioService.Start();
}

private void OnAudioLevelChanged(float level)
{
    Dispatcher.Invoke(() =>
    {
        AudioLevelText.Text = $"Audio Level: {level:F2}";
        AudioLevelBar.Value = level;
    });

    // Giới hạn tốc độ gửi HID để không làm nghẽn bàn phím
    var now = DateTime.Now;
    if (now - _lastRgbSendTime < RgbSendInterval)
    {
        return;
    }
    _lastRgbSendTime = now;

    // Màu xanh dương, độ sáng theo level (0-255)
    byte brightness = (byte)(level * 255);
    byte[] report = AulaHidService.BuildCustomColorReport(0, 0, brightness);
    _hidService.SendRgbReport(report);
}

    }
}