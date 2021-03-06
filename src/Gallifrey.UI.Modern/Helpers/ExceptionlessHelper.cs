﻿using System;
using System.Windows;
using Exceptionless;
using Gallifrey.UI.Modern.Flyouts;

namespace Gallifrey.UI.Modern.Helpers
{
    public class ExceptionlessHelper
    {
        private readonly ModelHelpers modelHelpers;

        public ExceptionlessHelper(ModelHelpers modelHelpers)
        {
            this.modelHelpers = modelHelpers;

            modelHelpers.Gallifrey.DailyTrackingEvent += GallifreyOnDailyTrackingEvent;
        }

        public void RegisterExceptionless()
        {
            if (modelHelpers.Gallifrey.VersionControl.IsAutomatedDeploy)
            {
                ExceptionlessClient.Default.Unregister();
                ExceptionlessClient.Default.Configuration.ApiKey = "e7ac6366507547639ce69fea261d6545";
                ExceptionlessClient.Default.Configuration.DefaultTags.Add(modelHelpers.Gallifrey.VersionControl.VersionName.Replace("\n", " - "));
                ExceptionlessClient.Default.Configuration.SetUserIdentity(modelHelpers.Gallifrey.Settings.InternalSettings.InstallationInstaceId.ToString(), modelHelpers.Gallifrey.JiraConnection.IsConnected ? modelHelpers.Gallifrey.JiraConnection.CurrentUser.displayName : "Unknown");
                ExceptionlessClient.Default.Configuration.SessionsEnabled = false;
                ExceptionlessClient.Default.Configuration.Enabled = true;
                ExceptionlessClient.Default.SubmittingEvent += ExceptionlessSubmittingEvent;
                ExceptionlessClient.Default.Register();
            }
        }

        private async void ExceptionlessSubmittingEvent(object sender, EventSubmittingEventArgs e)
        {
            if (e.IsUnhandledError)
            {
                e.Cancel = true;

                await Application.Current.Dispatcher.Invoke(async () =>
                {
                    modelHelpers.CloseAllFlyouts();
                    await modelHelpers.OpenFlyout(new Error(modelHelpers, e.Event));
                    modelHelpers.CloseApp(true);
                });
            }
        }

        private void GallifreyOnDailyTrackingEvent(object sender, EventArgs eventArgs)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ExceptionlessClient.Default.SubmitFeatureUsage(modelHelpers.Gallifrey.VersionControl.VersionName);
                });
            }
            catch (Exception)
            {
                //suppress errors if tracking fails
            }
        }
    }
}
