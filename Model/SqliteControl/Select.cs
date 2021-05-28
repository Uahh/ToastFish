using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SQLite;
using Dapper;


namespace ToastFish.Model.SqliteControl
{
    public class Select
    {
        public Select()
        {
            DataBase = ConnectToDatabase();
            DataBase.Open();
        }

        public static string TableName = "CET4_1";

        SQLiteConnection DataBase;
        IEnumerable<Word> WordList;
        IEnumerable<BookCount> CountList;

        /// <summary>
        /// 连接数据库
        /// </summary>
        SQLiteConnection ConnectToDatabase()
        {
            var databasePath = @"Data Source=./Resources/inami.db;Version=3";
            return new SQLiteConnection(databasePath);
        }

        /// <summary>
        /// 查找某本书的所有单词
        /// </summary>
        public void SelectWordList()
        {
            Word Temp = new Word();
            WordList = DataBase.Query<Word>("select * from " + TableName, Temp);
        }

        /// <summary>
        /// 标记单词已背过
        /// </summary>
        public void UpdateWord(int WordRank)
        {
            SQLiteCommand Update = DataBase.CreateCommand();
            Update.CommandText = "UPDATE " + TableName + " SET status = 1 WHERE wordRank = " + WordRank;
            Update.ExecuteNonQuery();
        }

        public void UpdateCount()
        {
            BookCount Temp = new BookCount();
            CountList = DataBase.Query<BookCount>("select * from Count", Temp);
            var CountArray = CountList.ToArray();
            foreach(var OneCount in CountArray)
            {
                if(OneCount.bookName == TableName)
                {
                    SQLiteCommand Update = DataBase.CreateCommand();
                    Update.CommandText = "UPDATE Count SET current = " + (OneCount.current + 1).ToString() + " WHERE bookName = '" + TableName + "'";
                    Update.ExecuteNonQuery();
                    break;
                }
            }
        }

        public List<int> SelectCount()
        {
            BookCount Temp = new BookCount();
            CountList = DataBase.Query<BookCount>("select * from Count", Temp);
            var CountArray = CountList.ToArray();
            List<int> Output = new List<int>();
            foreach (var OneCount in CountArray)
            {
                if (OneCount.bookName == TableName)
                {
                    Output.Add(OneCount.current);
                    Output.Add(OneCount.number);
                    return Output;
                }
            }
            return Output;
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
            var WordArray = WordList.ToArray();
            List<int> RandomList = new List<int>();
            foreach(var Word in WordList)
            {
                if(Word.status == 0) //单词是否背过
                {
                    RandomList.Add(Word.wordRank);
                }
            }

            Random Rd = new Random();
            for (int i = 0; i < Number; i++)
            {
                int Index = Rd.Next(RandomList.Count);
                Result.Add(WordArray[Index]);
            }

            return Result;
        }

        public Word GetRandomWord(List<Word> WordList)
        {
            Random Rd = new Random();
            int Index = Rd.Next(WordList.Count);
            return WordList[Index];
        }
    }

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

    public class BookCount
    {
        public String bookName { get; set; }
        public int number { get; set; }
        public int current { get; set; }
    }
}
