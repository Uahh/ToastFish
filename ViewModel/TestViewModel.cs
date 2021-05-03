using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ToastFish.Model;

namespace ToastFish.ViewModel
{
    public class TestViewModel : ViewModelBase
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public TestViewModel()
        {
            Test = new TestModel() { Introduction = "Hello World！" };
        }
        #region 属性

        private TestModel test;
        /// <summary>
        /// 欢迎词属性
        /// </summary>
        public TestModel Test
        {
            get { return test; }
            set { test = value; RaisePropertyChanged(() => Test); }
        }
        #endregion
    }
}
