using Dapper;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using ToastFish.Model.SM2plus;

namespace ToastFish.Model.SqliteControl
{
    public class Select
    {
        public Select()
        {
            DataBase = ConnectToDatabase();
            DataBase.Open();
        }
 
        public static string TABLE_NAME = "CET4_1";  // 当前书籍名字
        public static int WORD_NUMBER = 10;  // 当前单词数量
        public static int ENG_TYPE = 2;  // 英语类型1：美语，2：英语
        public static int AUTO_PLAY = 1;  // 英语自动发音
        public static int AUTO_LOG  = 1;  // 英语自动发音
        public SQLiteConnection DataBase;
        public IEnumerable<Word> AllWordList;
        public IEnumerable<JpWord> AllJpWordList;
        public IEnumerable<BookCount> CountList;
        List<Card> NewCardLst = new List<Card>();
        List<Card> ReviewedCardLst = new List<Card>();


        #region 更新与链接
        /// <summary>
        /// 连接数据库
        /// </summary>
        SQLiteConnection ConnectToDatabase()
        {
            //var databasePath = @"Data Source=./Resources/inami.db;Version=3";
            //var databasePath = @"Data Source="+System.IO.Directory.GetCurrentDirectory() + @"\Resources\inami.db;Version=3";
            string strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string databasePath = @"Data Source=" + System.IO.Path.GetDirectoryName(strExeFilePath) + @"\Resources\inami.db;Version=3";
            return new SQLiteConnection(databasePath);
        }

        /// <summary>
        /// 标记单词已背过
        /// </summary>
        public void UpdateWord(int WordRank)
        {
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = "UPDATE " + TABLE_NAME + " SET status = 1 WHERE wordRank = " + WordRank;
            Update.ExecuteNonQuery();
        }

        //重置单词记录
        public void ResetTableCount()
        {
            String cmdtext = $"UPDATE  {TABLE_NAME} SET status = 0 ";
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = cmdtext;
            Update.ExecuteNonQuery();
        }

        /// <summary>
        /// 更新CountTable的单词记录
        /// </summary>
        /// 
        public void UpdateTableCount()
        {
            String cmdtext = $"Select status from {TABLE_NAME}";
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = cmdtext;
            var dr = Update.ExecuteReader();
            List<string> statusLst = new List<string>();
            int Count = 0;
            int value = -1;
            while (dr.Read())//loop through the various columns and their info
            {
                var rawvalue = dr.GetValue(0);//0:cid;1:name; 2:type;3:notnull;4:dflt_value;5:pk

                string type = rawvalue.GetType().Name;
                if (type.Equals("String", StringComparison.OrdinalIgnoreCase))
                    value = int.Parse((string)rawvalue);
                else
                    value = int.Parse(rawvalue.ToString());
                if (value != 0)
                    Count++;
            }
            dr.Close();
            Update.CommandText = "UPDATE Count SET current = " + Count.ToString() + " WHERE bookName = '" + TABLE_NAME + "'";
            Update.ExecuteNonQuery();
        }

        //increase count by 1
        public void UpdateCount()
        {
            BookCount Temp = new BookCount();
            CountList = DataBase.Query<BookCount>($"select * from Count where bookName = {TABLE_NAME}", Temp);
            var CountArray = CountList.ToArray();
            foreach (var OneCount in CountArray)
            {
                if (OneCount.bookName == TABLE_NAME)
                {
                    int Count = OneCount.current + 1;
                    if (OneCount.bookName == "Goin")
                        Count %= 104;
                    SQLiteCommand Update = DataBase.CreateCommand();
                    Update.CommandText = "UPDATE Count SET current = " + Count.ToString() + " WHERE bookName = '" + TABLE_NAME + "'";
                    Update.ExecuteNonQuery();
                    break;
                }
            }
        }

        public void LoadGlobalConfig()
        {
            String cmdtext = $"PRAGMA table_info(Global)";
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = cmdtext;
            var dr = Update.ExecuteReader();
            List<string> HeadTileList = new List<string>();
            while (dr.Read())//loop through the various columns and their info
            {
                string name = (string)dr.GetValue(1);//0:cid;1:name; 2:type;3:notnull;4:dflt_value;5:pk
                HeadTileList.Add(name);
                //Console.WriteLine(name);
            }
            dr.Close();
            if (HeadTileList.Contains("EngType") == false)
            {
                Update.CommandText = $"ALTER TABLE Global ADD COLUMN EngType INTEGER NOT NULL DEFAULT {ENG_TYPE}";
                Update.ExecuteNonQuery();
            }
            if (HeadTileList.Contains("autoLog") == false)
            {
                Update.CommandText = $"ALTER TABLE Global ADD COLUMN autoLog INTEGER NOT NULL DEFAULT {AUTO_LOG}";
                Update.ExecuteNonQuery();
            }
            Global Temp = new Global();
            var GlobalVariable = DataBase.Query<Global>("select * from Global", Temp).ToArray();
            WORD_NUMBER = int.Parse(GlobalVariable[0].currentWordNumber);
            TABLE_NAME = GlobalVariable[0].currentBookName;
            AUTO_PLAY = GlobalVariable[0].autoPlay;
            ENG_TYPE = GlobalVariable[0].EngType;
            AUTO_LOG = GlobalVariable[0].autoLog;
        }

        public void UpdateGlobalConfig()
        {
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = $"UPDATE Global SET currentWordNumber ='{WORD_NUMBER}'" +
                $", currentBookName = '{TABLE_NAME}'" +
                $", autoPlay = '{AUTO_PLAY}'" +
                $", EngType = '{ENG_TYPE}' " +
                $", autoLog = '{AUTO_LOG}'";
            Update.ExecuteNonQuery();
        }

        public void UpdateBookName(string TableName)
        {
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = "UPDATE Global SET currentBookName = '" + TableName + "'";
            Update.ExecuteNonQuery();
            //Global Temp = new Global();
            //var GlobalVariable = DataBase.Query<Global>("select * from Global", Temp).ToArray();
        }

        public void UpdateNumber(int WordNumber)
        {
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = "UPDATE Global SET currentWordNumber = " + WordNumber.ToString();
            Update.ExecuteNonQuery();
        }

        /// <summary>
        /// 查询当前单词表当前进度
        /// </summary>
        public List<int> SelectCount()
        {
            BookCount Temp = new BookCount();
            CountList = DataBase.Query<BookCount>($"select * from Count where bookName = '{TABLE_NAME}'", Temp);
            var CountArray = CountList.ToArray();
            List<int> Output = new List<int>();
            // foreach (var OneCount in CountArray)
            // {
            //if (OneCount.bookName == TABLE_NAME)
            //{
            Output.Add(CountArray[0].current);
            Output.Add(CountArray[0].number);
            return Output;
            // }
            // }
            // return Output;
        }
        #endregion

        #region 英语部分
        /// <summary>
        /// 查找某本书的所有单词
        /// </summary>
        public void SelectWordList()
        {

            if (TABLE_NAME.IndexOf("自定义") != -1)
                TABLE_NAME = "GRE_2";

            //String cmdtext =$"SELECT name FROM PRAGMA_TABLE_INFO('{TABLE_NAME}')";
            String cmdtext = $"PRAGMA table_info({TABLE_NAME})";
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = cmdtext;
            var dr = Update.ExecuteReader();
            List<string> HeadTileList = new List<string>();
            while (dr.Read())//loop through the various columns and their info
            {
                string name = (string)dr.GetValue(1);//0:cid;1:name; 2:type;3:notnull;4:dflt_value;5:pk
                HeadTileList.Add(name);
                //Console.WriteLine(name);
            }
            dr.Close();
            if (HeadTileList.Contains("difficulty") == false)
            {
                Update.CommandText = $"ALTER TABLE {TABLE_NAME} ADD COLUMN difficulty REAL NOT NULL DEFAULT {Parameters.diffcultyDefaultValue}";
                Update.ExecuteNonQuery();
            }
            if (HeadTileList.Contains("daysBetweenReviews") == false)
            {
                Update.CommandText = $"ALTER TABLE {TABLE_NAME} ADD COLUMN daysBetweenReviews  REAL NOT NULL DEFAULT {Parameters.daysBetweenReviewsDefaultValue}";
                Update.ExecuteNonQuery();
            }
            if (HeadTileList.Contains("lastScore") == false)
            {
                Update.CommandText = $"ALTER TABLE {TABLE_NAME} ADD COLUMN lastScore REAL NOT NULL DEFAULT 0";
                Update.ExecuteNonQuery();
            }
            if (HeadTileList.Contains("dateLastReviewed") == false)
            {
                Update.CommandText = $"ALTER TABLE {TABLE_NAME} ADD COLUMN dateLastReviewed TEXT  DEFAULT NULL";
                Update.ExecuteNonQuery();
            }
            Word Temp = new Word();
            AllWordList = DataBase.Query<Word>("select * from " + TABLE_NAME, Temp);

            foreach (var Word in AllWordList)
            {
                Card cardi = new Card(Word);
                if (cardi.status != Cardstatus.Reviewed)
                    NewCardLst.Add(cardi);
                else
                    ReviewedCardLst.Add(cardi);
            }
        }

        public void updateCardDateBase(List<Card> cardList)
        {
            SQLiteCommand Update = DataBase.CreateCommand();

            foreach (var card in cardList)
            {
                /* String Command = $"UPDATE {TABLE_NAME} SET status = {(int)card.status} WHERE wordRank = {card.word.wordRank};";
                 Command += $"\nUPDATE {TABLE_NAME} SET difficulty ={card.difficulty} WHERE wordRank = {card.word.wordRank};";
                 Command += $"\nUPDATE {TABLE_NAME} SET daysBetweenReviews ={card.daysBetweenReviews} WHERE wordRank = {card.word.wordRank};";
                 Command += $"\nUPDATE {TABLE_NAME} SET lastScore ={card.lastScore} WHERE wordRank = {card.word.wordRank};";
                 Command += $"\nUPDATE {TABLE_NAME} SET dateLastReviewed ='{card.dateLastReviewed}' WHERE wordRank = {card.word.wordRank};";*/
                String Command = $"UPDATE {TABLE_NAME} SET status = {(int)card.status}, " +
                    $"difficulty ={card.difficulty}, daysBetweenReviews ={card.daysBetweenReviews}, " +
                    $"lastScore ={card.lastScore}, dateLastReviewed ='{card.dateLastReviewed}' " +
                    $"WHERE wordRank = {card.word.wordRank};";
                Update.CommandText = Command;
                Update.ExecuteNonQuery();
                //card.word
            }

        }

        public void GetOverdueReviewedCardList(int maxReviewedCardNumer, out List<Card> usedReviewedCardLst)
        {
            //List<Card> 
            usedReviewedCardLst = new List<Card>();

            if (ReviewedCardLst.Count() < maxReviewedCardNumer)
                maxReviewedCardNumer = ReviewedCardLst.Count();

            ReviewedCardLst.Sort((b, a) =>
            {
                // compare a to b to get decending order
                int result = a.percentOverdue.CompareTo(b.percentOverdue);
                return result;
            });

            for (int i = 0; i < maxReviewedCardNumer; i++)
            {
                Card card0 = ReviewedCardLst[0];
                usedReviewedCardLst.Add(card0);
                ReviewedCardLst.RemoveAt(0);
            }
        }

        public void GenerateRandomNewCardList(int maxNewCardNumber, out List<Card> usedNewCardLst)
        {
            //SelectWordList();

            //List<Card>
            usedNewCardLst = new List<Card>();

            if (NewCardLst.Count() < maxNewCardNumber)
                maxNewCardNumber = NewCardLst.Count();

            Random Rd = new Random();
            for (int i = 0; i < maxNewCardNumber; i++)
            {
                int Index = Rd.Next(NewCardLst.Count);
                usedNewCardLst.Add(NewCardLst[Index]);
                NewCardLst.RemoveAt(Index);
            }
        }


        /// <summary>
        /// 从词库里随机选择Number个单词
        /// </summary>
        /// <typeparam name="List<Word>"></typeparam>
        /// <param name="Number"></param>
        /// <returns></returns>
        public List<Word> GetRandomWordList(int Number)
        {
            List<Word> Result = new List<Word>();
            //SelectWordList();
            //var AllWordArray = AllWordList.ToList();



            //把所有没背过的单词序号都存在WordList里了
            List<Word> WordList = new List<Word>();
            foreach (var Word in AllWordList)
            {
                if (Word.status == 0) //单词是否背过
                {
                    WordList.Add(Word);
                }
            }

            if (WordList.Count() == 0)
                return Result;
            else if (WordList.Count() < Number)
                Number = WordList.Count();

            Random Rd = new Random();
            for (int i = 0; i < Number; i++)
            {
                int Index = Rd.Next(WordList.Count);//下标
                Result.Add(WordList[Index]);
                WordList.RemoveAt(Index);
            }
            return Result;
        }

        /// <summary>
        /// 获取俩随机单词，作为错误答案
        /// </summary>
        public List<Word> GetRandomWords(int Number)
        {
            List<Word> Result = new List<Word>();
            //SelectWordList();
            var AllWordArray = AllWordList.ToList();

            Random Rd = new Random();
            for (int i = 0; i < Number; i++)
            {
                int Index = Rd.Next(AllWordArray.Count);//下标
                Result.Add(AllWordArray[Index]);
                AllWordArray.RemoveAt(Index);
            }
            return Result;
        }
        #endregion

        #region 日语部分
        /// <summary>
        /// 查找某本书的所有单词
        /// </summary>
        public void SelectJpWordList()
        {
            JpWord Temp = new JpWord();
            AllJpWordList = DataBase.Query<JpWord>("select * from " + TABLE_NAME, Temp);
        }

        /// <summary>
        /// 从词库里随机选择Number个单词
        /// </summary>
        /// <typeparam name="List<Word>"></typeparam>
        /// <param name="Number"></param>
        /// <returns></returns>
        public List<JpWord> GetRandomJpWordList(int Number)
        {
            List<JpWord> Result = new List<JpWord>();
            SelectJpWordList();
            var AllWordArray = AllJpWordList.ToList();

            //把所有没背过的单词序号都存在WordList里了
            List<int> WordList = new List<int>();
            foreach (var JpWord in AllJpWordList)
            {
                if (JpWord.status == 0) //单词是否背过
                {
                    WordList.Add(JpWord.wordRank);
                }
            }

            if (WordList.Count() == 0)
                return Result;
            else if (WordList.Count() < Number)
                Number = WordList.Count();

            Random Rd = new Random();
            for (int i = 0; i < Number; i++)
            {
                int Index = Rd.Next(WordList.Count);//下标
                Result.Add(AllWordArray[Index]);
                AllWordArray.RemoveAt(Index);
            }
            return Result;
        }

        /// <summary>
        /// 获取俩随机单词，作为错误答案
        /// </summary>
        public List<JpWord> GetRandomJpWords(int Number)
        {
            List<JpWord> Result = new List<JpWord>();
            SelectJpWordList();
            var AllWordArray = AllJpWordList.ToList();

            Random Rd = new Random();
            for (int i = 0; i < Number; i++)
            {
                int Index = Rd.Next(AllWordArray.Count);//下标
                Result.Add(AllWordArray[Index]);
                AllWordArray.RemoveAt(Index);
            }
            return Result;
        }
        #endregion

        #region 五十音部分
        public List<GoinWord> GetGainWordList()
        {
            GoinWord Temp = new GoinWord();
            IEnumerable<GoinWord> AllGoinWordList = DataBase.Query<GoinWord>("select * from " + TABLE_NAME, Temp);
            return AllGoinWordList.ToList();
        }

        public int GetGoinProgress()
        {
            BookCount Temp = new BookCount();
            CountList = DataBase.Query<BookCount>("select * from Count where bookName = 'Goin'", Temp);
            var CountArray = CountList.ToList();
            return CountArray[0].current;
        }

        public List<GoinWord> GetTwoGoinRandomWords(GoinWord CurrentWord)
        {
            List<GoinWord> Result = new List<GoinWord>();
            List<GoinWord> WordList = GetGainWordList();

            Random Rd = new Random();
            for (int i = 0; i < 2; i++)
            {
                int Index = Rd.Next(WordList.Count);//下标
                if (CurrentWord.wordRank == Index + 1)
                {
                    i--;
                    continue;
                }
                Result.Add(WordList[Index]);
                WordList.RemoveAt(Index);
            }
            return Result;
        }
        #endregion
    }

    #region 查询类
    [Serializable]
    public class Word
    {
        public int wordRank { get; set; }
        public int status { get; set; }
        public String headWord { get; set; }
        public String usPhone { get; set; }
        public String ukPhone { get; set; }
        public String usSpeech { get; set; }
        public String ukSpeech { get; set; }
        public String tranCN { get; set; }
        public String pos { get; set; }
        public String tranOther { get; set; }
        public String question { get; set; }
        public String explain { get; set; }
        public String rightIndex { get; set; }
        public String examType { get; set; }
        public String choiceIndexOne { get; set; }
        public String choiceIndexTwo { get; set; }
        public String choiceIndexThree { get; set; }
        public String choiceIndexFour { get; set; }
        public String sentence { get; set; }
        public String sentenceCN { get; set; }
        public String phrase { get; set; }
        public String phraseCN { get; set; }
        public double difficulty { get; set; }
        public double daysBetweenReviews { get; set; }
        public double lastScore { get; set; }
        public String dateLastReviewed { get; set; }
    }

    [Serializable]
    public class BookCount
    {
        public String bookName { get; set; }
        public int number { get; set; }
        public int current { get; set; }
    }

    [Serializable]
    public class GoinWord
    {
        public int wordRank { get; set; }
        public string bookId { get; set; }
        public int status { get; set; }
        public string romaji { get; set; }
        public string hiragana { get; set; }
        public string katakana { get; set; }

    }

    [Serializable]
    public class Global
    {
        public string currentWordNumber { get; set; }
        public string currentBookName { get; set; }
        public int autoPlay { get; set; }
        public int EngType { get; set; }
        public int autoLog { get; set; }
    }

    [Serializable]
    public class JpWord
    {
        public int wordRank { get; set; }
        public string bookId { get; set; }
        public int status { get; set; }
        public String headWord { get; set; }
        public int Phone { get; set; }
        public String tranCN { get; set; }
        public String pos { get; set; }
        public String hiragana { get; set; }
    }

    [Serializable]
    public class CustomizeWord
    {
        public String firstLine { get; set; }
        public String secondLine { get; set; }
        public String thirdLine { get; set; }
        public String fourthLine { get; set; }
    }
    #endregion
}
