using System.Collections.Generic;

namespace COP.Cloud.Azure.Core.Models
{
    public class SensorData
    {
        public string SensorId { get; set; }

        public IEnumerable<double> Values { get; set; } = new List<double>();
    }
}
