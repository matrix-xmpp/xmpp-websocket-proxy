namespace XmppWebSocketProxy.Configuration
{
    public class ServerConfiguration
    {
        public bool Ssl { get; set; }
        public int Port { get; set; }
        public bool Libuv { get; set; }
        public string Path { get; set; }
        public Certificate Certificate { get; set; }
    }
}
