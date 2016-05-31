using System.Windows;
using GalaSoft.MvvmLight.Threading;
using Gallifrey.Versions;

namespace Gallifrey.UI.Modern.Alpha
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherHelper.Initialize();
            Modern.App.Run(InstanceType.Alpha);
        }
    }
}
