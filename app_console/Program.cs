using app_console.model;
using System.Threading.Channels;
using zhinst;

namespace app_console 
{
    public class Program { 
            static void Main(string[] args) {
            Zurich zurich = new Zurich(device:"dev3066", channel:0);
            //examples.DeviceInit_Settings();
            zurich.DeviceInit_Load(preset_flash_number: 2);
            zurich.GetSample();

            zurich.Subscribe();

            TriggerData trigger_data = zurich.PollSample();
            for(int i = 0; i<trigger_data.sample_length;i++){            
                Console.WriteLine("Amplitude: " + trigger_data.amplitude_list[i]+ ", Phase:" + trigger_data.phase_list[i]*180/Math.PI);
            }
            Console.WriteLine("--------------------------");
            Console.Write("Buffer length: " + trigger_data.sample_length);

            zurich.UnSubscribe();
            zurich.DeviceClose();
        }
    }
}