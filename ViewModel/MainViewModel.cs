using GalaSoft.MvvmLight;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using ToastFish.PushControl;
using ToastFish.Model.Download;
using ToastFish.Model.Mp3;
using ToastFish.ViewModel;
using System.Windows.Forms;

namespace ToastFish.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public MainViewModel()      
        {
            Push = new RelayCommand(PushTest);
        }
        public NotifyIcon notifyIcon;
        public ICommand Push { get; set; }

        private void PushTest()
        {
            PushWords.Recitation(10);
        }
    }
}