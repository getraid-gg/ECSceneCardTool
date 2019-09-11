using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ECSceneCardTool
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Copy;

            if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);

            if (files.Length != 1)
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            if (System.IO.Path.GetExtension(files[0]) != ".png")
            {
                e.Effects = DragDropEffects.None;
                return;
            }
        }

        private void Grid_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];

                FileManager.LoadScene(path, this);
            }
        }

        private void CardListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ShowCard(CardListBox.SelectedIndex);
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            FileManager.OpenSceneFile(this);
        }

        private void ExtractSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var items = CardListBox.Items;
            var selectedItems = CardListBox.SelectedItems;
            if (selectedItems.Count == 1)
            {
                CardInfo card = CardInfos[items.IndexOf(selectedItems[0])];
                FileManager.SaveCard(SceneData, card);
            }
            else if (selectedItems.Count > 1)
            {
                List<CardInfo> selectedCards = new List<CardInfo>();
                foreach (var item in selectedItems)
                {
                    selectedCards.Add(CardInfos[items.IndexOf(item)]);
                }
                FileManager.SaveCards(SceneData, selectedCards);
            }
        }

        private void ExtractAllButton_Click(object sender, RoutedEventArgs e)
        {
            FileManager.SaveCards(SceneData, CardInfos);
        }

        private void AddCardButton_Click(object sender, RoutedEventArgs e)
        {
            AppendCard(FileManager.OpenCharacterCard());
        }

        private void SaveSceneButton_Click(object sender, RoutedEventArgs e)
        {
            SaveScene();
        }
    }
}
