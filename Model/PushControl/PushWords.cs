using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using ToastFish.Model.SqliteControl;

namespace ToastFish.PushControl
{
    class PushWords
    {
        // 当前推送单词的状态
        public static int WORD_CURRENT_STATUS = 0;  // 背单词时候的状态，
        public static int QUESTION_CURRENT_RIGHT_ANSWER = -1;
        public static int QUESTION_CURRENT_STATUS = 0;

        /// <summary>
        /// 使用Task防止程序阻塞
        /// </summary>
        public static Task<bool> ProcessToastNotificationRecitation()
        {
            var tcs = new TaskCompletionSource<bool>();

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
                    tcs.TrySetResult(false);
                }
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

        public static Task<bool> ProcessToastNotificationQuestion()
        {
            var tcs = new TaskCompletionSource<bool>();

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
                    tcs.TrySetResult(false);
                }
                if (Status == QUESTION_CURRENT_RIGHT_ANSWER.ToString())
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

        public static void Recitation(int Number)
        {
            Select Query = new Select();
            List<Word> RandomList = Query.GetRandomWordList(Number);
            List<Word> CopyList = Clone<Word>(RandomList);
            while (CopyList.Count != 0)
            {
                Word CurrentWord = Query.GetRandomWord(CopyList);
                PushOneWord(CurrentWord);

                WORD_CURRENT_STATUS = 2;
                while (WORD_CURRENT_STATUS == 2)
                {
                    var task = PushControl.PushWords.ProcessToastNotificationRecitation();
                    if (task.Result)
                        WORD_CURRENT_STATUS = 1;
                    else
                        WORD_CURRENT_STATUS = 0;
                }
                if (WORD_CURRENT_STATUS == 1)
                {
                    CopyList.Remove(CurrentWord);
                }
            }
            PushMessage("背完了！！接下来开始测验！！！");
            Thread.Sleep(5000);
            // 背诵结束

            CopyList = Clone<Word>(RandomList);

            for (int i = CopyList.Count - 1; i >= 0; i--)
            {
                if (CopyList[i].question != null)
                    CopyList.Remove(CopyList[i]);
            }

            while (CopyList.Count != 0)
            {
                Word CurrentWord = Query.GetRandomWord(CopyList);
                List<Word> FakeWordList = Query.GetRandomWordList(3);
                
                PushOneTransQuestion(CurrentWord, FakeWordList[0].headWord, FakeWordList[1].headWord, FakeWordList[2].headWord);

                QUESTION_CURRENT_STATUS = 2;
                while (QUESTION_CURRENT_STATUS == 2)
                {
                    var task = PushControl.PushWords.ProcessToastNotificationQuestion();
                    if (task.Result)
                        QUESTION_CURRENT_STATUS = 1;
                    else
                        QUESTION_CURRENT_STATUS = 0;
                }
                if (QUESTION_CURRENT_STATUS == 1)
                {
                    CopyList.Remove(CurrentWord);
                    PushMessage("正确,太强了吧！");
                }
                else if (QUESTION_CURRENT_STATUS == 0)
                {
                    CopyList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错了")
                    .AddText("正确答案：" + CurrentWord.headWord)
                    .Show();
                }
            }





            for (int i = RandomList.Count - 1; i >= 0; i--)
            {
                if (RandomList[i].question == null)
                    RandomList.Remove(RandomList[i]);
            }

            while (RandomList.Count != 0)
            {
                Word CurrentWord = Query.GetRandomWord(RandomList);
                PushOneQuestion(CurrentWord);

                QUESTION_CURRENT_RIGHT_ANSWER = int.Parse(CurrentWord.rightIndex);
                QUESTION_CURRENT_STATUS = 2;
                while (QUESTION_CURRENT_STATUS == 2)
                {
                    var task = PushControl.PushWords.ProcessToastNotificationQuestion();
                    if (task.Result)
                        QUESTION_CURRENT_STATUS = 1;
                    else
                        QUESTION_CURRENT_STATUS = 0;
                }
                if (QUESTION_CURRENT_STATUS == 1)
                {
                    RandomList.Remove(CurrentWord);
                    PushMessage("正确,太强了吧！");
                }
                else if (QUESTION_CURRENT_STATUS == 0)
                {
                    RandomList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错了")
                    .AddText(CurrentWord.explain)
                    .Show();
                }
                PushMessage("结束了！恭喜！！！");
            }
        }

        public static void PushMessage(string Message, string Buttom = "")
        {
            if (Buttom != "")
                new ToastContentBuilder()
                .AddText("Toast Fish")
                .AddText(Message)
                .AddButton(new ToastButton()
                .SetContent(Buttom)
                .AddArgument("action", "succeed")
                .SetBackgroundActivation())
                .Show();
            else
                new ToastContentBuilder()
                .AddText("Toast Fish")
                .AddText(Message)
                .Show();
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

        public static void PushOneQuestion(Word CurrentWord)
        {
            string Question = CurrentWord.question;
            string A = "A." + CurrentWord.choiceIndexOne;
            string B = "B." + CurrentWord.choiceIndexTwo;
            string C = "C." + CurrentWord.choiceIndexThree;
            string D = "D." + CurrentWord.choiceIndexFour;

            new ToastContentBuilder()
            .AddText("Question")
            .AddText(Question)
            
            .AddButton(new ToastButton()
                .SetContent(A)
                .AddArgument("action", "1")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent(B)
                .AddArgument("action", "2")
                .SetBackgroundActivation())
            
            .AddButton(new ToastButton()
                .SetContent(C)
                .AddArgument("action", "3")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent(D)
                .AddArgument("action", "4")
                .SetBackgroundActivation())
            .Show();

        }

        public static void PushOneTransQuestion(Word CurrentWord, string B, string C, string D)
        {
            string Question = CurrentWord.tranCN;
            string A = CurrentWord.headWord;

            Random Rd = new Random();
            int AnswerIndex = Rd.Next(4);
            QUESTION_CURRENT_RIGHT_ANSWER = AnswerIndex;

            if (AnswerIndex == 0)
            {
                new ToastContentBuilder()
               .AddText("Question")
               .AddText(Question)

               .AddButton(new ToastButton()
                   .SetContent(A)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent(B)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent(C)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent(D)
                   .AddArgument("action", "3")
                   .SetBackgroundActivation())
               .Show();
            }
            else if (AnswerIndex == 1)
            {
               new ToastContentBuilder()
              .AddText("Question")
              .AddText(Question)

              .AddButton(new ToastButton()
                  .SetContent(B)
                  .AddArgument("action", "0")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(A)
                  .AddArgument("action", "1")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(C)
                  .AddArgument("action", "2")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(D)
                  .AddArgument("action", "3")
                  .SetBackgroundActivation())
              .Show();
            }
            else if (AnswerIndex == 2)
            {
               new ToastContentBuilder()
              .AddText("Question")
              .AddText(Question)

              .AddButton(new ToastButton()
                  .SetContent(C)
                  .AddArgument("action", "0")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(B)
                  .AddArgument("action", "1")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(A)
                  .AddArgument("action", "2")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(D)
                  .AddArgument("action", "3")
                  .SetBackgroundActivation())
              .Show();
            }
            else if (AnswerIndex == 3)
            {
               new ToastContentBuilder()
              .AddText("Question")
              .AddText(Question)

              .AddButton(new ToastButton()
                  .SetContent(D)
                  .AddArgument("action", "0")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(B)
                  .AddArgument("action", "1")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(C)
                  .AddArgument("action", "2")
                  .SetBackgroundActivation())

              .AddButton(new ToastButton()
                  .SetContent(A)
                  .AddArgument("action", "3")
                  .SetBackgroundActivation())
              .Show();
            }
        }

        /// <summary>
        /// 克隆Word列表
        /// </summary>
        /// <typeparam name="Word"></typeparam>
        /// <param name="RealObject"></param>
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
