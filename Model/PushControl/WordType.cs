using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToastFish.Model.SqliteControl;

namespace ToastFish.Model.PushControl
{
    class WordType
    {
        public int Number;
        public List<Word> WordList = null;
        public List<JpWord> JpWordList = null;
        public List<CustomizeWord> CustWordList = null;
        public bool GoinWordList = false;
    }
}
