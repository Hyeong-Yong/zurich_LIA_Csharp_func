using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using zhinst;

namespace ziDotNetExamples
{
    /// <summary>
    /// This exception is used to notify that the example could not be executed.
    ///
    /// <param name="msg">The reason why the example was ot executed</param>
    /// </summary>
    public class SkipException : Exception
    {
        public SkipException(string msg) : base(msg) { }
    }

    public class Examples
    {
        const string DEFAULT_DEVICE = "dev3066";

        // The resetDeviceToDefault will reset the device settings
        // to factory default. The call is quite expensive
        // in runtime. Never use it inside loops!
        private static void resetDeviceToDefault(ziDotNET daq, string dev)
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
        private static bool isDeviceFamily(ziDotNET daq, string dev, String family)
        {
            String path = String.Format("/{0}/features/devtype", dev);
            String devType = daq.getByte(path);
            return devType.StartsWith(family);
        }

        // The hasOption function checks if the device
        // does support a specific functionality, thus
        // has installed the option.
        private static bool hasOption(ziDotNET daq, string dev, String option)
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
        private static void apiServerVersionCheck(ziDotNET daq)
        {
            String serverVersion = daq.getByte("/zi/about/version");
            String apiVersion = daq.version();

            AssertEqual(serverVersion, apiVersion,
                   "Version mismatch between LabOne API and Data Server.");
        }

        // Connect initializes a session on the server.
        private static ziDotNET connect(string dev)
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

        private static void Skip(string msg)
        {
            throw new SkipException($"SKIP: {msg}");
        }

        private static void Fail(string msg = null)
        {
            if (msg == null)
            {
                throw new Exception("FAILED!");
            }
            throw new SkipException($"FAILED: {msg}!");
        }

        private static void AssertNotEqual<T>(T expected, T actual, string msg = null) where T : IComparable<T>
        {
            if (msg != null)
            {
                Debug.Assert(!expected.Equals(actual));
                return;
            }
            Debug.Assert(!expected.Equals(actual));
        }

        private static void AssertEqual<T>(T expected, T actual, string msg = null) where T : IComparable<T>
        {
            if (msg != null)
            {
                Debug.Assert(expected.Equals(actual), msg);
                return;
            }
            Debug.Assert(expected.Equals(actual));
        }

        // ExamplePollDemodSample connects to the device,
        // subscribes to a demodulator, polls the data for 0.1 s
        // and returns the data.
        public static void ExamplePollDemodSample(string dev = DEFAULT_DEVICE)
        {
            ziDotNET daq = connect(dev);
            SkipForDeviceFamily(daq, dev, "HDAWG");

            resetDeviceToDefault(daq, dev);
            String path = String.Format("/{0}/demods/0/sample", dev);
            daq.subscribe(path);
            // After the subscribe, poll() can be executed continuously within a loop.
            // Arguments to poll() are:
            // - 'duration': poll duration in seconds
            // - 'timeOutMilliseconds': timeout to wait for any packet from data server
            // - 'flags': combination of poll flags that determine the
            //   behavior upon sample loss (see e.g., Python API for more information)
            // - 'bufferSize': should be provided as 1 and will be removed in a
            //   future version
            Lookup lookup = daq.poll(0.1, 100, 0, 1);
            Dictionary<String, Chunk[]> nodes = lookup.nodes;  // Iterable nodes
            Chunk[] chunks = lookup[path];  // Iterable chunks
            Chunk chunk = lookup[path][0];  // Single chunk
                                            // Vector of samples
            ZIDemodSample[] demodSamples = lookup[path][0].demodSamples;
            // Single sample
            ZIDemodSample demodSample0 = lookup[path][0].demodSamples[0];
            daq.disconnect();

            Debug.Assert(0 != demodSample0.timeStamp);
        }

        // ExamplePollImpedanceSample connects to the device,
        // subscribes to a impedance stream, polls the data for 0.1 s
        // and returns the data.
        public static void ExamplePollImpedanceSample(string dev = DEFAULT_DEVICE)
        {
            ziDotNET daq = connect(dev);
            // This example only works for devices with installed
            // Impedance Analyzer (IA) option.
            if (!hasOption(daq, dev, "IA"))
            {
                daq.disconnect();
                Skip("Not supported by device.");
            }
            resetDeviceToDefault(daq, dev);
            // Enable impedance control
            daq.setInt(String.Format("/{0}/imps/0/enable", dev), 1);
            // Return R and Cp
            daq.setInt(String.Format("/{0}/imps/0/model", dev), 0);
            // Enable user compensation
            daq.setInt(String.Format("/{0}/imps/0/calib/user/enable", dev), 1);
            // Wait until auto ranging has settled
            System.Threading.Thread.Sleep(4000);
            // Subscribe to the impedance data stream
            String path = String.Format("/{0}/imps/0/sample", dev);
            daq.subscribe(path);
            // After the subscribe, poll() can be executed continuously within a loop.
            // Arguments to poll() are:
            // - 'duration': poll duration in seconds
            // - 'timeOutMilliseconds': timeout to wait for any packet from data server
            // - 'flags': combination of poll flags that determine the
            //   behavior upon sample loss (see e.g., Python API for more information)
            // - 'bufferSize': should be provided as 1 and will be removed in a
            //   future version
            Lookup lookup = daq.poll(0.1, 100, 0, 1);
            Dictionary<String, Chunk[]> nodes = lookup.nodes;  // Iterable nodes
            Chunk[] chunks = lookup[path];  // Iterable chunks
            Chunk chunk = lookup[path][0];  // Single chunk
                                            // Vector of samples
            ZIImpedanceSample[] impedanceSamples = lookup[path][0].impedanceSamples;
            // Single sample
            ZIImpedanceSample impedanceSample0 = lookup[path][0].impedanceSamples[0];
            // Extract the R||C representation values
            System.Diagnostics.Trace.WriteLine(
                    String.Format("Impedance Resistor value: {0} Ohm.", impedanceSample0.param0));
            System.Diagnostics.Trace.WriteLine(
                    String.Format("Impedance Capacitor value: {0} F.", impedanceSample0.param1));
            daq.disconnect();

            AssertNotEqual(0ul, impedanceSample0.timeStamp);
        }

        // ExamplePollDoubleData is similar to ExamplePollDemodSample,
        // but it subscribes and polls floating point data.
        public static void ExamplePollDoubleData(string dev = DEFAULT_DEVICE)
        {
            ziDotNET daq = connect(dev);
            String path = String.Format("/{0}/oscs/0/freq", dev);
            daq.getAsEvent(path);
            daq.subscribe(path);
            Lookup lookup = daq.poll(1, 100, 0, 1);
            Dictionary<String, Chunk[]> nodes = lookup.nodes;  // Iterable nodes
            Chunk[] chunks = lookup[path];  // Iterable chunks
            Chunk chunk = lookup[path][0];  // Single chunk
            ZIDoubleData[] doubleData = lookup[path][0].doubleData;  // Vector of samples
            ZIDoubleData doubleData0 = lookup[path][0].doubleData[0];  // Single sample
            daq.disconnect();

            AssertNotEqual(0ul, doubleData0.timeStamp);
        }

        // ExamplePollPwaData is similar to ExamplePollDemodSample,
        // but it subscribes and polls periodic waveform analyzer
        // data from a device with the Boxcar option.
        public static void ExamplePollPwaData(string dev = DEFAULT_DEVICE) // Timeout(10000)
        {
            ziDotNET daq = connect(dev);
            // The PWA example only works for devices with installed Boxcar (BOX) option
            if (hasOption(daq, dev, "BOX"))
            {
                String enablePath = String.Format("/{0}/inputpwas/0/enable", dev);
                daq.setInt(enablePath, 1);
                String path = String.Format("/{0}/inputpwas/0/wave", dev);
                daq.subscribe(path);
                Lookup lookup = daq.poll(1, 100, 0, 1);
                UInt64 timeStamp = lookup[path][0].pwaWaves[0].timeStamp;
                UInt64 sampleCount = lookup[path][0].pwaWaves[0].sampleCount;
                UInt32 inputSelect = lookup[path][0].pwaWaves[0].inputSelect;
                UInt32 oscSelect = lookup[path][0].pwaWaves[0].oscSelect;
                UInt32 harmonic = lookup[path][0].pwaWaves[0].harmonic;
                Double frequency = lookup[path][0].pwaWaves[0].frequency;
                Byte type = lookup[path][0].pwaWaves[0].type;
                Byte mode = lookup[path][0].pwaWaves[0].mode;
                Byte overflow = lookup[path][0].pwaWaves[0].overflow;
                Byte commensurable = lookup[path][0].pwaWaves[0].commensurable;
                double[] grid = lookup[path][0].pwaWaves[0].binPhase;
                double[] x = lookup[path][0].pwaWaves[0].x;
                double[] y = lookup[path][0].pwaWaves[0].y;
                String fileName = Environment.CurrentDirectory + "/pwa.txt";
                System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
                file.WriteLine("TimeStamp: {0}", timeStamp);
                file.WriteLine("Sample Count: {0}", sampleCount);
                file.WriteLine("Input Select: {0}", inputSelect);
                file.WriteLine("Osc Select: {0}", oscSelect);
                file.WriteLine("Frequency: {0}", frequency);
                for (int i = 0; i < grid.Length; ++i)
                {
                    file.WriteLine("{0} {1} {2}", grid[i], x[i], y[i]);
                }
                file.Close();

                AssertNotEqual(0ul, timeStamp);
                AssertNotEqual(0ul, sampleCount);
                AssertNotEqual(0, grid.Length);
            }
            daq.disconnect();
        }

        // ExamplePollScopeData is similar to ExamplePollDemodSample,
        // but it subscribes and polls scope data.
        public static void ExamplePollScopeData(string dev = DEFAULT_DEVICE)
        {
            ziDotNET daq = connect(dev);
            SkipForDeviceFamily(daq, dev, "HDAWG");

            resetDeviceToDefault(daq, dev);

            String enablePath = String.Format("/{0}/scopes/0/enable", dev);
            daq.setInt(enablePath, 1);
            String path = String.Format("/{0}/scopes/0/wave", dev);
            daq.subscribe(path);
            Lookup lookup = daq.poll(1, 100, 0, 1);
            UInt64 timeStamp = lookup[path][0].scopeWaves[0].header.timeStamp;
            UInt64 sampleCount = lookup[path][0].scopeWaves[0].header.totalSamples;
            daq.disconnect();

            AssertNotEqual(0ul, timeStamp);
            AssertNotEqual(0ul, sampleCount);
        }

        // ExamplePollVectorData connects to the device, requests data from
        // vector nodes, and polls until data is received.
        public static void ExamplePollVectorData(string dev = DEFAULT_DEVICE)
        {
            ziDotNET daq = connect(dev);

            // This example only works for devices with the AWG option
            if (hasOption(daq, dev, "AWG") || isDeviceFamily(daq, dev, "UHFQA") || isDeviceFamily(daq, dev, "UHFAWG") || isDeviceFamily(daq, dev, "HDAWG"))
            {
                resetDeviceToDefault(daq, dev);

                // Request vector node from device
                String path = String.Format("/{0}/awgs/0/waveform/waves/0", dev);
                daq.getAsEvent(path);

                // Poll until the node path is found in the result data
                double timeout = 20;
                double poll_time = 0.1;
                Lookup lookup = null;
                for (double time = 0; ; time += poll_time)
                {
                    lookup = daq.poll(poll_time, 100, 0, 1);
                    if (lookup.nodes.ContainsKey(path))
                        break;
                    if (time > timeout)
                        Fail("Vector node data not received within timeout");
                }

                Chunk[] chunks = lookup[path]; // Iterable chunks
                Chunk chunk = chunks[0];       // Single chunk
                ZIVectorData vectorData = chunk.vectorData[0];

                // The vector attribute of a ZIVectorData object holds a ZIVector object,
                // which can contain a String or arrays of the following types:
                // byte, UInt16, Uint32, Uint64, float, double

                // Waveform vector data is stored as 32-bit unsigned integer
                if (vectorData.vector != null)  // Check for empty container
                {
                    UInt32[] vector = vectorData.vector.data as UInt32[];
                }

                AssertNotEqual(0ul, vectorData.timeStamp);
            }
            daq.disconnect();
        }

        // ExampleGetDemodSample reads the demodulator sample value of the specified node.
        public static void ExampleGetDemodSample(string dev = DEFAULT_DEVICE)
        {
            ziDotNET daq = connect(dev);
            SkipForDeviceFamily(daq, dev, "HDAWG");

            resetDeviceToDefault(daq, dev);
            String path = String.Format("/{0}/demods/0/sample", dev);
            ZIDemodSample sample = daq.getDemodSample(path);
            System.Diagnostics.Trace.WriteLine(sample.frequency, "Sample frequency");
            daq.disconnect();

            AssertNotEqual(0ul, sample.timeStamp);
        }

        // ExampleSweeper instantiates a sweeper module and executes a sweep
        // over 100 data points from 1kHz to 100kHz and writes the result into a file.
        public static void ExampleSweeper(string dev = DEFAULT_DEVICE) // Timeout(40000)
        {
            ziDotNET daq = connect(dev);
            SkipForDeviceFamily(daq, dev, "HDAWG");

            resetDeviceToDefault(daq, dev);
            ziModule sweep = daq.sweeper();
            sweep.setByte("device", dev);
            sweep.setDouble("start", 1e3);
            sweep.setDouble("stop", 1e5);
            sweep.setDouble("samplecount", 100);
            String path = String.Format("/{0}/demods/0/sample", dev);
            sweep.subscribe(path);
            sweep.execute();
            while (!sweep.finished())
            {
                System.Threading.Thread.Sleep(100);
                double progress = sweep.progress() * 100;
                System.Diagnostics.Trace.WriteLine(progress, "Progress");
            }
            Lookup lookup = sweep.read();
            double[] grid = lookup[path][0].sweeperDemodWaves[0].grid;
            double[] x = lookup[path][0].sweeperDemodWaves[0].x;
            double[] y = lookup[path][0].sweeperDemodWaves[0].y;
            String fileName = Environment.CurrentDirectory + "/sweep.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
            ZIChunkHeader header = lookup[path][0].header;
            // Raw system time is the number of microseconds since linux epoch
            file.WriteLine("Raw System Time: {0}", header.systemTime);
            // Use the utility function ziSystemTimeToDateTime to convert to DateTime of .NET
            file.WriteLine("Converted System Time: {0}", ziUtility.ziSystemTimeToDateTime(lookup[path][0].header.systemTime));
            file.WriteLine("Created Timestamp: {0}", header.createdTimeStamp);
            file.WriteLine("Changed Timestamp: {0}", header.changedTimeStamp);
            for (int i = 0; i < grid.Length; ++i)
            {
                file.WriteLine("{0} {1} {2}", grid[i], x[i], y[i]);
            }
            file.Close();

            AssertEqual(1.0, sweep.progress());
            AssertNotEqual(0, grid.Length);

            sweep.clear();  // Release module resources. Especially important if modules are created
                            // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        // ExampleImpedanceSweeper instantiates a sweeper module and prepares
        // all settings for an impedance sweep over 30 data points.
        // The results are written to a file.
        public static void ExampleImpedanceSweeper(string dev = DEFAULT_DEVICE) // Timeout(40000)
        {
            ziDotNET daq = connect(dev);
            // This example only works for devices with installed
            // Impedance Analyzer (IA) option.
            if (!hasOption(daq, dev, "IA"))
            {
                daq.disconnect();
                Skip("Not supported by device.");
            }

            resetDeviceToDefault(daq, dev);
            // Enable impedance control
            daq.setInt(String.Format("/{0}/imps/0/enable", dev), 1);
            // Return D and Cs
            daq.setInt(String.Format("/{0}/imps/0/model", dev), 4);
            // Enable user compensation
            daq.setInt(String.Format("/{0}/imps/0/calib/user/enable", dev), 1);

            // ensure correct settings of order and oscselect
            daq.setInt(String.Format("/{0}/imps/0/demod/order", dev), 8);
            daq.setInt(String.Format("/{0}/imps/0/demod/oscselect", dev), 0);
            daq.sync();

            ziModule sweep = daq.sweeper();
            // Sweeper settings
            sweep.setByte("device", dev);
            sweep.setDouble("start", 1e3);
            sweep.setDouble("stop", 5e6);
            sweep.setDouble("samplecount", 30);
            sweep.setDouble("order", 8);
            sweep.setDouble("settling/inaccuracy", 0.0100000);
            sweep.setDouble("bandwidthcontrol", 2);
            sweep.setDouble("maxbandwidth", 10.0);
            sweep.setDouble("bandwidthoverlap", 1);
            sweep.setDouble("xmapping", 1);
            sweep.setDouble("omegasuppression", 100.0);
            sweep.setDouble("averaging/sample", 200);
            sweep.setDouble("averaging/time", 0.100);
            sweep.setDouble("averaging/tc", 20.0);
            String path = String.Format("/{0}/imps/0/sample", dev);
            sweep.subscribe(path);
            sweep.execute();
            while (!sweep.finished())
            {
                System.Threading.Thread.Sleep(100);
                double progress = sweep.progress() * 100;
                System.Diagnostics.Trace.WriteLine(progress, "Progress");
            }
            Lookup lookup = sweep.read();
            double[] grid = lookup[path][0].sweeperImpedanceWaves[0].grid;
            double[] x = lookup[path][0].sweeperImpedanceWaves[0].realz;
            double[] y = lookup[path][0].sweeperImpedanceWaves[0].imagz;
            double[] param0 = lookup[path][0].sweeperImpedanceWaves[0].param0;
            double[] param1 = lookup[path][0].sweeperImpedanceWaves[0].param1;
            UInt64[] flags = lookup[path][0].sweeperImpedanceWaves[0].flags;
            // Save measurement data to file
            String fileName = Environment.CurrentDirectory + "/impedance.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
            ZIChunkHeader header = lookup[path][0].header;
            // Raw system time is the number of microseconds since linux epoch
            file.WriteLine("Raw System Time: {0}", header.systemTime);
            // Use the utility function ziSystemTimeToDateTime to convert to DateTime of .NET
            file.WriteLine("Converted System Time: {0}", ziUtility.ziSystemTimeToDateTime(lookup[path][0].header.systemTime));
            file.WriteLine("Created Timestamp: {0}", header.createdTimeStamp);
            file.WriteLine("Changed Timestamp: {0}", header.changedTimeStamp);
            for (int i = 0; i < grid.Length; ++i)
            {
                file.WriteLine("{0} {1} {2} {3} {4} {5}",
                  grid[i],
                  x[i],
                  y[i],
                  param0[i],
                  param1[i],
                  flags[i]);
            }
            file.Close();

            AssertEqual(1.0, sweep.progress());
            AssertNotEqual(0, grid.Length);

            sweep.clear();  // Release module resources. Especially important if modules are created
                            // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        // ExampleImpedanceCompensation does a user compensation
        // of the impedance analyser.
        public static void ExampleImpedanceCompensation(string dev = DEFAULT_DEVICE) // Timeout(30000)
        {
            ziDotNET daq = connect(dev);
            // This example only works for devices with installed
            // Impedance Analyzer (IA) option.
            if (!hasOption(daq, dev, "IA"))
            {
                daq.disconnect();
                Skip("Not supported by device.");
            }

            resetDeviceToDefault(daq, dev);

            // Enable impedance control
            daq.setInt(String.Format("/{0}/imps/0/enable", dev), 1);
            ziModule calib = daq.impedanceModule();
            calib.execute();
            calib.setByte("device", dev);
            System.Threading.Thread.Sleep(200);
            calib.setInt("mode", 4);
            calib.setDouble("loads/2/r", 1000.0);
            calib.setDouble("loads/2/c", 0.0);
            calib.setDouble("freq/start", 100.0);
            calib.setDouble("freq/stop", 500e3);
            calib.setDouble("freq/samplecount", 21);

            daq.setInt(String.Format("/{0}/imps/0/demod/order", dev), 8);
            daq.setInt(String.Format("/{0}/imps/0/demod/oscselect", dev), 0);
            daq.sync();


            calib.setInt("step", 2);
            calib.setInt("calibrate", 1);
            while (true)
            {
                System.Threading.Thread.Sleep(100);
                double progress = calib.progress() * 100;
                System.Diagnostics.Trace.WriteLine(progress, "Progress");
                Int64 calibrate = calib.getInt("calibrate");
                if (calibrate == 0)
                {
                    break;
                }
            }
            String message = calib.getString("message");
            System.Diagnostics.Trace.WriteLine(message, "Message");
            AssertNotEqual(0, calib.progress());

            calib.clear();  // Release module resources. Especially important if modules are created
                            // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        // ExampleSpectrum instantiates the spectrum module,
        // reads the data and writes the result in to a file.
        public static void ExampleSpectrum(string dev = DEFAULT_DEVICE) // Timeout(20000)
        {
            ziDotNET daq = connect(dev);
            SkipForDeviceFamily(daq, dev, "HDAWG");
            resetDeviceToDefault(daq, dev);
            ziModule spectrum = daq.spectrum();
            spectrum.setByte("device", dev);
            spectrum.setInt("bit", 10);
            String path = String.Format("/{0}/demods/0/sample", dev);
            spectrum.subscribe(path);
            spectrum.execute();
            while (!spectrum.finished())
            {
                System.Threading.Thread.Sleep(100);
                double progress = spectrum.progress() * 100;
                System.Diagnostics.Trace.WriteLine(progress, "Progress");
            }
            Lookup lookup = spectrum.read();
            double[] grid = lookup[path][0].spectrumWaves[0].grid;
            double[] x = lookup[path][0].spectrumWaves[0].x;
            double[] y = lookup[path][0].spectrumWaves[0].y;
            String fileName = Environment.CurrentDirectory + "/spectrum.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
            for (int i = 0; i < grid.Length; ++i)
            {
                file.WriteLine("{0} {1} {2}", grid[i], x[i], y[i]);
            }
            file.Close();

            AssertEqual(1.0, spectrum.progress());
            AssertNotEqual(0, grid.Length);

            spectrum.clear();  // Release module resources. Especially important if modules are created
                               // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        // ExampleScopeModule instantiates a scope module.
        public static void ExampleScopeModule(string dev = DEFAULT_DEVICE) // Timeout(20000)
        {
            ziDotNET daq = connect(dev);
            if (isDeviceFamily(daq, dev, "HDAWG"))
            {
                daq.disconnect();
                Skip("Not supported by device.");
            }
            resetDeviceToDefault(daq, dev);
            ziModule scopeModule = daq.scopeModule();
            String path = String.Format("/{0}/scopes/0/wave", dev);
            scopeModule.subscribe(path);
            scopeModule.execute();
            // The HF2 devices do not have a single event functionality.
            if (!isDeviceFamily(daq, dev, "HF2"))
            {
                daq.setInt(String.Format("/{0}/scopes/0/single", dev), 1);
                daq.setInt(String.Format("/{0}/scopes/0/trigenable", dev), 0);
            }
            daq.setInt(String.Format("/{0}/scopes/0/enable", dev), 1);

            Lookup lookup;
            bool allSegments = false;
            do
            {
                System.Threading.Thread.Sleep(100);
                double progress = scopeModule.progress() * 100;
                System.Diagnostics.Trace.WriteLine(progress, "Progress");
                lookup = scopeModule.read();
                if (lookup.nodes.ContainsKey(path))
                {
                    ZIScopeWave[] scopeWaves = lookup[path][0].scopeWaves;
                    UInt64 totalSegments = scopeWaves[0].header.totalSegments;
                    UInt64 segmentNumber = scopeWaves[0].header.segmentNumber;
                    allSegments = (totalSegments == 0) ||
                                  (segmentNumber >= totalSegments - 1);
                }
            } while (!allSegments);
            ZIScopeWave[] scopeWaves1 = lookup[path][0].scopeWaves;
            float[,] wave = SimpleValue.getFloatVec2D(scopeWaves1[0].wave);
            // ...
            System.Diagnostics.Trace.WriteLine(wave.Length, "Wave Size");
            AssertNotEqual(0, wave.Length);

            scopeModule.clear();  // Release module resources. Especially important if modules are created
                                  // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        // ExampleDeviceSettings instantiates a deviceSettings module and performs a save
        // and load of device settings. The LabOne UI uses this module to save and
        // load the device settings.
        public static void ExampleDeviceSettings(string dev = DEFAULT_DEVICE) // Timeout(15000)
        {
            ziDotNET daq = connect(dev);
            resetDeviceToDefault(daq, dev);
            ziModule settings = daq.deviceSettings();
            // First save the current device settings
            settings.setString("device", dev);
            settings.setString("command", "save");
            settings.setString("filename", "test_settings");
            settings.setString("path", Environment.CurrentDirectory);
            settings.execute();
            while (!settings.finished())
            {
                System.Threading.Thread.Sleep(100);
            }
            // Remember the current device parameter for later comparison
            String path = String.Format("/{0}/oscs/0/freq", dev);
            Double originalValue = daq.getDouble(path);
            // Change the parameter
            daq.setDouble(path, 2 * originalValue);
            // Load device settings from file
            settings.setString("device", dev);
            settings.setString("command", "load");
            settings.setString("filename", "test_settings");
            settings.setString("path", Environment.CurrentDirectory);
            settings.execute();
            while (!settings.finished())
            {
                System.Threading.Thread.Sleep(100);
            }
            // Check the restored parameter
            Double newValue = daq.getDouble(path);

            AssertEqual(originalValue, newValue);

            settings.clear();  // Release module resources. Especially important if modules are created
                               // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        // ExamplePidAdvisor shows the usage of the PID advisor
        public static void ExamplePidAdvisor(string dev = DEFAULT_DEVICE) // Timeout(40000)
        {
            ziDotNET daq = connect(dev);
            if (!hasOption(daq, dev, "PID"))
            {
                daq.disconnect();
                Skip("Not supported by device.");
            }

            resetDeviceToDefault(daq, dev);

            daq.setInt(String.Format("/{0}/demods/*/rate", dev), 0);
            daq.setInt(String.Format("/{0}/demods/*/trigger", dev), 0);
            daq.setInt(String.Format("/{0}/sigouts/*/enables/*", dev), 0);
            daq.setInt(String.Format("/{0}/demods/*/enable", dev), 0);
            daq.setInt(String.Format("/{0}/scopes/*/enable", dev), 0);

            // now the settings relevant to this experiment
            // PID configuration.
            double target_bw = 10e3;    // Target bandwidth (Hz).
            int pid_input = 3;          // PID input (3 = Demod phase).
            int pid_input_channel = 0;  // Demodulator number.
            double setpoint = 0.0;      // Phase setpoint.
            int phase_unwrap = 1;       //
            int pid_output = 2;         // PID output (2 = oscillator frequency).
            int pid_output_channel = 0; // The index of the oscillator controlled by PID.
            double pid_center_frequency = 500e3;  // (Hz).
            double pid_limits = 10e3;            // (Hz).


            if (!isDeviceFamily(daq, dev, "HF2"))
            {
                daq.setInt(String.Format("/{0}/pids/0/input", dev), pid_input);
                daq.setInt(String.Format("/{0}/pids/0/inputchannel", dev), pid_input_channel);
                daq.setDouble(String.Format("/{0}/pids/0/setpoint", dev), setpoint);
                daq.setInt(String.Format("/{0}/pids/0/output", dev), pid_output);
                daq.setInt(String.Format("/{0}/pids/0/outputchannel", dev), pid_output_channel);
                daq.setDouble(String.Format("/{0}/pids/0/center", dev), pid_center_frequency);
                daq.setInt(String.Format("/{0}/pids/0/enable", dev), 0);
                daq.setInt(String.Format("/{0}/pids/0/phaseunwrap", dev), phase_unwrap);
                daq.setDouble(String.Format("/{0}/pids/0/limitlower", dev), -pid_limits);
                daq.setDouble(String.Format("/{0}/pids/0/limitupper", dev), pid_limits);
            }
            // Perform a global synchronisation between the device and the data server:
            // Ensure that the settings have taken effect on the device before starting
            // the pidAdvisor.
            daq.sync();

            // set up PID Advisor
            ziModule pidAdvisor = daq.pidAdvisor();

            // Turn off auto-calc on param change. Enabled
            // auto calculation can be used to automatically
            // update response data based on user input.
            pidAdvisor.setInt("auto", 0);
            pidAdvisor.setByte("device", dev);
            pidAdvisor.setDouble("pid/targetbw", target_bw);

            // PID advising mode (bit coded)
            // bit 0: optimize/tune P
            // bit 1: optimize/tune I
            // bit 2: optimize/tune D
            // Example: mode = 7: Optimize/tune PID
            pidAdvisor.setInt("pid/mode", 7);

            // PID index to use (first PID of device: 0)
            pidAdvisor.setInt("index", 0);

            // DUT model
            // source = 1: Lowpass first order
            // source = 2: Lowpass second order
            // source = 3: Resonator frequency
            // source = 4: Internal PLL
            // source = 5: VCO
            // source = 6: Resonator amplitude
            pidAdvisor.setInt("dut/source", 4);

            if (isDeviceFamily(daq, dev, "HF2"))
            {
                // Since the PLL and PID are 2 separate hardware units on the
                // device, we need to additionally specify that the PID
                // Advisor should model the HF2's PLL.
                pidAdvisor.setByte("pid/type", "pll");
            }

            // IO Delay of the feedback system describing the earliest response
            // for a step change. This parameter does not affect the shape of
            // the DUT transfer function
            pidAdvisor.setDouble("dut/delay", 0.0);

            // Other DUT parameters (not required for the internal PLL model)
            // pidAdvisor.setDouble('dut/gain', 1.0)
            // pidAdvisor.setDouble('dut/bw', 1000)
            // pidAdvisor.setDouble('dut/fcenter', 15e6)
            // pidAdvisor.setDouble('dut/damping', 0.1)
            // pidAdvisor.setDouble('dut/q', 10e3)

            // Start values for the PID optimization. Zero
            // values will imitate a guess. Other values can be
            // used as hints for the optimization process.
            pidAdvisor.setDouble("pid/p", 0);
            pidAdvisor.setDouble("pid/i", 0);
            pidAdvisor.setDouble("pid/d", 0);
            pidAdvisor.setInt("calculate", 0);

            // Start the module thread
            pidAdvisor.execute();
            System.Threading.Thread.Sleep(1000);

            // Advise
            pidAdvisor.setInt("calculate", 1);
            System.Diagnostics.Trace.WriteLine(
              "Starting advising. Optimization process may run up to a minute...");

            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                double progress = pidAdvisor.progress() * 100;
                System.Diagnostics.Trace.WriteLine(progress, "Progress");
                System.Threading.Thread.Sleep(1000);
                Int64 calc = pidAdvisor.getInt("calculate");
                if (calc == 0)
                {
                    break;
                }
            }

            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;

            System.Diagnostics.Trace.WriteLine(
              String.Format("Advice took {0} s.", watch.ElapsedMilliseconds / 1000.0));

            // Get the advised values
            double p_adv = pidAdvisor.getDouble("pid/p");
            double i_adv = pidAdvisor.getDouble("pid/i");
            double d_adv = pidAdvisor.getDouble("pid/d");
            double dlimittimeconstant_adv =
              pidAdvisor.getDouble("pid/dlimittimeconstant");
            double rate_adv = pidAdvisor.getDouble("pid/rate");
            double bw_adv = pidAdvisor.getDouble("bw");

            System.Diagnostics.Trace.WriteLine(p_adv, "P");
            System.Diagnostics.Trace.WriteLine(i_adv, "I");
            System.Diagnostics.Trace.WriteLine(d_adv, "D");
            System.Diagnostics.Trace.WriteLine(dlimittimeconstant_adv, "D_tc");
            System.Diagnostics.Trace.WriteLine(rate_adv, "rate");
            System.Diagnostics.Trace.WriteLine(bw_adv, "bw");

            // copy the values from the Advisor to the device
            pidAdvisor.setInt("todevice", 1);

            // Get all calculated parameters.
            Lookup result = pidAdvisor.get("*");

            // extract bode plot and step response
            double[] grid = result["/bode"][0].advisorWaves[0].grid;
            double[] x = result["/bode"][0].advisorWaves[0].x;
            double[] y = result["/bode"][0].advisorWaves[0].y;
            String fileName = Environment.CurrentDirectory + "/pidAdvisor.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
            for (int i = 0; i < grid.Length; ++i)
            {
                file.WriteLine("{0} {1} {2}", grid[i], x[i], y[i]);
            }
            file.Close();

            AssertEqual(1.0, pidAdvisor.progress());
            AssertNotEqual(0, grid.Length);

            pidAdvisor.clear();  // Release module resources. Especially important if modules are created
                                 // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        static double Sinc(double x)
        {
            return x != 0.0 ? Math.Sin(Math.PI * x) / (Math.PI * x) : 1.0;
        }

        // ExampleAwgModule shows the usage of the AWG module.
        // It uses the AWG sequencer to generate a wave form.
        // The defined waveform is applied, measured and the
        // results are written to a file.
        public static void ExampleAwgModule(string dev = DEFAULT_DEVICE) // Timeout(10000)
        {
            ziDotNET daq = connect(dev);
            resetDeviceToDefault(daq, dev);
            // check device type, option
            if (!isDeviceFamily(daq, dev, "UHFAWG") &&
                !isDeviceFamily(daq, dev, "UHFQA") &&
                !hasOption(daq, dev, "AWG"))
            {
                Skip("Test does not support this device.");
            }
            // Create instrument configuration: disable all outputs, demods and scopes.
            const ZIListNodes_enum flags = ZIListNodes_enum.ZI_LIST_NODES_LEAVESONLY;
            var hasDemods = daq.listNodes(String.Format("/{0}/demods/*", dev), flags).Count > 0;
            if (hasDemods)
            {
                daq.setInt(String.Format("/{0}/demods/*/enable", dev), 0);
                daq.setInt(String.Format("/{0}/demods/*/trigger", dev), 0);
            }

            daq.setInt(String.Format("/{0}/sigouts/*/enables/*", dev), 0);
            daq.setInt(String.Format("/{0}/scopes/*/enable", dev), 0);
            if (hasOption(daq, dev, "IA"))
            {
                daq.setInt(String.Format("/{0}/imps/*/enable", dev), 0);
            }
            daq.sync();

            // Now configure the instrument for this experiment. The following channels
            // and indices work on all device configurations. The values below may be
            // changed if the instrument has multiple input/output channels and/or either
            // the Multifrequency or Multidemodulator options installed.
            int in_channel = 0;
            double frequency = 1e6;
            double amp = 1.0;

            daq.setDouble(String.Format("/{0}/sigouts/0/amplitudes/*", dev), 0.0);
            daq.sync();

            daq.setInt(String.Format("/{0}/sigins/0/imp50", dev), 1);
            daq.setInt(String.Format("/{0}/sigins/0/ac", dev), 0);
            daq.setInt(String.Format("/{0}/sigins/0/diff", dev), 0);
            daq.setInt(String.Format("/{0}/sigins/0/range", dev), 1);
            daq.setDouble(String.Format("/{0}/oscs/0/freq", dev), frequency);
            daq.setInt(String.Format("/{0}/sigouts/0/on", dev), 1);
            daq.setInt(String.Format("/{0}/sigouts/0/range", dev), 1);
            daq.setDouble(String.Format("/{0}/awgs/0/outputs/0/amplitude", dev), amp);
            daq.setInt(String.Format("/{0}/awgs/0/outputs/0/mode", dev), 0);
            daq.setInt(String.Format("/{0}/awgs/0/time", dev), 0);
            daq.setInt(String.Format("/{0}/awgs/0/userregs/0", dev), 0);

            daq.sync();

            // Number of points in AWG waveform
            int AWG_N = 2000;

            // Define an AWG program as a string stored in the variable awg_program, equivalent to what would
            // be entered in the Sequence Editor window in the graphical UI.
            // This example demonstrates four methods of definig waveforms via the API
            // - (wave w0) loaded directly from programmatically generated CSV file wave0.csv.
            //             Waveform shape: Blackman window with negative amplitude.
            // - (wave w1) using the waveform generation functionalities available in the AWG Sequencer language.
            //             Waveform shape: Gaussian function with positive amplitude.
            // - (wave w2) using the vect() function and programmatic string replacement.
            //             Waveform shape: Single period of a sine wave.
            string awg_program =
              "const AWG_N = _c1_;\n" +
              "wave w0 = \"wave0\";\n" +
              "wave w1 = gauss(AWG_N, AWG_N/2, AWG_N/20);\n" +
              "wave w2 = vect(_w2_);\n" +
              "wave w3 = zeros(AWG_N);\n" +
              "setTrigger(1);\n" +
              "setTrigger(0);\n" +
              "playWave(w0);\n" +
              "playWave(w1);\n" +
              "playWave(w2);\n" +
              "playWave(w3);\n";

            // Reference waves

            // Define an array of values that are used to write values for wave w0 to a CSV file in the
            // module's data directory (Blackman windows)
            var waveform_0 = Enumerable.Range(0, AWG_N).Select(
              v => -1.0 * (0.42 - 0.5 * Math.Cos(2.0 * Math.PI * v / (AWG_N - 1)) + 0.08 * Math.Cos(4 * Math.PI * v / (AWG_N - 1))));
            double width = AWG_N / 20;
            var linspace = Enumerable.Range(0, AWG_N).Select(
              v => (v * AWG_N / ((double)AWG_N - 1.0d)) - AWG_N / 2);
            var waveform_1 = linspace.Select(
              v => Math.Exp(-v * v / (2 * width * width)));
            linspace = Enumerable.Range(0, AWG_N).Select(
              v => (v * 2 * Math.PI / ((double)AWG_N - 1.0d)));
            var waveform_2 = linspace.Select(
              v => Math.Sin(v));
            linspace = Enumerable.Range(0, AWG_N).Select(
              v => (v * 12 * Math.PI / ((double)AWG_N - 1.0d)) - 6 * Math.PI);
            var waveform_3 = linspace.Select(
              v => Sinc(v));

            // concatenated reference wave
            double f_s = 1.8e9; // sampling rate of scope and AWG
            double full_scale = 0.75;
            var y_expected = waveform_0.Concat(waveform_1).Concat(waveform_2).Concat(waveform_3).Select(
              v => v * full_scale * amp).ToArray();
            var x_expected = Enumerable.Range(0, 4 * AWG_N).Select(v => v / f_s).ToArray();

            // Replace placeholders in program
            awg_program = awg_program.Replace("_w2_", string.Join(",", waveform_2));
            awg_program = awg_program.Replace("_c1_", AWG_N.ToString());

            // Create an instance of the AWG Module
            ziModule awgModule = daq.awgModule();
            awgModule.setByte("device", dev);
            awgModule.execute();


            // Get the modules data directory
            string data_dir = awgModule.getString("directory");
            // All CSV files within the waves directory are automatically recognized by the AWG module
            data_dir = data_dir + "\\awg\\waves";
            if (!Directory.Exists(data_dir))
            {
                // The data directory is created by the AWG module and should always exist. If this exception is raised,
                // something might be wrong with the file system.
                Fail($"AWG module wave directory {data_dir} does not exist or is not a directory");
            }
            // Save waveform data to CSV
            string csv_file = data_dir + "\\wave0.csv";
            // The following line always formats a double as "3.14" and not "3,14".
            var waveform_0_formatted = waveform_0.Select(v => v.ToString(CultureInfo.InvariantCulture));
            File.WriteAllText(@csv_file, string.Join(",", waveform_0_formatted));

            // Transfer the AWG sequence program. Compilation starts automatically.
            // Note: when using an AWG program from a source file (and only then), the
            //       compiler needs to be started explicitly with
            //       awgModule.set("compiler/start", 1)
            awgModule.setByte("compiler/sourcestring", awg_program);
            while (awgModule.getInt("compiler/status") == -1)
            {
                System.Threading.Thread.Sleep(100);
            }

            // check compiler result
            long status = awgModule.getInt("compiler/status");
            if (status == 1)
            {
                // compilation failed
                String message = awgModule.getString("compiler/statusstring");
                System.Diagnostics.Trace.WriteLine("AWG Program:");
                System.Diagnostics.Trace.WriteLine(awg_program);
                System.Diagnostics.Trace.WriteLine("---");
                System.Diagnostics.Trace.WriteLine(message, "Compiler message:");
                Fail("Compilation failed");
            }
            if (status == 0)
            {
                System.Diagnostics.Trace.WriteLine("Compilation successful with no warnings" +
                  ", will upload the program to the instrument.");
            }
            if (status == 2)
            {
                System.Diagnostics.Trace.WriteLine("Compilation successful with warnings" +
                  ", will upload the program to the instrument.");
                String message = awgModule.getString("compiler/statusstring");
                System.Diagnostics.Trace.WriteLine("Compiler warning:");
                System.Diagnostics.Trace.WriteLine(message);
            }

            // wait for waveform upload to finish
            while (awgModule.getDouble("progress") < 1.0)
            {
                System.Diagnostics.Trace.WriteLine(
                  awgModule.getDouble("progress"), "Progress");
                System.Threading.Thread.Sleep(100);
            }

            // Replace w3 with waveform_3 using vector write.
            // Let N be the total number of waveforms and M>0 be the number of waveforms defined from CSV file. Then the index
            // of the waveform to be replaced is defined as following:
            // - 0,...,M-1 for all waveforms defined from CSV file alphabetically ordered by filename,
            // - M,...,N-1 in the order that the waveforms are defined in the sequencer program.
            // For the case of M=0, the index is defined as:
            // - 0,...,N-1 in the order that the waveforms are defined in the sequencer program.
            // Of course, for the trivial case of 1 waveform, use index=0 to replace it.
            // Here we replace waveform w3, the 4th waveform defined in the sequencer program. Using 0-based indexing the
            // index of the waveform we want to replace (w3, a vector of zeros) is 3:
            // Write the waveform to the memory. For the transferred array, only 16-bit unsigned integer
            // data (0...65536) is accepted.
            // For dual-channel waves, interleaving is required.

            // The following function corresponds to ziPython utility function 'convert_awg_waveform'.
            Func<double, ushort> convert_awg_waveform = v => (ushort)((32767.0) * v);
            daq.setVector(String.Format("/{0}/awgs/0/waveform/waves/3", dev), waveform_3.Select(convert_awg_waveform).ToArray());

            // Configure the Scope for measurement
            daq.setInt(
              String.Format("/{0}/scopes/0/channels/0/inputselect", dev), in_channel);
            daq.setInt(String.Format("/{0}/scopes/0/time", dev), 0);
            daq.setInt(String.Format("/{0}/scopes/0/enable", dev), 0);
            daq.setInt(String.Format("/{0}/scopes/0/length", dev), 16836);

            // Now configure the scope's trigger to get aligned data.
            daq.setInt(String.Format("/{0}/scopes/0/trigenable", dev), 1);
            // Here we trigger on UHF signal input 1. If the instrument has the DIG Option installed we could
            // trigger the scope using an AWG Trigger instead (see the `setTrigger(1);` line in `awg_program` above).
            // 0:   Signal Input 1
            // 192: AWG Trigger 1
            long trigchannel = 0;
            daq.setInt(String.Format("/{0}/scopes/0/trigchannel", dev), trigchannel);
            if (trigchannel == 0)
            {
                // Trigger on the falling edge of the negative blackman waveform `w0` from our AWG program.
                daq.setInt(String.Format("/{0}/scopes/0/trigslope", dev), 2);
                daq.setDouble(String.Format("/{0}/scopes/0/triglevel", dev), -0.600);

                // Set hysteresis triggering threshold to avoid triggering on noise
                // 'trighysteresis/mode' :
                //  0 - absolute, use an absolute value ('scopes/0/trighysteresis/absolute')
                //  1 - relative, use a relative value ('scopes/0trighysteresis/relative') of the trigchannel's input range
                //      (0.1=10%).
                daq.setDouble(String.Format("/{0}/scopes/0/trighysteresis/mode", dev), 0);
                daq.setDouble(String.Format("/{0}/scopes/0/trighysteresis/relative", dev), 0.025);

                // Set a negative trigdelay to capture the beginning of the waveform.
                daq.setDouble(String.Format("/{0}/scopes/0/trigdelay", dev), -1.0e-6);
            }
            else
            {
                // Assume we're using an AWG Trigger, then the scope configuration is simple: Trigger on rising edge.
                daq.setInt(String.Format("/{0}/scopes/0/trigslope", dev), 1);

                // Set trigdelay to 0.0: Start recording from when the trigger is activated.
                daq.setDouble(String.Format("/{0}/scopes/0/trigdelay", dev), 0.0);
            }

            // the trigger reference position relative within the wave, a value of 0.5 corresponds to the center of the wave
            daq.setDouble(String.Format("/{0}/scopes/0/trigreference", dev), 0.0);

            // Set the hold off time in-between triggers.
            daq.setDouble(String.Format("/{0}/scopes/0/trigholdoff", dev), 0.025);

            // Set up the Scope Module.
            ziModule scopeModule = daq.scopeModule();
            scopeModule.setInt("mode", 1);
            scopeModule.subscribe(String.Format("/{0}/scopes/0/wave", dev));
            daq.setInt(String.Format("/{0}/scopes/0/single", dev), 1);
            scopeModule.execute();
            daq.setInt(String.Format("/{0}/scopes/0/enable", dev), 1);
            daq.sync();
            System.Threading.Thread.Sleep(100);

            // Start the AWG in single-shot mode
            daq.setInt(String.Format("/{0}/awgs/0/single", dev), 1);
            daq.setInt(String.Format("/{0}/awgs/0/enable", dev), 1);

            // Read the scope data (manual timeout of 1 second)
            double local_timeout = 1.0;
            while (scopeModule.progress() < 1.0 && local_timeout > 0.0)
            {
                System.Diagnostics.Trace.WriteLine(
                  scopeModule.progress() * 100.0, "Scope Progress");
                System.Threading.Thread.Sleep(20);
                local_timeout -= 0.02;
            }
            string path = String.Format("/{0}/scopes/0/wave", dev);
            Lookup lookup = scopeModule.read();
            ZIScopeWave[] scopeWaves1 = lookup[path][0].scopeWaves;
            float[,] y_measured_in = SimpleValue.getFloatVec2D(scopeWaves1[0].wave);
            float[] y_measured = new float[y_measured_in.Length];
            for (int i = 0; i < y_measured_in.Length; ++i)
            {
                y_measured[i] = y_measured_in[0, i];
            }

            var x_measured = Enumerable.Range(0, y_measured.Length).Select(
              v => -(long)v * scopeWaves1[0].header.dt +
                (scopeWaves1[0].header.timeStamp -
                scopeWaves1[0].header.triggerTimeStamp) / f_s
                ).ToArray();

            // write signals to files
            String fileName = Environment.CurrentDirectory + "/awg_measured.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
            file.WriteLine("t [ns], measured signal [V]");
            for (int i = 0; i < y_measured.Length; ++i)
            {
                file.WriteLine("{0} {1}", x_measured[i] * 1e9, y_measured[i]);
            }
            file.Close();

            fileName = Environment.CurrentDirectory + "/awg_expected.txt";
            file = new System.IO.StreamWriter(fileName);
            file.WriteLine("t [ns], expected signal [V]");
            for (int i = 0; i < y_expected.Length; ++i)
            {
                file.WriteLine("{0} {1}", x_expected[i] * 1e9, y_expected[i]);
            }
            file.Close();

            // checks
            AssertNotEqual(0, x_measured.Length);
            AssertNotEqual(0, y_measured.Length);

            // find minimal difference
            double dMinMax = 1e10;
            for (int i = 0; i < x_measured.Length - x_expected.Length; i++)
            {
                double dMax = 0;
                for (int k = 0; k < x_expected.Length; k++)
                {
                    double d = Math.Abs(y_expected[k] - y_measured[k + i]);
                    if (d > dMax)
                    {
                        dMax = d;
                    }
                }

                if (dMax < dMinMax)
                {
                    dMinMax = dMax;
                }
            }
            Debug.Assert(dMinMax < 0.1);

            scopeModule.clear();  // Release module resources. Especially important if modules are created
                                  // inside a loop to prevent excessive resource consumption.
            awgModule.clear();
            daq.disconnect();
        }

        // ExampleAutorangingImpedance shows how to perform a manually triggered autoranging for impedance while working in manual range mode.
        public static void ExampleAutorangingImpedance(string dev = DEFAULT_DEVICE) // Timeout(25000)
        {
            ziDotNET daq = connect(dev);
            // check device type, option
            SkipRequiresOption(daq, dev, "IA");

            resetDeviceToDefault(daq, dev);
            // Create instrument configuration: disable all outputs, demods and scopes.
            daq.setInt(String.Format("/{0}/demods/*/enable", dev), 0);
            daq.setInt(String.Format("/{0}/demods/*/trigger", dev), 0);
            daq.setInt(String.Format("/{0}/sigouts/*/enables/*", dev), 0);
            daq.setInt(String.Format("/{0}/scopes/*/enable", dev), 0);
            daq.setInt(String.Format("/{0}/imps/*/enable", dev), 0);
            daq.sync();

            int imp = 0;
            long curr = daq.getInt(String.Format("/{0}/imps/{1}/current/inputselect", dev, imp));
            long volt = daq.getInt(String.Format("/{0}/imps/{1}/voltage/inputselect", dev, imp));
            double manCurrRange = 10e-3;
            double manVoltRange = 10e-3;

            // Now configure the instrument for this experiment. The following channels and indices work on all devices with IA option.
            // The values below may be changed if the instrument has multiple IA modules.
            daq.setInt(String.Format("/{0}/imps/{1}/enable", dev, imp), 1);
            daq.setInt(String.Format("/{0}/imps/{1}/mode", dev, imp), 0);
            daq.setInt(String.Format("/{0}/imps/{1}/auto/output", dev, imp), 1);
            daq.setInt(String.Format("/{0}/imps/{1}/auto/bw", dev, imp), 1);
            daq.setDouble(String.Format("/{0}/imps/{1}/freq", dev, imp), 500);
            daq.setInt(String.Format("/{0}/imps/{1}/auto/inputrange", dev, imp), 0);
            daq.setDouble(String.Format("/{0}/currins/{1}/range", dev, curr), manCurrRange);
            daq.setDouble(String.Format("/{0}/sigins/{1}/range", dev, volt), manVoltRange);
            daq.sync();

            // After setting the device in manual ranging mode we want to trigger manually a one time auto ranging to find a suitable range.
            // Therefore, we trigger the  auto ranging for the current input as well as for the voltage input.
            daq.setInt(String.Format("/{0}/currins/{1}/autorange", dev, curr), 1);
            daq.setInt(String.Format("/{0}/sigins/{1}/autorange", dev, volt), 1);

            // The auto ranging takes some time. We do not want to continue before the best range is found.
            // Therefore, we implement a loop to check if the auto ranging is finished.
            int count = 0;
            System.Threading.Thread.Sleep(100);
            bool finished = false;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (!finished)
            {
                ++count;
                System.Threading.Thread.Sleep(500);
                finished = (daq.getInt(String.Format("/{0}/currins/{1}/autorange", dev, curr)) == 0 &&
                            daq.getInt(String.Format("/{0}/sigins/{1}/autorange", dev, volt)) == 0);
            }
            watch.Stop();
            System.Diagnostics.Trace.WriteLine(
              String.Format("Auto ranging finished after {0} s.", watch.ElapsedMilliseconds / 1e3));

            double autoCurrRange = daq.getDouble(String.Format("/{0}/currins/{1}/range", dev, curr));
            double autoVoltRange = daq.getDouble(String.Format("/{0}/sigins/{1}/range", dev, volt));
            System.Diagnostics.Trace.WriteLine(
              String.Format("Current range changed from {0} A to {1} A.", manCurrRange, autoCurrRange));
            System.Diagnostics.Trace.WriteLine(
              String.Format("Voltage range changed from {0} A to {1} A.", manVoltRange, autoVoltRange));
            Debug.Assert(count > 1);
        }

        // ExampleDataAcquisition uses the new data acquisition module to record data
        // and writes the result in to a file.
        public static void ExampleDataAcquisition(string dev = DEFAULT_DEVICE) // Timeout(20000)
        {
            ziDotNET daq = connect(dev);

            SkipForDeviceFamilyAndOption(daq, dev, "MF", "MD");
            SkipForDeviceFamilyAndOption(daq, dev, "HF2", "MD");
            SkipForDeviceFamily(daq, dev, "HDAWG");

            resetDeviceToDefault(daq, dev);
            daq.setInt(String.Format("/{0}/demods/0/oscselect", dev), 0);
            daq.setInt(String.Format("/{0}/demods/1/oscselect", dev), 1);
            daq.setDouble(String.Format("/{0}/oscs/0/freq", dev), 2e6);
            daq.setDouble(String.Format("/{0}/oscs/1/freq", dev), 2.0001e6);
            daq.setInt(String.Format("/{0}/sigouts/0/enables/*", dev), 0);
            daq.setInt(String.Format("/{0}/sigouts/0/enables/0", dev), 1);
            daq.setInt(String.Format("/{0}/sigouts/0/enables/1", dev), 1);
            daq.setInt(String.Format("/{0}/sigouts/0/on", dev), 1);
            daq.setDouble(String.Format("/{0}/sigouts/0/amplitudes/0", dev), 0.2);
            daq.setDouble(String.Format("/{0}/sigouts/0/amplitudes/1", dev), 0.2);
            ziModule trigger = daq.dataAcquisitionModule();
            trigger.setInt("grid/mode", 4);
            double demodRate = daq.getDouble(String.Format("/{0}/demods/0/rate", dev));
            double duration = trigger.getDouble("duration");
            Int64 sampleCount = System.Convert.ToInt64(demodRate * duration);
            trigger.setInt("grid/cols", sampleCount);
            trigger.setByte("device", dev);
            trigger.setInt("type", 1);
            trigger.setDouble("level", 0.1);
            trigger.setDouble("hysteresis", 0.01);
            trigger.setDouble("bandwidth", 0.0);
            String path = String.Format("/{0}/demods/0/sample.r", dev);
            trigger.subscribe(path);
            String triggerPath = String.Format("/{0}/demods/0/sample.R", dev);
            trigger.setByte("triggernode", triggerPath);
            trigger.execute();
            while (!trigger.finished())
            {
                System.Threading.Thread.Sleep(100);
                double progress = trigger.progress() * 100;
                System.Diagnostics.Trace.WriteLine(progress, "Progress");
            }
            Lookup lookup = trigger.read();
            ZIDoubleData[] demodSample = lookup[path][0].doubleData;
            String fileName = Environment.CurrentDirectory + "/dataacquisition.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);
            ZIChunkHeader header = lookup[path][0].header;
            // Raw system time is the number of microseconds since linux epoch
            file.WriteLine("Raw System Time: {0}", header.systemTime);
            // Use the utility function ziSystemTimeToDateTime to convert to DateTime of .NET
            file.WriteLine("Converted System Time: {0}", ziUtility.ziSystemTimeToDateTime(lookup[path][0].header.systemTime));
            file.WriteLine("Created Timestamp: {0}", header.createdTimeStamp);
            file.WriteLine("Changed Timestamp: {0}", header.changedTimeStamp);
            file.WriteLine("Flags: {0}", header.flags);
            file.WriteLine("Name: {0}", header.name);
            file.WriteLine("Status: {0}", header.status);
            file.WriteLine("Group Index: {0}", header.groupIndex);
            file.WriteLine("Color: {0}", header.color);
            file.WriteLine("Active Row: {0}", header.activeRow);
            file.WriteLine("Trigger Number: {0}", header.triggerNumber);
            file.WriteLine("Grid Rows: {0}", header.gridRows);
            file.WriteLine("Grid Cols: {0}", header.gridCols);
            file.WriteLine("Grid Mode: {0}", header.gridMode);
            file.WriteLine("Grid Operation: {0}", header.gridOperation);
            file.WriteLine("Grid Direction: {0}", header.gridDirection);
            file.WriteLine("Grid Repetitions: {0}", header.gridRepetitions);
            file.WriteLine("Grid Col Delta: {0}", header.gridColDelta);
            file.WriteLine("Grid Col Offset: {0}", header.gridColOffset);
            file.WriteLine("Bandwidth: {0}", header.bandwidth);
            file.WriteLine("Center: {0}", header.center);
            file.WriteLine("NENBW: {0}", header.nenbw);
            for (int i = 0; i < demodSample.Length; ++i)
            {
                file.WriteLine("{0}", demodSample[i].value);
            }
            file.Close();

            AssertEqual(1, trigger.progress());
            AssertNotEqual(0, demodSample.Length);

            trigger.clear();  // Release module resources. Especially important if modules are created
                              // inside a loop to prevent excessive resource consumption.
            daq.disconnect();
        }

        // ExampleMultiDeviceDataAcquisition
        //
        // Run the example: Capture demodulator data from two devices using the Data Acquisition module.
        // The devices are first synchronized using the MultiDeviceSync Module.
        //
        // Hardware configuration:
        // The cabling of the instruments must follow the MDS cabling depicted in
        // the MDS tab of LabOne.
        // Additionally, Signal Out 1 of the leader device is split into Signal In 1 of the leader and follower.
        //
        // ATTENTION: test ignored because it requires special device setup
        public void SKIP_MULTIDEVICE_ExampleMultiDeviceDataAcquisition() // Timeout(25000)
        {
            String[] device_ids = { "dev3133", "dev3144" };

            ziDotNET daq = new ziDotNET();
            daq.init("localhost", 8004, zhinst.ZIAPIVersion_enum.ZI_API_VERSION_6);
            apiServerVersionCheck(daq);
            daq.connectDevice(device_ids[0], "1gbe", "");
            daq.connectDevice(device_ids[1], "1gbe", "");

            // Create instrument configuration: disable all outputs, demods and scopes.
            foreach (String dev in device_ids)
            {
                daq.setInt(String.Format("/{0}/demods/*/enable", dev), 0);
                daq.setInt(String.Format("/{0}/demods/*/trigger", dev), 0);
                daq.setInt(String.Format("/{0}/sigouts/*/enables/*", dev), 0);
                daq.setInt(String.Format("/{0}/scopes/*/enable", dev), 0);
                daq.setInt(String.Format("/{0}/imps/*/enable", dev), 0);
                daq.sync();
            }

            System.Diagnostics.Trace.WriteLine("Synchronizing devices " + String.Join(",", device_ids) + "...\n");

            ziModule mds = daq.multiDeviceSyncModule();
            mds.setInt("start", 0);
            mds.setInt("group", 0);
            mds.execute();
            mds.setString("devices", String.Join(",", device_ids));
            mds.setInt("start", 1);

            // Wait for MDS to complete
            double local_timeout = 20.0;
            long status = 0;
            while (status != 2 && local_timeout > 0.0)
            {
                status = mds.getInt("status");
                System.Threading.Thread.Sleep(100);
                local_timeout -= 0.1;
            }

            if (status != 2)
            {
                System.Diagnostics.Trace.WriteLine("Error during synchronization.\n");
                Fail();
            }
            System.Diagnostics.Trace.WriteLine("Devices successfully synchronized.");

            // Device settings
            int demod_c = 0; // demod channel, for paths on the device
            int out_c = 0;  // signal output channel
            int out_mixer_c = 0;
            int in_c = 0;  // signal input channel
            int osc_c = 0;  // oscillator

            double time_constant = 1.0e-3;  // [s]
            double demod_rate = 10e3;  // [Sa/s]
            int filter_order = 8;
            double osc_freq = 1e3;  // [Hz]
            double out_amp = 0.600;   // [V]

            // Device settings
            foreach (String dev in device_ids)
            {
                daq.setDouble(String.Format("/{0}/demods/{1}/phaseshift", dev, demod_c), 0);
                daq.setInt(String.Format("/{0}/demods/{1}/order", dev, demod_c), filter_order);
                daq.setDouble(String.Format("/{0}/demods/{1}/rate", dev, demod_c), demod_rate);
                daq.setInt(String.Format("/{0}/demods/{1}/harmonic", dev, demod_c), 1);
                daq.setInt(String.Format("/{0}/demods/{1}/enable", dev, demod_c), 1);
                daq.setInt(String.Format("/{0}/demods/{1}/oscselect", dev, demod_c), osc_c);
                daq.setInt(String.Format("/{0}/demods/{1}/adcselect", dev, demod_c), in_c);
                daq.setDouble(String.Format("/{0}/demods/{1}/timeconstant", dev, demod_c), time_constant);
                daq.setDouble(String.Format("/{0}/oscs/{1}/freq", dev, osc_c), osc_freq);
                daq.setInt(String.Format("/{0}/sigins/{1}/imp50", dev, in_c), 1);
                daq.setInt(String.Format("/{0}/sigins/{1}/ac", dev, in_c), 0);
                daq.setDouble(String.Format("/{0}/sigins/{1}/range", dev, in_c), out_amp / 2);

            }
            // settings on leader
            daq.setInt(String.Format("/{0}/sigouts/{1}/on", device_ids[0], out_c), 1);
            daq.setDouble(String.Format("/{0}/sigouts/{1}/range", device_ids[0], out_c), 1);
            daq.setDouble(String.Format("/{0}/sigouts/{1}/amplitudes/{2}", device_ids[0], out_c, out_mixer_c), out_amp);
            daq.setDouble(String.Format("/{0}/sigouts/{1}/enables/{2}", device_ids[0], out_c, out_mixer_c), 0);

            // Synchronization
            daq.sync();

            // measuring the transient state of demodulator filters using DAQ module

            // DAQ module
            // Create a Data Acquisition Module instance, the return argument is a handle to the module
            ziModule daqMod = daq.dataAcquisitionModule();
            // Configure the Data Acquisition Module
            // Device on which trigger will be performed
            daqMod.setString("device", device_ids[0]);
            // The number of triggers to capture (if not running in endless mode).
            daqMod.setInt("count", 1);
            daqMod.setInt("endless", 0);
            // 'grid/mode' - Specify the interpolation method of
            //   the returned data samples.
            //
            // 1 = Nearest. If the interval between samples on the grid does not match
            //     the interval between samples sent from the device exactly, the nearest
            //     sample (in time) is taken.
            //
            // 2 = Linear interpolation. If the interval between samples on the grid does
            //     not match the interval between samples sent from the device exactly,
            //     linear interpolation is performed between the two neighbouring
            //     samples.
            //
            // 4 = Exact. The subscribed signal with the highest sampling rate (as sent
            //     from the device) defines the interval between samples on the DAQ
            //     Module's grid. If multiple signals are subscribed, these are
            //     interpolated onto the grid (defined by the signal with the highest
            //     rate, "highest_rate"). In this mode, duration is
            //     read-only and is defined as num_cols/highest_rate.
            int grid_mode = 2;
            daqMod.setInt("grid/mode", grid_mode);
            //   type:
            //     NO_TRIGGER = 0
            //     EDGE_TRIGGER = 1
            //     DIGITAL_TRIGGER = 2
            //     PULSE_TRIGGER = 3
            //     TRACKING_TRIGGER = 4
            //     HW_TRIGGER = 6
            //     TRACKING_PULSE_TRIGGER = 7
            //     EVENT_COUNT_TRIGGER = 8
            daqMod.setInt("type", 1);
            //   triggernode, specify the triggernode to trigger on.
            //     SAMPLE.X = Demodulator X value
            //     SAMPLE.Y = Demodulator Y value
            //     SAMPLE.R = Demodulator Magnitude
            //     SAMPLE.THETA = Demodulator Phase
            //     SAMPLE.AUXIN0 = Auxilliary input 1 value
            //     SAMPLE.AUXIN1 = Auxilliary input 2 value
            //     SAMPLE.DIO = Digital I/O value
            string triggernode = String.Format("/{0}/demods/{1}/sample.r", device_ids[0], demod_c);
            daqMod.setString("triggernode", triggernode);
            //   edge:
            //     POS_EDGE = 1
            //     NEG_EDGE = 2
            //     BOTH_EDGE = 3
            daqMod.setInt("edge", 1);
            demod_rate = daq.getDouble(String.Format("/{0}/demods/{1}/rate", device_ids[0], demod_c));
            // Exact mode: To preserve our desired trigger duration, we have to set
            // the number of grid columns to exactly match.
            double trigger_duration = time_constant * 30;
            int sample_count = Convert.ToInt32(demod_rate * trigger_duration);
            daqMod.setInt("grid/cols", sample_count);
            // The length of each trigger to record (in seconds).
            daqMod.setDouble("duration", trigger_duration);
            daqMod.setDouble("delay", -trigger_duration / 4);
            // Do not return overlapped trigger events.
            daqMod.setDouble("holdoff/time", 0);
            daqMod.setDouble("holdoff/count", 0);
            daqMod.setDouble("level", out_amp / 6);
            // The hysterisis is effectively a second criteria (if non-zero) for triggering
            // and makes triggering more robust in noisy signals. When the trigger `level`
            // is violated, then the signal must return beneath (for positive trigger edge)
            // the hysteresis value in order to trigger.
            daqMod.setDouble("hysteresis", 0.01);
            // synchronizing the settings
            daq.sync();

            // Recording

            // Subscribe to the demodulators
            daqMod.unsubscribe("*");
            foreach (String dev in device_ids)
            {
                string node = String.Format("/{0}/demods/{1}/sample.r", dev, demod_c);
                daqMod.subscribe(node);
            }

            // Execute the module
            daqMod.execute();
            // Send a trigger
            daq.setDouble(String.Format("/{0}/sigouts/{1}/enables/{2}", device_ids[0], out_c, out_mixer_c), 1);
            while (!daqMod.finished())
            {
                System.Threading.Thread.Sleep(1000);
                System.Diagnostics.Trace.WriteLine(String.Format("Progress {0}", daqMod.progress()));
            }

            // Read the result
            Lookup result = daqMod.read();

            // Turn off the trigger
            daq.setDouble(String.Format("/{0}/sigouts/{1}/enables/{2}", device_ids[0], out_c, out_mixer_c), 0);
            // Finish the DAQ module
            daqMod.finish();

            daqMod.clear();  // Release module resources. Especially important if modules are created
                             // inside a loop to prevent excessive resource consumption.

            // Stop the MDS module, release memory and resources
            mds.clear();

            // Extracting and saving the data
            double mClockbase = daq.getDouble(String.Format("/{0}/clockbase", device_ids[0]));

            List<ZIDoubleData[]> data = new List<ZIDoubleData[]>();
            foreach (String dev in device_ids)
            {
                string node = string.Format("/{0}/demods/{1}/sample.r", dev, demod_c);
                data.Add(result[node][0].doubleData);
            }

            String fileName = Environment.CurrentDirectory + "/mds_dataacquisition.txt";
            System.IO.StreamWriter file = new System.IO.StreamWriter(fileName);

            for (int i = 0; i < data[0].Length; ++i)
            {
                file.WriteLine("{0},{1},{2}", (data[0][i].timeStamp - data[0][0].timeStamp) / mClockbase,
                  data[0][i].value, data[1][i].value);
            }
            file.Close();

            daq.disconnect();
        }

    }

}
