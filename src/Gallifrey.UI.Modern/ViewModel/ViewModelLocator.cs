using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Castle.Windsor;
using CommonServiceLocator.WindsorAdapter;
using GalaSoft.MvvmLight.Ioc;
using Gallifrey.UI.Modern.Models;
using Microsoft.Practices.ServiceLocation;

namespace Gallifrey.UI.Modern.ViewModel
{
    public class ViewModelLocator
    {
        static ViewModelLocator()
        {
            var container = new WindsorContainer();
            ServiceLocator.SetLocatorProvider(() => new WindsorServiceLocator(container));

            SimpleIoc.Default.Register<MainViewModel>();
        }

        public MainViewModel Main => ServiceLocator.Current.GetInstance<MainViewModel>();

        public static void Cleanup()
        { }
    }
}
