using System;
using System.Collections.Generic;

namespace ECSceneCardTool
{
    public partial class MainWindow
    {
        private byte[] SceneData = null;
        private List<CardInfo> CardInfos;

        public void LoadSceneData(byte[] sceneData)
        {
            SceneData = sceneData;

            if (sceneData != null)
            {
                CardInfos = CardExtractor.GetCardInfos(SceneData);

                UpdateCardView(SceneData, CardInfos);
            }
        }

        private void AppendCard(byte[] cardData)
        {
            var newSceneData = new byte[SceneData.Length + cardData.Length];
            
            var lastCard = CardInfos[CardInfos.Count - 1];
            var lastCardEndIndex = lastCard.PngStartIndex + lastCard.FileLength;
            
            Array.Copy(SceneData, newSceneData, lastCardEndIndex);
            var newCardInfo = CardExtractor.ReadCard(cardData, 0).OffsetStart(lastCardEndIndex);
            Array.Copy(cardData, 0, newSceneData, newCardInfo.PngStartIndex, newCardInfo.FileLength);

            Array.Copy(SceneData, lastCardEndIndex, newSceneData, newCardInfo.PngStartIndex + newCardInfo.FileLength, SceneData.Length - lastCardEndIndex);
            
            CardInfos.Add(newCardInfo);
            var characterCardCountLocation = CardExtractor.FindCharacterCardCount(SceneData);
            Array.Copy(BitConverter.GetBytes(CardInfos.Count), 0, newSceneData, characterCardCountLocation, 4);

            SceneData = newSceneData;
            UpdateCardView(SceneData, CardInfos);
        }
        
        private void SaveScene()
        {
            FileManager.SaveScene(SceneData);
        }

        partial void UpdateCardView(byte[] fileContents, List<CardInfo> cards);
    }
}
