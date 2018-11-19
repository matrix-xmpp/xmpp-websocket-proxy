namespace XmppWebSocketProxy
{
    using DotNetty.Handlers.Tls;
    using DotNetty.Transport.Channels;

    using System;
    using System.Net.Security;
    using System.Reactive.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Matrix;
    using Matrix.Network;
    using Matrix.Network.Handlers;
    using Matrix.Srv;
    using Matrix.Xml;
    using Matrix.Xmpp.Framing;
    using Matrix.Xmpp.Stream;
    using Matrix.Xmpp.Tls;

    using XmppWebSocketProxy.Handler;
    using Microsoft.Extensions.Logging;

    public sealed class XmppProxyConnection : XmppConnection
    {
        public XmppProxyConnection(
            ILogger<WebSocketServerHandler> logger,
            EventLoopGroups eventGroup)
            : base(new Action<IChannelPipeline>(pipeline =>
            {
                pipeline.AddBefore<StreamPropsCaptureHandler, CatchAllXmppStanzaHandler>();
            }), eventGroup.WorkerGroup)
        {
            this.logger = logger;
            this.HostnameResolver = new SrvNameResolver();
        }

        ILogger<WebSocketServerHandler> logger;

        public SessionState SessionState = SessionState.Disconnected;

        public WebSocketServerHandler WebSocket { get; set; }

        bool Tls => true;

        ITlsSettingsProvider TlsSettingsProvider { get; set; } = new DefaultClientTlsSettingsProvider();

        /// <summary>
        /// Connect to the XMPP server.
        /// This establishes the connection to the server, including TLS, authentication, resource binding and
        /// compression.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="AuthenticationException">Thrown when the authentication fails.</exception>
        /// <exception cref="BindException">Thrown when resource binding fails.</exception>
        /// <exception cref="StreamErrorException">Throws a StreamErrorException when the server returns a stream error.</exception>
        /// <exception cref="CompressionException">Throws a CompressionException when establishing stream compression fails.</exception>
        /// <exception cref="RegisterException">Throws aRegisterException when new account registration fails.</exception>
        public async Task<IChannel> ConnectAsync(CancellationToken cancellationToken)
        {
            var iChannel = await Bootstrap.ConnectAsync(XmppDomain, Port);
            SessionState = SessionState.Connected;

            if (HostnameResolver.Implements<IDirectTls>()
                && HostnameResolver.Cast<IDirectTls>().DirectTls == true)
            {
                await DoSslAsync(cancellationToken);
            }

            var feat = await SendStreamHeaderAsync(cancellationToken);
            var streamFeatures = await HandleStreamFeaturesAsync(feat, cancellationToken);

            var open = new Open()
            {
                From = Pipeline.Get<StreamPropsCaptureHandler>().From,
                Id = Pipeline.Get<StreamPropsCaptureHandler>().Id,
                Version = Pipeline.Get<StreamPropsCaptureHandler>().Version,
            };
            
            await WebSocket.SendText(open.ToString(false));
            
            // lets add the forwarding handler now
            this.Pipeline.AddBefore<CatchAllXmppStanzaHandler>(new ForwardHandler(this.WebSocket));
            await WebSocket.SendText(streamFeatures.ToString(false));

            return iChannel;
        }

        /// <summary>
        /// Starts SSL/TLS on a connection. This can be used for old Jabber style SSL.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task DoSslAsync(CancellationToken cancellationToken)
        {
            await Task.Run(async () =>
            {
                var tlsSettingsProvider = await TlsSettingsProvider.ProvideAsync(this);
                SessionState = SessionState.Securing;
                var tlsHandler =
                    new TlsHandler(stream
                    => new SslStream(stream,
                    true,
                    (sender, certificate, chain, errors) => CertificateValidator.RemoteCertificateValidationCallback(sender, certificate, chain, errors)),
                    tlsSettingsProvider);

                Pipeline.AddFirst(tlsHandler);
                SessionState = SessionState.Secure;
            }, cancellationToken);
        }

        /// <summary>
        /// Handle StartTls asynchronous
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<StreamFeatures> DoStartTlsAsync(CancellationToken cancellationToken)
        {
            SessionState = SessionState.Securing;

            var tlsSettingsProvider = await TlsSettingsProvider.ProvideAsync(this);
            var tlsHandler =
                new TlsHandler(stream
                => new SslStream(stream,
                true,
                (sender, certificate, chain, errors) => CertificateValidator.RemoteCertificateValidationCallback(sender, certificate, chain, errors)),
                tlsSettingsProvider);

            await SendAsync<Proceed>(new StartTls(), cancellationToken);
            Pipeline.AddFirst(tlsHandler);
            var streamFeatures = await ResetStreamAsync(cancellationToken);
            SessionState = SessionState.Secure;

            return streamFeatures;
        }

        private async Task<StreamFeatures> HandleStreamFeaturesAsync(StreamFeatures features, CancellationToken cancellationToken)
        {
            if (SessionState < SessionState.Securing && features.SupportsStartTls && Tls)
            {
                return await HandleStreamFeaturesAsync(await DoStartTlsAsync(cancellationToken), cancellationToken);
            }

            return features;
        }

    }
}
