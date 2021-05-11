using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using ToastFish.Model.SqliteControl;
using System.Diagnostics;

namespace ToastFish.PushControl
{
    class PushWords
    {
        public static int PUSH_CURRENT_STATUS = 0;

        public static Task<bool> ProcessToastNotification()
        {
            var tcs = new TaskCompletionSource<bool>();

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                ToastArguments Args = ToastArguments.Parse(toastArgs.Argument);
                string Status = Args["action"];
                if (Status == "succeed")
                {
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetResult(false);
                }
            };

            return tcs.Task;
        }

        public static async void Recitation(int Number)
        {
            Select Query = new Select();
            List<Word> RandomList = Query.GetRandomWordList(Number);
            List<Word> CopyList = Clone<Word>(RandomList);
            while(CopyList.Count != 0)
            {
                Word CurrentWord = Query.GetRandomWord(CopyList);
                PushOneWord(CurrentWord);
                
                PUSH_CURRENT_STATUS = 2;
                while (PUSH_CURRENT_STATUS == 2)
                {
                    var task = PushControl.PushWords.ProcessToastNotification();
                    if (task.Result)
                        PUSH_CURRENT_STATUS = 1;
                }
                if(PUSH_CURRENT_STATUS == 1)
                {
                    CopyList.Remove(CurrentWord);
                }
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

        public static void PushOneWord(Word CurrentWord)
        {
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
        }

        public static Word Clone<Word>(Word RealObject)
        {
            using (Stream objStream = new MemoryStream())
            {
                //利用 System.Runtime.Serialization序列化与反序列化完成引用对象的复制
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(objStream, RealObject);
                objStream.Seek(0, SeekOrigin.Begin);
                return (Word)formatter.Deserialize(objStream);
            }

        }
        /// <summary>
        /// 克隆对象列表
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="RealObject"></param>
        /// <returns></returns>
        public static List<Word> Clone<Word>(List<Word> RealObject)
        {
            using (Stream objStream = new MemoryStream())
            {
                //利用 System.Runtime.Serialization序列化与反序列化完成引用对象的复制
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(objStream, RealObject);
                objStream.Seek(0, SeekOrigin.Begin);
                return (List<Word>)formatter.Deserialize(objStream);
            }

        }
    }
}
