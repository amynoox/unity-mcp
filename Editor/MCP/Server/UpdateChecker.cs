// Copyright (C) KitWright. Licensed under MIT.

using System;

namespace KitWright.Editor.MCP.Server
{
    // Updates are selected in the consuming project's reviewed package files.
    // Keep the window's existing API, but never query releases, reuse cached
    // upstream update offers, install a Git package or import a unitypackage.
    internal static class UpdateChecker
    {
        internal const string UpdatePolicyMessage =
            "Updates are managed through reviewed project package files from " +
            "https://github.com/amynoox/unity-mcp. QA uses the concrete hash " +
            "committed in game master and keeps that revision throughout a test.";

        public static event Action StateChanged;

        public static UpdateStateSnapshot CurrentState => new UpdateStateSnapshot(
            PackageVersion.Current, string.Empty, string.Empty,
            "Project-managed Git package", UpdatePolicyMessage,
            false, false, false, false, 0f);

        public static void MaybeCheckForUpdatesInBackground()
        {
            StateChanged?.Invoke();
        }

        // Retained for existing UI bindings. This cannot replace the package.
        public static void UpdateToLatestFromWindow()
        {
            StateChanged?.Invoke();
        }

        private static string NormalizeVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return "0.0.0";

            return version.Trim().TrimStart('v', 'V');
        }

        internal static Version ParseComparableVersion(string version)
        {
            // Version.TryParse reads the numeric core only, so a -rc / +build suffix is cut first.
            var core = NormalizeVersion(version).Split('-', '+')[0];
            return Version.TryParse(core, out var parsed) ? parsed : new Version(0, 0, 0);
        }

        public sealed class UpdateStateSnapshot
        {
            public readonly string CurrentVersion;
            public readonly string LatestVersion;
            public readonly string LatestReleaseUrl;
            public readonly string InstallDescription;
            public readonly string StatusMessage;
            public readonly bool HasUpdateAvailable;
            public readonly bool IsChecking;
            public readonly bool IsUpdating;
            public readonly bool UpdateStarted;
            public readonly float Progress;

            public UpdateStateSnapshot(
                string currentVersion,
                string latestVersion,
                string latestReleaseUrl,
                string installDescription,
                string statusMessage,
                bool hasUpdateAvailable,
                bool isChecking,
                bool isUpdating,
                bool updateStarted,
                float progress)
            {
                CurrentVersion = currentVersion;
                LatestVersion = latestVersion;
                LatestReleaseUrl = latestReleaseUrl;
                InstallDescription = installDescription;
                StatusMessage = statusMessage;
                HasUpdateAvailable = hasUpdateAvailable;
                IsChecking = isChecking;
                IsUpdating = isUpdating;
                UpdateStarted = updateStarted;
                Progress = progress;
            }
        }
    }
}
