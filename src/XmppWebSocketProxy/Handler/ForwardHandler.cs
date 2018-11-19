namespace XmppWebSocketProxy.Handler
{
    using Matrix.Attributes;
    using Matrix.Network.Handlers;
    using Matrix.Xml;
    using Matrix.Xmpp.Base;
    using Matrix.Xmpp.Framing;

    /// <summary>
    /// A handler for forwarding stanzas in the XMPP tcp transport stream
    /// to the websocket connection
    /// </summary>
    /// <seealso cref="Matrix.Network.Handlers.XmppStanzaHandler" />
    [Name("Forward-Handler")]
    public class ForwardHandler : XmppStanzaHandler
    {
        public ForwardHandler(WebSocketServerHandler websocket)
        {
            // handle stream header
            Handle(
                el => el is Stream,

                async (context, xmppXElement) =>
                {
                    var stream = xmppXElement.Cast<Stream>();

                    var open = new Open() {
                        
                        From = stream.From,
                        Id = stream.Id,
                        Version = stream.Version
                    };
                    await websocket.SendText(open.ToString(false));
                });

            // handle all other stanzas
            Handle(
                el => !(el is Stream),

                async (context, xmppXElement) =>
                {
                    await websocket.SendText(xmppXElement.ToString(false));
                });
        }
    }
}
