﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Gallifrey.ChangeLog
{
    internal static class ChangeLogProvider
    {
        public static IDictionary<Version, ChangeLogVersionDetails> GetChangeLog(Version fromVersion, Version toVersion, XDocument changeLogContent)
        {
            var changeLog = new Dictionary<Version, ChangeLogVersionDetails>();

            foreach (var changeVersion in changeLogContent.Descendants("Version"))
            {
                var changeLogItem = BuildChangeLogItem(changeVersion);
                changeLog.Add(changeLogItem.Key, changeLogItem.Value);
            }
            
            if (fromVersion < toVersion && changeLog.Any(x => x.Key > fromVersion && x.Key <= toVersion))
            {
                return changeLog.Where(x => x.Key > fromVersion && x.Key <= toVersion).OrderByDescending(detail => detail.Key).ToDictionary(detail => detail.Key, detail => detail.Value);
            }

            if (fromVersion < toVersion && changeLog.Any() && toVersion > changeLog.Keys.Max())
            {
                return changeLog.Where(x => x.Key == changeLog.Keys.Max()).ToDictionary(detail => detail.Key, detail => detail.Value);
            }

            return changeLog.Where(x => x.Key <= toVersion).OrderByDescending(detail => detail.Key).ToDictionary(detail => detail.Key, detail => detail.Value);
        }

        private static KeyValuePair<Version, ChangeLogVersionDetails> BuildChangeLogItem(XElement changeVersion)
        {
            var version = new Version(changeVersion.Element("Number").Value);
            var details = BuildChangeLogVersionDetails(changeVersion);
            return new KeyValuePair<Version, ChangeLogVersionDetails>(version, details);
        }

        private static ChangeLogVersionDetails BuildChangeLogVersionDetails(XElement changeVersion)
        {
            var features = changeVersion.Descendants("Feature").Select(item => item.Value).ToList();
            var bugs = changeVersion.Descendants("Bug").Select(item => item.Value).ToList();
            var others = changeVersion.Descendants("Other").Select(item => item.Value).ToList();

            return new ChangeLogVersionDetails(features, bugs, others);
        }
    }
}