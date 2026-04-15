namespace Provider.Interfaces
{
    public interface IEventHubConfiguration
    {
        string EventHubName { get; }
        string EventHubConnectionString { get; }

        string EventHubConsumerGroup { get;}

    }

}
