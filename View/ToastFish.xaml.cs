using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using ToastFish.PushControl;
using ToastFish.ViewModel;
using ToastFish.Resources;
using System.Windows.Forms;
using ToastFish.Model.SqliteControl;
using System.Threading;
using ToastFish.Model.Mp3;
using System.Diagnostics;

namespace ToastFish
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        ToastFishModel vm = new ToastFishModel();
        Select se = new Select();
        Thread thread = new Thread(new ParameterizedThreadStart(PushWords.Recitation));
        public MainWindow()
        {

            Form1_Load();
            InitializeComponent();
            this.DataContext = vm;
            SetNotifyIcon();
            this.Closing += Window_Closing;
            ContextMenu();

            this.WindowState = (WindowState)FormWindowState.Minimized;
        }

        private void Form1_Load()

        {
            //获取当前活动进程的模块名称
            string moduleName = Process.GetCurrentProcess().MainModule.ModuleName;
            //返回指定路径字符串的文件名
            string processName = System.IO.Path.GetFileNameWithoutExtension(moduleName);
            //根据文件名创建进程资源数组
            Process[] processes = Process.GetProcessesByName(processName);
            //如果该数组长度大于1，说明多次运行
            if (processes.Length > 1)
            {
                System.Windows.Forms.MessageBox.Show("程序已经在运行了，不能运行两次。\n如果右下角软件已经退出，请在任务管理器中结束ToastFish任务。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);//弹出提示信息
                this.Close();//关闭当前窗体
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            e.Cancel = true;
            if (this.WindowState != WindowState.Minimized)
            {
                this.WindowState = WindowState.Minimized;
                this.ShowInTaskbar = false;
            }
        }

        private void SetNotifyIcon()
        {
            vm.notifyIcon = new System.Windows.Forms.NotifyIcon();
            vm.notifyIcon.Text = "ToastFish";
            System.Drawing.Icon icon = IconChika.chika16;

            vm.notifyIcon.Icon = icon;
            vm.notifyIcon.Visible = true;
            vm.notifyIcon.DoubleClick += NotifyIconDoubleClick;
        }
        private void NotifyIconDoubleClick(object sender, EventArgs e)
        {
            this.Activate();
            this.WindowState = WindowState.Normal;
            this.ShowInTaskbar = true;
            this.Topmost = true;
            this.Show();

            //MUSIC temp = new MUSIC();
            //temp.FileName = @"D:\WPF_Project\ToastFish\Resources\Goin\a.mp3";
            //temp.play();
        }

        #region 托盘右键菜单
        System.Windows.Forms.ToolStripMenuItem Begin = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem SetNumber = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem SelectBook = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem GotoHtml = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem ExitMenuItem = new System.Windows.Forms.ToolStripMenuItem();

        private new void ContextMenu()
        {
            ContextMenuStrip Cms = new ContextMenuStrip();

            vm.notifyIcon.ContextMenuStrip = Cms;


            Begin.Text = "开始！";
            Begin.Click += new EventHandler(Begin_Click);

            SetNumber.Text = "设置单词个数";
            SetNumber.Click += new EventHandler(SetNumber_Click);

            SelectBook.Text = "选择词汇";

            GotoHtml.Text = "使用说明";
            GotoHtml.Click += new EventHandler(HowToUse_Click);

            ExitMenuItem.Text = "退出";
            ExitMenuItem.Click += new EventHandler(ExitApp_Click);

            ToolStripItem CET4_1 = new ToolStripMenuItem("四级核心词汇");
            CET4_1.Click += new EventHandler(CET4_1_Click);
            ToolStripItem CET4_3 = new ToolStripMenuItem("四级完整词汇");
            CET4_3.Click += new EventHandler(CET4_3_Click);
            ToolStripItem CET6_1 = new ToolStripMenuItem("六级核心词汇");
            CET6_1.Click += new EventHandler(CET6_1_Click);
            ToolStripItem CET6_3 = new ToolStripMenuItem("六级完整词汇");
            CET6_3.Click += new EventHandler(CET6_3_Click);
            ToolStripItem GMAT_3 = new ToolStripMenuItem("GMAT词汇");
            GMAT_3.Click += new EventHandler(GMAT_3_Click);
            ToolStripItem GRE_2 = new ToolStripMenuItem("GRE词汇");
            GRE_2.Click += new EventHandler(GRE_2_Click);
            ToolStripItem IELTS_3 = new ToolStripMenuItem("IELTS词汇");
            IELTS_3.Click += new EventHandler(IELTS_3_Click);
            ToolStripItem TOEFL_2 = new ToolStripMenuItem("TOEFL词汇");
            TOEFL_2.Click += new EventHandler(TOEFL_2_Click);
            ToolStripItem SAT_2 = new ToolStripMenuItem("SAT词汇");
            SAT_2.Click += new EventHandler(SAT_2_Click);
            ToolStripItem KaoYan_1 = new ToolStripMenuItem("考研必考词汇");
            KaoYan_1.Click += new EventHandler(KaoYan_1_Click);
            ToolStripItem KaoYan_2 = new ToolStripMenuItem("考研完整词汇");
            KaoYan_2.Click += new EventHandler(KaoYan_2_Click);
            ToolStripItem Level4_1 = new ToolStripMenuItem("专四真题高频词");
            Level4_1.Click += new EventHandler(Level4_1_Click);
            ToolStripItem Level4luan_2 = new ToolStripMenuItem("专四核心词汇");
            Level4luan_2.Click += new EventHandler(Level4luan_2_Click);
            ToolStripItem Level8_1 = new ToolStripMenuItem("专八真题高频词");
            Level8_1.Click += new EventHandler(Level8_1_Click);
            ToolStripItem Level8luan_2 = new ToolStripMenuItem("专八核心词汇");
            Level8luan_2.Click += new EventHandler(Level8luan_2_Click);
            ToolStripItem Pdf = new ToolStripMenuItem("支持开发者");
            Pdf.Click += new EventHandler(OpenPdf_Click);
            ToolStripItem Use = new ToolStripMenuItem("使用说明");
            Use.Click += new EventHandler(HowToUse_Click);
            CET4_1.PerformClick();

            Cms.Items.Add(Begin);
            Cms.Items.Add(SetNumber);
            Cms.Items.Add(SelectBook);
            Cms.Items.Add(GotoHtml);
            Cms.Items.Add(ExitMenuItem);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(CET4_1);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(CET4_3);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(CET6_1);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(CET6_3);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(GMAT_3);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(GRE_2);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(IELTS_3);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(TOEFL_2);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(SAT_2);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(KaoYan_1);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(KaoYan_2);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(Level4_1);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(Level4luan_2);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(Level8_1);
            ((ToolStripDropDownItem)Cms.Items[2]).DropDownItems.Add(Level8luan_2);
            ((ToolStripDropDownItem)Cms.Items[3]).DropDownItems.Add(Use);
            ((ToolStripDropDownItem)Cms.Items[3]).DropDownItems.Add(Pdf);
        }


        private void Begin_Click(object sender, EventArgs e)
        {
            var state = thread.ThreadState;
            if(state == System.Threading.ThreadState.WaitSleepJoin || state == System.Threading.ThreadState.Stopped)
            {
                thread.Abort();
                while (thread.ThreadState != System.Threading.ThreadState.Aborted)
                {
                    Thread.Sleep(100);
                }
                thread = new Thread(new ParameterizedThreadStart(PushWords.Recitation));
                thread.Start(PushWords.WORD_NUMBER);
            }
            else
            {
                thread.Start(PushWords.WORD_NUMBER);
            }
        }

        private void SetNumber_Click(object sender, EventArgs e)
        {
            Thread thread = new Thread(new ThreadStart(PushWords.SetWordNumber));
            thread.Start();
        }

        private void Level8luan_2_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level8luan_2";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：专八核心词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void Level8_1_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level8_1";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：专八真题高频词\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void Level4luan_2_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level4luan_2";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：专四核心词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void Level4_1_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level4_1";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：专四真题高频词\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void KaoYan_2_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "KaoYan_2";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：考研完整词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void KaoYan_1_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "KaoYan_1";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：考研必考词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void SAT_2_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "SAT_2";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：SAT词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void TOEFL_2_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "TOEFL_2";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：TOEFL词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void IELTS_3_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "IELTS_3";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：IELTS词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void GRE_2_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "GRE_2";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：GRE词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void GMAT_3_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "GMAT_3";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：GMAT词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void CET6_3_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET6_3";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：六级完整词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void CET6_1_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET6_1";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：六级核心词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void CET4_3_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET4_3";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：四级完整词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void CET4_1_Click(object sender, EventArgs e)
        {
            //(sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET4_1";
            List<int> res = se.SelectCount();
            PushWords.PushMessage("当前词库：四级核心词汇\n当前进度：" + res[0].ToString() + "/" + res[1].ToString());
        }

        private void HowToUse_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://lab.magiconch.com/toast-fish/");
        }
        private void OpenPdf_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(".\\Resources\\Star.pdf");
        }
        private void ExitApp_Click(object sender, EventArgs e)
        {
            ToastNotificationManagerCompat.History.Clear();
            vm.notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }
        #endregion

    }
}

