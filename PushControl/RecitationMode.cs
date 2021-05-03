using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Toolkit.Uwp.Notifications;

namespace ToastFish.PushControl
{
    class RecitationMode
    {
        public void button_Click()
        {
            // 基本单词
            new ToastContentBuilder()
                .AddText("cancel  ('kænsl')\nvt.取消，撤销；删去", hintMaxLines: 2)
                .AddText("Our flight was cancelled.\n我们的航班取消了。", hintMaxLines: 2)
                //.AddAttributionText("Xsdm")
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
