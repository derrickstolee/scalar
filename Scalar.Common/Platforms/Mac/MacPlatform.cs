using Scalar.Common;
using Scalar.Common.FileSystem;
using Scalar.Common.Tracing;
using Scalar.Platform.POSIX;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Scalar.Platform.Mac
{
    public class MacPlatform : POSIXPlatform
    {
        private static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseCachePaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.DefaultScalarCacheFolderName),
        };
        private static readonly EnvironmentVariableBasePath[] EnvironmentVariableBaseDataPaths = new[] {
            new EnvironmentVariableBasePath(
                ScalarConstants.POSIXPlatform.EnvironmentVariables.LocalUserFolder,
                ScalarConstants.MacPlatform.LocalScalarDataPath),
        };

        public override string Name { get => "macOS"; }
        public override ScalarPlatformConstants Constants { get; } = new MacPlatformConstants();
        public override IPlatformFileSystem FileSystem { get; } = new MacFileSystem();

        public override string ScalarConfigPath
        {
            get
            {
                return Path.Combine(this.Constants.ScalarBinDirectoryPath, LocalScalarConfig.FileName);
            }
        }

        public override string GetOSVersionInformation()
        {
            ProcessResult result = ProcessHelper.Run("sw_vers", args: string.Empty, redirectOutput: true);
            return string.IsNullOrWhiteSpace(result.Output) ? result.Errors : result.Output;
        }

        public override FileBasedLock CreateFileBasedLock(
            PhysicalFileSystem fileSystem,
            ITracer tracer,
            string lockPath)
        {
            return new MacFileBasedLock(fileSystem, tracer, lockPath);
        }

        public override Dictionary<string, string> GetPhysicalDiskInfo(string path, bool sizeStatsOnly)
        {
            // DiskUtil will return disk statistics in xml format
            ProcessResult processResult = ProcessHelper.Run("diskutil", "info -plist /", true);
            Dictionary<string, string> result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(processResult.Output))
            {
                result.Add("DiskUtilError", processResult.Errors);
                return result;
            }

            try
            {
                // Parse the XML looking for FilesystemType
                XDocument xmlDoc = XDocument.Parse(processResult.Output);
                XElement filesystemTypeValue = xmlDoc.XPathSelectElement("plist/dict/key[text()=\"FilesystemType\"]")?.NextNode as XElement;
                result.Add("FileSystemType", filesystemTypeValue != null ? filesystemTypeValue.Value : "Not Found");
            }
            catch (XmlException ex)
            {
                result.Add("DiskUtilError", ex.ToString());
            }

            return result;
        }

        public override bool TryGetDefaultLocalCacheRoot(string enlistmentRoot, out string localCacheRoot, out string localCacheRootError)
        {
            return TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseCachePaths, out localCacheRoot, out localCacheRootError);
        }

        public class MacPlatformConstants : POSIXPlatformConstants
        {
            public override string InstallerExtension
            {
                get { return ".dmg"; }
            }

            public override string ScalarBinDirectoryPath
            {
                get { return Path.Combine("/usr", "local", this.ScalarBinDirectoryName); }
            }

            // Documented here (in the addressing section): https://www.unix.com/man-page/mojave/4/unix/
            public override int MaxPipePathLength => 104;

            public override bool CaseSensitiveFileSystem => false;
        }

        // Defined in
        // /Library/Developer/CommandLineTools/SDKs/MacOSX.sdk/usr/include/sys/syslimits.h
        protected override int MaxPathLength => 1024;

        protected override bool TryGetDefaultLocalDataRoot(out string localDataRoot, out string localDataRootError)
        {
            return TryGetEnvironmentVariableBasePath(EnvironmentVariableBaseDataPaths, out localDataRoot, out localDataRootError);
        }
    }
}
