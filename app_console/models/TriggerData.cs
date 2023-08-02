using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace app_console.model
{
    public class TriggerData
    {
        public TriggerData(List<double> amplitude_list, List<double> phase_list, int sample_length)
        {
            this.amplitude_list = amplitude_list;
            this.phase_list = phase_list;
            this.sample_length = sample_length;
        }

        public List<double> amplitude_list { get; }
        public List<double> phase_list { get; }
        public int sample_length { get; }
    }
}
