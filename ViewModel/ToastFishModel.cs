using GalaSoft.MvvmLight;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using ToastFish.Model.PushControl;
using ToastFish.Model.Download;
using ToastFish.Model.Mp3;
using ToastFish.ViewModel;
using System.Windows.Forms;

namespace ToastFish.ViewModel
{
    public class ToastFishModel : ViewModelBase
    {
        public ToastFishModel()      
        {
            Push = new RelayCommand(PushTest);
        }
        public NotifyIcon notifyIcon;
        public ICommand Push { get; set; }

        private void PushTest()
        {
            //PushWords.Recitation(173);
        }
    }
}
