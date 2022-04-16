using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using Dapper;
using System.Windows;

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
        public SQLiteConnection DataBase;
        public IEnumerable<Word> AllWordList;
        public IEnumerable<JpWord> AllJpWordList;
        public IEnumerable<BookCount> CountList;

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

        /// <summary>
        /// 更新CountTable的单词记录
        /// </summary>
        public void UpdateCount()
        {
            BookCount Temp = new BookCount();
            CountList = DataBase.Query<BookCount>("select * from Count", Temp);
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

        public void GetBookNameAndNumber()
        {
            Global Temp = new Global();
            var GlobalVariable = DataBase.Query<Global>("select * from Global", Temp).ToArray();
            TABLE_NAME = GlobalVariable[0].currentBookName;
            WORD_NUMBER = int.Parse(GlobalVariable[0].currentWordNumber);
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
            CountList = DataBase.Query<BookCount>("select * from Count", Temp);
            var CountArray = CountList.ToArray();
            List<int> Output = new List<int>();
            foreach (var OneCount in CountArray)
            {
                if (OneCount.bookName == TABLE_NAME)
                {
                    Output.Add(OneCount.current);
                    Output.Add(OneCount.number);
                    return Output;
                }
            }
            return Output;
        }
        #endregion

        #region 英语部分
        /// <summary>
        /// 查找某本书的所有单词
        /// </summary>
        public void SelectWordList()
        {
            Word Temp = new Word();
            if (TABLE_NAME.IndexOf("自定义") != -1)
                TABLE_NAME = "CET4_1";
            AllWordList = DataBase.Query<Word>("select * from " + TABLE_NAME, Temp);

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
            SelectWordList();
            //var AllWordArray = AllWordList.ToList();

            //把所有没背过的单词序号都存在WordList里了
            List<Word> WordList = new List<Word>();
            foreach(var Word in AllWordList)
            {
                if(Word.status == 0) //单词是否背过
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
            SelectWordList();
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
                if(CurrentWord.wordRank == Index + 1)
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
