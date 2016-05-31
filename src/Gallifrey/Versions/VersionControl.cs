using System;
using System.ComponentModel;
using System.Deployment.Application;
using System.Threading.Tasks;
using System.Windows.Forms;
using GalaSoft.MvvmLight;
using Gallifrey.AppTracking;

namespace Gallifrey.Versions
{
    public interface IVersionControl
    {
        InstanceType InstanceType { get; }
        bool IsAutomatedDeploy { get; }
        bool UpdateInstalled { get; }
        string VersionName { get; }
        Version DeployedVersion { get; }
        bool IsFirstRun { get; }
        string AppName { get; }
        Task<UpdateResult> CheckForUpdates(bool manualCheck = false);
        void ManualReinstall();
        event PropertyChangedEventHandler PropertyChanged;
    }

    public class VersionControl : ViewModelBase, IVersionControl
    {

        private readonly ITrackUsage trackUsage;
        public InstanceType InstanceType { get; private set; }

        public string VersionName
        {
            get { return _versionName; }
            private set
            {
                _versionName = value;
                RaisePropertyChanged();
            }
        }

        public string AppName { get; private set; }
        public bool UpdateInstalled { get; private set; }

        private DateTime lastUpdateCheck;
        private string _versionName;

        public bool IsAutomatedDeploy => ApplicationDeployment.IsNetworkDeployed;
        public Version DeployedVersion => UpdateInstalled ? ApplicationDeployment.CurrentDeployment.UpdatedVersion : ApplicationDeployment.CurrentDeployment.CurrentVersion;
        public bool IsFirstRun => ApplicationDeployment.CurrentDeployment.IsFirstRun;

        public VersionControl(InstanceType instanceType, ITrackUsage trackUsage)
        {
            this.trackUsage = trackUsage;
            InstanceType = instanceType;
            lastUpdateCheck = DateTime.MinValue;

            SetVersionName();

            var instance = InstanceType == InstanceType.Stable ? "" : $" ({InstanceType})";
            AppName = $"Gallifrey {instance}";
        }

        private void SetVersionName()
        {
            VersionName = IsAutomatedDeploy ? DeployedVersion.ToString() : Application.ProductVersion;
            var betaText = InstanceType == InstanceType.Stable ? "" : $" ({InstanceType})";

            if (!IsAutomatedDeploy)
            {
                betaText = " (Debug)";
            }

            VersionName = $"v{VersionName}{betaText}";
            
        }

        public Task<UpdateResult> CheckForUpdates(bool manualCheck = false)
        {
            if (!IsAutomatedDeploy)
            {
                return Task.Factory.StartNew(() => UpdateResult.NotDeployable);
            }

            if (lastUpdateCheck >= DateTime.UtcNow.AddMinutes(-5) && !manualCheck)
            {
                return Task.Factory.StartNew(() => UpdateResult.TooSoon);
            }

            if (manualCheck)
            {
                trackUsage.TrackAppUsage(TrackingType.UpdateCheckManual);
            }
            else
            {
                trackUsage.TrackAppUsage(TrackingType.UpdateCheck);
            }
            lastUpdateCheck = DateTime.UtcNow;

            try
            {
                var updateInfo = ApplicationDeployment.CurrentDeployment.CheckForDetailedUpdate(false);

                if (updateInfo.UpdateAvailable && updateInfo.AvailableVersion > ApplicationDeployment.CurrentDeployment.CurrentVersion)
                {
                    return Task.Factory.StartNew(() => ApplicationDeployment.CurrentDeployment.Update()).ContinueWith(task =>
                    {
                        SetVersionName();
                        UpdateInstalled = true;
                        return UpdateResult.Updated;
                    });
                }

                return Task.Factory.StartNew(() =>
                {
                    if (manualCheck)
                    {
                        Task.Delay(1500);
                    }
                    return UpdateResult.NoUpdate;
                });
            }
            catch (TrustNotGrantedException)
            {
                return Task.Factory.StartNew(() => UpdateResult.ReinstallNeeded);
            }
            catch (Exception)
            {
                return Task.Factory.StartNew(() => UpdateResult.Error);
            }
        }

        public void ManualReinstall()
        {
            var installUrl = ApplicationDeployment.CurrentDeployment.UpdateLocation.AbsoluteUri;
            DeploymentUtils.Uninstaller.UninstallMe();
            DeploymentUtils.Uninstaller.AutoInstall(installUrl);
            Application.Exit();
        }
    }
}