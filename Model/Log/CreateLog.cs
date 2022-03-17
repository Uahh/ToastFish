using System;
using System.IO;
using System.Collections.Generic;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using ToastFish.Model.SqliteControl;
using System.Windows;

namespace ToastFish.Model.Log
{
    class CreateLog
    {
        public void OutputExcel(String Path, object ObjList, String Type)
        {
            IWorkbook WorkBook = new XSSFWorkbook();
            ISheet sheet = WorkBook.CreateSheet("Word");
            List<Word> WordList;
            List<JpWord> JpWordList;
            List<GoinWord> GoinWordList;
            List<String> FirstLine;
            IRow row = sheet.CreateRow(0);

            if (Type == "英语")
            {
                WordList = (List<Word>)ObjList;
                FirstLine = new List<String> { "英语", "单词", "翻译", "英音", "美音", "词性", "短语", "短语翻译", "例句", "例句翻译", "问题", "问题翻译", "选项A", "选项B", "选项C", "选项D", "正确答案" };
                for (int i = 0; i < WordList.Count; i++)
                {
                    Word TempWord = WordList[i];
                    row = sheet.CreateRow(i + 1);
                    List<String> WordProperty = new List<String> { TempWord.headWord, TempWord.tranCN, TempWord.ukPhone, TempWord.usPhone, TempWord.pos, TempWord.phrase, TempWord.phraseCN, TempWord.sentence, TempWord.sentenceCN, TempWord.question, TempWord.explain, TempWord.choiceIndexOne, TempWord.choiceIndexTwo, TempWord.choiceIndexThree, TempWord.choiceIndexFour, TempWord.rightIndex };
                    for (int j = 0; j < WordProperty.Count; j++)
                    {
                        ICell cell = row.CreateCell(j + 1);
                        cell.SetCellValue(WordProperty[j]);
                    }
                }
            }
            else if (Type == "日语")
            {
                JpWordList = (List<JpWord>)ObjList;
                FirstLine = new List<String> { "日语", "单词", "翻译", "读音", "假名", "词性" };
                for (int i = 0; i < JpWordList.Count; i++)
                {
                    JpWord TempWord = JpWordList[i];
                    row = sheet.CreateRow(i + 1);
                    List<String> WordProperty = new List<String> { TempWord.headWord, TempWord.tranCN, TempWord.Phone.ToString(), TempWord.hiragana, TempWord.pos};
                    for (int j = 0; j < WordProperty.Count; j++)
                    {
                        ICell cell = row.CreateCell(j + 1);
                        cell.SetCellValue(WordProperty[j]);
                    }
                }
            }
            else if (Type == "五十音")
            {
                GoinWordList = (List<GoinWord>)ObjList;
                FirstLine = new List<String> { "五十音", "罗马音", "平假名", "片假名" };
                for (int i = 0; i < GoinWordList.Count; i++)
                {
                    GoinWord TempWord = GoinWordList[i];
                    row = sheet.CreateRow(i + 1);
                    List<String> WordProperty = new List<String> { TempWord.romaji, TempWord.hiragana, TempWord.katakana };
                    for (int j = 0; j < WordProperty.Count; j++)
                    {
                        ICell cell = row.CreateCell(j + 1);
                        cell.SetCellValue(WordProperty[j]);
                    }
                }
            }
            else
            {
                // 自定义
                FirstLine = new List<String> { "自定义", "第一行", "第二行", "第三行" };
            }
            row = sheet.CreateRow(0);
            for (int i = 0; i < FirstLine.Count; i++)
            {
                ICell cell = row.CreateCell(i);
                cell.SetCellValue(FirstLine[i]);
            }

            // 整理Cell长度
            for (int i = 0; i <= 25; i++) 
                sheet.AutoSizeColumn(i);
            
            // 写入文件
            using (FileStream stream = new FileStream(Path, FileMode.Create, FileAccess.Write))
            {
                WorkBook.Write(stream);
            }
        }
        public object ImportExcel(string path)
        {
            List<Word> WordList = new List<Word>();
            List<JpWord> JpWordList = new List<JpWord>();
            List<CustomizeWord> CustWordList = new List<CustomizeWord>();
            IWorkbook WorkBook;
            try
            {
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                // Try to read WorkBook as XLSX:
                try
                {
                    WorkBook = new XSSFWorkbook(fs);
                }
                catch
                {
                    WorkBook = null;
                }

                // If reading fails, try to read WorkBook as XLS:
                if (WorkBook == null)
                {
                    WorkBook = new HSSFWorkbook(fs);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Excel read error", MessageBoxButton.OK, MessageBoxImage.Error);
                return WordList;
            }

            ISheet Sheet = WorkBook.GetSheetAt(0);
            IRow FirstRow = Sheet.GetRow(0);
            if(FirstRow.GetCell(0).ToString() == "英语")
            {
                int RowCount = Sheet.LastRowNum;
                for (int i = 1; i <= RowCount; i++)
                {
                    IRow Row = Sheet.GetRow(i);
                    Word TempWord = new Word();
                    if (Row == null)
                        continue;
                    TempWord.headWord = Row.GetCell(1).ToString();
                    TempWord.tranCN = Row.GetCell(2).ToString();
                    TempWord.ukPhone = Row.GetCell(3).ToString();
                    TempWord.usPhone = Row.GetCell(4).ToString();
                    TempWord.pos = Row.GetCell(5).ToString();
                    TempWord.phrase = Row.GetCell(6).ToString();
                    TempWord.phraseCN = Row.GetCell(7).ToString();
                    TempWord.sentence = Row.GetCell(8).ToString();
                    TempWord.sentenceCN = Row.GetCell(9).ToString();
                    TempWord.question = Row.GetCell(10).ToString();
                    TempWord.explain = Row.GetCell(11).ToString();
                    TempWord.choiceIndexOne = Row.GetCell(12).ToString();
                    TempWord.choiceIndexTwo = Row.GetCell(13).ToString();
                    TempWord.choiceIndexThree = Row.GetCell(14).ToString();
                    TempWord.choiceIndexFour = Row.GetCell(15).ToString();
                    TempWord.rightIndex = Row.GetCell(16).ToString();
                    WordList.Add(TempWord);
                }
                return WordList;
            }
            else if (FirstRow.GetCell(0).ToString() == "日语")
            {
                int RowCount = Sheet.LastRowNum;
                for (int i = 1; i < RowCount; i++)
                {
                    IRow Row = Sheet.GetRow(i);
                    JpWord TempWord = new JpWord();
                    if (Row == null)
                        continue;
                    TempWord.headWord = Row.GetCell(1).ToString();
                    TempWord.tranCN = Row.GetCell(2).ToString();
                    TempWord.Phone = int.Parse(Row.GetCell(3).ToString());
                    TempWord.hiragana = Row.GetCell(4).ToString();
                    TempWord.pos = Row.GetCell(5).ToString();
                    JpWordList.Add(TempWord);
                }
                return JpWordList;
            }
            else if (FirstRow.GetCell(0).ToString() == "自定义")
            {
                int RowCount = Sheet.LastRowNum;
                int CellCount = FirstRow.LastCellNum;
                for (int i = 1; i < RowCount; i++)
                {
                    IRow Row = Sheet.GetRow(i);
                    CustomizeWord TempWord = new CustomizeWord();
                    if (Row == null)
                        continue;
                    TempWord.firstLine = Row.GetCell(1).ToString();
                    TempWord.secondLine = Row.GetCell(2).ToString();
                    TempWord.thirdLine = Row.GetCell(3).ToString();
                    TempWord.fourthLine = Row.GetCell(4).ToString();
                    CustWordList.Add(TempWord);
                }
                return CustWordList;
            }
            else
            {
                MessageBox.Show("导入失败，Excel格式不正确或Excel文件损坏");
                return null;
            }
        }
    }
}
