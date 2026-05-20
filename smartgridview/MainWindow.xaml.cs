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

        public MainWindow()
        {
            InitializeComponent();

            // イベント登録
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        // ウィンドウ状態を記録するためのクラス
        private class WindowConfig
        {
            public double Top { get; set; }
            public double Left { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ConfigFile))
            {
                try
                {
                    string json = File.ReadAllText(ConfigFile, new UTF8Encoding(false));
                    var config = JsonSerializer.Deserialize<WindowConfig>(json);
                    if (config != null)
                    {
                        this.Top = config.Top;
                        this.Left = config.Left;
                        this.Width = config.Width;
                        this.Height = config.Height;
                        this.WindowStartupLocation = WindowStartupLocation.Manual;
                    }
                }
                catch { /* 設定読み込み失敗時は規定値で動作 */ }
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            var config = new WindowConfig
            {
                Top = this.Top,
                Left = this.Left,
                Width = this.Width,
                Height = this.Height
            };

            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json, new UTF8Encoding(false));
        }

        /// <summary>
        /// 画面全体でのキー入力を監視し、Ctrl + V を捕捉する
        /// </summary>
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Ctrl + V (貼り付け処理)
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.V)
            {
                if (Clipboard.ContainsText())
                {
                    ParseAndDisplayRawData(Clipboard.GetText());
                    e.Handled = true;
                }
            }
            // Ctrl + C (値のみコピー) または Ctrl + Shift + C (ヘッダー付き)
            else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
            {
                bool withHeader = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                PerformCopy(withHeader);
                e.Handled = true;
            }
        }
        private void PerformCopy(bool withHeader)
        {
            if (dataGrid1.CurrentCell.Column != null && dataGrid1.CurrentCell.Item is DataRowView rowView)
            {
                string colName = dataGrid1.CurrentCell.Column.Header.ToString() ?? "";
                string cellValue = rowView[colName]?.ToString() ?? "";

                string textToCopy = withHeader ? $"{colName}\t{cellValue}" : cellValue;
                Clipboard.SetText(textToCopy);
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
            // クリックされた位置のビジュアル要素を遡って DataGridCell を探す
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridCell))
            {
                dep = VisualTreeHelper.GetParent(dep);
            }

            if (dep is DataGridCell cell)
            {
                // セルが属する「列」のバインド名を取得
                if (cell.Column is DataGridColumn column && column.Header != null)
                {
                    string columnName = column.Header.ToString() ?? string.Empty;

                    // セルが属する「行」のデータソース（DataRowView）を取得
                    if (cell.DataContext is DataRowView rowView)
                    {
                        // 行と列名が一致する位置の生のデータを直接引っ張る
                        if (rowView.Row.Table.Columns.Contains(columnName))
                        {
                            object cellValue = rowView[columnName];
                            string targetText = cellValue?.ToString() ?? string.Empty;

                            if (!string.IsNullOrEmpty(targetText))
                            {
                                try
                                {
                                    // クリップボードに確実に格納
                                    Clipboard.SetText(targetText);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"クリップボードへのコピーに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
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

            char delimiter = '\t';
            if (!lines[0].Contains('\t') && lines[0].Contains(','))
            {
                delimiter = ',';
            }

            DataTable dt = new DataTable();

            string[] firstLineCells = lines[0].Split(delimiter);
            List<string> columnNames = new List<string>();

            for (int i = 0; i < firstLineCells.Length; i++)
            {
                string colName = firstLineCells[i].Trim(' ', '"');

                if (string.IsNullOrEmpty(colName))
                {
                    colName = $"列 {i + 1}";
                }

                string uniqueColName = colName;
                int counter = 1;
                while (dt.Columns.Contains(uniqueColName))
                {
                    uniqueColName = $"{colName}_{counter++}";
                }

                dt.Columns.Add(uniqueColName, typeof(string));
                columnNames.Add(uniqueColName);
            }

            // 大量データ処理の高速化のため、DataTableのインデックス更新を抑制
            dt.BeginLoadData();

            for (int r = 1; r < lines.Length; r++)
            {
                string[] cells = lines[r].Split(delimiter);
                DataRow dr = dt.NewRow();

                int minColumns = Math.Min(columnNames.Count, cells.Length);
                for (int c = 0; c < minColumns; c++)
                {
                    dr[columnNames[c]] = cells[c].Trim(' ', '"');
                }

                dt.Rows.Add(dr);
            }

            dt.EndLoadData();

            dataGrid1.Columns.Clear();
            dataGrid1.ItemsSource = dt.DefaultView;
        }
    }
}