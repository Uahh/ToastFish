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
        }

        SQLiteConnection DataBase;
        IEnumerable<Word> WordList;

        /// <summary>
        /// 连接数据库
        /// </summary>
        SQLiteConnection ConnectToDatabase()
        {
            var databasePath = @"Data Source=D:\WPF_Project\ToastFish\Model\SqliteControl\inami.db;Version=3";
            return new SQLiteConnection(databasePath);
        }

        /// <summary>
        /// 查找某本书的所有单词
        /// </summary>
        public void SelectWordList()
        {
            Word temp = new Word();
            WordList = DataBase.Query<Word>("select * from CET4_1", temp);
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
        public String rightInde { get; set; }
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
}
