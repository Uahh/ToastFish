using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using ToastFish.Model.SqliteControl;
using System.Windows;

namespace ToastFish.Model.Log
{
    class CreateLog
    {
        public void OutputExcel(String Path, List<Word> WordList)
        {
            IWorkbook WorkBook = new XSSFWorkbook();
            ISheet sheet = WorkBook.CreateSheet("My sheet");
            List<String> FirstLine = new List<String> { "单词", "翻译", "英音", "美音", "短语", "短语翻译", "例句", "例句翻译", "问题", "问题翻译", "选项A", "选项B", "选项C", "选项D", "正确答案" };
            IRow row = sheet.CreateRow(0);
            for (int i = 0; i < FirstLine.Count; i++)
            {
                ICell cell = row.CreateCell(i);
                cell.SetCellValue(FirstLine[i]);
            }
            for(int i = 0; i < WordList.Count; i++)
            {
                Word TempWord = WordList[i];
                row = sheet.CreateRow(i + 1);
                List<String> WordProperty = new List<String> { TempWord.headWord, TempWord.tranCN, TempWord.ukPhone, TempWord.usPhone, TempWord.phrase, TempWord.phraseCN, TempWord.sentence, TempWord.sentenceCN, TempWord.question, TempWord.explain, TempWord.choiceIndexOne, TempWord.choiceIndexTwo, TempWord.choiceIndexThree, TempWord.choiceIndexFour, TempWord.rightIndex };
                for (int j = 0; j < WordProperty.Count; j++)
                {
                    ICell cell = row.CreateCell(j);
                    cell.SetCellValue(WordProperty[j]);
                }
            }
            for (int i = 0; i <= 25; i++) 
                sheet.AutoSizeColumn(i);
            using (FileStream stream = new FileStream(Path, FileMode.Create, FileAccess.Write))
            {
                WorkBook.Write(stream);
            }
        }
        public void ImportExcel(string path)
        {
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
                return;
            }

            ISheet Sheet = WorkBook.GetSheetAt(0);
            IRow FirstRow = Sheet.GetRow(0);
            int CellCount = FirstRow.LastCellNum;

        }
    }
}
