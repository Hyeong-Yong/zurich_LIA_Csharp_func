using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;


namespace ziDotNet_Csharp
{

  public class func
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


    /// <summary>
    /// ExamplePollDoubleData is similar to ExamplePollDemodSample, but it subscribes and polls floating point data.
    /// </summary>
    /// <param name="dev"></param>
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

        /// <summary>
    /// ExampleGetDemodSample reads the demodulator sample value of the specified node. 
    /// </summary>
    /// <param name="dev"></param>
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

        /// <summary>
    /// ExampleDeviceSettings instantiates a deviceSettings module and performs a save
    /// and load of device settings. The LabOne UI uses this module to save and
    /// load the device settings. 
    /// </summary>
    /// <param name="dev"></param>
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

    /// <summary>
    /// ExampleDataAcquisition uses the new data acquisition module to record data
    /// and writes the result in to a file.
    /// </summary>
    /// <param name="dev"></param>
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

  }

}