using System;
using COP.Cloud.Azure.Core.Models;
using Microsoft.WindowsAzure.Storage.Table;

namespace COP.Cloud.Azure.Functions.Core.Models
{
    public class PersistentSensorData : TableEntity
    {
        public PersistentSensorData()
        {
            RowKey = Guid.NewGuid().ToString();
        }

        public string SensorId
        {
            get { return PartitionKey; }
            set { PartitionKey = value; }
        }

        public DateTimeOffset CreatedAt { get; set; }

        [IgnoreProperty]
        public SensorType SensorType { get; set; }

        public string SensorTypeString
        {
            get { return SensorType.ToString(); }
            set { SensorType = (SensorType)Enum.Parse(typeof(SensorType), value); }
        }

        [IgnoreProperty]
        public AggregationType AggregationType { get; set; }

        public string AggregationTypeString
        {
            get { return AggregationType.ToString(); }
            set { AggregationType = (AggregationType)Enum.Parse(typeof(AggregationType), value); }
        }

        public double Value { get; set; }
    }
}
