﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Gallifrey.AppTracking;
using Gallifrey.UI.Modern.Flyouts;
using Gallifrey.UI.Modern.Helpers;
using Gallifrey.UI.Modern.Models;
using MahApps.Metro.Controls.Dialogs;

namespace Gallifrey.UI.Modern.MainViews
{
    public partial class ControlButtons
    {
        private MainViewModel ViewModel => (MainViewModel)DataContext;
        private ModelHelpers ModelHelpers => ((MainViewModel)DataContext).ModelHelpers;

        public ControlButtons()
        {
            InitializeComponent();
        }

        private void ControlButtons_OnLoaded(object sender, RoutedEventArgs e)
        {
            ModelHelpers.RemoteButtonTrigger += ModelHelpersOnRemoteButtonTrigger;
        }

        private async void AddButton(object sender, RoutedEventArgs e)
        {
            var startDate = ViewModel.TimerDates.FirstOrDefault(x => x.DateIsSelected)?.TimerDate ?? DateTime.Today;

            var addTimer = new AddTimer(ModelHelpers, startDate: startDate);
            await ModelHelpers.OpenFlyout(addTimer);
            if (addTimer.AddedTimer)
            {
                ModelHelpers.SetSelectedTimer(addTimer.NewTimerId);
            }
        }

        private async void CopyButton(object sender, RoutedEventArgs e)
        {
            var selectedTimers = ViewModel.GetSelectedTimerIds();

            if (selectedTimers.Count() > 1)
            {
                await DialogCoordinator.Instance.ShowMessageAsync(ModelHelpers.DialogContext, "Too Many Timers Selected", "Please Select Only One Timer When Copying Reference");
            }
            else if (selectedTimers != null && selectedTimers.Count() == 1)
            {
                var selectedTimer = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(selectedTimers.First());
                if (selectedTimer.TempTimer)
                {
                    await DialogCoordinator.Instance.ShowMessageAsync(ModelHelpers.DialogContext, "Not Avaliable", "A Temp Timer Does Not Have A Copyable Reference");
                }
                else
                {
                    Clipboard.SetText(selectedTimer.JiraReference);
                }
            }
        }

        private async void PasteButton(object sender, RoutedEventArgs e)
        {
            var jiraRef = Clipboard.GetText();

            if (ModelHelpers.Gallifrey.JiraConnection.DoesJiraExist(jiraRef))
            {
                var startDate = ViewModel.TimerDates.FirstOrDefault(x => x.DateIsSelected)?.TimerDate ?? DateTime.Today;
                var addTimer = new AddTimer(ModelHelpers, startDate: startDate, jiraRef: jiraRef);
                await ModelHelpers.OpenFlyout(addTimer);
                if (addTimer.AddedTimer)
                {
                    ModelHelpers.SetSelectedTimer(addTimer.NewTimerId);
                }
            }
            else
            {
                await DialogCoordinator.Instance.ShowMessageAsync(ModelHelpers.DialogContext, "Invalid Jira", $"Unable To Locate That Jira.\n\nJira Ref Pasted: '{jiraRef}'");
            }
        }

        private async void DeleteButton(object sender, RoutedEventArgs e)
        {
            var selectedTimerIds = ViewModel.GetSelectedTimerIds().ToList();

            foreach (var selectedTimerId in selectedTimerIds)
            {
                var timer = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(selectedTimerId);

                var result = await DialogCoordinator.Instance.ShowMessageAsync(ModelHelpers.DialogContext, "Are You Sure?", $"Are You Sure You Want To Delete {timer.JiraReference}\n\n{timer.JiraName}\nFor: {timer.DateStarted.Date.ToString("ddd, dd MMM")}", MessageDialogStyle.AffirmativeAndNegative, new MetroDialogSettings { AffirmativeButtonText = "Yes", NegativeButtonText = "No", DefaultButtonFocus = MessageDialogResult.Affirmative });

                if (result == MessageDialogResult.Affirmative)
                {
                    ModelHelpers.Gallifrey.JiraTimerCollection.RemoveTimer(selectedTimerId);
                }
            }

            ModelHelpers.RefreshModel();
        }

        private void SearchButton(object sender, RoutedEventArgs e)
        {
            var startDate = ViewModel.TimerDates.FirstOrDefault(x => x.DateIsSelected)?.TimerDate ?? DateTime.Today;
            ModelHelpers.OpenFlyout(new Search(ModelHelpers, false, startDate));
        }

        private async void EditButton(object sender, RoutedEventArgs e)
        {
            var selectedTimerIds = ViewModel.GetSelectedTimerIds().ToList();

            foreach (var selectedTimerId in selectedTimerIds)
            {
                var stoppedTimer = false;
                var runningTimerId = ModelHelpers.Gallifrey.JiraTimerCollection.GetRunningTimerId();

                if (runningTimerId.HasValue && runningTimerId.Value == selectedTimerId)
                {
                    ModelHelpers.Gallifrey.JiraTimerCollection.StopTimer(selectedTimerId, true);
                    stoppedTimer = true;
                }

                var editTimerFlyout = new EditTimer(ModelHelpers, selectedTimerId);
                await ModelHelpers.OpenFlyout(editTimerFlyout);

                if (stoppedTimer)
                {
                    var timer = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(editTimerFlyout.EditedTimerId);
                    if (timer.DateStarted.Date == DateTime.Now.Date)
                    {
                        ModelHelpers.Gallifrey.JiraTimerCollection.StartTimer(editTimerFlyout.EditedTimerId);
                    }
                }
            }

            ModelHelpers.RefreshModel();
        }

        private async void ExportButton(object sender, RoutedEventArgs e)
        {
            var selectedTimerIds = ViewModel.GetSelectedTimerIds().ToList();
            if (selectedTimerIds.Any())
            {
                if (selectedTimerIds.Count > 1)
                {
                    foreach (var selectedTimer in selectedTimerIds.Select(x => ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(x)).ToList())
                    {
                        if (selectedTimer.IsRunning)
                        {
                            selectedTimerIds.Remove(selectedTimer.UniqueId);
                        }
                        else if (selectedTimer.TempTimer)
                        {
                            selectedTimerIds.Remove(selectedTimer.UniqueId);
                        }
                    }
                }

                if (selectedTimerIds.Count == 1)
                {
                    await ModelHelpers.OpenFlyout(new Export(ModelHelpers, selectedTimerIds.First(), null));
                }
                else
                {
                    var timers = selectedTimerIds.Select(x => ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(x)).ToList();
                    await ModelHelpers.OpenFlyout(new BulkExport(ModelHelpers, timers));
                }
            }
        }

        private void LockTimerButton(object sender, RoutedEventArgs e)
        {
            ModelHelpers.OpenFlyout(new LockedTimer(ModelHelpers));
        }

        private void InfoButton(object sender, RoutedEventArgs e)
        {
            ModelHelpers.OpenFlyout(new Information(ModelHelpers));
        }

        private async void SettingsButton(object sender, RoutedEventArgs e)
        {
            await ModelHelpers.OpenFlyout(new Flyouts.Settings(ModelHelpers));
            if (!ModelHelpers.Gallifrey.JiraConnection.IsConnected)
            {
                await DialogCoordinator.Instance.ShowMessageAsync(ModelHelpers.DialogContext, "Connection Required", "You Must Have A Working Jira Connection To Use Gallifrey");
                ModelHelpers.CloseApp();
            }
            ModelHelpers.RefreshModel();
        }

        private void EmailButton(object sender, RoutedEventArgs e)
        {
            ModelHelpers.Gallifrey.TrackEvent(TrackingType.ContactClick);
            Process.Start(new ProcessStartInfo("mailto:contact@gallifreyapp.co.uk?subject=Gallifrey App Contact"));
        }

        private void TwitterButton(object sender, RoutedEventArgs e)
        {
            ModelHelpers.Gallifrey.TrackEvent(TrackingType.ContactClick);
            Process.Start(new ProcessStartInfo("https://twitter.com/GallifreyApp"));
        }

        private void PayPalButton(object sender, RoutedEventArgs e)
        {
            ModelHelpers.Gallifrey.TrackEvent(TrackingType.PayPalClick);
            Process.Start(new ProcessStartInfo("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=G3MWL8E6UG4RS"));
        }

        private void GitterButton(object sender, RoutedEventArgs e)
        {
            ModelHelpers.Gallifrey.TrackEvent(TrackingType.ContactClick);
            Process.Start(new ProcessStartInfo("https://gitter.im/BlythMeister/Gallifrey"));
        }

        private void GitHubButton(object sender, RoutedEventArgs e)
        {
            ModelHelpers.Gallifrey.TrackEvent(TrackingType.GitHubClick);
            Process.Start(new ProcessStartInfo("https://github.com/BlythMeister/Gallifrey"));
        }

        private void ModelHelpersOnRemoteButtonTrigger(object sender, RemoteButtonTrigger remoteButtonTrigger)
        {
            switch (remoteButtonTrigger)
            {
                case RemoteButtonTrigger.Add: AddButton(this, null); break;
                case RemoteButtonTrigger.Copy: CopyButton(this, null); break;
                case RemoteButtonTrigger.Paste: PasteButton(this, null); break;
                case RemoteButtonTrigger.Delete: DeleteButton(this, null); break;
                case RemoteButtonTrigger.Search: SearchButton(this, null); break;
                case RemoteButtonTrigger.Edit: EditButton(this, null); break;
                case RemoteButtonTrigger.Export: ExportButton(this, null); break;
                case RemoteButtonTrigger.LockTimer: LockTimerButton(this, null); break;
                case RemoteButtonTrigger.Settings: SettingsButton(this, null); break;
                case RemoteButtonTrigger.Info: InfoButton(this, null); break;
                case RemoteButtonTrigger.Twitter: TwitterButton(this, null); break;
                case RemoteButtonTrigger.Email: EmailButton(this, null); break;
                case RemoteButtonTrigger.Gitter: GitterButton(this, null); break;
                case RemoteButtonTrigger.GitHub: GitHubButton(this, null); break;
                case RemoteButtonTrigger.PayPal: PayPalButton(this, null); break;
                default: return;
            }
        }
    }
}