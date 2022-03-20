using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using System.Reflection;

namespace ToastFish.Model.StartWithWindows
{
    class StartWithWindows
    {
        /// <summary>
        /// 为本程序创建一个快捷方式。
        /// 放到“启动”文件夹里即可实现开机启动。
        /// </summary>
        /// <param name="lnkFilePath">快捷方式的完全限定路径。</param>
        public static void CreateShortcut(string lnkFilePath)
        {
            if (File.Exists(lnkFilePath))
            {
                File.Delete(lnkFilePath);
                return;
            }
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            dynamic shell = Activator.CreateInstance(shellType);
            var shortcut = shell.CreateShortcut(lnkFilePath);
            shortcut.TargetPath = Assembly.GetEntryAssembly().Location;
            shortcut.WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
            shortcut.Save();
        }
    }
}
