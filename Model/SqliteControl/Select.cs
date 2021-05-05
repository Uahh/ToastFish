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
            DataBase = connectToDatabase();
        }

        SQLiteConnection DataBase;
        IEnumerable<Word> WordList;

        SQLiteConnection connectToDatabase()
        {
            var databasePath = @"Data Source=D:\WPF_Project\ToastFish\Model\SqliteControl\inami.db;Version=3";
            return new SQLiteConnection(databasePath);
        }

        public void SelectWordList()
        {
            Word temp = new Word();
            WordList = DataBase.Query<Word>("select * from CET4_1", temp);
        }

        public List<Word> GetRandomWordList(int Number)
        {
            List<Word> Result = new List<Word>();
            SelectWordList();
            var WordArray = WordList.ToArray();
            List<int> RandomList = new List<int>();
            foreach(var Word in WordList)
            {
                if(Word.status == 0)
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
    }

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
