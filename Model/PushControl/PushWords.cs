using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;
using ToastFish.Model.SqliteControl;

namespace ToastFish.PushControl
{
    class PushWords
    {
        public static void Recitation(int number)
        {
            Select Query = new Select();
            List<Word> Result = Query.GetRandomWordList(number);

            //foreach(var Word in Result)
            //{

            //}

            // 基本单词
            new ToastContentBuilder()
                .AddText("cancel  ('kænsl')\nvt.取消，撤销；删去")
                .AddText("They are called ‘Supertrams’, the implication being that (= which is meant to suggest that ) they are more advanced than earlier models.\n它们被称作“超级电车”，这是暗指它们比先前的型号更先进。")
                .Show();

            ////选择题
            //new ToastContentBuilder()
            //.AddText("question")
            //.AddText("As we can no longer wait for the delivery of our order, we have to _______ it.", hintMaxLines: 2)
            //.AddButton(new ToastButton()
            //    .SetContent("A.postpone")
            //    .AddArgument("action", "reply")
            //    .SetBackgroundActivation())

            //.AddButton(new ToastButton()
            //    .SetContent("B.refuse")
            //    .AddArgument("action", "like")
            //    .SetBackgroundActivation())

            //.AddButton(new ToastButton()
            //    .SetContent("C.delay")
            //    .AddArgument("delay", "like")
            //    .SetBackgroundActivation())

            //.AddButton(new ToastButton()
            //    .SetContent("D.cancel1234567890")
            //    .AddArgument("action", "like")
            //    .SetBackgroundActivation())
            //.Show();
        }
    }
}
