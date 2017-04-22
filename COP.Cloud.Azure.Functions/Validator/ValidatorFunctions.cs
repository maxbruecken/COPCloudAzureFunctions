using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using COP.Cloud.Azure.Core.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace COP.Cloud.Azure.Functions.Validator
{
    public class ValidatorFunctions
    {
        public static async Task ValidateAggregatedSensorDataAsync(AggregatedSensorData aggregatedSensorData,
            CloudQueue validatedSensorDataQueue,
            CloudTable sensors,
            CloudTable sensorAlarms,
            TraceWriter log)
        {
            log.Info($"Incoming aggregated sensor data: sensor id {aggregatedSensorData.SensorId}.");

            var sensor = sensors.CreateQuery<Sensor>().Where(s => s.RowKey == aggregatedSensorData.SensorId).ToList().SingleOrDefault();

            if (sensor != null)
            {
                await CheckSensorAndUpdateLastSeen(sensors, sensor);
                if (aggregatedSensorData.AggregationType == AggregationType.Mean)
                {
                    await ValidateAggregatedData(aggregatedSensorData, sensorAlarms, sensor);
                }
                await ForwardAggregatedSensorData(aggregatedSensorData, validatedSensorDataQueue);
                return;
            }

            log.Error($"No sensor found for id {aggregatedSensorData.SensorId}");
        }

        private static async Task CheckSensorAndUpdateLastSeen(CloudTable sensors, Sensor sensor)
        {
            var now = DateTimeOffset.UtcNow;
            if (sensor.LastSeen < now)
            {
                sensor.LastSeen = now;
                sensor.ETag = "*"; // disable optimistic locking
                var mergeOperation = TableOperation.Merge(sensor);
                await sensors.ExecuteAsync(mergeOperation);
            }
        }

        private static async Task ValidateAggregatedData(AggregatedSensorData aggregatedSensorData, CloudTable sensorAlarms, Sensor sensor)
        {
            if (aggregatedSensorData.Value < sensor.Min || aggregatedSensorData.Value > sensor.Max)
            {
                await CreateSensorAlarm(sensorAlarms, sensor, AlarmStatus.InvalidData);
            }
        }

        private static async Task CreateSensorAlarm(CloudTable sensorAlarms, Sensor s, AlarmStatus alarmStatus, bool singleton = false)
        {
            if (singleton)
            {
                var existingAlarm = sensorAlarms.CreateQuery<SensorAlarm>().Where(a => a.PartitionKey == s.Id && a.StatusString == alarmStatus.ToString()).FirstOrDefault();
                if (existingAlarm != null) return;
            }
            var sensorAlarm = new SensorAlarm
            {
                SensorId = s.Id,
                Status = alarmStatus
            };
            var insertOperation = TableOperation.Insert(sensorAlarm);
            await sensorAlarms.ExecuteAsync(insertOperation).ConfigureAwait(false);
        }

        private static async Task ForwardAggregatedSensorData(AggregatedSensorData aggregatedSensorData, CloudQueue validatedSensorDataQueue)
        {
            await validatedSensorDataQueue.AddMessageAsync(new CloudQueueMessage(JsonConvert.SerializeObject(aggregatedSensorData)));
        }

        public static async Task CheckSensorsAndAlarms(TimerInfo timerInfo,
            CloudTable sensors,
            CloudTable sensorAlarms)
        {
            var deadLine = DateTimeOffset.UtcNow.AddMinutes(-5);
            await FireAlarmsForDeadSensors(sensors, sensorAlarms, deadLine);
            await RemoveObsoleteAlarms(sensors, sensorAlarms, deadLine);
        }

        private static async Task FireAlarmsForDeadSensors(CloudTable sensors, CloudTable sensorAlarms, DateTimeOffset deadLine)
        {
            var tasks = sensors
                .CreateQuery<Sensor>()
                .Where(s => s.LastSeen < deadLine)
                .ToList()
                .Select(s => CreateSensorAlarm(sensorAlarms, s, AlarmStatus.Dead, true));
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static async Task RemoveObsoleteAlarms(CloudTable sensors, CloudTable sensorAlarms, DateTimeOffset deadLine)
        {
            var tasks = sensors
                .CreateQuery<Sensor>()
                .Where(s => s.LastSeen > deadLine)
                .ToList()
                .SelectMany(s =>
                {
                    return sensorAlarms
                        .CreateQuery<SensorAlarm>()
                        .Where(a => a.PartitionKey == s.Id && a.StatusString == AlarmStatus.Dead.ToString())
                        .ToList();
                })
                .Select(async a =>
                {
                    a.ETag = "*"; //disable optimistic locking
                    var deleteOperation = TableOperation.Delete(a);
                    try
                    {
                        await sensorAlarms.ExecuteAsync(deleteOperation).ConfigureAwait(false);
                    }
                    catch (StorageException e)
                    {
                        if (e.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound) throw;
                    }
                });
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
    }
}