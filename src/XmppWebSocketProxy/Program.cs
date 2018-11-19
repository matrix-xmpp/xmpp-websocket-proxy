namespace XmppWebSocketProxy
{
    using DotNetty.Common;    
    using Microsoft.Extensions.DependencyInjection;

    class Program
    {
        static Program()
        {
            ResourceLeakDetector.Level = ResourceLeakDetector.DetectionLevel.Disabled;
        }     

        static void Main()
        {            
            ServiceLocator
                .Current.GetService<Server>()
                .RunServerAsync()
                .Wait();
        }
    }
}
