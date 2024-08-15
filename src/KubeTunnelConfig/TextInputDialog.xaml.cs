using System.Windows;
using System.Windows.Input;

namespace KubeTunnelConfig
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class TextInputDialog : Window
    {
        public TextInputDialog(string title, string defaultText = "")
        {
            InitializeComponent();
            Title = title;
            InputTextBox.Text = defaultText;
            InputTextBox.Focus();
        }

        public string InputText
        {
            get => InputTextBox.Text;
            set => InputTextBox.Text = value;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void InputTextBox_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || string.IsNullOrWhiteSpace(InputText)) 
                return;

            DialogResult = true;
            Close();
        }

        private void TextInputDialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }
    }
}
