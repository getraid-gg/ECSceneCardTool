using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace ECSceneCardTool
{
    public static class FileManager
    {
        public static void LoadScene(string path, MainWindow target)
        {
            byte[] contents;

            try
            {
                using (var file = new FileStream(path, FileMode.Open))
                {
                    using (var fileReader = new BinaryReader(file))
                    {
                        contents = fileReader.ReadBytes((int)file.Length);
                    }
                }
            }
            catch (IOException e)
            {
                MessageBox.Show($"Failed to open file: {e.Message}", "IO Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                target.LoadSceneData(contents);
            }
            catch (SceneLoadException e)
            {
                MessageBox.Show($"Failed to load scene: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void OpenSceneFile(MainWindow target)
        {
            var openDialog = new OpenFileDialog()
            {
                DefaultExt = ".png",
                Filter = "Emotion Creators Scene (*.png)|*.png"
            };

            if (openDialog.ShowDialog() == true)
            {
                var path = openDialog.FileName;
                LoadScene(path, target);
            }
        }

        public static void SaveCard(byte[] sceneData, CardInfo cardInfo)
        {
            var saveDialog = new SaveFileDialog()
            {
                AddExtension = true,
                DefaultExt = ".png",
                Filter = "Emotion Creators Character (*.png)|*.png",
                FileName = cardInfo.Name,
                OverwritePrompt = true
            };

            if (saveDialog.ShowDialog() == false)
            {
                return;
            }

            if (saveDialog.FileName != "")
            {
                while (true)
                {
                    try
                    {
                        using (var fileStream = saveDialog.OpenFile())
                        {
                            WriteCardFile(fileStream, sceneData, cardInfo);
                        }
                        break;
                    }
                    catch (IOException)
                    {
                        var result = MessageBox.Show($"Failed to open file for saving: {Path.GetFileName(saveDialog.FileName)}. Try again?",
                            "Error", MessageBoxButton.YesNo, MessageBoxImage.Error);
                        if (result == MessageBoxResult.No)
                        {
                            break;
                        }
                    }
                }
            }
        }

        public static void SaveCards(byte[] data, List<CardInfo> cards)
        {
            var folderBrowser = new VistaFolderBrowserDialog();
            folderBrowser.ShowDialog();
            
            if (folderBrowser.SelectedPath != "")
            {
                var isContinuing = true;
                foreach (CardInfo cardInfo in cards)
                {
                    var fileName = $"{cardInfo.Name}.png";
                    var fullPath = Path.Combine(folderBrowser.SelectedPath, fileName);

                    if (File.Exists(fullPath))
                    {
                        string newFileName;
                        string newFullPath;
                        var nameNumber = 1;
                        do
                        {
                            newFileName = $"{cardInfo.Name} ({nameNumber}).png";
                            newFullPath = Path.Combine(folderBrowser.SelectedPath, newFileName);
                            nameNumber++;
                        }
                        while (File.Exists(newFullPath));

                        var result = MessageBox.Show($"Card file {fileName} already exists. Overwrite? If No is selected, the new file will be named {newFileName}.",
                            "Overwrite?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Cancel)
                        {
                            break;
                        }
                        else if (result == MessageBoxResult.No)
                        {
                            fileName = newFileName;
                            fullPath = newFullPath;
                        }
                    }

                    while (true)
                    {
                        try
                        {
                            using (var fileStream = File.OpenWrite(fullPath))
                            {
                                fileStream.SetLength(0);
                                WriteCardFile(fileStream, data, cardInfo);
                            }
                            break;
                        }
                        catch (IOException)
                        {
                            var result = MessageBox.Show($"Failed to save file {fileName}. Try again?", 
                                "Error", MessageBoxButton.YesNoCancel, MessageBoxImage.Error);
                            if (result == MessageBoxResult.No)
                            {
                                break;
                            }
                            else if (result == MessageBoxResult.Cancel)
                            {
                                isContinuing = false;
                                break;
                            }
                        }
                    }

                    if (!isContinuing)
                    {
                        break;
                    }
                }
            }
        }

        private static void WriteCardFile(Stream fs, byte[] data, CardInfo cardInfo)
        {
            fs.Write(data, cardInfo.PngStartIndex, cardInfo.FileEndIndex - cardInfo.PngStartIndex);
        }
    }
}
