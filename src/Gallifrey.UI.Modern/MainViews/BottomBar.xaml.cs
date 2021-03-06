﻿using System.Diagnostics;
using System.Windows.Navigation;
using Gallifrey.AppTracking;
using Gallifrey.UI.Modern.Helpers;
using Gallifrey.UI.Modern.Models;

namespace Gallifrey.UI.Modern.MainViews
{
    public partial class BottomBar
    {
        private ModelHelpers ModelHelpers => ((MainViewModel)DataContext).ModelHelpers;

        public BottomBar()
        {
            InitializeComponent();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var uri = e.Uri.AbsoluteUri;

            if (uri.ToLower().Contains("paypal.com"))
            {
                ModelHelpers.Gallifrey.TrackEvent(TrackingType.PayPalClick);
            }
            else if (uri.ToLower().Contains("github.com"))
            {
                ModelHelpers.Gallifrey.TrackEvent(TrackingType.GitHubClick);
            }
            else
            {
                ModelHelpers.Gallifrey.TrackEvent(TrackingType.ContactClick);
            }

            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
    }
}
