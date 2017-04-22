using System.Threading.Tasks;
using COP.Cloud.Azure.Core.Models;
using COP.Cloud.Azure.Functions.Core.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Table;

namespace COP.Cloud.Azure.Functions.Writer
{
    public class WriterFunctions
    {
        public static async Task WriteValidatedSensorDataAsync([QueueTrigger("validated-sensor-data")] AggregatedSensorData validatedSensorData,
            [Table("sensordata")] CloudTable sensordatas,
            TraceWriter log)
        {
            log.Info($"Saving validated sensor data for sensor {validatedSensorData.SensorId} of type {validatedSensorData.SensorType}.");

            var sensorData = new PersistentSensorData
            {
                SensorId = validatedSensorData.SensorId,
                SensorType = validatedSensorData.SensorType,
                AggregationType = validatedSensorData.AggregationType,
                CreatedAt = validatedSensorData.TimeStamp,
                Value = validatedSensorData.Value
            };
            var insertOperation = TableOperation.Insert(sensorData);
            await sensordatas.ExecuteAsync(insertOperation);
        }
    }
}