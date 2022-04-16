using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

namespace ToastFish.Model.Mp3
{
    /// <summary>
    /// MUSIC 的摘要说明。
    /// </summary>
    public class MUSIC
    {
        public MUSIC()
        {
        }
        //定义API函数使用的字符串变量 
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        private string Name = "";
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        private string durLength = "";
        [MarshalAs(UnmanagedType.LPTStr, SizeConst = 128)]
        private string TemStr = "";
        int ilong;
        //定义播放状态枚举变量
        public enum State
        {
            mPlaying = 1,
            mPuase = 2,
            mStop = 3
        };
        //结构变量
        public struct structMCI
        {
            public bool bMut;
            public int iDur;
            public int iPos;
            public int iVol;
            public int iBal;
            public string iName;
            public State state;
        };
        public structMCI mc = new structMCI();
        //取得播放文件属性
        public string FileName
        {
            get
            {
                return mc.iName;
            }
            set
            {
                try
                {
                    TemStr = "";
                    TemStr = TemStr.PadLeft(127, Convert.ToChar(" "));
                    Name = Name.PadLeft(260, Convert.ToChar(" "));
                    mc.iName = value;
                    ilong = APIClass.GetShortPathName(mc.iName, Name, Name.Length);
                    Name = GetCurrPath(Name);


                    bool iswave = isFileWave(value);
                    if (iswave)
                    {
                        Name = "open " + Convert.ToChar(34) + Name + Convert.ToChar(34) + " type waveaudio alias media";

                    }
                    else
                    {
                        Name = "open " + Convert.ToChar(34) + Name + Convert.ToChar(34) + " type MPEGVideo alias media";
                    }

                    //Name = "open " + Convert.ToChar(34) + Name + Convert.ToChar(34) + " alias media";
                    ilong = APIClass.mciSendString("close all", TemStr, TemStr.Length, 0);
                    ilong = APIClass.mciSendString(Name, TemStr, TemStr.Length, 0);
                    ilong = APIClass.mciSendString("set media time format milliseconds", TemStr, TemStr.Length, 0);
                    mc.state = State.mStop;
                }
                catch
                {
                }
            }
        }
        //播放
        public void play()
        {
            TemStr = "";
            TemStr = TemStr.PadLeft(127, Convert.ToChar(" "));
            APIClass.mciSendString("play media", TemStr, TemStr.Length, 0);
            //APIClass.mciSendString("play media", null, 0, 0);
            mc.state = State.mPlaying;
        }
        //停止
        public void StopT()
        {
            TemStr = "";
            TemStr = TemStr.PadLeft(128, Convert.ToChar(" "));
            ilong = APIClass.mciSendString("close media", TemStr, 128, 0);
            ilong = APIClass.mciSendString("close all", TemStr, 128, 0);
            mc.state = State.mStop;
        }
        public void Puase()
        {
            TemStr = "";
            TemStr = TemStr.PadLeft(128, Convert.ToChar(" "));
            ilong = APIClass.mciSendString("pause media", TemStr, TemStr.Length, 0);
            mc.state = State.mPuase;
        }
        private string GetCurrPath(string name)
        {
            if (name.Length < 1) return "";
            name = name.Trim();
            name = name.Substring(0, name.Length - 1);
            return name;
        }
        //总时间
        public int Duration
        {
            get
            {
                durLength = "";
                durLength = durLength.PadLeft(128, Convert.ToChar(" "));
                APIClass.mciSendString("status media length", durLength, durLength.Length, 0);
                durLength = durLength.Trim();
                if (durLength == "") return 0;
                return (int)(Convert.ToDouble(durLength) / 1000f);
            }
        }
        //当前时间
        public int CurrentPosition
        {
            get
            {
                durLength = "";
                durLength = durLength.PadLeft(128, Convert.ToChar(" "));
                APIClass.mciSendString("status media position", durLength, durLength.Length, 0);
                mc.iPos = (int)(Convert.ToDouble(durLength) / 1000f);
                return mc.iPos;
            }
        }

        public bool isFileWave(string path)
        {
            path = Uri.UnescapeDataString(path);

            byte[] b = new byte[16];
            bool isSet1 = false;
            bool isSet2 = false;
            bool isWave = false;

            //Read bytes
            try
            {
                FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                //StreamReader sr = new StreamReader(fs, System.Text.Encoding.Default);
                // FileStream fs = new FileStream(path, FileMode.Open);
                //fs.Seek(-128, SeekOrigin.End);
                fs.Read(b, 0, 16);
                //Set flag
                String sFlag = System.Text.Encoding.Default.GetString(b, 0, 4).ToUpper();
                String sType = System.Text.Encoding.Default.GetString(b, 8, 4).ToUpper();
                if (sFlag.CompareTo("RIFF") == 0) isSet1 = true;
                if (sType.CompareTo("WAVE") == 0) isSet2 = true;

                if (isSet1 && isSet2)
                {
                    isWave = true;
                }
                fs.Close();
                fs.Dispose();
            }
            catch (IOException ex)
            {
                System.Windows.MessageBox.Show(ex.Message);
            }

            return isWave;
        }
    }
    public class APIClass
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetShortPathName(
         string lpszLongPath,
         string shortFile,
         int cchBuffer
      );
        [DllImport("winmm.dll", EntryPoint = "mciSendString", CharSet = CharSet.Auto)]
        public static extern int mciSendString(
           string lpstrCommand,
           string lpstrReturnString,
           int uReturnLength,
           int hwndCallback
          );
    }
}
