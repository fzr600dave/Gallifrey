﻿using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gallifrey.UI.Modern.Flyouts;
using Gallifrey.UI.Modern.Models;
using MahApps.Metro.Controls.Dialogs;

namespace Gallifrey.UI.Modern.MainViews
{
    /// <summary>
    /// Interaction logic for Notices.xaml
    /// </summary>
    public partial class Notices : UserControl
    {
        private MainViewModel ViewModel { get { return (MainViewModel)DataContext; } }
        private Mutex UnexportedMutex;

        public Notices()
        {
            InitializeComponent();
            UnexportedMutex = new Mutex(false);
        }

        private async void UnExportedClick(object sender, MouseButtonEventArgs e)
        {
            UnexportedMutex.WaitOne();
            var timers = ViewModel.Gallifrey.JiraTimerCollection.GetStoppedUnexportedTimers();

            if (timers.Any())
            {
                foreach (var jiraTimer in timers)
                {
                    await ViewModel.OpenFlyout(new Export(ViewModel, jiraTimer.UniqueId, null));

                    var updatedTimer = ViewModel.Gallifrey.JiraTimerCollection.GetTimer(jiraTimer.UniqueId);
                    if (!updatedTimer.FullyExported)
                    {
                        DialogCoordinator.Instance.ShowMessageAsync(ViewModel.DialogContext, "Stopping Bulk Export", "Will Stop Bulk Export As Timer Was Not Fully Exported\n\nWill Select The Cancelled Timer");
                        ViewModel.SetSelectedTimer(jiraTimer.UniqueId);
                        break;
                    }
                }
            }
            else
            {
                DialogCoordinator.Instance.ShowMessageAsync(ViewModel.DialogContext, "Nothing To Export", "No Un-Exported Timers To Bulk Export");
            }

            ViewModel.RefreshModel();
            UnexportedMutex.ReleaseMutex();
        }

        private void InstallUpdate(object sender, MouseButtonEventArgs e)
        {
            if (ViewModel.Gallifrey.VersionControl.UpdateInstalled)
            {
                System.Windows.Forms.Application.Restart();
                Application.Current.Shutdown();
            }
        }

        private void GoToRunningTimer(object sender, MouseButtonEventArgs e)
        {
            ViewModel.SelectRunningTimer();
        }
    }
}