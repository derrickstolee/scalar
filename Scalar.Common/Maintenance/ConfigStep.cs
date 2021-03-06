﻿using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Scalar.Common.Maintenance
{
    public class ConfigStep : GitMaintenanceStep
    {
        public override string Area => nameof(ConfigStep);

        [Flags]
        private enum GitCoreGVFSFlags
        {
            // GVFS_SKIP_SHA_ON_INDEX
            // Disables the calculation of the sha when writing the index
            SkipShaOnIndex = 1 << 0,

            // GVFS_BLOCK_COMMANDS
            // Blocks git commands that are not allowed in a GVFS/Scalar repo
            BlockCommands = 1 << 1,

            // GVFS_MISSING_OK
            // Normally git write-tree ensures that the objects referenced by the
            // directory exist in the object database.This option disables this check.
            MissingOk = 1 << 2,

            // GVFS_NO_DELETE_OUTSIDE_SPARSECHECKOUT
            // When marking entries to remove from the index and the working
            // directory this option will take into account what the
            // skip-worktree bit was set to so that if the entry has the
            // skip-worktree bit set it will not be removed from the working
            // directory.  This will allow virtualized working directories to
            // detect the change to HEAD and use the new commit tree to show
            // the files that are in the working directory.
            NoDeleteOutsideSparseCheckout = 1 << 3,

            // GVFS_FETCH_SKIP_REACHABILITY_AND_UPLOADPACK
            // While performing a fetch with a virtual file system we know
            // that there will be missing objects and we don't want to download
            // them just because of the reachability of the commits.  We also
            // don't want to download a pack file with commits, trees, and blobs
            // since these will be downloaded on demand.  This flag will skip the
            // checks on the reachability of objects during a fetch as well as
            // the upload pack so that extraneous objects don't get downloaded.
            FetchSkipReachabilityAndUploadPack = 1 << 4,

            // 1 << 5 has been deprecated

            // GVFS_BLOCK_FILTERS_AND_EOL_CONVERSIONS
            // With a virtual file system we only know the file size before any
            // CRLF or smudge/clean filters processing is done on the client.
            // To prevent file corruption due to truncation or expansion with
            // garbage at the end, these filters must not run when the file
            // is first accessed and brought down to the client. Git.exe can't
            // currently tell the first access vs subsequent accesses so this
            // flag just blocks them from occurring at all.
            BlockFiltersAndEolConversions = 1 << 6,

            // GVFS_PREFETCH_DURING_FETCH
            // While performing a `git fetch` command, use the gvfs-helper to
            // perform a "prefetch" of commits and trees.
            PrefetchDuringFetch = 1 << 7,
        }

        private bool? UseGvfsProtocol = true;

        public ConfigStep(ScalarContext context, bool? useGvfsProtocol = null) : base(context, requireObjectCacheLock: false)
        {
            this.UseGvfsProtocol = useGvfsProtocol;
        }

        public override string ProgressMessage => "Setting recommended config settings";

        public bool TrySetConfig(out string error)
        {
            string coreGVFSFlags = Convert.ToInt32(
                GitCoreGVFSFlags.BlockCommands |
                GitCoreGVFSFlags.MissingOk |
                GitCoreGVFSFlags.FetchSkipReachabilityAndUploadPack |
                GitCoreGVFSFlags.PrefetchDuringFetch)
                .ToString();

            string expectedHooksPath = Path.Combine(this.Context.Enlistment.WorkingDirectoryRoot, ScalarConstants.DotGit.Hooks.Root);
            expectedHooksPath = Paths.ConvertPathToGitFormat(expectedHooksPath);

            if (!this.UseGvfsProtocol.HasValue)
            {
                this.UseGvfsProtocol = this.Context.Enlistment.UsesGvfsProtocol;
            }

            // These settings are required for normal Scalar functionality.
            // They will override any existing local configuration values.
            //
            // IMPORTANT! These must parallel the settings in ControlGitRepo:Initialize
            //
            Dictionary<string, string> requiredSettings = new Dictionary<string, string>
            {
                { "am.keepcr", "true" },
                { "core.autocrlf", "false" },
                { "checkout.optimizenewbranch", "true" },
                { "core.fscache", "true" },
                { "core.multiPackIndex", "true" },
                { "core.preloadIndex", "true" },
                { "core.safecrlf", "false" },
                { "core.untrackedCache", ScalarPlatform.Instance.FileSystem.SupportsUntrackedCache ? "true" : "false" },
                { "core.filemode", ScalarPlatform.Instance.FileSystem.SupportsFileMode ? "true" : "false" },
                { "core.bare", "false" },
                { "core.logallrefupdates", "true" },
                { "core.hookspath", expectedHooksPath },
                { GitConfigSetting.CredentialUseHttpPath, "true" },
                { "credential.validate", "false" },
                { "gc.auto", "0" },
                { "gui.gcwarning", "false" },
                { "index.threads", "true" },
                { "index.version", "4" },
                { "merge.stat", "false" },
                { "merge.renames", "false" },
                { "pack.useBitmaps", "false" },
                { "pack.useSparse", "true" },
                { "receive.autogc", "false" },
                { "reset.quiet", "true" },
                { "feature.manyFiles", "false" },
                { "feature.experimental", "false" },
                { "fetch.writeCommitGraph", "false" },
            };

            if (this.UseGvfsProtocol.Value)
            {
                requiredSettings.Add("core.gvfs", coreGVFSFlags);
                requiredSettings.Add(ScalarConstants.GitConfig.UseGvfsHelper, "true");
                requiredSettings.Add("http.version", "HTTP/1.1");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                requiredSettings.Add("http.sslBackend", "schannel");
            }

            // If we do not use the GVFS protocol, then these config settings
            // are in fact optional.
            if (!this.TrySetConfig(requiredSettings, isRequired: this.UseGvfsProtocol.Value, out error))
            {
                error = $"Failed to set some required settings: {error}";
                this.Context.Tracer.RelatedError(error);
                return false;
            }

            // These settings are optional, because they impact performance but not functionality of Scalar.
            // These settings should only be set by the clone or repair verbs, so that they do not
            // overwrite the values set by the user in their local config.
            Dictionary<string, string> optionalSettings = new Dictionary<string, string>
            {
                { "status.aheadbehind", "false" },
            };

            if (!this.TrySetConfig(optionalSettings, isRequired: false, out error))
            {
                error = $"Failed to set some optional settings: {error}";
                this.Context.Tracer.RelatedError(error);
                return false;
            }

            error = null;
            return true;
        }

        protected override void PerformMaintenance()
        {
            this.TrySetConfig(out _);
            this.ConfigureWatchmanIntegration();
        }

        private bool TrySetConfig(Dictionary<string, string> configSettings, bool isRequired, out string error)
        {
            Dictionary<string, GitConfigSetting> existingConfigSettings = null;

            GitProcess.Result getConfigResult = this.RunGitCommand(
                process => process.TryGetAllConfig(localOnly: isRequired, configSettings: out existingConfigSettings),
                nameof(GitProcess.TryGetAllConfig));

            // If the settings are required, then only check local config settings, because we don't want to depend on
            // global settings that can then change independent of this repo.
            if (getConfigResult.ExitCodeIsFailure)
            {
                error = "Failed to get all config entries";
                return false;
            }

            foreach (KeyValuePair<string, string> setting in configSettings)
            {
                GitConfigSetting existingSetting;
                if (setting.Value != null)
                {
                    if (!existingConfigSettings.TryGetValue(setting.Key, out existingSetting) ||
                        (isRequired && !existingSetting.HasValue(setting.Value)))
                    {
                        this.Context.Tracer.RelatedInfo($"Setting config value {setting.Key}={setting.Value}");
                        GitProcess.Result setConfigResult = this.RunGitCommand(
                                                                    process => process.SetInLocalConfig(setting.Key, setting.Value),
                                                                    nameof(GitProcess.SetInLocalConfig));
                        if (setConfigResult.ExitCodeIsFailure)
                        {
                            error = setConfigResult.Errors;
                            return false;
                        }
                    }
                }
                else
                {
                    if (existingConfigSettings.TryGetValue(setting.Key, out _))
                    {
                        this.RunGitCommand(
                                process => process.DeleteFromLocalConfig(setting.Key),
                                nameof(GitProcess.DeleteFromLocalConfig));
                    }
                }
            }

            error = null;
            return true;
        }

        private void ConfigureWatchmanIntegration()
        {
            string watchmanLocation = ProcessHelper.GetProgramLocation(ScalarPlatform.Instance.Constants.ProgramLocaterCommand, "watchman");
            if (string.IsNullOrEmpty(watchmanLocation))
            {
                this.Context.Tracer.RelatedWarning("Watchman is not installed - skipping Watchman configuration.");
                return;
            }

            try
            {
                string fsMonitorWatchmanSampleHookPath = Path.Combine(
                    this.Context.Enlistment.WorkingDirectoryRoot,
                    ScalarConstants.DotGit.Hooks.FsMonitorWatchmanSamplePath);

                string queryWatchmanPath = Path.Combine(
                    this.Context.Enlistment.WorkingDirectoryRoot,
                    ScalarConstants.DotGit.Hooks.QueryWatchmanPath);

                this.Context.FileSystem.CopyFile(
                    fsMonitorWatchmanSampleHookPath,
                    queryWatchmanPath,
                    overwrite: false);

                this.RunGitCommand(
                    process => process.SetInLocalConfig("core.fsmonitor", ".git/hooks/query-watchman"),
                    "config");

                this.Context.Tracer.RelatedInfo("Watchman configured!");
            }
            catch (IOException ex)
            {
                EventMetadata metadata = this.CreateEventMetadata(ex);
                this.Context.Tracer.RelatedError(metadata, $"Failed to configure Watchman integration: {ex.Message}");
            }
        }
    }
}
