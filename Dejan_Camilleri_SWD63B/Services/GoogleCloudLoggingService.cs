using Google.Api;
using Google.Cloud.Logging.V2;
using Google.Cloud.Logging.Type;
using Dejan_Camilleri_SWD63B.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class GoogleCloudLoggingService : ICloudLoggingService
    {
        private readonly LoggingServiceV2Client _client;
        private readonly string _projectId;
        private readonly string _logName;
        private readonly MonitoredResource _resource;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<GoogleCloudLoggingService> _local;

        public GoogleCloudLoggingService(
            IConfiguration config,
            ILogger<GoogleCloudLoggingService> localLogger,
            IWebHostEnvironment env)
        {
            _local = localLogger;
            _env = env;
            //var projectId = config["ProjectId"]
            //     ?? config["ProjectId"]
            //     ?? throw new InvalidOperationException("ProjectId must be set");
            //_projectId = config["ProjectId"] ?? throw new InvalidOperationException("ProjectId must be set");
            _projectId = "boxwood-night-449813-s9";
            _logName = config["LogName"] ?? "app-log";
            _client = LoggingServiceV2Client.Create();
            _resource = new MonitoredResource
            {
                Type = "global",
                Labels = { { "project_id", _projectId } }
            };
            _local.LogInformation("Google Cloud Logging initialized");
        }

        public Task LogInformationAsync(string message, Dictionary<string, string>? labels = null)
            => LogAsync(LogSeverity.Info, message, labels);

        public Task LogWarningAsync(string message, Dictionary<string, string>? labels = null)
            => LogAsync(LogSeverity.Warning, message, labels);

        public Task LogErrorAsync(string message, Exception? ex = null, Dictionary<string, string>? labels = null)
        {
            if (ex != null)
                message += $"\nException: {JsonSerializer.Serialize(new { ex.Message, ex.StackTrace })}";
            return LogAsync(LogSeverity.Error, message, labels);
        }

        public Task LogDebugAsync(string message, Dictionary<string, string>? labels = null)
            => LogAsync(LogSeverity.Debug, message, labels);

        public Task LogCriticalAsync(string message, Exception? ex = null, Dictionary<string, string>? labels = null)
        {
            if (ex != null) message += $"\nException: {JsonSerializer.Serialize(ex)}";
            return LogAsync(LogSeverity.Critical, message, labels);
        }

        public async Task LogAsync(LogSeverity severity, string message, Dictionary<string, string>? labels = null)
        {
            var logName = new LogName(_projectId, _logName).ToString();
            var entry = new LogEntry
            {
                LogName = logName,
                Severity = severity,
                TextPayload = message,
                Resource = _resource,
                Timestamp = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow)
            };

            // merge labels
            var all = new Dictionary<string, string>
            {
                ["environment"] = _env.EnvironmentName,
                ["application"] = "SupportApp"
            };
            if (labels != null)
                foreach (var kv in labels) all[kv.Key] = kv.Value;
            entry.Labels.Add(all);

            await _client.WriteLogEntriesAsync(logName, _resource, null, new[] { entry });
        }

    }
}
