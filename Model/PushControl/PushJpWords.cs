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

        public static Task<int> ProcessToastNotificationGoinQuestion()
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
                    tcs.TrySetResult(-1);
                }
                if (Status == QUESTION_CURRENT_RIGHT_ANSWER.ToString())
                {
                    tcs.TrySetResult(1);
                }
                else
                {
                    tcs.TrySetResult(0);
                }
            };
            return tcs.Task;
        }

        public static void OrderGoin(Object Num)
        {
            int Number = (int)Num;
            Select Query = new Select();
            List<GoinWord> GoinList = Query.GetGainWordList();
            int GoinProgress = Query.GetGoinProgress();
            int Limit = GoinProgress + Number;

            List<GoinWord> TestList = new List<GoinWord>();

            GoinWord CurrentWord = new GoinWord();
            while (GoinProgress < Limit)
            {
                if (WORD_CURRENT_STATUS != 3)
                    CurrentWord = GoinList[GoinProgress - 1];
                PushGoinWord(CurrentWord);

                WORD_CURRENT_STATUS = 2;
                while (WORD_CURRENT_STATUS == 2)
                {
                    var task = ProcessToastNotificationOrderGoin();
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
                    TestList.Add(CurrentWord);
                    //Query.UpdateWord(CurrentWord.wordRank);
                    Query.UpdateCount();
                    GoinProgress += 1;
                }

                Number--;
                if (GoinProgress > 104)
                {
                    GoinProgress %= 104;
                    Limit = GoinProgress + Number;
                }
            }
            PushWords.PushMessage("背完了！接下来开始测验！");
            Thread.Sleep(3000);

            while (TestList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = GetRandomGoinWord(TestList);
                List<GoinWord> FakeWordList = Query.GetTwoGoinRandomWords(CurrentWord);

                Random Rd = new Random();
                int Type = Rd.Next(3);
                string RightAnswer = "";
                if(Type == 0)
                {
                    PushOneGoinWordQuestion_1(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.hiragana;
                }
                else if(Type == 1)
                {
                    PushOneGoinWordQuestion_2(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.katakana;
                }
                else if(Type == 2)
                {
                    PushOneGoinWordQuestion_3(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.katakana;
                }

                QUESTION_CURRENT_STATUS = 2;
                while (QUESTION_CURRENT_STATUS == 2)
                {
                    var task = ProcessToastNotificationGoinQuestion();
                    if (task.Result == 1)
                        QUESTION_CURRENT_STATUS = 1;
                    else if (task.Result == 0)
                        QUESTION_CURRENT_STATUS = 0;
                    else if (task.Result == -1)
                        QUESTION_CURRENT_STATUS = -1;
                }

                if (QUESTION_CURRENT_STATUS == 1)
                {
                    TestList.Remove(CurrentWord);
                    PushWords.PushMessage("正确,太强了吧！");
                    Thread.Sleep(3000);
                }
                else if (QUESTION_CURRENT_STATUS == 0)
                {
                    //CopyList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误 正确答案：" + AnswerDict[QUESTION_CURRENT_RIGHT_ANSWER.ToString()] + '.' + RightAnswer)
                    .Show();
                    Thread.Sleep(3000);
                }
            }
            ToastNotificationManagerCompat.History.Clear();
            PushWords.PushMessage("结束了！恭喜！");
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

            .AddButton(new ToastButton()
                .SetContent("发音")
                .AddArgument("action", "voice")
                .SetBackgroundActivation())
            .Show();
        }

        public static void PushOneGoinWordQuestion_1(GoinWord CurrentWord, GoinWord B, GoinWord C)
        {
            string Question = CurrentWord.romaji;
            string A = CurrentWord.hiragana;

            Random Rd = new Random();
            int AnswerIndex = Rd.Next(3);
            QUESTION_CURRENT_RIGHT_ANSWER = AnswerIndex;

            if (AnswerIndex == 0)
            {
                new ToastContentBuilder()
               .AddText("选择平假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + A)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B.hiragana)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C.hiragana)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())

               .Show();
            }
            else if (AnswerIndex == 1)
            {
                new ToastContentBuilder()
                .AddText("选择平假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + B.hiragana)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + A)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C.hiragana)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
            else if (AnswerIndex == 2)
            {
                new ToastContentBuilder()
                .AddText("选择平假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + C.hiragana)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B.hiragana)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + A)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
        }

        public static void PushOneGoinWordQuestion_2(GoinWord CurrentWord, GoinWord B, GoinWord C)
        {
            string Question = CurrentWord.hiragana;
            string A = CurrentWord.katakana;

            Random Rd = new Random();
            int AnswerIndex = Rd.Next(3);
            QUESTION_CURRENT_RIGHT_ANSWER = AnswerIndex;

            if (AnswerIndex == 0)
            {
                new ToastContentBuilder()
               .AddText("选择片假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + A)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B.katakana)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C.katakana)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())

               .Show();
            }
            else if (AnswerIndex == 1)
            {
                new ToastContentBuilder()
                .AddText("选择片假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + B.katakana)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + A)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C.katakana)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
            else if (AnswerIndex == 2)
            {
                new ToastContentBuilder()
                .AddText("选择片假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + C.katakana)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B.katakana)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + A)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
        }

        public static void PushOneGoinWordQuestion_3(GoinWord CurrentWord, GoinWord B, GoinWord C)
        {
            string Question = CurrentWord.romaji;
            string A = CurrentWord.katakana;

            Random Rd = new Random();
            int AnswerIndex = Rd.Next(3);
            QUESTION_CURRENT_RIGHT_ANSWER = AnswerIndex;

            if (AnswerIndex == 0)
            {
                new ToastContentBuilder()
               .AddText("选择片假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + A)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B.katakana)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C.katakana)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())

               .Show();
            }
            else if (AnswerIndex == 1)
            {
                new ToastContentBuilder()
                .AddText("选择片假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + B.katakana)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + A)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C.katakana)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
            else if (AnswerIndex == 2)
            {
                new ToastContentBuilder()
                .AddText("选择片假名\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + C.katakana)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B.katakana)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + A)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
        }

        public static GoinWord GetRandomGoinWord(List<GoinWord> WordList)
        {
            Random Rd = new Random();
            int Index = Rd.Next(WordList.Count);
            return WordList[Index];
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
