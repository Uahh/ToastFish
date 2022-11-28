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
using ToastFish.Model.SM2plus;
using System.Windows.Forms;

namespace ToastFish.Model.PushControl
{
    class MyHotObservable : IObservable<string>
    {
        private Subject<string> subject = new Subject<string>();
        string last_event = "";



        public IDisposable Subscribe(IObserver<string> observer)
        {
            return this.subject.Subscribe(observer);
        }

        public void raiseEvent(string events)
        {
            this.subject.OnNext(events);
            last_event = events;
        }

    }


    class PushWords
    {
        // 当前推送单词的状态
        public int WORD_CURRENT_STATUS = 0;  // 背单词时候的状态
        public string WORD_NUMBER_STRING = "";  // 设置的单词数量
        public int QUESTION_CURRENT_RIGHT_ANSWER = -1;  // 当前问题的答案
        public int QUESTION_CURRENT_STATUS = 0;  // 问题的回答状态
        public Dictionary<string, string> AnswerDict = new Dictionary<string, string> {
            {"0","A"},{"1","B"},{"2","C"},{"3","D"}
        };
        public static MyHotObservable HotKeytObservable = new MyHotObservable();

        /// <summary>
        /// 判断字符串是否为数字
        /// </summary>
        public bool IsNumber(string str)
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
        public Word GetRandomWord(List<Word> WordList)
        {
            Random Rd = new Random();
            int Index = Rd.Next(WordList.Count);
            return WordList[Index];
        }

        public List<Word> GetRandomWordLst(Word CurrentWord, List<Word> WordList, int num)
        {
            Debug.Assert(num < WordList.Count);
            Random Rd = new Random();
            List<Word> CopyList = new List<Word>(WordList.ToArray());  // Clone<Word>(WordList);

            int id1 = CopyList.FindIndex(wordi =>
            {
                return (wordi.wordRank == CurrentWord.wordRank);
            });
            if (id1 >= 0)
                CopyList.RemoveAt(id1);

            List<Word> randwordLst = new List<Word>();
            for (int i = 0; i < num; i++)
            {
                int Index = Rd.Next(CopyList.Count);
                randwordLst.Add(CopyList[Index]);
                CopyList.RemoveAt(Index);
            }
            Debug.WriteLine($"copyList.count={CopyList.Count}");
            Debug.WriteLine($"WordList.count={WordList.Count}");
            return randwordLst;
        }

        /// <summary>
        /// 推送单词的Task
        /// </summary>
        public async Task<int> ProcessToastNotificationRecitation()//CancellationToken cancellationToken
        {
            var Tcs = new TaskCompletionSource<int>();

            using (HotKeytObservable.Subscribe(events =>
            {
                Debug.WriteLine("HotKeytObservable.Subscribe:" + events);
                switch (events)
                {
                    case "1": // succeed
                        Tcs.TrySetResult(0);
                        break;
                    case "2"://fail
                        Tcs.TrySetResult(1);
                        break;
                    case "3"://voice
                        Tcs.TrySetResult(2);
                        break;
                    default:
                        break;
                }

            }))
            {
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
                    // Debug.WriteLine("Debuging....");
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
                return await Tcs.Task;
            }
        }

        public async Task<int> ProcessToastNotificationRecitationSM2()//CancellationToken cancellationToken
        {
            var Tcs = new TaskCompletionSource<int>();

            using (HotKeytObservable.Subscribe(events =>
            {
                Debug.WriteLine("HotKeytObservable.Subscribe:" + events);
                switch (events)
                {
                    case "1": //again
                        Tcs.TrySetResult(1);
                        break;
                    case "2"://hard
                        Tcs.TrySetResult(2);
                        break;
                    case "3"://good
                        Tcs.TrySetResult(3);
                        break;
                    case "4"://easy
                        Tcs.TrySetResult(4);
                        break;
                    case "S"://voice
                        Tcs.TrySetResult(0);
                        break;
                    default:
                        break;
                }

            }))
            {
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
                    //Debug.WriteLine("Debuging....");
                    if (Status == "easy")
                    {
                        Tcs.TrySetResult(4);
                    }
                    else if (Status == "good")
                    {
                        Tcs.TrySetResult(3);
                    }
                    else if (Status == "hard")
                    {
                        Tcs.TrySetResult(2);
                    }
                    else if (Status == "again")
                    {
                        Tcs.TrySetResult(1);
                    }
                    else if (Status == "voice")
                    {
                        Tcs.TrySetResult(0);
                    }
                    else
                    {
                        Tcs.TrySetResult(0);
                    }
                };
                return await Tcs.Task;
            }
        }

        /// <summary>
        /// 推送问题的Task
        /// </summary>
        public async Task<int> ProcessToastNotificationQuestion()
        {
            var Tcs = new TaskCompletionSource<int>();

            using (HotKeytObservable.Subscribe(events =>
            {
                Debug.WriteLine("HotKeytObservable.Subscribe:" + events);
                int Ans = -1;
                switch (events)
                {
                    case "1":  // A
                        Ans = 0;
                        break;
                    case "2":  // B
                        Ans = 1;
                        break;
                    case "3":  // C
                        Ans = 2;
                        break;
                    case "4":  // D
                        Ans = 3;
                        break;
                    default:
                        break;
                }
                if (Ans == QUESTION_CURRENT_RIGHT_ANSWER)
                {
                    Tcs.TrySetResult(1);
                }
                else
                {
                    Tcs.TrySetResult(0);
                }

            }))
            {
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
                return await Tcs.Task;
            }
        }

        /// <summary>
        /// 设置单词数量的Task
        /// </summary>
        public Task<int> ProcessToastNotificationSetNumber()
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
                    Tcs.TrySetResult(0);
                }
                if (Status == "yes")
                {
                    WORD_NUMBER_STRING = (string)toastArgs.UserInput["number"];
                    Tcs.TrySetResult(1);
                }
                else
                {
                    Tcs.TrySetResult(0);
                }
            };
            return Tcs.Task;
        }

        /// <summary>
        /// 设置单词数量
        /// </summary>
        public void SetWordNumber()
        {
            new ToastContentBuilder()
            .AddText("这次要背多少个？")
            .AddToastInput(new ToastSelectionBox("number")
            {
                DefaultSelectionBoxItemId = Select.WORD_NUMBER.ToString(),
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
            var task = this.ProcessToastNotificationSetNumber();
            if (task.Result == 1)
            {
                if (IsNumber(WORD_NUMBER_STRING))
                {
                    Select.WORD_NUMBER = int.Parse(WORD_NUMBER_STRING);
                    Select Temp = new Select();
                    Temp.UpdateNumber(Select.WORD_NUMBER);
                    PushMessage("已设置单词数量为：" + WORD_NUMBER_STRING);
                }
            }
        }

        public void SetEngType()
        {
            new ToastContentBuilder()
            .AddText("请选择发音类型？")
            .AddToastInput(new ToastSelectionBox("number")
            {
                DefaultSelectionBoxItemId = Select.ENG_TYPE.ToString(),
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
            var task = this.ProcessToastNotificationSetNumber();
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
                    Select Temp = new Select();
                    Temp.UpdateGlobalConfig();
                    //Select.UpdateGlobalConfig();
                }
            }
        }

        public double pushCard(Card card, Cardstatus cardstatus, int numNewCards, int numLearingCards, int numReviewedCards)
        {
            Word CurrentWord = card.word;
            int answer;
            double result = -1;
            bool isFinished = false;
            string word_pron, word_save_name;
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
            words.Add(word_save_name);
            words.Add(word_pron);
            if (Select.AUTO_PLAY != 0)
            {
                bool isOK = Download.DownloadMp3.PlayMp3(words);
                if (isOK == false)
                {
                    SpeechSynthesizer synth = new SpeechSynthesizer();
                    synth.SpeakAsync(CurrentWord.headWord);
                }
            }
            while (isFinished != true)
            {
                PushOneWordSM2(CurrentWord, cardstatus, numNewCards, numLearingCards, numReviewedCards);
                try
                {
                    var task = this.ProcessToastNotificationRecitationSM2();
                    answer = task.Result;
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e.Message);
                    return result;
                }
                if (answer == 1)
                {
                    result = Parameters.Again;
                    isFinished = true;
                }
                else if (answer == 2)
                {
                    result = Parameters.Hard;
                    isFinished = true;
                }
                else if (answer == 3)
                {
                    result = Parameters.Good;
                    isFinished = true;
                }
                else if (answer == 4)
                {
                    result = Parameters.Easy;
                    isFinished = true;
                }
                else if (answer == 0)
                {
                    bool isOK = Download.DownloadMp3.PlayMp3(words);
                    if (isOK == false)
                    {
                        SpeechSynthesizer synth = new SpeechSynthesizer();
                        synth.SpeakAsync(CurrentWord.headWord);
                    }
                }
            }
            return result;
        }

        public static void RecitationSM2(Object wordtype)
        {
            WordType WordList = (WordType)wordtype;
            PushWords pushWords = new PushWords();
            if (WordList.WordList != null)
            {
                Recitation(wordtype);
                return;
            }
            Select Query = new Select();
            Query.SelectWordList(); //import database
            Query.GenerateRandomNewCardList(WordList.Number, out List<Card> NewCardLst);
            Query.GetOverdueReviewedCardList(2 * WordList.Number, out List<Card> ReviewedCardLst);
            //NewCardLst.Count;
            //ReviewedCardLst.Count;
            List<Card> LearningCardLst = new List<Card>();
            List<Card> FinishedCardLst = new List<Card>();

            double Score;
            // New Card First
            Debug.WriteLine($"开始背单词 @{DateTime.Now}");
            while (NewCardLst.Count != 0)
            {
                Card newCardi = NewCardLst[0];
                // Cardstatus cardstatus, int numNewCards, int numLearingCards, int numReviewedCards)
                Score = pushWords.pushCard(newCardi, newCardi.status, NewCardLst.Count, LearningCardLst.Count, ReviewedCardLst.Count);
                if (Score == -1)
                {
                    MessageBox.Show("卡题出错！");
                    return;
                }
                NewCardLst.RemoveAt(0);
                newCardi.updateCard(Score);
                if (newCardi.status != Cardstatus.Reviewed)
                {
                    LearningCardLst.Add(newCardi);
                }
                else
                {
                    FinishedCardLst.Add(newCardi);
                }
                LearningCardLst.Sort((a, b) =>
                {
                    // compare a to b to get ascending order
                    int result = a.dateLearingDue.CompareTo(b.dateLearingDue);
                    return result;
                });
                for (int j = 0; j < LearningCardLst.Count; j++)
                {
                    Card Cardj = LearningCardLst[j];
                    if (Cardj.isDue())
                    {
                        Score = pushWords.pushCard(Cardj, Cardj.status, NewCardLst.Count, LearningCardLst.Count, ReviewedCardLst.Count);
                        if (Score == -1)
                        {
                            MessageBox.Show("卡题出错！");
                            return;
                        }
                        Cardj.updateCard(Score);
                        if (Cardj.status == Cardstatus.Reviewed)
                        {
                            LearningCardLst.RemoveAt(j);
                            FinishedCardLst.Add(Cardj);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            //Reviewed Card Next
            while (ReviewedCardLst.Count != 0)
            {
                Card newCardi = ReviewedCardLst[0];
                // Cardstatus cardstatus, int numNewCards, int numLearingCards, int numReviewedCards)
                Score = pushWords.pushCard(newCardi, newCardi.status, NewCardLst.Count, LearningCardLst.Count, ReviewedCardLst.Count);
                if (Score == -1)
                {
                    MessageBox.Show("卡题出错！");
                    return;
                }
                ReviewedCardLst.RemoveAt(0);
                newCardi.updateCard(Score);
                if (newCardi.status != Cardstatus.Reviewed)
                {
                    LearningCardLst.Add(newCardi);
                }
                else
                {
                    FinishedCardLst.Add(newCardi);
                }
                LearningCardLst.Sort((a, b) =>
                {
                    // compare a to b to get ascending order
                    int result = a.dateLearingDue.CompareTo(b.dateLearingDue);
                    return result;
                });
                for (int j = 0; j < LearningCardLst.Count; j++)
                {
                    Card Cardj = LearningCardLst[j];
                    if (Cardj.isDue())
                    {
                        Score = pushWords.pushCard(Cardj, Cardj.status, NewCardLst.Count, LearningCardLst.Count, ReviewedCardLst.Count);
                        if (Score == -1)
                        {
                            MessageBox.Show("卡题出错！");
                            return;
                        }
                        Cardj.updateCard(Score);
                        if (Cardj.status == Cardstatus.Reviewed)
                        {
                            LearningCardLst.RemoveAt(j);
                            FinishedCardLst.Add(Cardj);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            //the Remain Learing Card
            LearningCardLst.Sort((a, b) =>
            {
                // compare a to b to get ascending order
                int result = a.dateLearingDue.CompareTo(b.dateLearingDue);
                return result;
            });
            while (LearningCardLst.Count != 0)
            {
                Card Cardj = LearningCardLst[0];
                Score = pushWords.pushCard(Cardj, Cardj.status, NewCardLst.Count, LearningCardLst.Count, ReviewedCardLst.Count);
                if (Score == -1)
                {
                    MessageBox.Show("卡题出错！");
                    return;
                }
                Cardj.updateCard(Score);
                if (Cardj.status == Cardstatus.Reviewed)
                {
                    LearningCardLst.RemoveAt(0);
                    FinishedCardLst.Add(Cardj);
                }
                else
                {
                    LearningCardLst.Sort((a, b) =>
                    {
                        // compare a to b to get ascending order
                        int result = a.dateLearingDue.CompareTo(b.dateLearingDue);
                        return result;
                    });
                }

            }

            Debug.WriteLine($"更新数据库 @{DateTime.Now}");
            Query.updateCardDateBase(FinishedCardLst);
            Debug.WriteLine($"数据库更新完毕 @{DateTime.Now}");

            FinishedCardLst.Sort((b, a) =>
            {
                // compare a to b to get decending order
                int result = a.percentOverdue.CompareTo(b.percentOverdue);
                return result;
            });
            List<Word> RandomList = new List<Word>();
            List<Word> AllFinshedWordList = new List<Word>();

            foreach (var cardi in FinishedCardLst)
            {
                AllFinshedWordList.Add(cardi.word);
                if (cardi.lastScore != Parameters.Easy)
                    RandomList.Add(cardi.word);
            }
            if (RandomList.Count > 0)
            {
                pushWords.PushMessage("背完了！接下来开始测验记忆模糊的单词！");
                pushWords.PushWaitAllQuestions(RandomList, (List<Word>)Query.AllWordList);
            }
            pushWords.PushMessage("结束了！恭喜！");

            if (Select.AUTO_LOG != 0)
            {
                CreateLog Log = new CreateLog();
                String LogName = "Log\\" + DateTime.Now.ToString().Replace('/', '-').Replace(' ', '_').Replace(':', '-') + "_英语.xlsx";
                Log.OutputExcel(LogName, AllFinshedWordList, "英语");
            }


        }

        /// <summary>
        /// 背诵单词
        /// </summary>
        public static void Recitation(Object Words)
        {
            Select Query = new Select();
            PushWords pushWords = new PushWords();

            WordType WordList = (WordType)Words;
            List<Word> RandomList;
            bool ImportFlag = true;

            if (WordList.WordList == null)
            {
                Query.SelectWordList();
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
                pushWords.PushMessage("好..好像词库里没有单词了，您就是摸鱼之王！");
                return;
            }
            else if (RandomList.Count == 0 && ImportFlag == true)
            {
                return;
            }
            List<Word> CopyList = pushWords.Clone<Word>(RandomList);
            Word CurrentWord = new Word();
            Debug.WriteLine($"开始背单词 @{DateTime.Now}");
            while (CopyList.Count != 0)
            {
                if (pushWords.WORD_CURRENT_STATUS != 3)
                    CurrentWord = CopyList[0];// GetRandomWord(CopyList);
                pushWords.PushOneWord(CurrentWord);

                pushWords.WORD_CURRENT_STATUS = 2;
                while (pushWords.WORD_CURRENT_STATUS == 2)
                {
                    int result = -1;
                    try
                    {
                        var task = pushWords.ProcessToastNotificationRecitation();
                        result = task.Result;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine(e.Message);
                        return;
                    }
                    if (result == 0)
                    {
                        pushWords.WORD_CURRENT_STATUS = 1;
                    }
                    else if (result == 1)
                    {
                        pushWords.WORD_CURRENT_STATUS = 0;
                    }
                    else if (result == 2)
                    {
                        pushWords.WORD_CURRENT_STATUS = 3;
                        string word_pron, word_save_name;
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
                        bool ret = Download.DownloadMp3.PlayMp3(words);
                        if (ret == false)
                        {
                            SpeechSynthesizer synth = new SpeechSynthesizer();
                            synth.SpeakAsync(CurrentWord.headWord);
                        }
                    }
                }
                if (pushWords.WORD_CURRENT_STATUS == 1)
                {
                    if (ImportFlag == false)
                    {
                        Query.UpdateWord(CurrentWord.wordRank);
                        Query.UpdateCount();
                    }
                    CopyList.Remove(CurrentWord);
                }
                else if (pushWords.WORD_CURRENT_STATUS == 0)
                {
                    if (CopyList.Count == 2)
                    {
                        CopyList.Remove(CurrentWord);
                        CopyList.Add(CurrentWord);
                    }
                    else if (CopyList.Count >= 3)
                    {
                        CopyList.Remove(CurrentWord);
                        Random Rd = new Random();
                        int Index = Rd.Next(CopyList.Count - 1);
                        CopyList.Insert(Index + 1, CurrentWord);
                    }

                }
            }
            Debug.WriteLine($"背完了！接下来开始测验！@{DateTime.Now}");
            pushWords.PushMessage("背完了！接下来开始测验！");
            Thread.Sleep(3000);

            /* 背诵结束 */
            Debug.WriteLine($"开始做题 @{DateTime.Now}");
            Query.SelectWordList();
            pushWords.PushWaitAllQuestions(RandomList, (List<Word>)Query.AllWordList);

            Debug.WriteLine($"结束了！恭喜！ @{DateTime.Now}");

            ToastNotificationManagerCompat.History.Clear();
            pushWords.PushMessage("结束了！恭喜！");
        }

        public void UnorderWord(Object Num)
        {
            int Number = (int)Num;
            Select Query = new Select();
            Query.SelectWordList();
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
        public void PushMessage(string Message, string Buttom = "")
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
        public void PushOneWord(Word CurrentWord)
        {
            ToastNotificationManagerCompat.History.Clear();
            string Phoneme;
            switch (Select.ENG_TYPE)
            {
                case 1:
                    Phoneme = CurrentWord.usPhone;
                    break;
                default:
                    Phoneme = CurrentWord.ukPhone;
                    break;
            }
            string WordPhonePosTran = CurrentWord.headWord + "  (" + Phoneme + ")\n" + CurrentWord.pos + ". " + CurrentWord.tranCN;
            string SentenceTran = "";
            if (CurrentWord.sentence != null && CurrentWord.sentence.Length < 50)
            {
                SentenceTran = CurrentWord.sentence + '\n' + CurrentWord.sentenceCN;
            }
            else if (CurrentWord.phrase != null)
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

        public void PushOneWordSM2(Word CurrentWord, Cardstatus cardstatus, int numNewCards, int numLearingCards, int numReviewedCards)
        {
            ToastNotificationManagerCompat.History.Clear();
            string Phoneme;
            switch (Select.ENG_TYPE)
            {
                case 1:
                    Phoneme = CurrentWord.usPhone;
                    break;
                default:
                    Phoneme = CurrentWord.ukPhone;
                    break;
            }
            string WordPhonePosTran = CurrentWord.headWord + "  (" + Phoneme + ")\n" + CurrentWord.pos + ". " + CurrentWord.tranCN;
            string SentenceTran = "";
            if (CurrentWord.sentence != null && CurrentWord.sentence.Length < 50)
            {
                SentenceTran = CurrentWord.sentence + '\n' + CurrentWord.sentenceCN;
            }
            else if (CurrentWord.phrase != null)
            {
                SentenceTran = CurrentWord.phrase + '\n' + CurrentWord.phraseCN;
            }
            string HeadTile;
            if (cardstatus == Cardstatus.Reviewed)
                HeadTile = "状态：复习 " + " 新:" + numNewCards + " 背:" + numLearingCards + " 复:" + numReviewedCards;
            else if (cardstatus == Cardstatus.New)
                HeadTile = "状态：新开 " + " 新:" + numNewCards + " 背:" + numLearingCards + " 复:" + numReviewedCards;
            else if (cardstatus == Cardstatus.Step1 || cardstatus == Cardstatus.Step2)
                HeadTile = "状态：新学-阶段" + (int)cardstatus + " 新:" + numNewCards + " 背:" + numLearingCards + " 复:" + numReviewedCards;
            else
                HeadTile = "状态：重学-阶段" + ((int)cardstatus - (int)Cardstatus.Step2) + " 新:" + numNewCards + " 背:" + numLearingCards + " 复:" + numReviewedCards;


            new ToastContentBuilder()
            .AddText(WordPhonePosTran)
            .AddText(SentenceTran)
            .AddText(HeadTile)

            .AddButton(new ToastButton()
                .SetContent("没有印象")
                .AddArgument("action", "again")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("记忆模糊")
                .AddArgument("action", "hard")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("暂时记住")
                .AddArgument("action", "good")
                .SetBackgroundActivation())

            .AddButton(new ToastButton()
                .SetContent("已经牢记")
                .AddArgument("action", "easy")
                .SetBackgroundActivation())

            /*  .AddButton(new ToastButton()
                  .SetContent("发音")
                  .AddArgument("action", "voice")
                  .SetBackgroundActivation())*/

            .Show();
        }

        /// <summary>
        /// 推送翻译和填空选择题/
        /// </summary>
        public void PushWaitAllQuestions(List<Word> RandomList, List<Word> AllWordList)
        {
            /* 背诵结束 */
            //中译英
            List<Word> CopyList = Clone<Word>(RandomList);
            Word CurrentWord;
            CopyList.RemoveAll(word =>
            {
                bool result = false;
                if (word.question != null && word.question != "")
                    result = true;
                return result;
            });
            Debug.WriteLine($"开始翻译选择 @{DateTime.Now}");
            while (CopyList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                Thread.Sleep(500);
                CurrentWord = CopyList[0];
                List<Word> rndWords;
                if (RandomList.Count >= 10)
                {
                    rndWords = GetRandomWordLst(CurrentWord, RandomList, 2);
                }
                {
                    rndWords = GetRandomWordLst(CurrentWord, AllWordList, 2);
                }

                bool result = PushWaitTransQuestion(CurrentWord, rndWords[0].headWord, rndWords[1].headWord);
                if (result)
                {
                    CopyList.RemoveAt(0);
                    Thread.Sleep(500);
                }
                else
                {
                    //CopyList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误 正确答案：" + AnswerDict[QUESTION_CURRENT_RIGHT_ANSWER.ToString()] + '.' + CurrentWord.headWord)
                    .Show();
                    CopyList.RemoveAt(0);
                    CopyList.Add(CurrentWord);
                    Thread.Sleep(5000);
                }
            }
            //填空题
            CopyList = Clone<Word>(RandomList);
            CopyList.RemoveAll(word =>
            {
                bool result = false;
                if (word.question == null || word.question == "")
                    result = true;
                return result;
            });
            Debug.WriteLine($"开始填空 @{DateTime.Now}");
            while (CopyList.Count != 0)
            {
                ToastNotificationManagerCompat.History.Clear();
                //CurrentWord = GetRandomWord(CopyList);
                CurrentWord = CopyList[0];
                QUESTION_CURRENT_RIGHT_ANSWER = int.Parse(CurrentWord.rightIndex) - 1;
                PushOneQuestion(CurrentWord);
                bool isFinished = PushWaitFillQuestion(CurrentWord);

                if (isFinished)
                {
                    CopyList.RemoveAt(0);
                    // CopyList.Remove(CurrentWord);
                    //Thread.Sleep(500);
                }
                else
                {
                    //RandomList.Remove(CurrentWord);
                    new ToastContentBuilder()
                    .AddText("错误, 正确答案：" + AnswerDict[QUESTION_CURRENT_RIGHT_ANSWER.ToString()])
                    .AddText(CurrentWord.explain)
                    .Show();
                    Thread.Sleep(6000);
                    CopyList.RemoveAt(0);
                    CopyList.Add(CurrentWord);
                }
            }
            ToastNotificationManagerCompat.History.Clear();

        }

        /// <summary>
        /// 推送一道选择题
        /// </summary>
        public bool PushWaitFillQuestion(Word CurrentWord)
        {
            bool isFinished = false;
            int rst = -1;
            PushOneQuestion(CurrentWord);
            try
            {
                var task = ProcessToastNotificationQuestion();
                rst = task.Result;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            if (rst == 1)
                isFinished = true;
            return isFinished;
        }

        public void PushOneQuestion(Word CurrentWord)
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
        public bool PushWaitTransQuestion(Word CurrentWord, string headWord1, string headWord2)
        {
            bool isFinshed = false;
            int rst = -1;
            PushOneTransQuestion(CurrentWord, headWord1, headWord2);

            try
            {
                var task = ProcessToastNotificationQuestion();
                rst = task.Result;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
            }
            if (rst == 1)
                isFinshed = true;
            return isFinshed;
        }

        public void PushOneTransQuestion(Word CurrentWord, string B, string C)
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
        public List<Word> Clone<Word>(List<Word> RealObject)
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
