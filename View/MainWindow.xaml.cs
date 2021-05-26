using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ToastFish;
using ToastFish.PushControl;
using ToastFish.ViewModel;
using Windows.Foundation.Collections;
using ToastFish.Resources;
using System.Windows.Forms;
using ToastFish.PushControl;
using ToastFish.Model.SqliteControl;

namespace ToastFish
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        MainViewModel vm = new MainViewModel();
        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = vm;
            SetNotifyIcon();
            this.Closing += Window_Closing;
            ContextMenu();

            this.WindowState = (WindowState)FormWindowState.Minimized;
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
        }

        #region 托盘右键菜单
        System.Windows.Forms.ToolStripMenuItem exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem SelectBook = new System.Windows.Forms.ToolStripMenuItem();
        System.Windows.Forms.ToolStripMenuItem Begin = new System.Windows.Forms.ToolStripMenuItem();

        private new void ContextMenu()
        {
            ContextMenuStrip Cms = new ContextMenuStrip();

            vm.notifyIcon.ContextMenuStrip = Cms;


            Begin.Text = "开始！";
            Begin.Click += new EventHandler(Begin_Click);

            SelectBook.Text = "选择词汇";
            
            exitMenuItem.Text = "退出";
            exitMenuItem.Click += new EventHandler(ExitApp_Click);

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
            ToolStripItem Level4_2 = new ToolStripMenuItem("专四核心词汇");
            Level4_2.Click += new EventHandler(Level4_2_Click);
            ToolStripItem Level8_1 = new ToolStripMenuItem("专八真题高频词");
            Level8_1.Click += new EventHandler(Level8_1_Click);
            ToolStripItem Level8luan_2 = new ToolStripMenuItem("专八核心词汇");
            Level8luan_2.Click += new EventHandler(Level8luan_2_Click);
            CET4_1.PerformClick();

            //Cms.Items.Add(new ToolStripSeparator());
            Cms.Items.Add(Begin);
            Cms.Items.Add(SelectBook);
            Cms.Items.Add(exitMenuItem);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(CET4_1);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(CET4_3);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(CET6_1);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(CET6_3);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(GMAT_3);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(GRE_2);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(IELTS_3);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(TOEFL_2);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(SAT_2);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(KaoYan_1);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(KaoYan_2);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(Level4_1);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(Level4_2);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(Level8_1);
            ((ToolStripDropDownItem)Cms.Items[1]).DropDownItems.Add(Level8luan_2);
        }

        private void Begin_Click(object sender, EventArgs e)
        {
            PushWords.Recitation(10);
        }

        private void Level8luan_2_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level8luan_2";
            PushWords.PushMessage("当前词库：专八核心词汇");
        }

        private void Level8_1_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level8_1";
            PushWords.PushMessage("当前词库：专八真题高频词");
        }

        private void Level4_2_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level4_2";
            PushWords.PushMessage("当前词库：专四核心词汇");
        }

        private void Level4_1_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "Level4_1";
            PushWords.PushMessage("当前词库：专四真题高频词");
        }

        private void KaoYan_2_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "KaoYan_2";
            PushWords.PushMessage("当前词库：考研完整词汇");
        }

        private void KaoYan_1_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "KaoYan_1";
            PushWords.PushMessage("当前词库：考研必考词汇");
        }

        private void SAT_2_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "SAT_2";
            PushWords.PushMessage("当前词库：SAT词汇");
        }

        private void TOEFL_2_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "TOEFL_2";
            PushWords.PushMessage("当前词库：TOEFL词汇");
        }

        private void IELTS_3_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "IELTS_3";
            PushWords.PushMessage("当前词库：IELTS词汇");
        }

        private void GRE_2_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "GRE_2";
            PushWords.PushMessage("当前词库：GRE词汇");
        }

        private void GMAT_3_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "GMAT_3";
            PushWords.PushMessage("当前词库：GMAT词汇");
        }

        private void CET6_3_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET6_3";
            PushWords.PushMessage("当前词库：六级完整词汇");
        }

        private void CET6_1_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET6_1";
            PushWords.PushMessage("当前词库：六级核心词汇");
        }

        private void CET4_3_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET4_3";
            PushWords.PushMessage("当前词库：四级完整词汇");
        }

        private void CET4_1_Click(object sender, EventArgs e)
        {
            (sender as ToolStripMenuItem).Checked = !(sender as ToolStripMenuItem).Checked;
            Select.TableName = "CET4_1";
            PushWords.PushMessage("当前词库：四级核心词汇");
        }
        private void ExitApp_Click(object sender, EventArgs e)
        {
            vm.notifyIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        }
        #endregion

    }
}

