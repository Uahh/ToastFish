using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using ToastFish.Model.SqliteControl;
using ToastFish.Model.Mp3;
using System.Threading;
using System.Speech.Synthesis;

namespace ToastFish.Model.PushControl
{
    class PushJpWords
    {
        // 当前推送单词的状态
        public static int WORD_CURRENT_STATUS = 0;  // 背单词时候的状态
        public static int WORD_NUMBER = 10;  // 单词数量
        public static string WORD_NUMBER_STRING = "";  // 设置的单词数量
        public static int QUESTION_CURRENT_RIGHT_ANSWER = -1;  // 当前问题的答案
        public static int QUESTION_CURRENT_STATUS = 0;  // 问题的回答状态
        public static Dictionary<string, string> AnswerDict = new Dictionary<string, string> {
            {"0","A"},{"1","B"},{"2","C"},{"3","D"}
        };

        public static Task<int> ProcessToastNotificationOrderGoin()
        {
            var tcs = new TaskCompletionSource<int>();

            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                ToastArguments Args = ToastArguments.Parse(toastArgs.Argument);
                string Status = "";
                try
                {
                    Status = Args["action"];
                }
                catch
                {
                }
                if (Status == "succeed")
                {
                    tcs.TrySetResult(0);
                }
                else if (Status == "fail")
                {
                    tcs.TrySetResult(1);
                }
                else if (Status == "voice")
                {
                    tcs.TrySetResult(2);
                }
                else
                {
                    tcs.TrySetResult(1);
                }
            };
            return tcs.Task;
        }

        public static void OrderGoin(Object Number)
        {
            Select Query = new Select();
            List<GoinWord> GoinList = Query.GetGainWordList();
            int GoinProgress = Query.GetGoinProgress();
            int Limit = GoinProgress + (int)Number;

            List < GoinWord> CopyList = Clone<GoinWord>(GoinList);
            GoinWord CurrentWord = new GoinWord();
            while (GoinProgress < Limit)
            {
                if (WORD_CURRENT_STATUS != 3)
                    CurrentWord = CopyList[GoinProgress - 1];
                PushGoinWord(CurrentWord);

                WORD_CURRENT_STATUS = 2;
                while (WORD_CURRENT_STATUS == 2)
                {
                    var task = PushWords.ProcessToastNotificationRecitation();
                    if (task.Result == 0)
                    {
                        WORD_CURRENT_STATUS = 1;
                    }
                    else if (task.Result == 1)
                    {
                        WORD_CURRENT_STATUS = 0;
                    }
                    else if (task.Result == 2)
                    {
                        WORD_CURRENT_STATUS = 3;
                        MUSIC temp = new MUSIC();
                        temp.FileName = ".\\Resources\\Goin\\" + CurrentWord.romaji + ".mp3";
                        temp.play();
                    }
                }
                if (WORD_CURRENT_STATUS == 1)
                {
                    Query.UpdateWord(CurrentWord.wordRank);
                    Query.UpdateCount();
                    GoinProgress += 1;
                }
            }
            PushWords.PushMessage("背完了！接下来开始测验！");
            Thread.Sleep(3000);
        }

        public static void PushGoinWord(GoinWord CurrentWord)
        {
            ToastNotificationManagerCompat.History.Clear();
            string OneLine = "平假名：" + CurrentWord.hiragana + " 片假名：" + CurrentWord.katakana;
            string TwoLine = "罗马音：" + CurrentWord.romaji;

            new ToastContentBuilder()
            .AddText(OneLine)
            .AddText(TwoLine)

            .AddButton(new ToastButton()
                .SetContent("记住了！")
                .AddArgument("action", "succeed")
                .SetBackgroundActivation())

            //.AddButton(new ToastButton()
            //    .SetContent("暂时跳过..")
            //    .AddArgument("action", "fail")
            //    .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("发音")
                .AddArgument("action", "voice")
                .SetBackgroundActivation())
            .Show();
        }

        public static List<GoinList> Clone<GoinList>(List<GoinList> RealObject)
        {
            using (Stream objStream = new MemoryStream())
            {
                //利用 System.Runtime.Serialization序列化与反序列化完成引用对象的复制
                IFormatter formatter = new BinaryFormatter();
                formatter.Serialize(objStream, RealObject);
                objStream.Seek(0, SeekOrigin.Begin);
                return (List<GoinList>)formatter.Deserialize(objStream);
            }

        }
    }
}
