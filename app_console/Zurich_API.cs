using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zhinst;

namespace app_console
{
    public class SkipException : Exception
    {
        public SkipException(string msg) : base(msg) { }
    }

    public class Zurich_API
    {

        #region protected function
        // The resetDeviceToDefault will reset the device settings
        // to factory default. The call is quite expensive
        // in runtime. Never use it inside loops!
        protected static void resetDeviceToDefault(ziDotNET daq, string dev)
        {
            if (isDeviceFamily(daq, dev, "HDAWG"))
            {
                // The HDAWG device does currently not support presets
                return;
            }
            if (isDeviceFamily(daq, dev, "HF2"))
            {
                // The HF2 devices do not support the preset functionality.
                daq.setDouble(String.Format("/{0}/demods/*/rate", dev), 250);
                return;
            }

            daq.setInt(String.Format("/{0}/system/preset/index", dev), 0);
            daq.setInt(String.Format("/{0}/system/preset/load", dev), 1);
            while (daq.getInt(String.Format("/{0}/system/preset/busy", dev)) != 0)
            {
                System.Threading.Thread.Sleep(100);
            }
            System.Threading.Thread.Sleep(1000);
        }

        // The isDeviceFamily checks for a specific device family.
        // Currently available families: "HF2", "UHF", "MF"
        protected static bool isDeviceFamily(ziDotNET daq, string dev, String family)
        {
            String path = String.Format("/{0}/features/devtype", dev);
            String devType = daq.getByte(path);
            return devType.StartsWith(family);
        }

        // The hasOption function checks if the device
        // does support a specific functionality, thus
        // has installed the option.
        protected static bool hasOption(ziDotNET daq, string dev, String option)
        {
            String path = String.Format("/{0}/features/options", dev);
            String options = daq.getByte(path);
            return options.Contains(option);
        }

        public static void SkipRequiresOption(ziDotNET daq, string dev, string option)
        {
            if (hasOption(daq, dev, option))
            {
                return;
            }
            daq.disconnect();
            Skip($"Required a device with option {option}.");
        }

        public static void SkipForDeviceFamily(ziDotNET daq, string dev, string family)
        {
            if (isDeviceFamily(daq, dev, family))
            {
                Skip($"This example may not be run on a device of familiy {family}.");
                daq.disconnect();
            }
        }

        public static void SkipForDeviceFamilyAndOption(ziDotNET daq, string dev, string family, string option)
        {
            if (isDeviceFamily(daq, dev, family))
            {
                SkipRequiresOption(daq, dev, option);
            }
        }

        // Please handle version mismatches depending on your
        // application requirements. Version mismatches often relate
        // to functionality changes of some nodes. The API interface is still
        // identical. We strongly recommend to keep the version of the
        // API and data server identical. Following approaches are possible:
        // - Convert version mismatch to a warning for the user to upgrade / downgrade
        // - Convert version mismatch to an error to enforce full matching
        // - Do an automatic upgrade / downgrade
        protected static void apiServerVersionCheck(ziDotNET daq)
        {
            String serverVersion = daq.getByte("/zi/about/version");
            String apiVersion = daq.version();

            AssertEqual(serverVersion, apiVersion,
                   "Version mismatch between LabOne API and Data Server.");
        }

        // Connect initializes a session on the server.
        protected static ziDotNET connect(string dev)
        {
            ziDotNET daq = new ziDotNET();
            String id = daq.discoveryFind(dev);
            String iface = daq.discoveryGetValueS(dev, "connected");
            if (string.IsNullOrWhiteSpace(iface))
            {
                // Device is not connected to the server
                String ifacesList = daq.discoveryGetValueS(dev, "interfaces");
                // Select the first available interface and use it to connect
                string[] ifaces = ifacesList.Split('\n');
                if (ifaces.Length > 0)
                {
                    iface = ifaces[0];
                }
            }
            String host = daq.discoveryGetValueS(dev, "serveraddress");
            long port = daq.discoveryGetValueI(dev, "serverport");
            long api = daq.discoveryGetValueI(dev, "apilevel");
            System.Diagnostics.Trace.WriteLine(
              String.Format("Connecting to server {0}:{1} wich API level {2}",
              host, port, api));
            daq.init(host, Convert.ToUInt16(port), (ZIAPIVersion_enum)api);
            // Ensure that LabOne API and LabOne Data Server are from
            // the same release version.
            apiServerVersionCheck(daq);
            // If device is not yet connected a reconnect
            // will not harm.
            System.Diagnostics.Trace.WriteLine(
              String.Format("Connecting to {0} on inteface {1}", dev, iface));
            daq.connectDevice(dev, iface, "");

            return daq;
        }

        protected static void Skip(string msg)
        {
            throw new SkipException($"SKIP: {msg}");
        }

        protected static void Fail(string msg = null)
        {
            if (msg == null)
            {
                throw new Exception("FAILED!");
            }
            throw new SkipException($"FAILED: {msg}!");
        }

        protected static void AssertNotEqual<T>(T expected, T actual, string msg = null) where T : IComparable<T>
        {
            if (msg != null)
            {
                Debug.Assert(!expected.Equals(actual));
                return;
            }
            Debug.Assert(!expected.Equals(actual));
        }

        protected static void AssertEqual<T>(T expected, T actual, string msg = null) where T : IComparable<T>
        {
            if (msg != null)
            {
                Debug.Assert(expected.Equals(actual), msg);
                return;
            }
            Debug.Assert(expected.Equals(actual));
        }
        #endregion


        static double Sinc(double x)
        {
            return x != 0.0 ? Math.Sin(Math.PI * x) / (Math.PI * x) : 1.0;
        }
    }
}
