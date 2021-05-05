using GalaSoft.MvvmLight;
using System.Windows.Input;
using GalaSoft.MvvmLight.CommandWpf;
using ToastFish.PushControl;

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
            PushWords.Recitation(15);
        }
    }
}