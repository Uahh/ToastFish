using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using ToastFish.Model.SqliteControl;

namespace ToastFish.PushControl
{
    class PushWords
    {
        public static void Recitation(int number)
        {
            Select Query = new Select();
            List<Word> Result = Query.GetRandomWordList(number);

            foreach (var CurrentWord in Result)
            {
                bool Cur = PushOneWord(CurrentWord);
                break;
            }

            //// 基本单词
            //new ToastContentBuilder()
            //    .AddText("cancel  ('kænsl')\nvt.取消，撤销；删去")
            //    .AddText("they are more advanced than earlier models.\n它们比先前的型号更先进。")
            //    .AddButton(new ToastButton()
            //        .SetContent("Reply")
            //        .AddArgument("action", "reply")
            //        .SetBackgroundActivation())

            //    .AddButton(new ToastButton()
            //        .SetContent("Like")
            //        .AddArgument("action", "like")
            //        .SetBackgroundActivation())
            //    .Show();

            ////选择题
            //new ToastContentBuilder()
            //.AddText("question")
            //.AddText("As we can no longer wait for the delivery of our order, we have to _______ it.", hintMaxLines: 2)
            //.AddButton(new ToastButton()
            //    .SetContent("A.postpone")
            //    .AddArgument("action", "reply")
            //    .SetBackgroundActivation())

            //.AddButton(new ToastButton()
            //    .SetContent("B.refuse")
            //    .AddArgument("action", "like")
            //    .SetBackgroundActivation())

            //.AddButton(new ToastButton()
            //    .SetContent("C.delay")
            //    .AddArgument("delay", "like")
            //    .SetBackgroundActivation())

            //.AddButton(new ToastButton()
            //    .SetContent("D.cancel1234567890")
            //    .AddArgument("action", "like")
            //    .SetBackgroundActivation())
            //.Show();
        }

        public static bool PushOneWord(Word CurrentWord) 
        {
            bool Ans = false;
            string WordPhonePosTran = CurrentWord.headWord + "  (" + CurrentWord.usPhone + ")\n" + CurrentWord.pos + ". " + CurrentWord.tranCN;
            string SentenceTran = "";
            if(CurrentWord.sentence != null && CurrentWord.sentence.Length < 50)
            {
                SentenceTran = CurrentWord.sentence + '\n' + CurrentWord.sentenceCN;
            }
            else if(CurrentWord.phrase != null)
            {
                SentenceTran = CurrentWord.phrase + '\n' + CurrentWord.phraseCN;
            }
            new ToastContentBuilder()
            .AddText(WordPhonePosTran)
            .AddText(SentenceTran)
            
            .AddButton(new ToastButton()
                .SetContent("记住了！")
                .AddArgument("action", "succeed")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("没记住..")
                .AddArgument("action", "fail")
                .SetBackgroundActivation())
            .Show();

            return Ans;
        }
    }
}
