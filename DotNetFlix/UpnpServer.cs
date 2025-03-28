using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DotNetFlix;

internal class UpnpServer
{
    private const string MulticastAddress = "239.255.255.250";
    private const string ServerIpAddress = "192.168.1.238";
    private const int MulticastPort = 1900;
    public const string UUID = "0871be82-ba56-4601-88d7-4cd06a1f30bd";

    private string AliveMessage =>
    "NOTIFY * HTTP/1.1\r\n" +
    "HOST: 239.255.255.250:1900\r\n" +
    "CACHE-CONTROL: max-age=1800\r\n" +
    "LOCATION: http://192.168.1.238/upnp/Description.xml\r\n" +
    "SERVER: WIN64/6.2 UPnP/1.0 DLNADOC/1.5 sdlna/1.0\r\n" +
    "NTS: ssdp:alive\r\n" +
    "NT: upnp:rootdevice\r\n" +
    "USN: uuid:"+UUID+"::upnp:rootdevice\r\n" +
    "\r\n"
    ;

    readonly Thread _thread;

    public UpnpServer() 
    {
        _thread = new Thread(Run);
        _thread.Start();
    }

    void Run()
    {
        using var udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(ServerIpAddress), 0)); 
        var multicastIpAddress = IPAddress.Parse(MulticastAddress);
        var endpoint = new IPEndPoint(multicastIpAddress, MulticastPort);
        udpClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);

        while (true)
        {
            var messageBytes = Encoding.UTF8.GetBytes(AliveMessage);
            udpClient.Send(messageBytes, messageBytes.Length, endpoint);
            Console.WriteLine("Sent SSDP:ALIVE message.");
            Thread.Sleep(10000);  
        }
    }
}
