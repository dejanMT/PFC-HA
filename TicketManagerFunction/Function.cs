using Google.Cloud.Functions.Framework;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using System.Net;
using System.Text.Json;
using System;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using StackExchange.Redis;

namespace TicketManagerFunction;

public class TicketManagerFunction
{
    private readonly ILogger _logger;
    private readonly SubscriberServiceApiClient _pubsubClient;
    private readonly ConnectionMultiplexer _redis;
    private readonly MailgunClient _mailgun;
    private readonly LoggingServiceV2Client _logging;

    public TicketManagerFunction(ILogger<TicketManagerFunction> logger, IConfiguration config)
    {
        _logger = logger;
        _pubsubClient = SubscriberServiceApiClient.Create();
        _redis = ConnectionMultiplexer.Connect(config["RedisConnection"]);
        _mailgun = new MailgunClient(config["MailgunApiKey"], config["MailgunDomain"]);
        _logging = LoggingServiceV2Client.Create();
    }

    [Function("TicketManager")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
    {
        var project = Environment.GetEnvironmentVariable("PROJECT_ID")!;
        var topic = Environment.GetEnvironmentVariable("TICKETS_TOPIC")!;
        var subName = $"{topic}-sub";
        var priorities = new[] { "High", "Medium", "Low" };

        foreach (var prio in priorities)
        {
            var filter = $"attributes.priority=\"{prio}\"";
            var pullReq = new PullRequest
            {
                Subscription = SubscriptionName.FromProjectSubscription(project, subName).ToString(),
                MaxMessages = 10,
                ReturnImmediately = true
            };
            var resp = await _pubsubClient.PullAsync(pullReq);
            if (!resp.ReceivedMessages.Any()) continue;

            foreach (var msg in resp.ReceivedMessages)
            {
                var ticketJson = msg.Message.Data.ToStringUtf8();
                var ticket = JsonSerializer.Deserialize<Ticket>(ticketJson)!;
                // 1️⃣ Save to Redis
                var db = _redis.GetDatabase();
                await db.ListRightPushAsync($"tickets:{prio.ToLower()}", ticketJson);

                // 2️⃣ Send email
                var mail = await _mailgun.SendEmailAsync(
                    from: "support@yourdomain.com",
                    to: Environment.GetEnvironmentVariable("TECH_EMAILS")!.Split(','),
                    subject: $"[Ticket {ticket.Id}] New {prio} priority",
                    text: ticket.Description);

                // 3️⃣ Log the send
                var entry = new LogEntry
                {
                    LogName = new LogName(project, "ticket-manager-log").ToString(),
                    Resource = new MonitoredResource { Type = "global", Labels = { { "project_id", project } } },
                    Severity = LogSeverity.Info,
                    TextPayload = $"Sent {prio} ticket {ticket.Id} to {string.Join(",", mail.Recipients)}"
                };
                entry.Labels["ticketId"] = ticket.Id;
                entry.Labels["recipient"] = string.Join(",", mail.Recipients);
                await _logging.WriteLogEntriesAsync(entry);

                // 4️⃣ Ack the Pub/Sub message
                await _pubsubClient.AcknowledgeAsync(
                    new AcknowledgeRequest
                    {
                        Subscription = pullReq.Subscription,
                        AckIds = { msg.AckId }
                    });
            }

            // as soon as we found and processed high‐prio, stop looking lower
            return req.CreateResponse(HttpStatusCode.OK);
        }

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}

