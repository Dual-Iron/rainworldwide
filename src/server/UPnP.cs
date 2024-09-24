using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text;
using System;

namespace Server;

public enum UpnpState
{
    InProgress, Finished, Error
}

// Taken from https://pastebin.com/raw/wLUcLgvx which is from https://github.com/RevenantX/LiteNetLib/issues/11
static public class Upnp
{
    const string name = "RWWide";

    static string xmlURL = "";
    static string RouterIP = "";
    static string insertIP = "";
    static string Port = "";
    static bool Gateway;
    static List<string> maplayout = [];
    static readonly List<string> ServiceTypes = [];
    static readonly List<string> ControlTypes = [];

    public static UpnpState State { get; private set; }

    public static void Open(int port)
    {
        insertIP = GetIP();
        Port = port.ToString();
        System.Threading.ThreadPool.QueueUserWorkItem(_ => Response());
    }

    static string GetIP()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());

        foreach (var ip in host.AddressList) {
            if (ip.AddressFamily == AddressFamily.InterNetwork) {
                return ip.ToString();
            }
        }

        throw new Exception("No network adapters with an IPv4 address in the system!");
    }

    static void Response()
    {
        Log($"Started attempt to open port {Port}");

        bool finished = false;

        string[] foundDevices = new string[200];
        int deviceCount = 0;

        var ssdpMsg = "M-SEARCH * HTTP/1.1\r\nHOST:239.255.255.250:1900\r\nMAN:\"ssdp:discover\"\r\nST:ssdp:all\r\nMX:5\r\n\r\n";
        var ssdpPacket = Encoding.ASCII.GetBytes(ssdpMsg);
        IPAddress multicastAddress = IPAddress.Parse("239.255.255.250");
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastAddress, IPAddress.Any));
        socket.SendTo(ssdpPacket, SocketFlags.None, new IPEndPoint(multicastAddress, 1900));
        var response = new byte[8000];
        EndPoint ep = new IPEndPoint(IPAddress.Any, 0);

        int tries = 0;
        while (!finished) {
            try {
                DoTheThing(ref finished, foundDevices, ref deviceCount, socket, response, ref ep);
            }
            catch (Exception e) {
                Log($"UPnP error: {e.Message}");
            }

            if (++tries > 10) {
                State = UpnpState.Error;
                Log($"Ceased attempt to open port {Port}");
                return;
            }
        }

        State = UpnpState.Finished;
        Log($"Successfully opened port {Port}");
    }

    private static void DoTheThing(ref bool finished, string[] foundDevices, ref int deviceCount, Socket socket, byte[] response, ref EndPoint ep)
    {
        socket.ReceiveFrom(response, ref ep);
        var str = Encoding.UTF8.GetString(response);
        var aSTR = str.Split('\n');
        foreach (string i in aSTR) {

            if (i.IndexOf("SERVER: ", StringComparison.CurrentCultureIgnoreCase) != -1) {
                string newDev = i.Replace("Server: ", "");
                if (!foundDevices.Contains(newDev)) {
                    foundDevices[deviceCount] = newDev;
                    deviceCount++;
                }
            }

            if (i.IndexOf("LOCATION: ", StringComparison.CurrentCultureIgnoreCase) != -1) {
                xmlURL = Regex.Replace(i, "Location: ", "", RegexOptions.IgnoreCase);
                RouterIP = GetBetween(i, "http://", "/");
            }

            if (i.IndexOf("InternetGatewayDevice", StringComparison.CurrentCultureIgnoreCase) != -1) {
                Gateway = true;
            }

            if (!Gateway) {
                continue;
            }

            using WebClient webClient = new();
            string routerXML = webClient.DownloadString(xmlURL);

            if (routerXML.Contains("urn:schemas-upnp-org:service:WANIPConnection:0"))
                ServiceTypes.Add("urn:schemas-upnp-org:service:WANIPConnection:0");

            if (routerXML.Contains("urn:schemas-upnp-org:service:WANIPConnection:1"))
                ServiceTypes.Add("urn:schemas-upnp-org:service:WANIPConnection:1");

            if (routerXML.Contains("urn:schemas-upnp-org:service:WANPPPConnection:0"))
                ServiceTypes.Add("urn:schemas-upnp-org:service:WANPPPConnection:0");

            if (routerXML.Contains("urn:schemas-upnp-org:service:WANPPPConnection:1"))
                ServiceTypes.Add("urn:schemas-upnp-org:service:WANPPPConnection:1");

            if (ServiceTypes.Count == 0) {
                socket.Close();
                finished = true;
            }

            foreach (string svcType in ServiceTypes) {
                int strIndex = routerXML.IndexOf(svcType);
                string ctrlType = GetBetween(routerXML.Substring(strIndex), "<controlURL>", "</controlURL>");
                ControlTypes.Add(ctrlType);
            }

            socket.Shutdown(SocketShutdown.Both);
            GetMappings();
            finished = true;
        }
    }

    static void GetMappings()
    {
        for (int s = 0; s < ServiceTypes.Count; s++) {
            maplayout = [];
            string xmlresponse = "";
            int i = 0;
            int dex = 0;
            switch (i) {

                case 0:
                    var soapBody =
                        "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:"
                            + "GetGenericPortMappingEntry"
                            + "xmlns:u=\"" + ServiceTypes[s] + "\">"
                            + "<NewPortMappingIndex>" + dex.ToString() + "</NewPortMappingIndex>"
                            + "<NewRemoteHost></NewRemoteHost>"
                            + "<NewExternalPort></NewExternalPort>"
                            + "<NewProtocol></NewProtocol>"
                            + "<NewInternalPort></NewInternalPort>"
                            + "<NewInternalClient></NewInternalClient>"
                            + "<NewEnabled>1</NewEnabled>"
                            + "<NewPortMappingDescription></NewPortMappingDescription>"
                            + "<NewLeaseDuration></NewLeaseDuration>"
                            + "</u:GetGenericPortMappingEntry></s:Body></s:Body></s:Envelope>\r\n\r\n";

                    byte[] body = Encoding.ASCII.GetBytes(soapBody);
                    var url = "http://" + RouterIP + ControlTypes[s];

                    try {
                        var wr = WebRequest.Create(url);
                        wr.Method = "POST";
                        wr.Headers.Add("SOAPAction", "\"" + ServiceTypes[s] + "#GetGenericPortMappingEntry" + "\"");
                        wr.ContentType = "text/xml;charset=\"utf-8\"";
                        wr.ContentLength = body.Length;
                        var stream = wr.GetRequestStream();
                        stream.Write(body, 0, body.Length);
                        stream.Flush();
                        stream.Close();

                        using HttpWebResponse response = (HttpWebResponse)wr.GetResponse();
                        Stream receiveStream = response.GetResponseStream();
                        StreamReader readStream = new(receiveStream, Encoding.UTF8);
                        xmlresponse = readStream.ReadToEnd();
                        response.Close();
                        readStream.Close();
                    }
                    catch {
                        goto case 2;
                    }
                    goto case 1;

                case 1:
                    string thisip = GetBetween(xmlresponse, "<NewInternalClient>", "</NewInternalClient>");
                    string thisport = GetBetween(xmlresponse, "<NewInternalPort>", "</NewInternalPort>");
                    string thisprod = GetBetween(xmlresponse, "<NewPortMappingDescription>", "</NewPortMappingDescription>");

                    if (thisip == insertIP && thisprod == name)
                        break;

                    dex++;
                    maplayout.Add(thisport);
                    goto case 0;

                case 2:
                    for (int p = int.Parse(Port); p < ushort.MaxValue; p++) {
                        if (!maplayout.Contains(p.ToString())) {
                            Port = p.ToString();
                            SetPort(s);
                            break;
                        }
                    }
                    break;
            }
        }
    }

    static void SetPort(int count)
    {
        string portType = "UDP";

        // NewLeaseDuration 0 means default value of 604800 seconds is used
        // http://upnp.org/specs/gw/UPnP-gw-WANIPConnection-v2-Service.pdf
        var soapBody =
        "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:"
                + "AddPortMapping"
                + " xmlns:u=\"" + ServiceTypes[count] + "\">"
                + "<NewRemoteHost></NewRemoteHost>"
                + "<NewExternalPort>" + Port + "</NewExternalPort>"
                + "<NewProtocol>" + portType + "</NewProtocol>"
                + "<NewInternalPort>" + Port + "</NewInternalPort>"
                + "<NewInternalClient>" + insertIP + "</NewInternalClient>"
                + "<NewEnabled>1</NewEnabled>"
                + "<NewPortMappingDescription>" + name + "</NewPortMappingDescription>"
                + "<NewLeaseDuration>0</NewLeaseDuration>"
                + "</u:AddPortMapping></s:Body></s:Body></s:Envelope>\r\n\r\n";

        byte[] body = Encoding.ASCII.GetBytes(soapBody);
        var url = "http://" + RouterIP + ControlTypes[count];
        var wr = WebRequest.Create(url);
        wr.Method = "POST";
        wr.Headers.Add("SOAPAction", "\"" + ServiceTypes[count] + "#" + "AddPortMapping" + "\"");
        wr.ContentType = "text/xml;charset=\"utf-8\"";
        wr.ContentLength = body.Length;
        var stream = wr.GetRequestStream();
        stream.Write(body, 0, body.Length);
        stream.Flush();
        stream.Close();
    }

    static string GetBetween(string strSource, string strStart, string strEnd)
    {
        int Start, End;
        Start = strSource.IndexOf(strStart, 0) + strStart.Length;
        End = strSource.IndexOf(strEnd, Start);
        return strSource.Substring(Start, End - Start);
    }
}
