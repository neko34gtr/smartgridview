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

            // 抽出キーワードと置換名を保持するリスト（全項目復元済み）
            public List<KeywordMapping> Mappings { get; set; } = new List<KeywordMapping>
            {
                new KeywordMapping { Target = "郵便番号", Replacement = "郵便番号" },
                new KeywordMapping { Target = "住所", Replacement = "住所" },
                new KeywordMapping { Target = "電話番号", Replacement = "電話番号" },
                new KeywordMapping { Target = "姓", Replacement = "姓" },
                new KeywordMapping { Target = "名", Replacement = "名" },
                new KeywordMapping { Target = "名前", Replacement = "名前" },
                new KeywordMapping { Target = "姓名", Replacement = "姓名" },
                new KeywordMapping { Target = "カナ", Replacement = "カナ" }
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

            List<int> includedColumnIndices = new List<int>();
            List<string> columnNames = new List<string>();

            bool isExtractionMode = chkExtractionMode.IsChecked ?? false;

            for (int i = 0; i < firstLineCells.Length; i++)
            {
                string colName = firstLineCells[i].Trim(' ', '"');
                string finalColName = colName;
                bool isTarget = true;

                // 抽出モード時の判定と置換処理
                if (isExtractionMode && _config.Mappings.Count > 0)
                {
                    var match = _config.Mappings.Find(m => colName.Contains(m.Target));
                    if (match != null) finalColName = match.Replacement;
                    else isTarget = false;
                }

                if (!isTarget) continue;

                string uniqueColName = finalColName;
                int counter = 1;
                while (dt.Columns.Contains(uniqueColName)) uniqueColName = $"{finalColName}_{counter++}";

                dt.Columns.Add(uniqueColName, typeof(string));
                columnNames.Add(uniqueColName);
                includedColumnIndices.Add(i);
            }

            dt.BeginLoadData();
            for (int r = 1; r < lines.Length; r++)
            {
                string[] cells = lines[r].Split(delimiter);
                DataRow dr = dt.NewRow();
                for (int i = 0; i < includedColumnIndices.Count; i++)
                {
                    int originalIndex = includedColumnIndices[i];
                    if (originalIndex < cells.Length) dr[columnNames[i]] = cells[originalIndex].Trim(' ', '"');
                }
                dt.Rows.Add(dr);
            }
            dt.EndLoadData();

            // データが全行空の列を削除するロジック
            for (int i = dt.Columns.Count - 1; i >= 0; i--)
            {
                bool hasData = false;
                foreach (DataRow row in dt.Rows)
                {
                    if (!string.IsNullOrWhiteSpace(row[i].ToString())) { hasData = true; break; }
                }
                if (!hasData) dt.Columns.RemoveAt(i);
            }

            dataGrid1.Columns.Clear();
            dataGrid1.ItemsSource = dt.DefaultView;
        }
    }
}