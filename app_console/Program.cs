using zhinst;
using ziDotNetExamples;

namespace ziDotNetExamples 
{

    public class Program { 
            static void Main(string[] args) {
            Examples examples = new Examples();
            //examples.DeviceInit_Settings();
            examples.DeviceInit_Load(preset_flash_number: 2);
            //examples.GetDemodSample();
            examples.Subscribe(channel: 0);
            //Thread.Sleep(1500);
            //examples.Subscribe(channel: 1);
            examples.PollDemodSample(channel: 0);
            Thread.Sleep(100);
            examples.PollDemodSample(channel: 0);
            Thread.Sleep(100);
            examples.PollDemodSample(channel: 0);
            examples.PollDemodSample(channel: 0);

            examples.UnSubscribe(channel: 0);
            //examples.UnSubscribe(channel: 1);
            examples.DeviceClose();

            //Examples.ExamplePollDemodSample();
        }
    }
}