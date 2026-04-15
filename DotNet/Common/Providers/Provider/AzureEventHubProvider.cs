using Azure.Core;
using Azure.Identity;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;
using Microsoft.Identity.Web;
using Newtonsoft.Json.Linq;
using Provider.Interfaces;
using Provider.Model;


namespace Provider
{
#pragma warning disable CS8618

    public class AzureEventHubProvider : IAzureEventHubProvider
    {
        private readonly string _eventHubNamespace;
        private readonly string _eventHubName;
        private EventHubProducerClient _producerClient;
        private EventHubConsumerClient _consumerClient;

        public AzureEventHubProvider(string eventHubNamespace, string eventHubName)
        {
            _eventHubNamespace = eventHubNamespace ?? throw new ArgumentNullException(nameof(eventHubNamespace));
            _eventHubName = eventHubName ?? throw new ArgumentNullException(nameof(eventHubName));
            InitializeClients();
        }
        public AzureEventHubProvider(EventHubConfig config)
        {
            _eventHubNamespace = config.EventHubNamespace ?? throw new ArgumentNullException(nameof(config.EventHubNamespace));
            _eventHubName = config.EventHubName ?? throw new ArgumentNullException(nameof(config.EventHubName));
            InitializeClients(config.CredentialConfig);
        }
        private void InitializeClients(CredentialConfig? credentialConfig = null)
        {

#if DEBUG
            var credential = new DefaultAzureCredential();
#else
            var mui = string.IsNullOrWhiteSpace(credentialConfig?.MuiClientId) ? default : credentialConfig?.MuiClientId;
            TokenCredential? credential = default;

            if (!string.IsNullOrEmpty(credentialConfig?.AppClientId) || !string.IsNullOrEmpty(credentialConfig?.TenantId))
            {
                credential = new ClientAssertionCredential(
                            credentialConfig?.TenantId,
                            credentialConfig?.AppClientId,
                            async cancellationToken =>
                            {
                                var assertion = new ManagedIdentityClientAssertion(mui);
                                return await assertion.GetSignedAssertionAsync(default);
                            });
            }
            else
            {
                credential = new ManagedIdentityCredential(mui);
            }
#endif
            var fullyQualifiedNamespace = $"{_eventHubNamespace}.servicebus.windows.net";
            var retryOptions = new EventHubsRetryOptions
            {
                Mode = EventHubsRetryMode.Exponential,
                Delay = TimeSpan.FromSeconds(1), // Initial delay between retries
                MaximumDelay = TimeSpan.FromSeconds(30), // Maximum delay between retries
                MaximumRetries = 5 // Maximum number of retry attempts

            };
            var producerOptions = new EventHubProducerClientOptions { RetryOptions = retryOptions };
            var consumerOptions = new EventHubConsumerClientOptions { RetryOptions = retryOptions };
            _producerClient = new EventHubProducerClient(fullyQualifiedNamespace, _eventHubName, credential, producerOptions);
            _consumerClient = new EventHubConsumerClient(EventHubConsumerClient.DefaultConsumerGroupName, fullyQualifiedNamespace, _eventHubName, credential, consumerOptions);
        }

        public void SetProducerClient(EventHubProducerClient producerClient)
        {
            _producerClient = producerClient;
        }
        public async Task SendMessagesAsync(List<JObject> messages, Func<JObject, string> partitionKeySelector = null!)
        {
            if (messages == null || messages.Count == 0)
                throw new ArgumentNullException(nameof(messages));

            foreach (var message in messages)
            {
                var eventData = new EventData(message.ToString());
                var boption = new CreateBatchOptions();
                if (partitionKeySelector != null)
                {
                    var partitionKey = partitionKeySelector(message);
                    if (!string.IsNullOrEmpty(partitionKey))
                    {
                        eventData.Properties.Add("partitionKey", partitionKey);
                        boption.PartitionKey = partitionKey;
                    }
                }

                using EventDataBatch eventBatch = await _producerClient.CreateBatchAsync(boption);
                eventBatch.TryAdd(eventData);
                await _producerClient.SendAsync(eventBatch);
            }
        }

        public async Task ReceiveMessagesAsync(Func<List<JObject>, Task> processMessagesAsync, int batchSize, int waitTimeInSeconds)
        {
            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");
            if (waitTimeInSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(waitTimeInSeconds), "Wait time must be greater than zero.");

            var messageList = new List<JObject>();
            var cancellationTokenSource = new CancellationTokenSource();

            async Task TimerCallback()
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(waitTimeInSeconds * 1000, cancellationTokenSource.Token);
                    if (messageList.Count > 0)
                    {
                        await processMessagesAsync(messageList);
                        messageList.Clear();
                    }
                }
            }

            var timerTask = TimerCallback();

            await foreach (PartitionEvent partitionEvent in _consumerClient.ReadEventsAsync(cancellationTokenSource.Token))
            {
                var message = JObject.Parse(partitionEvent.Data.EventBody.ToString());
                messageList.Add(message);

                // Process messages in batch
                if (messageList.Count >= batchSize)
                {
                    await processMessagesAsync(messageList);
                    messageList.Clear();
                }
            }

            cancellationTokenSource.Cancel();
            await timerTask;

            // Process any remaining messages
            if (messageList.Count > 0)
            {
                await processMessagesAsync(messageList);
            }
        }
    }

}
