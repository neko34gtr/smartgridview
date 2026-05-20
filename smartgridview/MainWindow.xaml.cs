using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace smartgridview
{
    public partial class MainWindow : Window
    {
        private const string ConfigFile = "smartgridview.json";

        // 設定を保持するクラス
        private class AppConfig
        {
            public double Top { get; set; } = 100;
            public double Left { get; set; } = 100;
            public double Width { get; set; } = 800;
            public double Height { get; set; } = 500;
            public bool IsExtractionMode { get; set; } = false;
            // デフォルトキーワードをここにリストで初期化
            public List<string> ExtractionKeywords { get; set; } = new List<string>
            {
                "郵便番号", "住所", "電話番号", "性", "名", "名前", "姓名", "カナ"
            };
        }

        private AppConfig _config = new AppConfig();

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ConfigFile))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFile, new UTF8Encoding(false));
                    _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                    this.Top = _config.Top;
                    this.Left = _config.Left;
                    this.Width = _config.Width;
                    this.Height = _config.Height;
                    this.chkExtractionMode.IsChecked = _config.IsExtractionMode;
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                }
                catch { }
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _config.Top = this.Top;
            _config.Left = this.Left;
            _config.Width = this.Width;
            _config.Height = this.Height;
            _config.IsExtractionMode = chkExtractionMode.IsChecked ?? false;

            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json, new UTF8Encoding(false));
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_config.ExtractionKeywords) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _config.ExtractionKeywords = win.Keywords;
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow { Owner = this };
            win.ShowDialog();
        }

        /// <summary>
        /// 画面全体でのキー入力を監視し、Ctrl + V を捕捉する
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
            {
                if (Clipboard.ContainsText())
                {
                    string clipboardText = Clipboard.GetText();
                    ParseAndDisplayRawData(clipboardText);
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// ファイルが画面にドラッグ＆ドロップされたときの処理
        /// </summary>
        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];

                    try
                    {
                        string ext = Path.GetExtension(filePath).ToLower();
                        if (ext == ".csv" || ext == ".tsv" || ext == ".txt")
                        {
                            string fileText = File.ReadAllText(filePath, Encoding.UTF8);
                            ParseAndDisplayRawData(fileText);
                        }
                        else
                        {
                            MessageBox.Show("対応していないファイル形式です。CSV、TSV、TXTファイルをドロップしてください。", "通知", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"ファイルの読み込み中にエラーが発生しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        /// <summary>
        /// データグリッドのセルがダブルクリックされたときの処理（確実なデータソース直結方式）
        /// </summary>
        private void dataGrid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridCell)) dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridCell cell && cell.Column is DataGridColumn column && column.Header != null)
            {
                string columnName = column.Header.ToString() ?? string.Empty;
                if (cell.DataContext is DataRowView rowView)
                {
                    if (rowView.Row.Table.Columns.Contains(columnName))
                    {
                        object cellValue = rowView[columnName];
                        string targetText = cellValue?.ToString() ?? string.Empty;

                        if (!string.IsNullOrEmpty(targetText))
                        {
                            try { Clipboard.SetText(targetText); }
                            catch (Exception ex) { MessageBox.Show($"失敗:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error); }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// テキストデータを解析してDataGridにバインドする（貼り付け・D&D共通）
        /// </summary>
        private void ParseAndDisplayRawData(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0) return;

            char delimiter = lines[0].Contains('\t') ? '\t' : ',';

            DataTable dt = new DataTable();
            string[] firstLineCells = lines[0].Split(delimiter);
            List<string> columnNames = new List<string>();
            List<int> validColumnIndices = new List<int>();

            // 抽出キーワード設定の取得
            bool isExtractionMode = chkExtractionMode.IsChecked ?? false;
            List<string> keywords = _config.ExtractionKeywords;

            for (int i = 0; i < firstLineCells.Length; i++)
            {
                string colName = firstLineCells[i].Trim(' ', '"');

                // 抽出モード時のフィルタリング
                if (isExtractionMode && keywords.Count > 0)
                {
                    if (!keywords.Exists(k => colName.Contains(k))) continue;
                }

                if (string.IsNullOrEmpty(colName)) colName = $"列 {i + 1}";

                string uniqueColName = colName;
                int counter = 1;
                while (dt.Columns.Contains(uniqueColName))
                {
                    uniqueColName = $"{colName}_{counter++}";
                }

                dt.Columns.Add(uniqueColName, typeof(string));
                columnNames.Add(uniqueColName);
                validColumnIndices.Add(i);
            }

            dt.BeginLoadData();
            for (int r = 1; r < lines.Length; r++)
            {
                string[] cells = lines[r].Split(delimiter);
                DataRow dr = dt.NewRow();

                for (int i = 0; i < validColumnIndices.Count; i++)
                {
                    int originalIndex = validColumnIndices[i];
                    if (originalIndex < cells.Length)
                    {
                        dr[columnNames[i]] = cells[originalIndex].Trim(' ', '"');
                    }
                }
                dt.Rows.Add(dr);
            }
            dt.EndLoadData();

            dataGrid1.Columns.Clear();
            dataGrid1.ItemsSource = dt.DefaultView;
        }
    }
}