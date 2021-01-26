using Microsoft.Win32;
using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Git;
using Scalar.Common.Tracing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Management.Automation;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;

namespace Scalar.Platform.Windows
{
    public class WindowsPlatform : ScalarPlatform
    {
        private const string WindowsVersionRegistryKey = "SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion";
        private const string BuildLabRegistryValue = "BuildLab";
        private const string BuildLabExRegistryValue = "BuildLabEx";

        public WindowsPlatform() : base(underConstruction: new UnderConstructionFlags())
        {
        }

        public override IGitInstallation GitInstallation { get; } = new WindowsGitInstallation();
        public override IPlatformFileSystem FileSystem { get; } = new WindowsFileSystem();
        public override string Name { get => "Windows"; }
        public override ScalarPlatformConstants Constants { get; } = new WindowsPlatformConstants();

        public override string ScalarConfigPath
        {
            get
            {
                return Path.Combine(ScalarPlatform.Instance.GetSecureDataRootForScalar(), LocalScalarConfig.FileName);
            }
        }

        public static string GetStringFromRegistry(string key, string valueName)
        {
            object value = GetValueFromRegistry(RegistryHive.LocalMachine, key, valueName);
            return value as string;
        }

        public static object GetValueFromRegistry(RegistryHive registryHive, string key, string valueName)
        {
            object value = GetValueFromRegistry(registryHive, key, valueName, RegistryView.Registry64);
            if (value == null)
            {
                value = GetValueFromRegistry(registryHive, key, valueName, RegistryView.Registry32);
            }

            return value;
        }

        public static bool TrySetDWordInRegistry(RegistryHive registryHive, string key, string valueName, uint value)
        {
            RegistryKey localKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Registry64);
            RegistryKey localKeySub = localKey.OpenSubKey(key, writable: true);

            if (localKeySub == null)
            {
                localKey = RegistryKey.OpenBaseKey(registryHive, RegistryView.Registry32);
                localKeySub = localKey.OpenSubKey(key, writable: true);
            }

            if (localKeySub == null)
            {
                return false;
            }

            localKeySub.SetValue(valueName, value, RegistryValueKind.DWord);
            return true;
        }

        public override void InitializeEnlistmentACLs(string enlistmentPath)
        {
            // The following permissions are typically present on deskop and missing on Server
            //
            //   ACCESS_ALLOWED_ACE_TYPE: NT AUTHORITY\Authenticated Users
            //          [OBJECT_INHERIT_ACE]
            //          [CONTAINER_INHERIT_ACE]
            //          [INHERIT_ONLY_ACE]
            //        DELETE
            //        GENERIC_EXECUTE
            //        GENERIC_WRITE
            //        GENERIC_READ
            DirectorySecurity rootSecurity = DirectoryEx.GetAccessControl(enlistmentPath);
            AccessRule authenticatedUsersAccessRule = rootSecurity.AccessRuleFactory(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                unchecked((int)(NativeMethods.FileAccess.DELETE | NativeMethods.FileAccess.GENERIC_EXECUTE | NativeMethods.FileAccess.GENERIC_WRITE | NativeMethods.FileAccess.GENERIC_READ)),
                true,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow);

            // The return type of the AccessRuleFactory method is the base class, AccessRule, but the return value can be cast safely to the derived class.
            // https://msdn.microsoft.com/en-us/library/system.security.accesscontrol.filesystemsecurity.accessrulefactory(v=vs.110).aspx
            rootSecurity.AddAccessRule((FileSystemAccessRule)authenticatedUsersAccessRule);
            DirectoryEx.SetAccessControl(enlistmentPath, rootSecurity);
        }

        public override string GetOSVersionInformation()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                string buildLabVersion = GetStringFromRegistry(WindowsVersionRegistryKey, BuildLabRegistryValue);
                sb.AppendFormat($"Windows BuildLab version {buildLabVersion}");
                sb.AppendLine();

                string buildLabExVersion = GetStringFromRegistry(WindowsVersionRegistryKey, BuildLabExRegistryValue);
                sb.AppendFormat($"Windows BuildLabEx version {buildLabExVersion}");
                sb.AppendLine();
            }
            catch (Exception e)
            {
                sb.AppendFormat($"Failed to record Windows version information. Exception: {e}");
            }

            return sb.ToString();
        }

        public override string GetCommonAppDataRootForScalar()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData, Environment.SpecialFolderOption.Create),
                ScalarConstants.WindowsPlatform.ScalarSpecialFolderName);
        }

        public override string GetSecureDataRootForScalar()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolderOption.Create),
                ScalarConstants.WindowsPlatform.ScalarSpecialFolderName,
                "ProgramData");
        }

        public override void StartBackgroundScalarProcess(ITracer tracer, string programName, string[] args)
        {
            string programArguments = string.Empty;
            try
            {
                programArguments = string.Join(" ", args.Select(arg => arg.Contains(' ') ? "\"" + arg + "\"" : arg));
                ProcessStartInfo processInfo = new ProcessStartInfo(programName, programArguments);
                processInfo.WindowStyle = ProcessWindowStyle.Hidden;
                processInfo.UseShellExecute = true;

                Process executingProcess = new Process();
                executingProcess.StartInfo = processInfo;
                executingProcess.Start();
            }
            catch (Exception ex)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add(nameof(programName), programName);
                metadata.Add(nameof(programArguments), programArguments);
                metadata.Add("Exception", ex.ToString());
                tracer.RelatedError(metadata, "Failed to start background process.");
                throw;
            }
        }

        public override void PrepareProcessToRunInBackground()
        {
            // No additional work required
        }

        public override bool IsElevated()
        {
            using (WindowsIdentity id = WindowsIdentity.GetCurrent())
            {
                return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        public override void ConfigureVisualStudio(string gitBinPath, ITracer tracer)
        {
            try
            {
                const string GitBinPathEnd = "\\cmd\\git.exe";
                string[] gitVSRegistryKeyNames =
                {
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\VSCommon\\15.0\\TeamFoundation\\GitSourceControl",
                    "HKEY_CURRENT_USER\\Software\\Microsoft\\VSCommon\\16.0\\TeamFoundation\\GitSourceControl"
                };
                const string GitVSRegistryValueName = "GitPath";

                if (!gitBinPath.EndsWith(GitBinPathEnd))
                {
                    tracer.RelatedWarning(
                        "Unable to configure Visual Studio’s GitSourceControl regkey because invalid git.exe path found: " + gitBinPath,
                        Keywords.Telemetry);

                    return;
                }

                string regKeyValue = gitBinPath.Substring(0, gitBinPath.Length - GitBinPathEnd.Length);
                foreach (string registryKeyName in gitVSRegistryKeyNames)
                {
                    Registry.SetValue(registryKeyName, GitVSRegistryValueName, regKeyValue);
                }
            }
            catch (Exception ex)
            {
                EventMetadata metadata = new EventMetadata();
                metadata.Add("Operation", nameof(this.ConfigureVisualStudio));
                metadata.Add("Exception", ex.ToString());
                tracer.RelatedWarning(metadata, "Error while trying to set Visual Studio’s GitSourceControl regkey");
            }
        }

        public override bool TryVerifyAuthenticodeSignature(string path, out string subject, out string issuer, out string error)
        {
            using (PowerShell powershell = PowerShell.Create())
            {
                powershell.AddScript($"Get-AuthenticodeSignature -FilePath {path}");

                Collection<PSObject> results = powershell.Invoke();
                if (powershell.HadErrors || results.Count <= 0)
                {
                    subject = null;
                    issuer = null;
                    error = $"Powershell Get-AuthenticodeSignature failed, could not verify authenticode for {path}.";
                    return false;
                }

                Signature signature = results[0].BaseObject as Signature;
                bool isValid = signature.Status == SignatureStatus.Valid;
                subject = signature.SignerCertificate.SubjectName.Name;
                issuer = signature.SignerCertificate.IssuerName.Name;
                error = isValid == false ? signature.StatusMessage : null;
                return isValid;
            }
        }

        public override string GetCurrentUser()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return identity.User.Value;
        }

        public override Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly) => WindowsPhysicalDiskInfo.GetPhysicalDiskInfo(path, sizeStatsOnly);

        public override FileBasedLock CreateFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath)
        {
            return new WindowsFileBasedLock(fileSystem, tracer, lockPath);
        }

        public override string GetTemplateHooksDirectory()
        {
            string gitBinPath = GitInstallation.GetInstalledGitBinPath();

            string tail = Path.Combine("cmd", "git.exe");
            if (gitBinPath.EndsWith(tail))
            {
                string gitBasePath = gitBinPath.Substring(0, gitBinPath.Length - tail.Length);
                return Path.Combine(gitBasePath, "mingw64", ScalarConstants.InstalledGit.HookTemplateDir);
            }

            return null;
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            string pathRoot;

            try
            {
                pathRoot = Path.GetPathRoot(enlistmentRoot);
            }
            catch (ArgumentException e)
            {
                localCacheRoot = null;
                localCacheRootError = $"Failed to determine the root of '{enlistmentRoot}'): {e.Message}";
                return false;
            }

            if (string.IsNullOrEmpty(pathRoot))
            {
                localCacheRoot = null;
                localCacheRootError = $"Failed to determine the root of '{enlistmentRoot}', path does not contain root directory information";
                return false;
            }

            try
            {
                localCacheRoot = Path.Combine(pathRoot, ScalarConstants.DefaultScalarCacheFolderName);
                localCacheRootError = null;
                return true;
            }
            catch (ArgumentException e)
            {
                localCacheRoot = null;
                localCacheRootError = $"Failed to build local cache path using root directory '{pathRoot}'): {e.Message}";
                return false;
            }
        }

        public override bool TryKillProcessTree(int processId, out int exitCode, out string error)
        {
            ProcessResult result = ProcessHelper.Run("taskkill", $"/pid {processId} /f /t");
            error = result.Errors;
            exitCode = result.ExitCode;
            return result.ExitCode == 0;
        }

        private static object GetValueFromRegistry(RegistryHive registryHive, string key, string valueName, RegistryView view)
        {
            RegistryKey localKey = RegistryKey.OpenBaseKey(registryHive, view);
            RegistryKey localKeySub = localKey.OpenSubKey(key);

            object value = localKeySub == null ? null : localKeySub.GetValue(valueName);
            return value;
        }

        public class WindowsPlatformConstants : ScalarPlatformConstants
        {
            public override string ExecutableExtension
            {
                get { return ".exe"; }
            }

            public override string InstallerExtension
            {
                get { return ".exe"; }
            }

            public override string ScalarBinDirectoryPath
            {
                get
                {
                    return Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        this.ScalarBinDirectoryName);
                }
            }

            public override string ScalarBinDirectoryName
            {
                get { return ScalarConstants.WindowsPlatform.ScalarSpecialFolderName; }
            }

            public override string ScalarExecutableName
            {
                get { return "Scalar" + this.ExecutableExtension; }
            }

            public override string ProgramLocaterCommand
            {
                get { return "where"; }
            }

            // Tests show that 250 is the max supported pipe name length
            public override int MaxPipePathLength => 250;

            public override bool CaseSensitiveFileSystem => false;
        }
    }
}
