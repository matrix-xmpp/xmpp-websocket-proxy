namespace XmppWebSocketProxy
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System;
    using XmppWebSocketProxy.Handler;

    public class ServiceLocator
    {
        static ServiceProvider serviceProvider;
        static IServiceCollection serviceCollection;

        static IConfigurationRoot configuration = new ConfigurationBuilder()
              .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
              .AddJsonFile("appsettings.json")
              .Build();
        
        public static ServiceProvider Current
        {
            get
            {
                if (serviceProvider == null)
                    InitServices();

                return serviceProvider;
            }
        }

        public static IServiceCollection ServiceCollection
        {
            get
            {
                if (serviceProvider == null)
                    InitServices();

                return serviceCollection;
            }
        }

        private static void InitServices()
        {
            serviceCollection = new ServiceCollection();

            serviceCollection
                .AddSingleton(new LoggerFactory().AddConsole(configuration.GetSection("Logging")))
                .AddLogging()
                .AddSingleton(configuration)
                .AddSingleton(configuration.GetSection("Server").Get<Configuration.ServerConfiguration>())
                .AddSingleton<EventLoopGroups>()
                .AddSingleton<Server>()
                .AddTransient<XmppProxyConnection>()
                .AddTransient<WebSocketServerHandler>();
                

            serviceProvider = serviceCollection.BuildServiceProvider();
        }
    }
}
