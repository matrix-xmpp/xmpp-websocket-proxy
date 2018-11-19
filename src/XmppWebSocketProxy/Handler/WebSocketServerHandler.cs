namespace XmppWebSocketProxy.Handler
{
    using System;    
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Http;
    using DotNetty.Codecs.Http.WebSockets;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    using static DotNetty.Codecs.Http.HttpVersion;
    using static DotNetty.Codecs.Http.HttpResponseStatus;

    using Matrix.Xmpp.Framing;
    using Matrix.Xml;
    using System.Threading;
    using XmppWebSocketProxy.Configuration;
    using Microsoft.Extensions.Logging;

    public sealed class WebSocketServerHandler : SimpleChannelInboundHandler<object>
    {        
        public WebSocketServerHandler(
            ILogger<WebSocketServerHandler> logger,
            EventLoopGroups eventGroup,
            ServerConfiguration serverConfiguration,
            XmppProxyConnection xmppProxyConnection)
        {
            this.logger = logger;
            this.eventGroup = eventGroup.WorkerGroup;
            this.settings = serverConfiguration;
            this.xmppProxy = xmppProxyConnection;
            this.xmppProxy.WebSocket = this;
        }

        ILogger<WebSocketServerHandler> logger;
        IEventLoopGroup eventGroup;
        ServerConfiguration settings;
        XmppProxyConnection xmppProxy;

        IChannelHandlerContext ctx;               
        WebSocketServerHandshaker handshaker;

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            base.HandlerAdded(context);
            this.ctx = context;
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            if (msg is IFullHttpRequest request)
            {
                this.HandleHttpRequest(ctx, request);
            }
            else if (msg is WebSocketFrame frame)
            {
                this.HandleWebSocketFrame(ctx, frame);
            }
        }

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        void HandleHttpRequest(IChannelHandlerContext ctx, IFullHttpRequest req)
        {
            // Handle a bad request.
            if (!req.Result.IsSuccess)
            {
                SendHttpResponse(ctx, req, new DefaultFullHttpResponse(Http11, BadRequest));
                return;
            }

            // Allow only GET methods.
            if (!Equals(req.Method, HttpMethod.Get))
            {
                SendHttpResponse(ctx, req, new DefaultFullHttpResponse(Http11, Forbidden));
                return;
            }

            // Send the demo page and favicon.ico
            if ("/".Equals(req.Uri))
            {
                IByteBuffer content = Unpooled.WrappedBuffer(Encoding.ASCII.GetBytes(
                    "<html><head><title>XMPP WebSocket Proxy</title></head>" + Environment.NewLine +
                    "<body>" + Environment.NewLine +
                    "<h1>XMPP WebSocket server</h1>" + Environment.NewLine +
                    "<p>running at: " + GetWebSocketLocation(req) + "</p>" + Environment.NewLine +
                    "</body>" + Environment.NewLine +
                    "</html>"));

                var res = new DefaultFullHttpResponse(Http11, OK, content);

                res.Headers.Set(HttpHeaderNames.ContentType, "text/html; charset=UTF-8");
                HttpUtil.SetContentLength(res, content.ReadableBytes);

                SendHttpResponse(ctx, req, res);
                return;
            }
            if ("/favicon.ico".Equals(req.Uri))
            {
                var res = new DefaultFullHttpResponse(Http11, NotFound);
                SendHttpResponse(ctx, req, res);
                return;
            }

            // Handshake
            var wsFactory = new WebSocketServerHandshakerFactory(
                GetWebSocketLocation(req), "xmpp", true, 5 * 1024 * 1024);
            this.handshaker = wsFactory.NewHandshaker(req);
            if (this.handshaker == null)
            {
                WebSocketServerHandshakerFactory.SendUnsupportedVersionResponse(ctx.Channel);
            }
            else
            {
                this.handshaker.HandshakeAsync(ctx.Channel, req);
            }
        }

        async void HandleWebSocketFrame(IChannelHandlerContext ctx, WebSocketFrame frame)
        {
            // Check for closing frame
            if (frame is CloseWebSocketFrame)
            {
                await this.handshaker.CloseAsync(ctx.Channel, (CloseWebSocketFrame)frame.Retain());
                Console.WriteLine("Websocket closed" );
                return;
            }

            if (frame is PingWebSocketFrame)
            {
                await ctx.WriteAsync(new PongWebSocketFrame((IByteBuffer)frame.Content.Retain()));
                return;
            }

            if (frame is TextWebSocketFrame)
            {
                // Echo the frame   
                var textFrame = frame as TextWebSocketFrame;

                var text = textFrame.Text();

                logger.LogDebug($"RECV: {text}");
                
                var el = XmppXElement.LoadXml(text);
                if (el.OfType<Open>())
                {                    
                    if (xmppProxy.SessionState == Matrix.SessionState.Disconnected)
                    {                    
                        xmppProxy.XmppDomain = el.Cast<Open>().To;
                        await xmppProxy.ConnectAsync(CancellationToken.None);
                    }
                    else
                    {
                        await xmppProxy.ResetStreamAsync(CancellationToken.None);
                    }

                    return;
                }
                else if (el.OfType<Close>())
                {
                    if (xmppProxy != null && xmppProxy.SessionState != Matrix.SessionState.Disconnected)
                    {
                        await xmppProxy.DisconnectAsync();
                        await ctx.WriteAndFlushAsync(new TextWebSocketFrame(new Close().ToString(false)));
                    }
                }
                else
                {
                    await xmppProxy.SendAsync(el);
                }               
      
                return;
            }
        }

        static void SendHttpResponse(IChannelHandlerContext ctx, IFullHttpRequest req, IFullHttpResponse res)
        {
            // Generate an error page if response getStatus code is not OK (200).
            if (res.Status.Code != 200)
            {
                IByteBuffer buf = Unpooled.CopiedBuffer(Encoding.UTF8.GetBytes(res.Status.ToString()));
                res.Content.WriteBytes(buf);
                buf.Release();
                HttpUtil.SetContentLength(res, res.Content.ReadableBytes);
            }

            // Send the response and close the connection if necessary.
            Task task = ctx.Channel.WriteAndFlushAsync(res);
            if (!HttpUtil.IsKeepAlive(req) || res.Status.Code != 200)
            {
                task.ContinueWith((t, c) => ((IChannelHandlerContext)c).CloseAsync(), 
                    ctx, TaskContinuationOptions.ExecuteSynchronously);
            }
        }

        /// <summary>
        /// Sends a text websocket frame.
        /// </summary>
        /// <param name="text">The text to send withing the frame.</param>
        /// <returns></returns>
        public async Task SendText(string text)
        {
            logger.LogDebug($"SEND: {text}");
            await this.ctx.WriteAndFlushAsync(new TextWebSocketFrame(text));
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception ex)
        {
            logger.LogError($"{ex}");
            ctx.CloseAsync();
        }

        private string GetWebSocketLocation(IFullHttpRequest req)
        {
            bool result = req.Headers.TryGet(HttpHeaderNames.Host, out ICharSequence value);
            string location= value.ToString() + this.settings.Path;

            if (settings.Ssl)
            {
                return "wss://" + location;
            }
            else
            {
                return "ws://" + location;
            }
        }
    }
}
