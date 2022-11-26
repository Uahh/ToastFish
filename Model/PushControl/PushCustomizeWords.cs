using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToastFish.Model.Log;
using ToastFish.Model.SqliteControl;

namespace ToastFish.Model.PushControl
{
    class PushCustomizeWords : PushWords
    {
        public static void PushOneWord(CustomizeWord CurrentWord)
        {
            
            new ToastContentBuilder()
            .AddText(CurrentWord.firstLine + '\n' + CurrentWord.secondLine)
            .AddText(CurrentWord.thirdLine + '\n' + CurrentWord.fourthLine)

            .AddButton(new ToastButton()
                .SetContent("记住了！")
                .AddArgument("action", "succeed")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("暂时跳过..")
                .AddArgument("action", "fail")
                .SetBackgroundActivation())

            .Show();
        }
        public static new void Recitation(Object Words)
        {
            WordType WordList = (WordType)Words;
            PushCustomizeWords pushCustomizeWords = new PushCustomizeWords();
            List<CustomizeWord> RandomList;

            if (WordList.CustWordList == null)
                return;
            else
                RandomList = WordList.CustWordList;

            //CreateLog Log = new CreateLog();
            //String LogName = "Log\\" + DateTime.Now.ToString().Replace('/', '-').Replace(' ', '_').Replace(':', '-') + "_自定义.xlsx";
            //Log.OutputExcel(LogName, RandomList, "自定义");
            
            if (RandomList.Count == 0)
                return;

            CustomizeWord CurrentWord = new CustomizeWord();
            //int index = 0;
            while (RandomList.Count != 0)
            {
                //if (index == RandomList.Count - 1)
                //    index = 0;
                CurrentWord = RandomList[0];
                PushOneWord(CurrentWord);

                pushCustomizeWords.WORD_CURRENT_STATUS = 2;
                while (pushCustomizeWords.WORD_CURRENT_STATUS == 2)
                {
                    var task = pushCustomizeWords.ProcessToastNotificationRecitation();
                    if (task.Result == 0)
                    {
                        pushCustomizeWords.WORD_CURRENT_STATUS = 1;
                    }
                    else if (task.Result == 1)
                    {
                        pushCustomizeWords.WORD_CURRENT_STATUS = 0;
                    }
                }
             
                RandomList.Remove(CurrentWord);
                if (pushCustomizeWords.WORD_CURRENT_STATUS == 0)
                    RandomList.Add(CurrentWord);
            }
            pushCustomizeWords.PushMessage("背完了！");
        }
    }
}
