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
    // キーワード抽出とヘッダー置換のためのマッピング構造体
    public class KeywordMapping
    {
        public string Target { get; set; } = "";
        public string Replacement { get; set; } = "";
    }

    /// <summary>
    /// メインウィンドウの処理クラス
    /// </summary>
    public partial class MainWindow : Window
    {
        // 設定ファイルの保存パス
        private const string ConfigFile = "smartgridview.json";

        /// <summary>
        /// アプリケーションの設定情報を保持するクラス
        /// </summary>
        private class AppConfig
        {
            public double Top { get; set; } = 100;
            public double Left { get; set; } = 100;
            public double Width { get; set; } = 800;
            public double Height { get; set; } = 500;
            public bool IsExtractionMode { get; set; } = false;
            public bool IsSpreadsheetCompatible { get; set; } = true;

            // 抽出キーワードと置換名を保持するリスト
            public List<KeywordMapping> Mappings { get; set; } = new List<KeywordMapping>
            {
                new KeywordMapping { Target = "EU郵便番号", Replacement = "" },
                new KeywordMapping { Target = "EU住所", Replacement = "" },
                new KeywordMapping { Target = "EU自宅固定番号", Replacement = "" },
                new KeywordMapping { Target = "EU携帯番号", Replacement = "" },
                new KeywordMapping { Target = "EU姓", Replacement = "" },
                new KeywordMapping { Target = "EU名", Replacement = "" },
                new KeywordMapping { Target = "名前", Replacement = "" },
                new KeywordMapping { Target = "姓名", Replacement = "" },
                new KeywordMapping { Target = "カナ", Replacement = "" }
            };
        }

        // 現在のアプリケーション設定インスタンス
        private AppConfig _config = new AppConfig();

        /// <summary>
        /// コンストラクタ：初期化とイベント登録
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        /// <summary>
        /// ウィンドウ読み込み時：設定ファイルの読み込みと反映
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (File.Exists(ConfigFile))
            {
                try
                {
                    // BOM無しUTF-8として設定ファイルを読み込む
                    string json = File.ReadAllText(ConfigFile, new UTF8Encoding(false));
                    _config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                    this.Top = _config.Top;
                    this.Left = _config.Left;
                    this.Width = _config.Width;
                    this.Height = _config.Height;
                    this.chkExtractionMode.IsChecked = _config.IsExtractionMode;
                    this.chkSpreadsheetCompatible.IsChecked = _config.IsSpreadsheetCompatible;
                    this.WindowStartupLocation = WindowStartupLocation.Manual;
                }
                catch { /* 読み込み失敗時はデフォルト設定を維持 */ }
            }
        }

        /// <summary>
        /// ウィンドウ閉鎖時：現在の設定をJSONファイルへ保存
        /// </summary>
        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _config.Top = this.Top;
            _config.Left = this.Left;
            _config.Width = this.Width;
            _config.Height = this.Height;
            _config.IsExtractionMode = chkExtractionMode.IsChecked ?? false;
            _config.IsSpreadsheetCompatible = chkSpreadsheetCompatible.IsChecked ?? true;

            // 整形されたJSON形式でBOM無しUTF-8保存
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFile, json, new UTF8Encoding(false));
        }

        /// <summary>
        /// 設定ボタンクリック：キーワード設定画面を表示
        /// </summary>
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow(_config.Mappings) { Owner = this };
            if (win.ShowDialog() == true)
            {
                _config.Mappings = new List<KeywordMapping>(win.Mappings);
            }
        }

        /// <summary>
        /// Aboutボタンクリック：バージョン情報画面を表示
        /// </summary>
        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow { Owner = this };
            win.ShowDialog();
        }

        /// <summary>
        /// 画面全体でのキー入力を監視し、Ctrl + V を捕捉してクリップボードから解析
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
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"ファイルの読み込み中にエラーが発生しました:\n{ex.Message}");
                    }
                }
            }
        }
        /// <summary>
        /// データグリッドの右クリック：選択行を詳細ウィンドウで表示
        /// </summary>
        private void dataGrid1_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (dataGrid1.SelectedItem is DataRowView rowView)
            {
                var win = new VerticalViewWindow(rowView.Row) { Owner = this };
                win.ShowDialog();
            }
        }
        /// <summary>
        /// データグリッドのセルがダブルクリックされたときの処理（値をクリップボードへコピー）
        /// </summary>
        private void dataGrid1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridCell)) dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridCell cell && cell.Column is DataGridColumn column && column.Header != null)
            {
                string columnName = column.Header.ToString() ?? string.Empty;
                if (cell.DataContext is DataRowView rowView && rowView.Row.Table.Columns.Contains(columnName))
                {
                    string targetText = rowView[columnName]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(targetText)) Clipboard.SetText(targetText);
                }
            }
        }

        /// <summary>
        /// テキストデータを解析してDataGridにバインドする
        /// </summary>
        private void ParseAndDisplayRawData(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                if (lines.Length <= 1) return;

                char delimiter = lines[0].Contains('\t') ? '\t' : ',';
                DataTable dt = new DataTable();
                string[] firstLineCells = lines[0].Split(delimiter);

                // スプレッドシート互換(isCompatible=true)なら、空ヘッダーはスキップ
                // 互換性なし(isCompatible=false)なら、"Column_i" として名前を強制補完
                bool isCompatible = chkSpreadsheetCompatible.IsChecked ?? true;

                // 1. ヘッダー解析：列定義の確定
                for (int i = 0; i < firstLineCells.Length; i++)
                {
                    string colName = firstLineCells[i].Trim(' ', '"');

                    if (string.IsNullOrWhiteSpace(colName))
                    {
                        // スプレッドシート互換(チェック有)なら、無視せず「Column_i」として維持する
                        // 互換性なし(チェック無)なら、空ヘッダーの列をスキップする
                        if (isCompatible)
                        {
                            colName = $"Column_{i}";
                        }
                        else
                        {
                            continue;
                        }
                    }

                    string uniqueName = colName;
                    int counter = 1;
                    while (dt.Columns.Contains(uniqueName)) uniqueName = $"{colName}_{counter++}";

                    dt.Columns.Add(uniqueName, typeof(string));
                }

                // 2. データ行解析：スプレッドシート同等の補完処理
                dt.BeginLoadData();
                for (int r = 1; r < lines.Length; r++)
                {
                    if (string.IsNullOrWhiteSpace(lines[r])) continue;

                    string[] rawCells = lines[r].Split(delimiter);
                    DataRow dr = dt.NewRow();

                    for (int i = 0; i < dt.Columns.Count; i++)
                    {
                        if (i < rawCells.Length)
                        {
                            dr[i] = rawCells[i].Trim(' ', '"');
                        }
                        else
                        {
                            dr[i] = string.Empty;
                        }
                    }
                    dt.Rows.Add(dr);
                }
                dt.EndLoadData();

                // 3. 抽出モードの適用（後処理として列をフィルタリング）
                if (chkExtractionMode.IsChecked == true)
                {
                    List<string> keepCols = new List<string>();
                    foreach (DataColumn col in dt.Columns)
                    {
                        if (_config.Mappings.Exists(m => col.ColumnName.Contains(m.Target)))
                            keepCols.Add(col.ColumnName);
                    }

                    for (int i = dt.Columns.Count - 1; i >= 0; i--)
                    {
                        if (!keepCols.Contains(dt.Columns[i].ColumnName))
                            dt.Columns.RemoveAt(i);
                    }
                }

                // 既存のItemsSourceをクリアして、競合を防ぐ
                dataGrid1.ItemsSource = null;
                dataGrid1.Columns.Clear();
                dataGrid1.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"解析エラー:\n{ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}