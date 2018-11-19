namespace XmppWebSocketProxy
{
    using DotNetty.Codecs.Http;
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using DotNetty.Transport.Libuv;
    using Microsoft.Extensions.Logging;
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;
    using XmppWebSocketProxy.Configuration;
    using XmppWebSocketProxy.Handler;    

    public class Server
    {
        ILogger<Server> logger;
        ServerConfiguration serverConfiguration;
        
        EventLoopGroups eventLoopGroups;
        WebSocketServerHandler webSockettHandler;

        public Server(
            ILogger<Server> logger,
            EventLoopGroups eventLoopGroups,
            ServerConfiguration serverSettings,
            WebSocketServerHandler webSockettHandler)
        {
            this.logger = logger;
            this.serverConfiguration = serverSettings;
            this.eventLoopGroups = eventLoopGroups;
            this.webSockettHandler = webSockettHandler;
        }


        public async Task RunServerAsync()
        {
            logger.LogInformation($"{RuntimeInformation.OSArchitecture} {RuntimeInformation.OSDescription}");
            logger.LogInformation($"{RuntimeInformation.ProcessArchitecture} {RuntimeInformation.FrameworkDescription}");
            logger.LogInformation($"Processor Count : {Environment.ProcessorCount}");
            logger.LogInformation("Transport type : " + (serverConfiguration.Libuv ? "Libuv" : "Socket"));

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }

            logger.LogInformation($"Server garbage collection : {(GCSettings.IsServerGC ? "Enabled" : "Disabled")}");
            logger.LogInformation($"Current latency mode for garbage collection: {GCSettings.LatencyMode}");

            X509Certificate2 tlsCertificate = null;
            if (serverConfiguration.Ssl)
            {
                tlsCertificate = new X509Certificate2(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, serverConfiguration.Certificate.Location),
                    serverConfiguration.Certificate.Password);
            }
            try
            {
                var bootstrap = new ServerBootstrap();
                bootstrap.Group(eventLoopGroups.BossGroup, eventLoopGroups.WorkerGroup);

                if (serverConfiguration.Libuv)
                {
                    bootstrap.Channel<TcpServerChannel>();
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        bootstrap
                            .Option(ChannelOption.SoReuseport, true)
                            .ChildOption(ChannelOption.SoReuseaddr, true);
                    }
                }
                else
                {
                    bootstrap.Channel<TcpServerSocketChannel>();
                }

                bootstrap
                    .Option(ChannelOption.SoBacklog, 8192)
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (tlsCertificate != null)
                        {
                            pipeline.AddLast(TlsHandler.Server(tlsCertificate));
                        }
                        pipeline.AddLast(new HttpServerCodec());
                        pipeline.AddLast(new HttpObjectAggregator(65536));
                        pipeline.AddLast(webSockettHandler);
                    }));


                int port = serverConfiguration.Port;
                IChannel bootstrapChannel = await bootstrap.BindAsync(IPAddress.Loopback, port);

                logger.LogInformation("Open your web browser and navigate to "
                    + $"{(serverConfiguration.Ssl ? "https" : "http")}"
                    + $"://{IPAddress.Loopback}:{port}/");

                logger.LogInformation("Listening on "
                    + $"{(serverConfiguration.Ssl ? "wss" : "ws")}"
                    + $"://{IPAddress.Loopback}:{port}/websocket");

                Console.ReadLine();

                await bootstrapChannel.CloseAsync();
            }
            finally
            {
                eventLoopGroups.WorkerGroup.ShutdownGracefullyAsync().Wait();
                eventLoopGroups.BossGroup.ShutdownGracefullyAsync().Wait();
            }
        }
    }
}
