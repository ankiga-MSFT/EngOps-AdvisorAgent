using Provider.Interfaces;

namespace Provider.Model
{
    public class EventHubConfiguration : IEventHubConfiguration
    {
        public string EventHubName { get; private set; } = null!;
        public string EventHubConnectionString { get; private set; }

        public string EventHubConsumerGroup { get; private set; } = null!;
        public EventHubConfiguration(string eventhubConnection, string eventhubName=null!,  string eventhubConsumerGroup=null!)
        {
            EventHubName = eventhubName;
            EventHubConnectionString = eventhubConnection;
            EventHubConsumerGroup = eventhubConsumerGroup;
        }


    }
}
