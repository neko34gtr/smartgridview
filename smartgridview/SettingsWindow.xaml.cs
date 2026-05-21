using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace smartgridview
{
    /// <summary>
    /// 設定画面クラス
    /// </summary>
    public partial class SettingsWindow : Window
    {
        // 画面と同期するマッピングデータ
        public ObservableCollection<KeywordMapping> Mappings { get; private set; }

        /// <summary>
        /// コンストラクタ：現在のマッピング情報を受け取って初期化
        /// </summary>
        public SettingsWindow(List<KeywordMapping> currentMappings)
        {
            InitializeComponent();
            Mappings = new ObservableCollection<KeywordMapping>(currentMappings);
            mappingGrid.ItemsSource = Mappings;
        }

        /// <summary>
        /// 保存ボタン押下：結果をTrueにして画面を閉じる
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        /// <summary>
        /// キャンセルボタン押下
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e) => this.Close();
    }
}