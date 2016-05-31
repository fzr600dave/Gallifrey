using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Timers;
using GalaSoft.MvvmLight;
using Gallifrey.Comparers;
using Gallifrey.ExtensionMethods;
using Gallifrey.UI.Modern.Helpers;
using Gallifrey.Versions;

namespace Gallifrey.UI.Modern.Models
{
    public class MainViewModel : ViewModelBase
    {
        private string _exported;
        private string _totalTimerCount;
        private string _unexportedTime;
        private string _exportTarget;
        private string _exported1;
        private string _exportedTargetTotalMinutes;
        private string _versionName;
        private string _loggedInAs;
        private string _loggedInDisplayName;
        private bool _hasUpdate;
        private bool _hasInactiveTime;
        private bool _timerRunning;
        private bool _haveTimeToExport;
        private bool _haveTempTime;
        private bool _isPremium;
        private bool _isStable;
        private string _inactiveMinutes;
        public ModelHelpers ModelHelpers { get; }
        public ObservableCollection<TimerDateModel> TimerDates { get; private set; }

        public string InactiveMinutes
        {
            get { return _inactiveMinutes; }
            private set
            {
                _inactiveMinutes = value;
                RaisePropertyChanged();
            }
        }

        public MainViewModel(ModelHelpers modelHelpers)
        {
            ModelHelpers = modelHelpers;
            TimerDates = new ObservableCollection<TimerDateModel>();
            var runningWatcher = new Timer(500);
            runningWatcher.Elapsed += runningWatcherElapsed;
            runningWatcher.Start();

            var backgroundRefresh = new Timer(3600000);
            backgroundRefresh.Elapsed += (sender, args) => RefreshModel();
            backgroundRefresh.Start();

            modelHelpers.Gallifrey.VersionControl.PropertyChanged += VersionControlPropertyChanged;
            modelHelpers.RefreshModelEvent += (sender, args) => RefreshModel();
            modelHelpers.SelectRunningTimerEvent += (sender, args) => SelectRunningTimer();
            modelHelpers.SelectTimerEvent += (sender, timerId) => SetSelectedTimer(timerId);
        }

        public string ExportedNumber
        {
            get { return _exported; }
            set
            {
                _exported = value;
                RaisePropertyChanged();
            }
        }

        public string TotalTimerCount
        {
            get { return _totalTimerCount; }
            set
            {
                _totalTimerCount = value;
                RaisePropertyChanged();
            }
        }

        public string UnexportedTime
        {
            get { return _unexportedTime; }
            set
            {
                _unexportedTime = value;
                RaisePropertyChanged();
            }
        }

        public string ExportTarget
        {
            get { return _exportTarget; }
            set
            {
                _exportTarget = value;
                RaisePropertyChanged();
            }
        }

        public string Exported
        {
            get { return _exported1; }
            set
            {
                _exported1 = value;
                RaisePropertyChanged();
            }
        }

        public string ExportedTargetTotalMinutes
        {
            get { return _exportedTargetTotalMinutes; }
            set
            {
                _exportedTargetTotalMinutes = value;
                RaisePropertyChanged();
            }
        }

        public string VersionName
        {
            get { return _versionName; }
            set
            {
                _versionName = value;
                RaisePropertyChanged();
            }
        }

        public string LoggedInAs
        {
            get { return _loggedInAs; }
            set
            {
                _loggedInAs = value;
                RaisePropertyChanged();
            }
        }

        public string LoggedInDisplayName
        {
            get { return _loggedInDisplayName; }
            set
            {
                _loggedInDisplayName = value;
                RaisePropertyChanged();
            }
        }

        public bool HasUpdate
        {
            get { return _hasUpdate; }
            set
            {
                _hasUpdate = value;
                RaisePropertyChanged();
            }
        }

        public bool HasInactiveTime
        {
            get { return _hasInactiveTime; }
            set
            {
                _hasInactiveTime = value;
                RaisePropertyChanged();
            }
        }

        public bool TimerRunning
        {
            get { return _timerRunning; }
            set
            {
                _timerRunning = value;
                RaisePropertyChanged();
            }
        }

        public bool HaveTimeToExport
        {
            get { return _haveTimeToExport; }
            set
            {
                _haveTimeToExport = value;
                RaisePropertyChanged();
            }
        }

        public bool HaveTempTime
        {
            get { return _haveTempTime; }
            set
            {
                _haveTempTime = value;
                RaisePropertyChanged();
            }
        }

        public bool IsPremium
        {
            get { return _isPremium; }
            set
            {
                _isPremium = value;
                RaisePropertyChanged();
            }
        }

        public bool IsStable
        {
            get { return _isStable; }
            set
            {
                _isStable = value;
                RaisePropertyChanged();
            }
        }

        public string AppTitle
        {
            get
            {
                var instanceType = ModelHelpers.Gallifrey.VersionControl.InstanceType;
                var appName = IsPremium ? "Gallifrey Premium" : "Gallifrey";
                return instanceType == InstanceType.Stable ? $"{appName}" : $"{appName} ({instanceType})";
            }
        }

        public string TimeToExportMessage
        {
            get
            {
                var unexportedTime = ModelHelpers.Gallifrey.JiraTimerCollection.GetTotalExportableTime();
                var unexportedTimers = ModelHelpers.Gallifrey.JiraTimerCollection.GetStoppedUnexportedTimers();
                var unexportedCount = unexportedTimers.Count(x => !x.TempTimer);

                var excludingRunning = string.Empty;
                var runningTimerId = ModelHelpers.Gallifrey.JiraTimerCollection.GetRunningTimerId();
                if (runningTimerId.HasValue)
                {
                    var runningTimer = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(runningTimerId.Value);
                    if (!runningTimer.FullyExported && !runningTimer.TempTimer)
                    {
                        excludingRunning = "(Excluding 1 Running Timer)";
                    }
                }

                return unexportedTime.TotalMinutes > 0 ? $"You Have {unexportedCount} Timer{(unexportedCount > 1 ? "s" : "")} Worth {unexportedTime.FormatAsString(false)} To Export {excludingRunning}" : string.Empty;
            }
        }

        public string TempTimeMessage
        {
            get
            {
                var tempTime = ModelHelpers.Gallifrey.JiraTimerCollection.GetTotalTempTime();
                var unexportedTimers = ModelHelpers.Gallifrey.JiraTimerCollection.GetStoppedUnexportedTimers();
                var unexportedCount = unexportedTimers.Count(x => x.TempTimer);

                var excludingRunning = string.Empty;
                var runningTimerId = ModelHelpers.Gallifrey.JiraTimerCollection.GetRunningTimerId();
                if (runningTimerId.HasValue)
                {
                    var runningTimer = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(runningTimerId.Value);
                    if (runningTimer.TempTimer)
                    {
                        excludingRunning = "(Excluding 1 Running Timer)";
                    }
                }

                return tempTime.TotalMinutes > 0 ? $"You Have {unexportedCount} Temp Timer{(unexportedCount > 1 ? "s" : "")} Worth {tempTime.FormatAsString(false)} {excludingRunning}" : string.Empty;
            }
        }

        public string CurrentRunningTimerDescription
        {
            get
            {
                var runningTimerId = ModelHelpers.Gallifrey.JiraTimerCollection.GetRunningTimerId();
                if (!runningTimerId.HasValue) return string.Empty;
                var runningTimer = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimer(runningTimerId.Value);

                return $"Currently Running {runningTimer.JiraReference} ({runningTimer.JiraName})";
            }
        }

        public string ExportedTotalMinutes
        {
            get
            {
                var maximum = ModelHelpers.Gallifrey.Settings.AppSettings.GetTargetThisWeek().TotalMinutes;
                var value = ModelHelpers.Gallifrey.JiraTimerCollection.GetTotalExportedTimeThisWeek(ModelHelpers.Gallifrey.Settings.AppSettings.StartOfWeek).TotalMinutes;

                if (value > maximum)
                {
                    value = maximum;
                }

                return value.ToString();
            }
        }

        private void VersionControlPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("VersionName"));
        }

        private void runningWatcherElapsed(object sender, ElapsedEventArgs e)
        {
            ExportedNumber = ModelHelpers.Gallifrey.JiraTimerCollection.GetNumberExported().Item1.ToString();
            TotalTimerCount = ModelHelpers.Gallifrey.JiraTimerCollection.GetNumberExported().Item2.ToString();
            UnexportedTime = ModelHelpers.Gallifrey.JiraTimerCollection.GetTotalUnexportedTime().FormatAsString(false);
            ExportTarget = ModelHelpers.Gallifrey.Settings.AppSettings.GetTargetThisWeek().FormatAsString(false);
            Exported =
                ModelHelpers.Gallifrey.JiraTimerCollection.GetTotalExportedTimeThisWeek(
                    ModelHelpers.Gallifrey.Settings.AppSettings.StartOfWeek).FormatAsString(false);
            ExportedTargetTotalMinutes = ModelHelpers.Gallifrey.Settings.AppSettings.GetTargetThisWeek().TotalMinutes.ToString();
            VersionName = ModelHelpers.Gallifrey.VersionControl.UpdateInstalled ? "Click To Install New Version" : ModelHelpers.Gallifrey.VersionControl.VersionName;
            LoggedInAs = ModelHelpers.Gallifrey.JiraConnection.CurrentUser != null ? $"Logged in as {ModelHelpers.Gallifrey.JiraConnection.CurrentUser.key} ({ModelHelpers.Gallifrey.JiraConnection.CurrentUser.emailAddress})" : "Please Connect To Jira";
            LoggedInDisplayName = ModelHelpers.Gallifrey.JiraConnection.CurrentUser != null ? ModelHelpers.Gallifrey.JiraConnection.CurrentUser.displayName : "Not Logged In To Jira";
            HasUpdate = ModelHelpers.Gallifrey.VersionControl.UpdateInstalled;
            HasInactiveTime = !string.IsNullOrWhiteSpace(InactiveMinutes);
            TimerRunning = !string.IsNullOrWhiteSpace(CurrentRunningTimerDescription);
            HaveTimeToExport = !string.IsNullOrWhiteSpace(TimeToExportMessage);
            HaveTempTime = !string.IsNullOrWhiteSpace(TempTimeMessage);
            IsPremium = ModelHelpers.Gallifrey.Settings.InternalSettings.IsPremium;
            IsStable = ModelHelpers.Gallifrey.VersionControl.InstanceType == InstanceType.Stable;
        }

        private void RefreshModel()
        {
            var workingDays = ModelHelpers.Gallifrey.Settings.AppSettings.ExportDays.ToList();
            var workingDate = DateTime.Now.AddDays((ModelHelpers.Gallifrey.Settings.AppSettings.KeepTimersForDays - 1) * -1).Date;
            var validTimerDates = new List<DateTime>();

            while (workingDate.Date <= DateTime.Now.Date)
            {
                if (workingDays.Contains(workingDate.DayOfWeek))
                {
                    validTimerDates.Add(workingDate.Date);
                }
                workingDate = workingDate.AddDays(1);
            }

            foreach (var timerDate in ModelHelpers.Gallifrey.JiraTimerCollection.GetValidTimerDates())
            {
                if (!validTimerDates.Contains(timerDate))
                {
                    validTimerDates.Add(timerDate.Date);
                }
            }

            foreach (var timerDate in validTimerDates.OrderBy(x => x.Date))
            {
                var dateModel = TimerDates.FirstOrDefault(x => x.TimerDate.Date == timerDate.Date);

                if (dateModel == null)
                {
                    dateModel = new TimerDateModel(timerDate, ModelHelpers.Gallifrey.JiraTimerCollection);
                    TimerDates.Add(dateModel);
                }

                var dateTimers = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimersForADate(timerDate).ToList();

                foreach (var timer in dateTimers)
                {
                    if (!dateModel.Timers.Any(x => x.JiraTimer.UniqueId == timer.UniqueId))
                    {
                        dateModel.AddTimerModel(new TimerModel(timer));
                    }
                }

                var removeTimers = dateModel.Timers.Where(timerModel => !dateTimers.Any(x => x.UniqueId == timerModel.JiraTimer.UniqueId)).ToList();

                foreach (var removeTimer in removeTimers)
                {
                    dateModel.RemoveTimerModel(removeTimer);
                }
            }

            //see if the order would be different now, and if so, recreate the TimerDates
            var orderedCollection = TimerDates.Where(x => validTimerDates.Contains(x.TimerDate)).OrderByDescending(x => x.TimerDate).ToList();
            if (orderedCollection.Count != TimerDates.Count)
            {
                TimerDates.Clear();
                foreach (var timerDateModel in orderedCollection)
                {
                    TimerDates.Add(timerDateModel);
                }
            }
            else
            {
                for (var i = 0; i < TimerDates.Count; i++)
                {
                    var main = TimerDates[i];
                    var ordered = orderedCollection[i];

                    if (main.TimerDate != ordered.TimerDate)
                    {
                        TimerDates.Clear();
                        foreach (var timerDateModel in orderedCollection)
                        {
                            TimerDates.Add(timerDateModel);
                        }
                        break;
                    }
                }
            }

            runningWatcherElapsed(this, null);
        }

        private void SetSelectedTimer(Guid value)
        {
            foreach (var timerDateModel in TimerDates)
            {
                var selectedTimerFound = false;
                foreach (var timerModel in timerDateModel.Timers)
                {
                    if (timerModel.JiraTimer.UniqueId == value)
                    {
                        timerModel.SetSelected(true);
                        selectedTimerFound = true;
                    }
                    else
                    {
                        timerModel.SetSelected(false);
                    }
                }

                timerDateModel.SetSelected(selectedTimerFound);
            }
        }

        private void SelectRunningTimer()
        {
            var runningTimer = ModelHelpers.Gallifrey.JiraTimerCollection.GetRunningTimerId();
            if (runningTimer.HasValue)
            {
                //SetSelectedTimer(runningTimer.Value);
                ModelHelpers.SetSelectedTimer(runningTimer.Value);
            }
            else
            {
                var dates = ModelHelpers.Gallifrey.JiraTimerCollection.GetValidTimerDates();
                if (dates.Any())
                {
                    var date = dates.Max();
                    var topTimer = ModelHelpers.Gallifrey.JiraTimerCollection.GetTimersForADate(date).OrderBy(x => x.JiraReference, new JiraReferenceComparer()).FirstOrDefault();
                    if (topTimer != null)
                    {
                        SetSelectedTimer(topTimer.UniqueId);
                    }
                }
            }
        }

        public void SetNoActivityMilliseconds(int millisecondsSinceActivity)
        {
            if (millisecondsSinceActivity == 0)
            {
                InactiveMinutes = string.Empty;
            }
            else
            {
                var minutesSinceActivity = (millisecondsSinceActivity / 1000) / 60;
                var minutesPlural = string.Empty;
                if (minutesSinceActivity > 1)
                {
                    minutesPlural = "s";
                }

                InactiveMinutes = $"No Timer Running For {minutesSinceActivity} Minute{minutesPlural}";
            }
        }

        public IEnumerable<Guid> GetSelectedTimerIds()
        {
            foreach (var timerDateModel in TimerDates.Where(x => x.DateIsSelected))
            {
                foreach (var timerModel in timerDateModel.Timers.Where(timerModel => timerModel.TimerIsSelected))
                {
                    yield return timerModel.JiraTimer.UniqueId;
                }
            }
        }
    }
}