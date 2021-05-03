using GalaSoft.MvvmLight;
using ToastFish.PushControl;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;

namespace ToastFish.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public MainViewModel()
        {
            Push = new RelayCommand(PushTest);
        }
        public ICommand Push { get; set; }

        private void PushTest()
        {
            RecitationMode recitationMode = new RecitationMode();
            recitationMode.button_Click();
        }
    }
}