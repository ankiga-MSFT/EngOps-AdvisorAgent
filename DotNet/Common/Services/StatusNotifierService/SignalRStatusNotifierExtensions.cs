//using Azure.Core;
//using Microsoft.Azure.SignalR;
//using Microsoft.Azure.SignalR.Management;
//using Microsoft.Extensions.DependencyInjection;

//namespace CXOAI.StatusNotifier;

///// <summary>
///// Extension methods to register Azure SignalR status notifier services.
///// </summary>
//public static class SignalRStatusNotifierExtensions
//{
//    /// <summary>
//    /// Registers <see cref="ServiceHubContext"/> for the "orchestrator" hub
//    /// and a factory to create per-session <see cref="SignalRStatusNotifier"/> instances.
//    /// </summary>
//    /// <param name="services">The service collection.</param>
//    /// <param name="connectionString">Azure SignalR Service endpoint URL.</param>
//    public static IServiceCollection AddSignalRStatusNotifier(
//        this IServiceCollection services, string connectionString)
//    {
//        //services.AddSingleton(sp =>
//        //{
//        //    var credential = sp.GetRequiredService<TokenCredential>();
//        //    var serviceEndpoint = new ServiceEndpoint(new Uri(connectionString), credential);

//        //    var serviceManager = new ServiceManagerBuilder()
//        //        .WithOptions(o =>
//        //        {
//        //            o.ServiceEndpoints = new ServiceEndpoint[]
//        //            {
//        //                serviceEndpoint
//        //            };
//        //        })
//        //        .BuildServiceManager();

//        //    return serviceManager.CreateHubContextAsync("orchestrator", default)
//        //        .GetAwaiter().GetResult();
//        //});

//        return services;
//    }
//}
