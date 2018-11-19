namespace XmppWebSocketProxy
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Libuv;
    using XmppWebSocketProxy.Configuration;

    public class EventLoopGroups
    {
        public EventLoopGroups(ServerConfiguration serverConfiguration)
        {
            IEventLoopGroup bossGroup;
            IEventLoopGroup workGroup;
            
            if (serverConfiguration.Libuv)
            {
                var dispatcher = new DispatcherEventLoopGroup();
                bossGroup = dispatcher;
                workGroup = new WorkerEventLoopGroup(dispatcher);
            }
            else
            {
                bossGroup = new MultithreadEventLoopGroup(1);
                workGroup = new MultithreadEventLoopGroup();
            }

            BossGroup = bossGroup;
            WorkerGroup = workGroup;            
        }

        public IEventLoopGroup WorkerGroup { get; private set; }
        public IEventLoopGroup BossGroup { get ; private set; }
    }
}
