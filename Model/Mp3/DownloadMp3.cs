using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ToastFish.Model.Mp3;

namespace ToastFish.Model.Download
{
    class DownloadMp3
    {
        /// <summary>
        /// http下载文件
        /// </summary>
        /// <param name="url">下载文件地址</param>
        /// <param name="path">文件存放地址，包含文件名</param>
        /// <returns></returns>
        public bool HttpDownload(string Url, string Name)
        {
            string CachePath = System.IO.Directory.GetCurrentDirectory() + @"\Mp3Cache";
            if (!System.IO.Directory.Exists(CachePath))
            {
                System.IO.Directory.CreateDirectory(CachePath);  //创建临时文件目录
            }
            string CacheFail = CachePath + @"\" + Name + ".mp3"; 
            try
            {
                if (System.IO.File.Exists(CacheFail))
                {
                    System.IO.File.Move(CacheFail, CachePath);
                }
                
                // 设置参数
                HttpWebRequest request = WebRequest.Create(Url) as HttpWebRequest;
                //发送请求并获取相应回应数据
                HttpWebResponse response = request.GetResponse() as HttpWebResponse;
                //直到request.GetResponse()程序才开始向目标网页发送Post请求
                Stream responseStream = response.GetResponseStream();
                //创建本地文件写入流
                //Stream stream = new FileStream(tempFile, FileMode.Create);
                byte[] bArr = new byte[1024];
                MemoryStream ms = new MemoryStream();
                StreamWriter writer = new StreamWriter(ms);
                int size = responseStream.Read(bArr, 0, (int)bArr.Length);
                while (size > 0)
                {
                    //stream.Write(bArr, 0, size);
                    //fs.Write(bArr, 0, size);
                    ms.Write(bArr, 0, size);
                    ms.Flush();
                    size = responseStream.Read(bArr, 0, (int)bArr.Length);
                }
                if (ms.Length > 0)
                {
                    FileStream fs = new FileStream(CacheFail, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    fs.Write(ms.ToArray(), 0, (int) ms.Length);
                    fs.Close();
                }
                //stream.Close();

                responseStream.Close();
                //System.IO.File.Move(tempFile, path);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HttpDownload() Error Message:{ex.Message}");
                return false;
            }
        }

        public static bool PlayMp3(object Speech)
        {
            bool ret = true;
            List<string> TempSpeech = (List<string>)Speech;
            string mp3_full_name = System.IO.Directory.GetCurrentDirectory() + @"\Mp3Cache\" + TempSpeech[0] + ".mp3";
            if (File.Exists(mp3_full_name)){
                MUSIC MIC = new MUSIC();
                MIC.FileName = mp3_full_name;
                MIC.play();
                return ret;
            }
            DownloadMp3 Download = new DownloadMp3();
            bool flag = Download.HttpDownload("https://dict.youdao.com/dictvoice?audio=" + TempSpeech[1], TempSpeech[0]);
            if (flag == true)
            {
                MUSIC MIC = new MUSIC();
                MIC.FileName = mp3_full_name;
                MIC.play();
            }
            else { ret = false; };
            return ret;
        }
    } 
}
