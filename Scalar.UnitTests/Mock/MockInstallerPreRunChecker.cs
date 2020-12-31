using Scalar.Common.Tracing;
using Scalar.Upgrader;
using System;
using System.Collections.Generic;

namespace Scalar.UnitTests.Mock.Upgrader
{
    public class MockInstallerPrerunChecker : InstallerPreRunChecker
    {
        public const string GitUpgradeCheckError = "Unable to upgrade Git";

        private FailOnCheckType failOnCheck;

        public MockInstallerPrerunChecker(ITracer tracer) : base(tracer, string.Empty)
        {
        }

        [Flags]
        public enum FailOnCheckType
        {
            Invalid = 0,
            IsElevated = 0x2,
            BlockingProcessesRunning = 0x4,
            UnattendedMode = 0x8,
        }

        public void SetReturnFalseOnCheck(FailOnCheckType prerunCheck)
        {
            this.failOnCheck |= prerunCheck;
        }

        public void SetReturnTrueOnCheck(FailOnCheckType prerunCheck)
        {
            this.failOnCheck &= ~prerunCheck;
        }

        public void Reset()
        {
            this.failOnCheck = FailOnCheckType.Invalid;

            this.SetReturnFalseOnCheck(MockInstallerPrerunChecker.FailOnCheckType.UnattendedMode);
            this.SetReturnFalseOnCheck(MockInstallerPrerunChecker.FailOnCheckType.BlockingProcessesRunning);
        }

        public void SetCommandToRerun(string command)
        {
            this.CommandToRerun = command;
        }

        protected override bool IsElevated()
        {
            return this.FakedResultOfCheck(FailOnCheckType.IsElevated);
        }

        protected override bool IsScalarUpgradeSupported()
        {
            return true;
        }

        protected override bool IsUnattended()
        {
            return this.FakedResultOfCheck(FailOnCheckType.UnattendedMode);
        }

        protected override bool IsBlockingProcessRunning(out HashSet<string> processes)
        {
            processes = new HashSet<string>();

            bool isRunning = this.FakedResultOfCheck(FailOnCheckType.BlockingProcessesRunning);
            if (isRunning)
            {
                processes.Add("git");
            }

            return isRunning;
        }

        protected override bool TryRunScalarWithArgs(string args, out string error)
        {
            error = "Unknown Scalar command";
            return false;
        }

        private bool FakedResultOfCheck(FailOnCheckType checkType)
        {
            return !this.failOnCheck.HasFlag(checkType);
        }
    }
}
