namespace XmppWebSocketProxy.Handler
{
    using Matrix.Attributes;
    using Matrix.Network.Handlers;
    using Matrix.Xml;
    using Matrix.Xmpp.Base;

    /// <summary>
    /// A handler which captures the id and version from the XMPP
    /// TCP socket transport stream
    /// </summary>
    /// <seealso cref="Matrix.Network.Handlers.XmppStanzaHandler" />
    [Name("StreamPropsCapture-Handler")]
    public class StreamPropsCaptureHandler : XmppStanzaHandler
    {
        /// <summary>
        /// Gets the XMPP stream identifier.
        /// </summary>        
        public string Id { get; private set; }

        /// <summary>
        /// Gets the version.
        /// </summary>        
        public string Version { get; private set; }
        
        /// <summary>
        /// Gets the server domain.
        /// </summary>        
        public string From { get; private set; }

        public StreamPropsCaptureHandler()
        {
            // handle stream header
            Handle(
                el => el is Stream,
                (context, xmppXElement) =>
                {
                    var stream = xmppXElement.Cast<Stream>();

                    this.Id = stream.Id;
                    this.Version = stream.Version;
                    this.From = stream.From;
                });
        }
    }
}
