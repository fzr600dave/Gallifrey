﻿using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Xml.Linq;
using Gallifrey.AppTracking;
using Gallifrey.UI.Helpers;
using Gallifrey.UI.Models;
using MahApps.Metro.Controls.Dialogs;

namespace Gallifrey.UI.Flyouts
{
    public partial class Information
    {
        private readonly ModelHelpers modelHelpers;

        public Information(ModelHelpers modelHelpers)
        {
            this.modelHelpers = modelHelpers;
            InitializeComponent();
            DataContext = new InformationModel(modelHelpers.Gallifrey);
            modelHelpers.Gallifrey.TrackEvent(TrackingType.InformationShown);
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private async void ChangeLogButton(object sender, RoutedEventArgs e)
        {
            modelHelpers.CloseFlyout(this);
            var changeLog = modelHelpers.Gallifrey.GetChangeLog(XDocument.Parse(Properties.Resources.ChangeLog)).ToList();
            if (changeLog.Any())
            {
                await modelHelpers.OpenFlyout(new ChangeLog(changeLog));
            }
            else
            {
                await DialogCoordinator.Instance.ShowMessageAsync(modelHelpers.DialogContext, "No Change Log", "There Is No Change Log To Show");
            }
            modelHelpers.OpenFlyout(this);
        }

        private void InstallationIdClick(object sender, MouseButtonEventArgs e)
        {
            Clipboard.SetText(modelHelpers.Gallifrey.Settings.InternalSettings.InstallationInstaceId.ToString());
        }
    }
}