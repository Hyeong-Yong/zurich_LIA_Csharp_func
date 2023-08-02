using app_console.model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using zhinst;

namespace app_console
{
        public class Zurich : Zurich_API
        {
            public Zurich(string device = "dev3066", int channel = 0)
            {
                dev = device;
                ch = channel;
            }

            /// <summary>
            /// Singleton
            /// </summary>
            ////private static 인스턴스 객체
            //private static Examples? examples;

            //private Examples()
            //{
            //}

            ////public static 의 객체반환 함수
            //public static Examples Instance() { 
            //if(examples == null)
            //    {
            //        examples = new Examples();
            //    }
            //    return examples;
            //}

            private int ch;
            public  int Channel { get => ch; }

            private string dev;
            public  string Device { get => dev; }

            private const string DEFAULT_DEVICE = "dev3066";
            ziDotNET daq = new ziDotNET();

            public void Subscribe()
            {
                string path = Demods_pathMaker(ch, "sample");
                daq.subscribe(path);
                Thread.Sleep(1000);
            }
            
            /// <summary>
            /// Whether enable or disable to transfer the demodulator sample to the PC.
            /// </summary>
            /// <param name="enable">True: Being enabled to transfer the sample to PC, False : Being disabled.</param>
            public void Set_TransferSample(bool enable)
            {
                string path = Demods_pathMaker(ch, "enable");
            if (enable)
            {
                daq.setInt(path, 1);
            }
            else
            {
                daq.setInt(path, 0);
            }
        }

        /// <summary>
        /// Change Sampling rate
        /// </summary>
        /// <param name="sampling_rate">input sampling rate, e.g., 10e3</param>
        public void Change_SamplingRate(double sampling_rate)
            {
               string path = Demods_pathMaker(ch, "rate");
               daq.setDouble(path, sampling_rate);
            }

            /// <summary>
            /// set trigger enable 
            /// </summary>
            /// <param name="enable">True: enable (hardware trigger mode), False: Disable (Continuous mode)</param>            
            public void Set_Trigger(bool enable){
                    string path = Demods_pathMaker(ch, "trigger");

                    if (enable)
                    {
                        daq.setInt(path, 1);
                    }
                    else
                    {
                        daq.setInt(path, 0);
                    }
                }


            public void UnSubscribe(int channel = 0)
            {
                string path = Demods_pathMaker(channel, "sample");
                daq.unsubscribe(path);
            }

            // ExamplePollDemodSample connects to the device,
            // subscribes to a demodulator, polls the data for 0.1 s
            // and returns the data.
            public TriggerData PollSample()
            {
                string path = Demods_pathMaker(ch, "sample");

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
                Chunk chunk = lookup[path][ch];  // Single chunk

                // Vector of samples
                ZIDemodSample[] samples = chunk.demodSamples;

                int demodSample_length = chunk.demodSamples.Length;
                List<double> amplitude_list = new List<double>();
                List<double> phase_list = new List<double>();
                foreach (var sample in samples)
                    {
                        double amplitude = Math.Sqrt((Math.Pow(sample.x, 2) + Math.Pow(sample.y, 2)));
                        amplitude_list.Add(amplitude);
                        double phase = Math.Atan2(sample.y, sample.x);
                        phase_list.Add(phase);
                    }
                Console.WriteLine($"Frequency: {chunk.demodSamples[ch].frequency}");
                Console.WriteLine($"Buffer Length: {demodSample_length}");
                Console.WriteLine("--------------------");

                TriggerData trigger_data = new TriggerData(amplitude_list, phase_list, demodSample_length);
                
                
                Debug.Assert(0 != chunk.demodSamples[ch].timeStamp);

            return trigger_data;

            }



        // ExampleGetDemodSample reads the demodulator sample value of the specified node.
        public void GetSample()
            {
                String path = String.Format("/{0}/demods/{1}/sample", dev, ch);
                ZIDemodSample sample = daq.getDemodSample(path);

                double amplitude = Math.Sqrt((Math.Pow(sample.x, 2) + Math.Pow(sample.y, 2)));
                double phase = Math.Atan2(sample.y, sample.x);
                Console.WriteLine($"Amplitude: {amplitude}, Phase: {phase*180/Math.PI}, Frequency: {sample.frequency}");
                Console.WriteLine("-----------Flush buffer------------");
                AssertNotEqual(0ul, sample.timeStamp);
            }

            public void DeviceInit_Load(int preset_flash_number = 1) // Timeout(15000)
            {
                daq = connect(dev);


                // go to the flash 0 (factory setting)
                //resetDeviceToDefault(daq, dev);
                string path;

                //ziModule settings = daq.deviceSettings();

                //// First save the current device settings
                //settings.setString("device", dev);
                //settings.setString("command", "save");
                //settings.setString("filename", "test_settings");
                //settings.setString("path", Environment.CurrentDirectory);
                //settings.execute();
                //while (!settings.finished())
                //{
                //    System.Threading.Thread.Sleep(100);
                //}

                //// Remember the current device parameter for later comparison
                //String path = $"/{device}/oscs/0/freq";
                //Double originalValue = daq.getDouble(path);

                //// Change the parameter
                //daq.setDouble(path, 2 * originalValue);

                //// Load device settings from file
                //settings.setString("device", dev);
                //settings.setString("command", "load");
                //settings.setString("filename", "test_settings");
                //settings.setString("path", Environment.CurrentDirectory);
                //settings.execute();
                //while (!settings.finished())
                //{
                //    System.Threading.Thread.Sleep(100);
                //}

                //// Check the restored parameter
                //Double newValue = daq.getDouble(path);

                //AssertEqual(originalValue, newValue);

                //settings.clear();  // Release module resources. Especially important if modules are created
                //                   // inside a loop to prevent excessive resource consumption.

                // Load flash setting
                // default : 1
                if (preset_flash_number <= 6 && preset_flash_number >= 0)
                {
                    daq.setInt($"/{dev}/system/preset/index", preset_flash_number);
                    daq.setInt($"/{dev}/system/preset/load", 1);
                }
                else
                {
                    Console.WriteLine("preset_flash_numer must be in range of 0 to 6");
                }

                // Is flash loading done?
                path = $"/{dev}/system/preset/busy";
                Int64 isBusy = daq.getInt(path);
                while (isBusy == 1)
                {
                    System.Threading.Thread.Sleep(10);
                    isBusy = daq.getInt(path);
                }

                //daq.disconnect();
            }

            public void DeviceInit_Settings() // Timeout(15000)
            {
                daq = connect(dev);
                resetDeviceToDefault(daq, dev);
                string path;

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
                path = $"/{dev}/oscs/0/freq";
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


                //daq.disconnect();
            }

            public void DeviceClose()
            {
                daq.disconnect();
            }

            private string Demods_pathMaker(int channel = 0, string function = "", string device = DEFAULT_DEVICE)
            {
                string path = $"/{device}/demods/{channel}/{function}";
                return path;
            }
        }
}

