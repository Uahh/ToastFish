using System;
using System.Collections.Generic;
using ToastFish.Model.SqliteControl;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Speech.Synthesis;
using System.Threading;

namespace ToastFish.Model.PushControl
{
    class PushJpWords : PushWords
    {
        public static JpWord GetRandomWord(List<JpWord> WordList)
        {
            Random Rd = new Random();
            int Index = Rd.Next(WordList.Count);
            return WordList[Index];
        }

        public static string GetJapaneseVoiceName()
        {
            SpeechSynthesizer synth = new SpeechSynthesizer();
            foreach (InstalledVoice voice in synth.GetInstalledVoices())
            {
                VoiceInfo info = voice.VoiceInfo;
                if (info.Culture.IetfLanguageTag == "ja-JP")
                    return info.Name;
            }
            return "";
        }

        public static void PushOneWord(JpWord CurrentWord)
        {
            ToastNotificationManagerCompat.History.Clear();
            string OneLine = CurrentWord.headWord + "  (" + CurrentWord.hiragana + ")";
            if (CurrentWord.Phone != -1)
                OneLine += "  重音：" + CurrentWord.Phone.ToString();
            OneLine += "\n" + CurrentWord.tranCN;
            new ToastContentBuilder()
            .AddText(OneLine)
            .AddText(CurrentWord.pos)

            .AddButton(new ToastButton()
                .SetContent("记住了！")
                .AddArgument("action", "succeed")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("暂时跳过..")
                .AddArgument("action", "fail")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("发音")
                .AddArgument("action", "voice")
                .SetBackgroundActivation())
            .Show();
        }

        public static void PushOneTransQuestion(JpWord CurrentWord, string B, string C)
        {
            string Question = CurrentWord.tranCN;
            string A = CurrentWord.headWord;

            Random Rd = new Random();
            int AnswerIndex = Rd.Next(3);
            QUESTION_CURRENT_RIGHT_ANSWER = AnswerIndex;

            if (AnswerIndex == 0)
            {
                new ToastContentBuilder()
               .AddText("翻译\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + A)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())

               .Show();
            }
            else if (AnswerIndex == 1)
            {
                new ToastContentBuilder()
                .AddText("翻译\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + B)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + A)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + C)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
            else if (AnswerIndex == 2)
            {
                new ToastContentBuilder()
                .AddText("翻译\n" + Question)

               .AddButton(new ToastButton()
                   .SetContent("A." + C)
                   .AddArgument("action", "0")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("B." + B)
                   .AddArgument("action", "1")
                   .SetBackgroundActivation())

               .AddButton(new ToastButton()
                   .SetContent("C." + A)
                   .AddArgument("action", "2")
                   .SetBackgroundActivation())
               .Show();
            }
        }

        public static new void Recitation(Object Number)
        {
            Select Query = new Select();
            List<JpWord> RandomList = Query.GetRandomJpWordList((int)Number);
            if (RandomList.Count == 0)
            {
                PushMessage("好..好像词库里没有单词了，您就是摸鱼之王！");
                return;
            }
            List<JpWord> CopyList = Clone<JpWord>(RandomList);
            JpWord CurrentWord = new JpWord();
            while (CopyList.Count != 0)
            {
                if (WORD_CURRENT_STATUS != 3)
                    CurrentWord = GetRandomWord(CopyList);
                PushOneWord(CurrentWord);

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
                        SpeechSynthesizer synth = new SpeechSynthesizer();
                        try
                        {
                            synth.SelectVoice(GetJapaneseVoiceName());
                        }
                        catch
                        {

                        }
                        synth.SpeakAsync(CurrentWord.hiragana);
                    }
                }
                if (WORD_CURRENT_STATUS == 1)
                {
                    Query.UpdateWord(CurrentWord.wordRank);
                    Query.UpdateCount();
                    CopyList.Remove(CurrentWord);
                }
            }
            PushMessage("背完了！接下来开始测验！");
            Thread.Sleep(3000);


            while (RandomList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = GetRandomWord(RandomList);
                List<JpWord> FakeWordList = Query.GetRandomJpWords(2);

                PushOneTransQuestion(CurrentWord, FakeWordList[0].headWord, FakeWordList[1].headWord);

                QUESTION_CURRENT_STATUS = 2;
                while (QUESTION_CURRENT_STATUS == 2)
                {
                    var task = ProcessToastNotificationQuestion();
                    if (task.Result == 1)
                        QUESTION_CURRENT_STATUS = 1;
                    else if (task.Result == 0)
                        QUESTION_CURRENT_STATUS = 0;
                    else if (task.Result == -1)
                        QUESTION_CURRENT_STATUS = -1;
                }

                if (QUESTION_CURRENT_STATUS == 1)
                {
                    RandomList.Remove(CurrentWord);
                    Thread.Sleep(500);
                }
                else if (QUESTION_CURRENT_STATUS == 0)
                {
                    //CopyList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误 正确答案：" + AnswerDict[QUESTION_CURRENT_RIGHT_ANSWER.ToString()] + '.' + CurrentWord.headWord)
                    .Show();
                    Thread.Sleep(3000);
                }
            }

            ToastNotificationManagerCompat.History.Clear();
            PushMessage("结束了！恭喜！");
        }

        public static new void UnorderWord(Object Num)
        {
            int Number = (int)Num;
            Select Query = new Select();
            List<JpWord> TestList = Query.GetRandomJpWords(Number);

            JpWord CurrentWord = new JpWord();

            while (TestList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = GetRandomWord(TestList);
                List<JpWord> FakeWordList = Query.GetRandomJpWords(2);

                PushOneTransQuestion(CurrentWord, FakeWordList[0].headWord, FakeWordList[1].headWord);

                QUESTION_CURRENT_STATUS = 2;
                while (QUESTION_CURRENT_STATUS == 2)
                {
                    var task = ProcessToastNotificationQuestion();
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
                    Thread.Sleep(500);
                }
                else if (QUESTION_CURRENT_STATUS == 0)
                {
                    //CopyList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误 正确答案：" + AnswerDict[QUESTION_CURRENT_RIGHT_ANSWER.ToString()] + '.' + CurrentWord.headWord)
                    .Show();
                    Thread.Sleep(3000);
                }
            }
            ToastNotificationManagerCompat.History.Clear();
            PushMessage("结束了！恭喜！");
        }
    }
}
