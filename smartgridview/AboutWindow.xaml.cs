using System.Windows;

namespace smartgridview
{
    public partial class AboutWindow : Window
    {
        public AboutWindow() => InitializeComponent();
        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}
