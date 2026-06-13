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
                FirstLine = new List<String> { "自定义", "第一行", "第二行", "第三行", "第四行" };
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
            IWorkbook WorkBook = null;
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
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
                        fs.Position = 0;
                        WorkBook = new HSSFWorkbook(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Excel read error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }

            ISheet Sheet = WorkBook.GetSheetAt(0);
            if (Sheet == null)
            {
                MessageBox.Show("导入失败，Excel文件没有可读取的工作表");
                return null;
            }

            IRow FirstRow = Sheet.GetRow(0);
            string importType = GetCellValue(FirstRow, 0);
            if(importType == "英语")
            {
                int RowCount = Sheet.LastRowNum;
                for (int i = 1; i <= RowCount; i++)
                {
                    IRow Row = Sheet.GetRow(i);
                    Word TempWord = new Word();
                    if (Row == null)
                        continue;
                    TempWord.headWord = GetCellValue(Row, 1);
                    TempWord.tranCN = GetCellValue(Row, 2);
                    TempWord.ukPhone = GetCellValue(Row, 3);
                    TempWord.usPhone = GetCellValue(Row, 4);
                    TempWord.pos = GetCellValue(Row, 5);
                    TempWord.phrase = GetCellValue(Row, 6);
                    TempWord.phraseCN = GetCellValue(Row, 7);
                    TempWord.sentence = GetCellValue(Row, 8);
                    TempWord.sentenceCN = GetCellValue(Row, 9);
                    TempWord.question = GetCellValue(Row, 10);
                    TempWord.explain = GetCellValue(Row, 11);
                    TempWord.choiceIndexOne = GetCellValue(Row, 12);
                    TempWord.choiceIndexTwo = GetCellValue(Row, 13);
                    TempWord.choiceIndexThree = GetCellValue(Row, 14);
                    TempWord.choiceIndexFour = GetCellValue(Row, 15);
                    TempWord.rightIndex = GetCellValue(Row, 16);
                    if (string.IsNullOrWhiteSpace(TempWord.headWord))
                        continue;
                    WordList.Add(TempWord);
                }
                return WordList;
            }
            else if (importType == "日语")
            {
                int RowCount = Sheet.LastRowNum;
                for (int i = 1; i <= RowCount; i++)
                {
                    IRow Row = Sheet.GetRow(i);
                    JpWord TempWord = new JpWord();
                    if (Row == null)
                        continue;
                    TempWord.headWord = GetCellValue(Row, 1);
                    TempWord.tranCN = GetCellValue(Row, 2);
                    int phone;
                    TempWord.Phone = int.TryParse(GetCellValue(Row, 3), out phone) ? phone : 0;
                    TempWord.hiragana = GetCellValue(Row, 4);
                    TempWord.pos = GetCellValue(Row, 5);
                    if (string.IsNullOrWhiteSpace(TempWord.headWord))
                        continue;
                    JpWordList.Add(TempWord);
                }
                return JpWordList;
            }
            else if (importType == "自定义")
            {
                int RowCount = Sheet.LastRowNum;
                for (int i = 1; i <= RowCount; i++)
                {
                    IRow Row = Sheet.GetRow(i);
                    CustomizeWord TempWord = new CustomizeWord();
                    if (Row == null)
                        continue;
                    TempWord.firstLine = GetCellValue(Row, 1);
                    TempWord.secondLine = GetCellValue(Row, 2);
                    TempWord.thirdLine = GetCellValue(Row, 3);
                    TempWord.fourthLine = GetCellValue(Row, 4);
                    if (string.IsNullOrWhiteSpace(TempWord.firstLine)
                        && string.IsNullOrWhiteSpace(TempWord.secondLine)
                        && string.IsNullOrWhiteSpace(TempWord.thirdLine)
                        && string.IsNullOrWhiteSpace(TempWord.fourthLine))
                    {
                        continue;
                    }
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

        private string GetCellValue(IRow row, int cellIndex)
        {
            if (row == null)
                return string.Empty;

            ICell cell = row.GetCell(cellIndex);
            if (cell == null)
                return string.Empty;

            return cell.ToString().Trim();
        }
    }
}
