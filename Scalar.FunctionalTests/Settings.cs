using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Scalar.FunctionalTests.Tools;

namespace Scalar.FunctionalTests.Properties
{
    public static class Settings
    {
        public enum ValidateWorkingTreeMode
        {
            None = 0,
            Full = 1,
            SparseMode = 2,
        }

        public static class Default
        {
            private static string discoveredScalarExec;
            private static string discoveredScalarServiceExec;

            public static string CurrentDirectory { get; private set; }

            public static string RepoToClone { get; set; }
            public static string PathToBash { get; set; }
            public static string Commitish { get; set; }
            public static string ControlGitRepoRoot { get; set; }
            public static string EnlistmentRoot { get; set; }
            public static string PathToGit { get; set; }
            public static string ScalarFileName { get; set; }
            public static string ScalarServiceFileName { get; set; }
            public static string BinaryFileNameExtension { get; set; }

            public static void Initialize()
            {
                CurrentDirectory = Path.GetFullPath(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]));

                RepoToClone = @"https://gvfs.visualstudio.com/ci/_git/ForTests";
                Commitish = @"FunctionalTests/20180214";

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string root = @"C:\Repos\ScalarFunctionalTests";
                    ControlGitRepoRoot = Path.Combine(root, "ControlRepo");
                    EnlistmentRoot = Path.Combine(root, "enlistment");

                    PathToGit = @"C:\Program Files\Git\cmd\git.exe";
                    PathToBash = @"C:\Program Files\Git\bin\bash.exe";

                    ScalarFileName = @"Scalar.exe";
                    ScalarServiceFileName = @"Scalar.Service.exe";
                    BinaryFileNameExtension = ".exe";
                }
                else
                {
                    string root = Path.Combine(Environment.GetEnvironmentVariable("HOME"), "Scalar.FT");
                    EnlistmentRoot = Path.Combine(root, "test");
                    ControlGitRepoRoot = Path.Combine(root, "control");

                    PathToGit = "/usr/local/bin/git";
                    PathToBash = "/bin/bash";

                    ScalarFileName = "scalar";
                    BinaryFileNameExtension = string.Empty;
                }
            }

            public static string GetPathToScalar()
            {
                if (ScalarTestConfig.PathToScalar is null)
                {
                    return discoveredScalarExec ??= GetExecutableOnPath(ScalarFileName);
                }

                return ScalarTestConfig.PathToScalar;
            }

            public static string GetPathToScalarService()
            {
                if (ScalarTestConfig.PathToScalarService is null)
                {
                    return discoveredScalarServiceExec ??= GetExecutableOnPath(ScalarServiceFileName);
                }

                return ScalarTestConfig.PathToScalarService;
            }

            private static string GetExecutableOnPath(string fileName)
            {
                string locatorExec = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";

                ProcessResult result = ProcessHelper.Run(locatorExec, fileName);
                Assert.AreEqual(0, result.ExitCode, $"'{locatorExec}' returned {result.ExitCode} when looking for '{fileName}'");

                string firstPath = null;
                if (!string.IsNullOrWhiteSpace(result.Output))
                {
                    firstPath = result.Output
                        .Split(new[] {'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault();
                }

                Assert.NotNull(firstPath, $"Failed to find '{fileName}");
                return firstPath;
            }
        }
    }
}
