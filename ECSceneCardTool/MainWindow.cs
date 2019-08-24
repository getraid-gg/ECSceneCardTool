using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ECSceneCardTool
{
    partial class MainWindow : Window
    {
        private List<BitmapImage> CardImages = new List<BitmapImage>();

        partial void UpdateCardView(byte[] fileContents, List<CardInfo> cards)
        {
            CardListBox.Items.Clear();
            CardImages.Clear();
            foreach (CardInfo card in cards)
            {
                CardListBox.Items.Add(new ListViewItem() { Content = card.Name });

                var image = new BitmapImage();
                using (var imageStream = new MemoryStream(SceneData, card.PngStartIndex, card.PngEndIndex - card.PngStartIndex))
                {
                    image.BeginInit();
                    image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.UriSource = null;
                    image.StreamSource = imageStream;
                    image.EndInit();
                }
                image.Freeze();

                CardImages.Add(image);
            }
        }

        private void ShowCard(int index)
        {
            if (index == -1)
            {
                CardPreview.Source = null;
            }
            else
            {
                CardPreview.Source = CardImages[index];
            }
        }
    }
}
