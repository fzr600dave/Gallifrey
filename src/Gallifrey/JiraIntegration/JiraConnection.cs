﻿using System;
using System.Collections.Generic;
using System.Linq;
using Gallifrey.AppTracking;
using Gallifrey.Comparers;
using Gallifrey.Exceptions.JiraIntegration;
using Gallifrey.Jira;
using Gallifrey.Jira.Enum;
using Gallifrey.Jira.Model;
using Gallifrey.Settings;

namespace Gallifrey.JiraIntegration
{
    public interface IJiraConnection
    {
        void ReConnect(IJiraConnectionSettings newJiraConnectionSettings, IExportSettings newExportSettings);
        bool DoesJiraExist(string jiraRef);
        Issue GetJiraIssue(string jiraRef, bool includeWorkLogs = false);
        IEnumerable<string> GetJiraFilters();
        IEnumerable<Issue> GetJiraIssuesFromFilter(string filterName);
        IEnumerable<Issue> GetJiraIssuesFromSearchText(string searchText);
        IEnumerable<Issue> GetJiraIssuesFromJQL(string jqlText);
        void LogTime(string jiraRef, DateTime exportTimeStamp, TimeSpan exportTime, WorkLogStrategy strategy, bool addStandardComment, string comment = "", TimeSpan? remainingTime = null);
        IEnumerable<Issue> GetJiraCurrentUserOpenIssues();
        IEnumerable<JiraProject> GetJiraProjects();
        IEnumerable<RecentJira> GetRecentJirasFound();
        void UpdateCache();
        void AssignToCurrentUser(string jiraRef);
        User CurrentUser { get; }
        bool IsConnected { get; }
        void SetInProgress(string jiraRef);
    }

    public class JiraConnection : IJiraConnection
    {
        private readonly ITrackUsage trackUsage;
        private readonly IRecentJiraCollection recentJiraCollection;
        private readonly List<JiraProject> jiraProjectCache;
        private IJiraConnectionSettings jiraConnectionSettings;
        private IExportSettings exportSettings;
        private IJiraClient jira;
        private DateTime lastCacheUpdate;

        public User CurrentUser { get; private set; }
        public bool IsConnected => jira != null;

        public JiraConnection(ITrackUsage trackUsage)
        {
            this.trackUsage = trackUsage;
            recentJiraCollection = new RecentJiraCollection();
            jiraProjectCache = new List<JiraProject>();
            lastCacheUpdate = DateTime.MinValue;
        }

        public void ReConnect(IJiraConnectionSettings newJiraConnectionSettings, IExportSettings newExportSettings)
        {
            exportSettings = newExportSettings;
            jiraConnectionSettings = newJiraConnectionSettings;
            CheckAndConnectJira();
            UpdateJiraProjectCache();
        }

        private void CheckAndConnectJira(bool useRestApi = true)
        {
            if (jira == null)
            {
                if (string.IsNullOrWhiteSpace(jiraConnectionSettings.JiraUrl) ||
                    string.IsNullOrWhiteSpace(jiraConnectionSettings.JiraUsername) ||
                    string.IsNullOrWhiteSpace(jiraConnectionSettings.JiraPassword))
                {
                    throw new MissingJiraConfigException("Required settings to create connection to jira are missing");
                }

                try
                {
                    var url = jiraConnectionSettings.JiraUrl.Replace("/secure/Dashboard.jspa", "");

                    if (useRestApi)
                    {
                        jira = new JiraRestClient(url, jiraConnectionSettings.JiraUsername, jiraConnectionSettings.JiraPassword);
                    }
                    else
                    {
                        jira = new JiraSoapClient(url, jiraConnectionSettings.JiraUsername, jiraConnectionSettings.JiraPassword);
                    }

                    CurrentUser = jira.GetCurrentUser();

                    TrackingType trackingType;
                    if (useRestApi)
                    {
                        trackingType = url.Contains(".atlassian.net") ? TrackingType.JiraConnectCloudRest : TrackingType.JiraConnectSelfhostRest;
                    }
                    else
                    {
                        trackingType = url.Contains(".atlassian.net") ? TrackingType.JiraConnectCloudSoap : TrackingType.JiraConnectSelfhostSoap;
                    }

                    trackUsage.TrackAppUsage(trackingType);
                }
                catch (Exception ex)
                {
                    jira = null;
                    if (useRestApi)
                    {
                        CheckAndConnectJira(false);
                    }
                    else
                    {
                        throw new JiraConnectionException("Error creating instance of Jira", ex);
                    }
                }
            }
        }

        public bool DoesJiraExist(string jiraRef)
        {
            try
            {
                CheckAndConnectJira();
                var issue = GetJiraIssue(jiraRef);
                if (issue != null)
                {
                    return true;
                }
            }
            catch (Exception)
            {
                recentJiraCollection.Remove(jiraRef);
                return false;
            }

            recentJiraCollection.Remove(jiraRef);
            return false;
        }

        public Issue GetJiraIssue(string jiraRef, bool includeWorkLogs = false)
        {
            try
            {
                CheckAndConnectJira();
                var issue = includeWorkLogs ? jira.GetIssueWithWorklogs(jiraRef, CurrentUser.key) : jira.GetIssue(jiraRef);

                recentJiraCollection.AddRecentJira(issue);
                return issue;
            }
            catch (Exception ex)
            {
                recentJiraCollection.Remove(jiraRef);
                throw new NoResultsFoundException($"Unable to locate Jira {jiraRef}", ex);
            }
        }

        public IEnumerable<string> GetJiraFilters()
        {
            try
            {
                CheckAndConnectJira();
                trackUsage.TrackAppUsage(TrackingType.SearchLoad);
                var returnedFilters = jira.GetFilters();
                return returnedFilters.Select(returned => returned.name);
            }
            catch (Exception ex)
            {
                throw new NoResultsFoundException("Error loading filters", ex);
            }
        }

        public IEnumerable<Issue> GetJiraIssuesFromFilter(string filterName)
        {
            try
            {
                CheckAndConnectJira();
                trackUsage.TrackAppUsage(TrackingType.SearchFilter);
                var issues = jira.GetIssuesFromFilter(filterName);
                recentJiraCollection.AddRecentJiras(issues);
                return issues.OrderBy(x => x.key, new JiraReferenceComparer());
            }
            catch (Exception ex)
            {
                throw new NoResultsFoundException("Error loading jiras from filter", ex);
            }
        }

        public IEnumerable<Issue> GetJiraIssuesFromSearchText(string searchText)
        {
            try
            {
                CheckAndConnectJira();
                trackUsage.TrackAppUsage(TrackingType.SearchText);
                var issues = jira.GetIssuesFromJql(GetJql(searchText));
                recentJiraCollection.AddRecentJiras(issues);
                return issues.OrderBy(x => x.key, new JiraReferenceComparer());
            }
            catch (Exception ex)
            {
                throw new NoResultsFoundException("Error loading jiras from search text", ex);
            }
        }

        public IEnumerable<Issue> GetJiraIssuesFromJQL(string jqlText)
        {
            try
            {
                CheckAndConnectJira();
                var issues = jira.GetIssuesFromJql(jqlText);
                recentJiraCollection.AddRecentJiras(issues);
                return issues.OrderBy(x => x.key, new JiraReferenceComparer());
            }
            catch (Exception ex)
            {
                throw new NoResultsFoundException("Error loading jiras from search text", ex);
            }
        }

        public IEnumerable<Issue> GetJiraCurrentUserOpenIssues()
        {
            try
            {
                CheckAndConnectJira();
                var issues = jira.GetIssuesFromJql("assignee in (currentUser()) AND status not in (Closed,Resolved)");
                recentJiraCollection.AddRecentJiras(issues);
                return issues.OrderBy(x => x.key, new JiraReferenceComparer());
            }
            catch (Exception ex)
            {
                throw new NoResultsFoundException("Error loading jiras from search text", ex);
            }
        }

        private void UpdateJiraProjectCache()
        {
            if (lastCacheUpdate < DateTime.UtcNow.AddMinutes(-30))
            {
                try
                {
                    CheckAndConnectJira();
                    var projects = jira.GetProjects();
                    jiraProjectCache.Clear();
                    jiraProjectCache.AddRange(projects.Select(project => new JiraProject(project.key, project.name)));
                    lastCacheUpdate = DateTime.UtcNow;
                }
                catch (Exception) { }
            }
        }

        public IEnumerable<JiraProject> GetJiraProjects()
        {
            return jiraProjectCache;
        }

        public IEnumerable<RecentJira> GetRecentJirasFound()
        {
            return recentJiraCollection.GetRecentJiraCollection();
        }

        public void UpdateCache()
        {
            recentJiraCollection.RemoveExpiredCache();
            UpdateJiraProjectCache();
        }

        public void AssignToCurrentUser(string jiraRef)
        {
            try
            {
                CheckAndConnectJira();
                jira.AssignIssue(jiraRef, CurrentUser.name);
            }
            catch (Exception ex)
            {
                throw new JiraConnectionException("Unable to assign issue.", ex);
            }

        }

        public void SetInProgress(string jiraRef)
        {
            var inProgressStatuses = new List<string>
            {
                "In Progress",
                "Start Progress"
            };

            TransitionIssue(jiraRef, inProgressStatuses);
        }

        public void LogTime(string jiraRef, DateTime exportTimeStamp, TimeSpan exportTime, WorkLogStrategy strategy, bool addStandardComment, string comment = "", TimeSpan? remainingTime = null)
        {
            trackUsage.TrackAppUsage(TrackingType.ExportOccured);

            var jiraIssue = jira.GetIssue(jiraRef);

            if (string.IsNullOrWhiteSpace(comment)) comment = exportSettings.EmptyExportComment;
            if (!string.IsNullOrWhiteSpace(exportSettings.ExportCommentPrefix))
            {
                comment = $"{exportSettings.ExportCommentPrefix}: {comment}";
            }
            
            var erroredOnWorkLogAttempt1 = false;
            
            try
            {
                jira.AddWorkLog(jiraRef, strategy, comment, exportTime, DateTime.SpecifyKind(exportTimeStamp, DateTimeKind.Local), remainingTime);
            }
            catch (Exception)
            {
                erroredOnWorkLogAttempt1 = true;
            }

            if (erroredOnWorkLogAttempt1)
            {
                var wasClosed = TryReopenJira(jiraIssue);

                try
                {
                    jira.AddWorkLog(jiraRef, strategy, comment, exportTime, DateTime.SpecifyKind(exportTimeStamp, DateTimeKind.Local), remainingTime);
                }
                catch (Exception ex)
                {
                    throw new WorkLogException("Error logging work", ex);
                }

                if (wasClosed)
                {
                    try
                    {
                        ReCloseJira(jiraRef);
                    }
                    catch (Exception ex)
                    {
                        throw new StateChangedException("Time Logged, but state is now open", ex);
                    }
                }
            }

            if (addStandardComment)
            {
                try
                {
                    jira.AddComment(jiraRef, comment);
                }
                catch (Exception ex)
                {
                    throw new CommentException("Comment was not added", ex);
                }
            }
        }

        private void ReCloseJira(string jiraRef)
        {
            var inProgressStatuses = new List<string>
            {
                "Close Issue",
                "Closed"
            };

            TransitionIssue(jiraRef, inProgressStatuses);
        }

        private bool TryReopenJira(Issue jiraIssue)
        {
            var wasClosed = false;
            if (jiraIssue.fields.status.name == "Closed")
            {
                try
                {
                    var inProgressStatuses = new List<string>
                    {
                        "Reopen Issue",
                        "Open"
                    };

                    TransitionIssue(jiraIssue.key, inProgressStatuses);
                    wasClosed = true;
                }
                catch (Exception)
                {
                    wasClosed = false;
                }
            }
            return wasClosed;
        }

        private void TransitionIssue(string jiraRef, IEnumerable<string> possibleTransitionValues)
        {
            Exception lastex = null;
            foreach (var possibleTransitionValue in possibleTransitionValues)
            {
                try
                {
                    jira.TransitionIssue(jiraRef, possibleTransitionValue);
                    return;
                }
                catch (Exception ex)
                {
                    lastex = ex;
                }
            }

            throw new StateChangedException("Cannot Set In Progress Status", lastex);
        }

        private string GetJql(string searchText)
        {
            var jql = string.Empty;
            var searchTerm = string.Empty;
            var projects = jira.GetProjects();
            foreach (var keyword in searchText.Split(' '))
            {
                var foundProject = false;
                if (projects.Any(project => project.key == keyword))
                {
                    jql = $"project = \"{keyword}\"";
                    foundProject = true;
                }

                if (!foundProject)
                {
                    searchTerm += " " + keyword;
                }
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                if (!string.IsNullOrWhiteSpace(jql))
                {
                    jql += " AND ";
                }
                jql += $" text ~ \"{searchTerm}\"";
            }

            try
            {
                if ((jira.GetIssue(searchText) != null))
                {
                    jql = $"key = \"{searchText}\" OR ({jql})";
                }

            }
            catch
            {
            }

            return jql;
        }
    }
}
