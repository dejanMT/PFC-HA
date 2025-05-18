using Dejan_Camilleri_SWD63B.Interfaces;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using System.Text.Json;

namespace Dejan_Camilleri_SWD63B.Services
{
    public class PubSubService : IPubSubService
    {
        private readonly PublisherClient _publisher;

        public PubSubService(IConfiguration config)
        {
            var projectId = config["ProjectId"];
            _publisher = PublisherClient.Create(TopicName.FromProjectTopic(projectId, "tickets-topic"));
        }


        /// <summary>
        /// Publish a ticket to the Pub/Sub topic with a specified priority.
        /// </summary>
        /// <param name="payload"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public async Task PublishTicketAsync(object payload, string priority)
        {
            string json = JsonSerializer.Serialize(payload);
            var message = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(json),
                Attributes = { { "priority", priority } }
            };
            await _publisher.PublishAsync(message);
        }
    }
}
