using System.Collections.Generic;
using System.Windows;

namespace smartgridview
{
    public partial class SettingsWindow : Window
    {
        public List<string> Keywords { get; private set; } = new List<string>();

        public SettingsWindow(List<string> currentKeywords)
        {
            InitializeComponent();
            keywordsTextBox.Text = string.Join("\r\n", currentKeywords);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            Keywords.Clear();
            string[] lines = keywordsTextBox.Text.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line)) Keywords.Add(line.Trim());
            }
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}