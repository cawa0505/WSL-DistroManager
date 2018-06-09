using DistroManager.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DistroManager
{
    internal sealed class Distro : IDisposable
    {
        private bool _disposed;
        private readonly string _distributionName;
        private IntPtr _wslApiDll;
        private NativeMethods.WSL_IS_DISTRIBUTION_REGISTERED _isDistributionRegistered;
        private NativeMethods.WSL_GET_DISTRIBUTION_CONFIGURATION _getDistributionConfiguration;
        private NativeMethods.WSL_REGISTER_DISTRIBUTION _registerDistribution;
        private NativeMethods.WSL_UNREGISTER_DISTRIBUTION _unregisterDistribution;
        private NativeMethods.WSL_CONFIGURE_DISTRIBUTION _configureDistribution;
        private NativeMethods.WSL_LAUNCH_INTERACTIVE _launchInteractive;
        private NativeMethods.WSL_LAUNCH _launch;

        public Distro(string distributionName) : base()
        {
            if (String.IsNullOrWhiteSpace(distributionName))
                throw new ArgumentNullException(nameof(distributionName));

            _distributionName = distributionName;
            _wslApiDll = NativeMethods.LoadLibraryEx("wslapi.dll", IntPtr.Zero, NativeMethods.LOAD_LIBRARY_SEARCH_SYSTEM32);

            if (_wslApiDll != IntPtr.Zero)
            {
                _isDistributionRegistered = (NativeMethods.WSL_IS_DISTRIBUTION_REGISTERED)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_wslApiDll, "WslIsDistributionRegistered"), typeof(NativeMethods.WSL_IS_DISTRIBUTION_REGISTERED));
                _getDistributionConfiguration = (NativeMethods.WSL_GET_DISTRIBUTION_CONFIGURATION)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_wslApiDll, "WslGetDistributionConfiguration"), typeof(NativeMethods.WSL_GET_DISTRIBUTION_CONFIGURATION));
                _registerDistribution = (NativeMethods.WSL_REGISTER_DISTRIBUTION)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_wslApiDll, "WslRegisterDistribution"), typeof(NativeMethods.WSL_REGISTER_DISTRIBUTION));
                _unregisterDistribution = (NativeMethods.WSL_UNREGISTER_DISTRIBUTION)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_wslApiDll, "WslUnregisterDistribution"), typeof(NativeMethods.WSL_UNREGISTER_DISTRIBUTION));
                _configureDistribution = (NativeMethods.WSL_CONFIGURE_DISTRIBUTION)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_wslApiDll, "WslConfigureDistribution"), typeof(NativeMethods.WSL_CONFIGURE_DISTRIBUTION));
                _launchInteractive = (NativeMethods.WSL_LAUNCH_INTERACTIVE)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_wslApiDll, "WslLaunchInteractive"), typeof(NativeMethods.WSL_LAUNCH_INTERACTIVE));
                _launch = (NativeMethods.WSL_LAUNCH)Marshal.GetDelegateForFunctionPointer(NativeMethods.GetProcAddress(_wslApiDll, "WslLaunch"), typeof(NativeMethods.WSL_LAUNCH));
            }
        }

        ~Distro()
        {
            Dispose(false);
        }

        public bool IsOptionalComponentInstalled
        {
            get
            {
                return (_wslApiDll != IntPtr.Zero &&
                    _isDistributionRegistered != null &&
                    _getDistributionConfiguration != null &&
                    _registerDistribution != null &&
                    _unregisterDistribution != null &&
                    _configureDistribution != null &&
                    _launchInteractive != null &&
                    _launch != null);
            }
        }

        public bool IsDistributionRegistered
        {
            get { return _isDistributionRegistered(_distributionName); }
        }

        public int RegisterDistro(string tarGzFileName)
        {
            if (!File.Exists(tarGzFileName))
                return NativeMethods.E_FILENOTFOUND;

            if (!Helpers.IsAdministrator())
            {
                Console.Error.WriteLine(Resources.MSG_INSUFFICIENT_RIGHTS);
                return NativeMethods.E_ACCESSDENIED;
            }

            int hr = _registerDistribution(_distributionName, tarGzFileName);

            if (NativeMethods.FAILED(hr))
                Console.Out.WriteLine(Resources.MSG_WSL_REGISTER_DISTRIBUTION_FAILED, hr);

            return hr;
        }

        public int UnregisterDistro()
        {
            if (!Helpers.IsAdministrator())
            {
                Console.Error.WriteLine(Resources.MSG_INSUFFICIENT_RIGHTS);
                return NativeMethods.E_ACCESSDENIED;
            }

            int hr = _unregisterDistribution(_distributionName);

            if (NativeMethods.FAILED(hr))
                Console.Out.WriteLine(Resources.MSG_WSL_UNREGISTER_DISTRIBUTION_FAILED, hr);

            return hr;
        }

        public int GetDistroConfig(out int version, out int defaultUID, out NativeMethods.WSL_DISTRIBUTION_FLAGS wslDistributionFlags, List<KeyValuePair<string, string>> environmentVariables)
        {
            int hr = _getDistributionConfiguration(_distributionName, out version, out defaultUID, out wslDistributionFlags, out IntPtr ptrArray, out int count);

            if (NativeMethods.FAILED(hr))
                Console.Out.WriteLine(Resources.MSG_WSL_GET_DISTRIBUTION_CONFIGURE_FAILED, hr);

            if (environmentVariables != null)
            {
                for (int i = 0; i < count * 2; i++)
                {
                    var eachPtr = Marshal.ReadIntPtr(ptrArray, i * Marshal.SizeOf(typeof(int)));
                    var contents = Marshal.PtrToStringAnsi(eachPtr);

                    if (String.IsNullOrWhiteSpace(contents))
                        continue;

                    var kvp = contents.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                    environmentVariables.Add(new KeyValuePair<string, string>(kvp.ElementAtOrDefault(0), kvp.ElementAtOrDefault(1)));
                }
            }

            return hr;
        }

        public int SetDistroConfig(int defaultUID, NativeMethods.WSL_DISTRIBUTION_FLAGS wslDistributionFlags)
        {
            if (!Helpers.IsAdministrator())
            {
                Console.Error.WriteLine(Resources.MSG_INSUFFICIENT_RIGHTS);
                return NativeMethods.E_ACCESSDENIED;
            }

            int hr = _configureDistribution(_distributionName, defaultUID, wslDistributionFlags);

            if (NativeMethods.FAILED(hr))
                Console.Out.WriteLine(Resources.MSG_WSL_CONFIGURE_DISTRIBUTION_FAILED, hr);

            return hr;
        }

        public int LaunchInteractive(string command, bool useCurrentWorkingDirectory, out int exitCode)
        {
            int hr = _launchInteractive(_distributionName, command, useCurrentWorkingDirectory, out exitCode);

            if (NativeMethods.FAILED(hr))
                Console.Out.WriteLine(Resources.MSG_WSL_LAUNCH_INTERACTIVE_FAILED, command, hr);

            return hr;
        }

        public int Launch(string command, bool useCurrentWorkingDirectory, IntPtr stdIn, IntPtr stdOut, IntPtr stdErr, out IntPtr process)
        {
            int hr = _launch(_distributionName, command, useCurrentWorkingDirectory, stdIn, stdOut, stdErr, out process);

            if (NativeMethods.FAILED(hr))
                Console.Out.WriteLine(Resources.MSG_WSL_LAUNCH_FAILED, hr);

            return hr;
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects).
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                if (_wslApiDll != IntPtr.Zero)
                {
                    NativeMethods.FreeLibrary(_wslApiDll);
                    _wslApiDll = IntPtr.Zero;
                }

                // Set large fields to null.
                _isDistributionRegistered = null;
                _registerDistribution = null;
                _configureDistribution = null;
                _launchInteractive = null;
                _launch = null;

                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
