using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace COP.Cloud.Azure.Core.Models
{
    public class Sensor : TableEntity
    {
        public Sensor()
        {
            PartitionKey = string.Empty;
        }

        [IgnoreProperty]
        public string Id
        {
            get { return RowKey; }
            set { RowKey = value; }
        }

        [IgnoreProperty]
        public SensorType Type { get; set; }

        public string SensorTypeString
        {
            get { return Type.ToString(); }
            set { Type = (SensorType)Enum.Parse(typeof(SensorType), value); }
        }
        
        public double Min { get; set; }
        
        public double Max { get; set; }
        
        public DateTimeOffset LastSeen { get; set; } = new DateTimeOffset(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    public enum SensorType
    {
        Temperature, Voltage
    }
}
