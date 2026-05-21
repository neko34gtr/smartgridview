using System.Data;
using System.Windows;
using System.Windows.Controls;

namespace smartgridview
{
    public partial class VerticalViewWindow : Window
    {
        public VerticalViewWindow(DataRow row)
        {
            InitializeComponent();

            foreach (DataColumn col in row.Table.Columns)
            {
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                grid.ColumnDefinitions.Add(new ColumnDefinition());

                var lbl = new TextBlock { Text = col.ColumnName, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center };
                var val = new TextBox { Text = row[col].ToString(), IsReadOnly = true, Margin = new Thickness(10, 0, 0, 0) };

                Grid.SetColumn(lbl, 0);
                Grid.SetColumn(val, 1);
                grid.Children.Add(lbl);
                grid.Children.Add(val);

                ContainerStackPanel.Children.Add(grid);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}