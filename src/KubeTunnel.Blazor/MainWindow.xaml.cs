using System.Windows;
using MahApps.Metro.Controls;

namespace KubeTunnel
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public MainWindow()
        {
            Resources.Add("services", Startup.Services);
            InitializeComponent();
        }
    }
}
