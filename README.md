# XMPP WebSocket Proxy
This is a standalone Websocket connection manager for XMPP. It allows to proxy XMPP WebSocket connections to XMPP streams using the standard TCP XMPP transport.
Not every public or provate XMPP server is running a XMPP WebSocket connection manager. Use can use this project to connect to **any** XMPP server over websockets.

## Dependencies
* [.NET Core](https://dotnet.github.io/)
* [DotNetty](https://github.com/Azure/DotNetty)
* [MatriX vNext](https://github.com/matrix-xmpp/matrix-vnext)

## Build and run
```
git clone https://github.com/matrix-xmpp/xmpp-websocket-proxy.git

cd ./src/XmppWebSocketProxy/
dotnet restore
dotnet build
dotnet run
```

## Configure
Configuration is done over appsettings.json

| config key                    | description                                 |
|-------------------------------|---------------------------------------------|
| Server__Libuv                 | use Libuv (true/false)                      |
| Server__Port                  | port to listen on for WebSocket connections |
| Server__Ssl                   | Use ws:// or wss:// protocol                |
| Server__Certificate__Location | path to teh Ssl certificate when using wss  |
| Server__Certificate__Password | password for the cert file                  |
