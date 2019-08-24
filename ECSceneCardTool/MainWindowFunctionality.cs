using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        partial void UpdateCardView(byte[] fileContents, List<CardInfo> cards);
    }
}
