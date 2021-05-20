using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
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

namespace ToastFish
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}

public class NotifyIconViewModel
{
    /// <summary>
    /// 如果窗口没显示，就显示窗口
    /// </summary>
    public ICommand ShowWindowCommand
    {
        get
        {
            return new DelegateCommand
            {
                CanExecuteFunc = () => Application.Current.MainWindow == null,
                CommandAction = () =>
                {
                    Application.Current.MainWindow = new MainWindow();
                    Application.Current.MainWindow.Show();
                }
            };
        }
    }

    /// <summary>
    /// 隐藏窗口
    /// </summary>
    public ICommand HideWindowCommand
    {
        get
        {
            return new DelegateCommand
            {
                CommandAction = () => Application.Current.MainWindow.Close(),
                CanExecuteFunc = () => Application.Current.MainWindow != null
            };
        }
    }


    /// <summary>
    /// 关闭软件
    /// </summary>
    public ICommand ExitApplicationCommand
    {
        get
        {
            return new DelegateCommand { CommandAction = () => Application.Current.Shutdown() };
        }
    }
}


public class DelegateCommand : ICommand
{
    public Action CommandAction { get; set; }
    public Func<bool> CanExecuteFunc { get; set; }

    public void Execute(object parameter)
    {
        CommandAction();
    }

    public bool CanExecute(object parameter)
    {
        return CanExecuteFunc == null || CanExecuteFunc();
    }

    public event EventHandler CanExecuteChanged
    {
        add { CommandManager.RequerySuggested += value; }
        remove { CommandManager.RequerySuggested -= value; }
    }
}

