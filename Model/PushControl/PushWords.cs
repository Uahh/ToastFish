using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using ToastFish.Model.SqliteControl;
using System.Threading;
using System.Speech.Synthesis;
using ToastFish.Model.Log;
using System.Diagnostics;
using System.Reactive.Subjects;

namespace ToastFish.Model.PushControl
{
     class MyHotObservable : IObservable<string>
    {
        private  Subject<string> subject = new Subject<string>();
        string  last_event = "";



        public IDisposable Subscribe(IObserver<string> observer)
        {
            return this.subject.Subscribe(observer);
        }

        public void raiseEvent(string  events)
        {
            this.subject.OnNext(events);
            last_event = events;
        }

    }


    class PushWords
    {
        // 当前推送单词的状态
        public static int WORD_CURRENT_STATUS = 0;  // 背单词时候的状态
        public static string WORD_NUMBER_STRING = "";  // 设置的单词数量
        public static int QUESTION_CURRENT_RIGHT_ANSWER = -1;  // 当前问题的答案
        public static int QUESTION_CURRENT_STATUS = 0;  // 问题的回答状态
        public static Dictionary<string, string> AnswerDict = new Dictionary<string, string> {
            {"0","A"},{"1","B"},{"2","C"},{"3","D"}
        };
        public static MyHotObservable HotKeytObservable= new MyHotObservable();

        /// <summary>
        /// 判断字符串是否为数字
        /// </summary>
        public static bool IsNumber(string str)
        {
            char[] ch = new char[str.Length];
            ch = str.ToCharArray();
            for (int i = 0; i < ch.Length; i++)    
            {
                if (ch[i] < 48 || ch[i] > 57)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 从List中获取一个随机单词
        /// </summary>
        public static Word GetRandomWord(List<Word> WordList)
        {
            Random Rd = new Random();
            int Index = Rd.Next(WordList.Count);
            return WordList[Index];
        }

        /// <summary>
        /// 推送单词的Task
        /// </summary>
        public async static Task<int> ProcessToastNotificationRecitation()//CancellationToken cancellationToken
        {
            var tcs = new TaskCompletionSource<int>();

            using (HotKeytObservable.Subscribe(events =>
            {
                Debug.WriteLine("HotKeytObservable.Subscribe:" + events);
                switch (events)
                {
                    case "D"://voice
                        tcs.TrySetResult(2);
                        break;
                    case "A": // succeed
                        tcs.TrySetResult(0);
                        break;
                    case "S"://fail
                        tcs.TrySetResult(1);
                        break;
                    default:
                        break;
                }

            })) {
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
                    Debug.Print("Debunging....");
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
                return await tcs.Task;
            }            
        }
        
        /// <summary>
        /// 推送问题的Task
        /// </summary>
        public static async Task<int> ProcessToastNotificationQuestion()
        {
            var tcs = new TaskCompletionSource<int>();

            using (HotKeytObservable.Subscribe(events =>
            {
                Debug.WriteLine("HotKeytObservable.Subscribe:" + events);
                int ans = -1;
                switch (events)
                {
                    case "A"://A
                        ans = 0;
                        break;
                    case "S"://B
                        ans = 1;
                        break;
                    case "D"://C
                        ans = 2;
                        break;
                    case "F"://D
                        ans = 3;
                        break;
                    default:
                        break;
                }
                if (ans == QUESTION_CURRENT_RIGHT_ANSWER)
                {
                    tcs.TrySetResult(1);
                }
                else
                {
                    tcs.TrySetResult(0);
                }

            })) {
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
                return await tcs.Task;
            }            
        }

        /// <summary>
        /// 设置单词数量的Task
        /// </summary>
        public static Task<int> ProcessToastNotificationSetNumber()
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
                    tcs.TrySetResult(0);
                }
                if (Status == "yes")
                {
                    WORD_NUMBER_STRING = (string)toastArgs.UserInput["number"];
                    tcs.TrySetResult(1);
                }
                else
                {
                    tcs.TrySetResult(0);
                }
            };
            return tcs.Task;
        }

        /// <summary>
        /// 设置单词数量
        /// </summary>
        public static void SetWordNumber()
        {
            new ToastContentBuilder()
            .AddText("这次要背多少个？")
            .AddToastInput(new ToastSelectionBox("number")
            {
                DefaultSelectionBoxItemId = "10",
                Items =
                {
                    new ToastSelectionBoxItem("5", "5"),
                    new ToastSelectionBoxItem("10", "10"),
                    new ToastSelectionBoxItem("15", "15"),
                    new ToastSelectionBoxItem("20", "20")
                }
            })
            .AddButton(new ToastButton()
                .SetContent("确定")
                .AddArgument("action", "yes")
                .SetBackgroundActivation())
            .Show();
            var task = PushWords.ProcessToastNotificationSetNumber();
            if(task.Result == 1)
            {
                if(IsNumber(WORD_NUMBER_STRING))
                {
                    Select.WORD_NUMBER = int.Parse(WORD_NUMBER_STRING);
                    Select Temp = new Select();
                    Temp.UpdateNumber(Select.WORD_NUMBER);
                    PushMessage("已设置单词数量为：" + WORD_NUMBER_STRING);
                }
            } 
        }

        public static void SetEngType()
        {
            new ToastContentBuilder()
            .AddText("请选择发音类型？")
            .AddToastInput(new ToastSelectionBox("number")
            {
                DefaultSelectionBoxItemId = "1",
                Items =
                {
                    new ToastSelectionBoxItem("1", "美国"),
                    new ToastSelectionBoxItem("2", "英国")
                }
            })
            .AddButton(new ToastButton()
                .SetContent("确定")
                .AddArgument("action", "yes")
                .SetBackgroundActivation())
            .Show();
            var task = PushWords.ProcessToastNotificationSetNumber();
            if (task.Result == 1)
            {
                if (IsNumber(WORD_NUMBER_STRING))
                {
                    Select.ENG_TYPE = int.Parse(WORD_NUMBER_STRING);
                    string rst;
                    switch (Select.ENG_TYPE)
                    {
                        case 1:
                            rst = "美国";
                            break;
                        default:
                            rst = "英国";
                            break;

                    }
                    PushMessage("已设置英语类型为：" + rst);
                }
            }
        }

        

        /// <summary>
        /// 背诵单词
        /// </summary>
        public static void Recitation(Object Words)
        {
            WordType WordList = (WordType)Words;
            
            Select Query = new Select();
            List<Word> RandomList;
            bool ImportFlag = true;

            if (WordList.WordList == null)
            {
                RandomList = Query.GetRandomWordList((int)WordList.Number);
                ImportFlag = false;
            }
            else
            {
                RandomList = WordList.WordList;
            }

            if (ImportFlag == false)
            {
                CreateLog Log = new CreateLog();
                String LogName = "Log\\" + DateTime.Now.ToString().Replace('/', '-').Replace(' ', '_').Replace(':', '-') + "_英语.xlsx";
                Log.OutputExcel(LogName, RandomList, "英语");
            }

            if (RandomList.Count == 0 && ImportFlag == false)
            {
                PushMessage("好..好像词库里没有单词了，您就是摸鱼之王！");
                return;
            }
            else if (RandomList.Count == 0 && ImportFlag == true)
            {
                return;
            }
            List<Word> CopyList = Clone<Word>(RandomList);
            Word CurrentWord = new Word();
            while (CopyList.Count != 0)
            {
                if (WORD_CURRENT_STATUS != 3)
                    CurrentWord = CopyList[0];// GetRandomWord(CopyList);
                PushOneWord(CurrentWord);

                WORD_CURRENT_STATUS = 2;
                while (WORD_CURRENT_STATUS == 2)
                {
                    int result = -1;
                    try
                    {
                        var task =  PushWords.ProcessToastNotificationRecitation();
                        result = task.Result;
                    }
                    catch (Exception e) {
                        Debug.WriteLine(e.Message);
                        return;
                    }
                    if (result == 0)
                    {
                        WORD_CURRENT_STATUS = 1;
                    }
                    else if (result == 1)
                    {
                        WORD_CURRENT_STATUS = 0;
                    }
                    else if (result == 2)
                    {
                        WORD_CURRENT_STATUS = 3;
                        string word_pron,word_save_name;
                        switch (Select.ENG_TYPE)
                        {
                            case 1:
                                word_save_name = CurrentWord.headWord + "_us";
                                word_pron = CurrentWord.headWord + "&type=1";
                                break;
                            default:
                                word_save_name = CurrentWord.headWord + "_uk";
                                word_pron = CurrentWord.headWord + "&type=2";
                                break;
                        }
                        List<string> words = new List<string>();
                        //将Person对象放入集合
                        words.Add(word_save_name);
                        words.Add(word_pron);
                        bool ret= Download.DownloadMp3.PlayMp3(words);
                        if (ret==false) {
                            SpeechSynthesizer synth = new SpeechSynthesizer();
                            synth.SpeakAsync(CurrentWord.headWord);
                        }
                    }
                }
                if (WORD_CURRENT_STATUS == 1)
                {
                    if (ImportFlag == false)
                    {
                        Query.UpdateWord(CurrentWord.wordRank);
                        Query.UpdateCount();
                    }
                    CopyList.Remove(CurrentWord);
                } else if (WORD_CURRENT_STATUS == 0) {
                    if (CopyList.Count == 2) 
                    {
                        CopyList.Remove(CurrentWord);
                        CopyList.Add(CurrentWord);
                    }
                    else if(CopyList.Count >= 3){
                        CopyList.Remove(CurrentWord);                        
                        Random Rd = new Random();
                        int Index = Rd.Next(CopyList.Count-1);
                        CopyList.Insert(Index+1,CurrentWord);
                    }
                    
                }
            }
            PushMessage("背完了！接下来开始测验！");
            Thread.Sleep(3000);

            /* 背诵结束 */

            CopyList = Clone<Word>(RandomList);
            for (int i = CopyList.Count - 1; i >= 0; i--)
            {
                if (CopyList[i].question != null || CopyList[i].question == "")
                    CopyList.Remove(CopyList[i]);
            }

            while (CopyList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = GetRandomWord(CopyList);
                List<Word> FakeWordList = Query.GetRandomWords(2);

                PushOneTransQuestion(CurrentWord, FakeWordList[0].headWord, FakeWordList[1].headWord);

                QUESTION_CURRENT_STATUS = 2;
                while (QUESTION_CURRENT_STATUS == 2)
                {
                    int rst = -1;
                    try
                    {
                        var task = ProcessToastNotificationQuestion();
                        rst = task.Result;
                    }
                    catch (Exception e) { 
                        Debug.WriteLine(e.Message);
                        break;
                    }                    

                    if (rst == 1)
                        QUESTION_CURRENT_STATUS = 1;
                    else if(rst == 0)
                        QUESTION_CURRENT_STATUS = 0;
                    else if (rst == -1)
                        QUESTION_CURRENT_STATUS = -1;
                }
                
                if (QUESTION_CURRENT_STATUS == 1)
                {
                    CopyList.Remove(CurrentWord);
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

            for (int i = RandomList.Count - 1; i >= 0; i--)
            {
                if (RandomList[i].question == null || RandomList[i].question == "")
                    RandomList.Remove(RandomList[i]);
            }

            while (RandomList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                CurrentWord = GetRandomWord(RandomList);
                QUESTION_CURRENT_RIGHT_ANSWER = int.Parse(CurrentWord.rightIndex) - 1;
                PushOneQuestion(CurrentWord);

                QUESTION_CURRENT_STATUS = 2;
                while (QUESTION_CURRENT_STATUS == 2)
                {
                    int rst = -1;
                    try
                    {
                        var task = ProcessToastNotificationQuestion();
                        rst = task.Result;
                    } catch (Exception e) {
                        Debug.WriteLine(e.Message);
                        break;
                    }
                    if (rst == 1)
                        QUESTION_CURRENT_STATUS = 1;
                    else if (rst == 0)
                        QUESTION_CURRENT_STATUS = 0;
                }
                if (QUESTION_CURRENT_STATUS == 1)
                {
                    RandomList.Remove(CurrentWord);
                    Thread.Sleep(500);
                }
                else if (QUESTION_CURRENT_STATUS == 0)
                {
                    //RandomList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误, 正确答案：" + AnswerDict[QUESTION_CURRENT_RIGHT_ANSWER.ToString()])
                    .AddText(CurrentWord.explain)
                    .Show();
                    Thread.Sleep(6000);
                }
            }
            ToastNotificationManagerCompat.History.Clear();
            PushMessage("结束了！恭喜！");
        }

        public static void UnorderWord(Object Num)
        {
            int Number = (int)Num;
            Select Query = new Select();
            List<Word> TestList = Query.GetRandomWords(Number);

            CreateLog Log = new CreateLog();
            String LogName = "Log\\" + DateTime.Now.ToString().Replace('/', '-').Replace(' ', '_').Replace(':', '-') + "_随机英语单词.xlsx";
            Log.OutputExcel(LogName, TestList, "英语");

            Word CurrentWord = new Word();

            while (TestList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = GetRandomWord(TestList);
                List<Word> FakeWordList = Query.GetRandomWords(2);

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

        /// <summary>
        /// 推送一条通知
        /// </summary>
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

        /// <summary>
        /// 推送一个单词
        /// </summary>
        /// <param name="CurrentWord"></param>
        public static void PushOneWord(Word CurrentWord)
        {
            ToastNotificationManagerCompat.History.Clear();
            string Phoneme;
            switch (Select.ENG_TYPE) {
                case 1:
                    Phoneme = CurrentWord.usPhone;
                    break;
                default:
                    Phoneme = CurrentWord.ukPhone;
                    break;
            }
            string WordPhonePosTran = CurrentWord.headWord + "  (" + Phoneme + ")\n" + CurrentWord.pos + ". " + CurrentWord.tranCN;
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
                .SetContent("暂时跳过..")
                .AddArgument("action", "fail")
                .SetBackgroundActivation())
            
            .AddButton(new ToastButton()
                .SetContent("发音")
                .AddArgument("action", "voice")
                .SetBackgroundActivation())
            .Show();
        }

        /// <summary>
        /// 推送一道选择题
        /// </summary>
        public static void PushOneQuestion(Word CurrentWord)
        {
            string Question = CurrentWord.question;
            string A = CurrentWord.choiceIndexOne;
            string B = CurrentWord.choiceIndexTwo;
            string C = CurrentWord.choiceIndexThree;
            string D = CurrentWord.choiceIndexFour;

            new ToastContentBuilder()
            .AddText("选择题")
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

        /// <summary>
        /// 推送翻译问题
        /// </summary>
        public static void PushOneTransQuestion(Word CurrentWord, string B, string C)
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
