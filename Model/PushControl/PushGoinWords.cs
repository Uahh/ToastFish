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
using ToastFish.Model.Log;

namespace ToastFish.Model.PushControl
{
    class PushGoinWords : PushJpWords
    {
        // 当前推送单词的状态
        public static int WORD_NUMBER = 10;  // 单词数量

        public Task<int> ProcessToastNotificationOrderGoin()
        {
            var Tcs = new TaskCompletionSource<int>();

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
                    Tcs.TrySetResult(0);
                }
                else if (Status == "fail")
                {
                    Tcs.TrySetResult(1);
                }
                else if (Status == "voice")
                {
                    Tcs.TrySetResult(2);
                }
                else
                {
                    Tcs.TrySetResult(1);
                }
            };
            return Tcs.Task;
        }

        public Task<int> ProcessToastNotificationGoinQuestion()
        {
            var Tcs = new TaskCompletionSource<int>();

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
                    Tcs.TrySetResult(-1);
                }
                if (Status == QUESTION_CURRENT_RIGHT_ANSWER.ToString())
                {
                    Tcs.TrySetResult(1);
                }
                else
                {
                    Tcs.TrySetResult(0);
                }
            };
            return Tcs.Task;
        }

        public static void OrderGoin(Object Words)
        {
            WordType WordList = (WordType)Words;
            PushGoinWords pushGoinWords= new PushGoinWords();
            int Number = (int)WordList.Number;
            Select Query = new Select();
            List<GoinWord> GoinList = Query.GetGainWordList();
            int GoinProgress = Query.GetGoinProgress();
            int Limit = GoinProgress + Number;
            List<GoinWord> TestList = new List<GoinWord>();

            CreateLog Log = new CreateLog();
            String LogName = "Log\\" + DateTime.Now.ToString().Replace('/', '-').Replace(' ', '_').Replace(':', '-') + "_五十音.xlsx";
            Log.OutputExcel(LogName, GoinList, "五十音");

            GoinWord CurrentWord = new GoinWord();
            while (GoinProgress < Limit)
            {
                if (pushGoinWords.WORD_CURRENT_STATUS != 3)
                    CurrentWord = GoinList[GoinProgress - 1];
                pushGoinWords.PushGoinWord(CurrentWord);

                pushGoinWords.WORD_CURRENT_STATUS = 2;
                while (pushGoinWords.WORD_CURRENT_STATUS == 2)
                {
                    var task = pushGoinWords.ProcessToastNotificationOrderGoin();
                    if (task.Result == 0)
                    {
                        pushGoinWords.WORD_CURRENT_STATUS = 1;
                    }
                    else if (task.Result == 1)
                    {
                        pushGoinWords.WORD_CURRENT_STATUS = 0;
                    }
                    else if (task.Result == 2)
                    {
                        pushGoinWords.WORD_CURRENT_STATUS = 3;
                        MUSIC temp = new MUSIC();
                        temp.FileName = ".\\Resources\\Goin\\" + CurrentWord.romaji + ".mp3";
                        temp.play();
                    }
                }
                if (pushGoinWords.WORD_CURRENT_STATUS == 1)
                {
                    TestList.Add(CurrentWord);
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
            pushGoinWords.PushMessage("背完了！接下来开始测验！");
            Thread.Sleep(3000);

            while (TestList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = pushGoinWords.GetRandomGoinWord(TestList);
                List<GoinWord> FakeWordList = Query.GetTwoGoinRandomWords(CurrentWord);

                Random Rd = new Random();
                int Type = Rd.Next(3);
                string RightAnswer = "";
                if(Type == 0)
                {
                    pushGoinWords.PushOneGoinWordQuestion_1(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.hiragana;
                }
                else if(Type == 1)
                {
                    pushGoinWords.PushOneGoinWordQuestion_2(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.katakana;
                }
                else if(Type == 2)
                {
                    pushGoinWords.PushOneGoinWordQuestion_3(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.katakana;
                }

                pushGoinWords.QUESTION_CURRENT_STATUS = 2;
                while (pushGoinWords.QUESTION_CURRENT_STATUS == 2)
                {
                    var task = pushGoinWords.ProcessToastNotificationGoinQuestion();
                    if (task.Result == 1)
                        pushGoinWords.QUESTION_CURRENT_STATUS = 1;
                    else if (task.Result == 0)
                        pushGoinWords.QUESTION_CURRENT_STATUS = 0;
                    else if (task.Result == -1)
                        pushGoinWords.QUESTION_CURRENT_STATUS = -1;
                }

                if (pushGoinWords.QUESTION_CURRENT_STATUS == 1)
                {
                    TestList.Remove(CurrentWord);
                    Thread.Sleep(500);
                }
                else if (pushGoinWords.QUESTION_CURRENT_STATUS == 0)
                {
                    //CopyList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误 正确答案：" + pushGoinWords.AnswerDict[pushGoinWords.QUESTION_CURRENT_RIGHT_ANSWER.ToString()] + '.' + RightAnswer)
                    .Show();
                    Thread.Sleep(3000);
                }
            }
            ToastNotificationManagerCompat.History.Clear();
            pushGoinWords.PushMessage("结束了！恭喜！");
        }

        public static void UnorderGoin(Object Num)
        {
            int Number = (int)Num;
            Select Query = new Select();
            List<GoinWord> TestList = Query.GetGainWordList();
            PushGoinWords pushGoinWords = new PushGoinWords();

            while (TestList.Count > Number)
            {
                Random Rd = new Random();
                int Index = Rd.Next(TestList.Count);
                TestList.RemoveAt(Index);
            }

            CreateLog Log = new CreateLog();
            String LogName = "Log\\" + DateTime.Now.ToString().Replace('/', '-').Replace(' ', '_').Replace(':', '-') + "_随机五十音.xlsx";
            Log.OutputExcel(LogName, TestList, "五十音");

            GoinWord CurrentWord = new GoinWord();
            while (TestList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = pushGoinWords.GetRandomGoinWord(TestList);
                List<GoinWord> FakeWordList = Query.GetTwoGoinRandomWords(CurrentWord);

                Random Rd = new Random();
                int Type = Rd.Next(3);
                string RightAnswer = "";
                if (Type == 0)
                {
                    pushGoinWords.PushOneGoinWordQuestion_1(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.hiragana;
                }
                else if (Type == 1)
                {
                    pushGoinWords.PushOneGoinWordQuestion_2(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.katakana;
                }
                else if (Type == 2)
                {
                    pushGoinWords.PushOneGoinWordQuestion_3(CurrentWord, FakeWordList[0], FakeWordList[1]);
                    RightAnswer = CurrentWord.katakana;
                }

                pushGoinWords.QUESTION_CURRENT_STATUS = 2;
                while (pushGoinWords.QUESTION_CURRENT_STATUS == 2)
                {
                    var task = pushGoinWords.ProcessToastNotificationGoinQuestion();
                    if (task.Result == 1)
                        pushGoinWords.QUESTION_CURRENT_STATUS = 1;
                    else if (task.Result == 0)
                        pushGoinWords.QUESTION_CURRENT_STATUS = 0;
                    else if (task.Result == -1)
                        pushGoinWords.QUESTION_CURRENT_STATUS = -1;
                }

                if (pushGoinWords.QUESTION_CURRENT_STATUS == 1)
                {
                    TestList.Remove(CurrentWord);
                    //PushWords.PushMessage("正确,太强了吧！");
                    //Thread.Sleep(3000);
                }
                else if (pushGoinWords.QUESTION_CURRENT_STATUS == 0)
                {
                    //CopyList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误 正确答案：" + pushGoinWords.AnswerDict[pushGoinWords.QUESTION_CURRENT_RIGHT_ANSWER.ToString()] + '.' + RightAnswer)
                    .Show();
                    Thread.Sleep(3000);
                }
            }
            ToastNotificationManagerCompat.History.Clear();
            pushGoinWords.PushMessage("结束了！恭喜！");
        }

        public void PushGoinWord(GoinWord CurrentWord)
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

        public void PushOneGoinWordQuestion_1(GoinWord CurrentWord, GoinWord B, GoinWord C)
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

        public void PushOneGoinWordQuestion_2(GoinWord CurrentWord, GoinWord B, GoinWord C)
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

        public void PushOneGoinWordQuestion_3(GoinWord CurrentWord, GoinWord B, GoinWord C)
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

        public GoinWord GetRandomGoinWord(List<GoinWord> WordList)
        {
            Random Rd = new Random();
            int Index = Rd.Next(WordList.Count);
            return WordList[Index];
        }
    }
}
