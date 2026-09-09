using System.Windows;

namespace AulaWpfApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ScanKeyboard_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Đang quét bàn phím AULA 60 HE...",
                "AULA Control",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }
    }
}