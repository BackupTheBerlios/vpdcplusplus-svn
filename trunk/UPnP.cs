using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.IO;

namespace DCPlusPlus
{
    //TODO add MediaRenderer support + mini web server to stream downloads to the MediaRenderer
    //     add upnp lights support  (i.e. dim selected light to a defined percentage or turn them out completely
    //     if a video stream is beeing played ... (i will not be able to debug this in rl ;-) )

    /// <summary>
    /// A simple class to use Routers
    /// who support the UPnP Protocol
    /// takes care of discovery and
    /// can setup port mappings and
    /// tell the external ip address
    /// 
    /// (big thanks to Zac Bowling for providing such a nice intro for Upnp 
    /// http://zbowling.com/projects/upnp/)
    /// </summary>
    [TestFixture]
    public class UPnP
    {
        /// <summary>
        /// a class to help with making soap requests
        /// </summary>
        public class SOAP
        {
            private List<string> headers;
            public List<string> Headers
            {
                get { return headers; }
                set { headers = value; }
            }
            private string response;

            public string Response
            {
                get { return response; }
                set { response = value; }
            }

            private string body;

            public string Body
            {
                get { return body; }
                set { body = value; }
            }
            private string url;

            public string Url
            {
                get { return url; }
                set { url = value; }
            }

            private string request_method;

            public string RequestMethod
            {
                get { return request_method; }
                set { request_method = value; }
            }

            private string soap_action;

            public string SoapAction
            {
                get { return soap_action; }
                set { soap_action = value; }
            }

            protected bool busy = false;
            /// <summary>
            /// Get the Status of the retrieval
            /// </summary>
            public bool IsBusy
            {
                get
                {
                    return (busy);
                }
            }


            public SOAP()
            {

            }
            public SOAP(string url, string body, string soap_action, string request_method)
            {
                this.url = url;
                this.body = body;
                this.soap_action = soap_action;
                this.request_method = request_method;
            }
            public SOAP(string url, string body, string soap_action)
            {
                this.url = url;
                this.body = body;
                this.soap_action = soap_action;
                this.request_method = "POST";
            }

            protected Socket socket = null;
            protected byte[] receive_buffer = null;
            private void Disconnect()
            {
                if (busy)
                {
                    try
                    {
                        if (socket != null && socket.Connected)
                        {
                            //if(receive_operation!=null) socket //socket.EndReceive(receive_operation);
                            socket.Shutdown(SocketShutdown.Both);
                            //Thread.Sleep(10);
                            socket.Close();
                            socket = null;
                            //receive_operation = null;
                            busy = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Uri uri = new Uri(url);
                        Console.WriteLine("Error during disconnecting  from server: " + uri.Host + ":" + uri.Port+"(exception: "+ex.Message);
                    }
                }
            }
            /// <summary>
            /// Connect to the server
            /// </summary>
            private void Connect()
            {
                if (busy)
                {//better handling of fast user retries
                    Disconnect();
                }
                //Console.WriteLine("Connecting to Hub: "+name);
                Uri uri = new Uri(url);
                try
                {
                    busy = true;
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    AsyncCallback event_host_resolved = new AsyncCallback(OnHostResolve);
                    Dns.BeginGetHostEntry(uri.DnsSafeHost, event_host_resolved, socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to server: " + uri.Host+":"+uri.Port + "(exception:" + ex.Message + ")");
                    Disconnect();
                    if (RequestFinished != null)
                        RequestFinished(this, false);
                }
            }
            /// <summary>
            /// Callback for hostname resolving
            /// </summary>
            /// <param name="result">Async Result/State</param>
            private void OnHostResolve(IAsyncResult result)
            {
                Socket resolve_socket = (Socket)result.AsyncState;
                Uri uri = new Uri(url);
                try
                {
                    IPHostEntry ip_entry = Dns.EndGetHostEntry(result);
                    if (ip_entry != null && ip_entry.AddressList.Length > 0)
                    {
                        string ip = ip_entry.AddressList[0].ToString(); // correct the ip string
                        IPEndPoint endpoint = new IPEndPoint(ip_entry.AddressList[0], uri.Port);
                        AsyncCallback event_connect = new AsyncCallback(OnConnect);
                        socket.BeginConnect(endpoint, event_connect, socket);
                    }
                    else
                    {
                        Console.WriteLine("Unable to connect to server: " + uri.Host + ":" + uri.Port + ")");
                        Disconnect();
                        if (RequestFinished != null)
                            RequestFinished(this, false);
                    }

                }

                catch (SocketException sex)
                {
                    if (sex.ErrorCode == 11001) //TODO i know , or correctly i dont know ...
                    {
                        Console.WriteLine("Error during Address resolve of server: " + uri.Host + ":" + uri.Port );
                        Disconnect();
                        if (RequestFinished != null)
                            RequestFinished(this, false);
                    }
                    else
                    {
                        Console.WriteLine("Error during Address resolve of server: " + uri.Host + ":" + uri.Port );
                        Disconnect();
                        if (RequestFinished != null)
                            RequestFinished(this, false);
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error during Address resolve of server: " + uri.Host + ":" + uri.Port + " exception: "+ex.Message);
                    Disconnect();
                    if (RequestFinished != null)
                        RequestFinished(this, false);
                }
            }
            /// <summary>
            /// Callback for server connecting
            /// </summary>
            /// <param name="result">Async Result/State</param>
            private void OnConnect(IAsyncResult result)
            {
                Socket connect_socket = (Socket)result.AsyncState;
                try
                {
                    if (connect_socket.Connected)
                    {
                        AsyncCallback event_receive = new AsyncCallback(OnReceive);
                        receive_buffer = new byte[32768];
                        connect_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, connect_socket);
                        //Console.WriteLine("Successfully connected to Hub: " + name);
                        string request = PrepareRequest();
                        SendRequest(request);
                    }
                    else
                    {
                        Uri uri = new Uri(url);
                        Console.WriteLine("Error during connect to server: " + uri.Host + ":" + uri.Port);
                        Disconnect();
                        if (RequestFinished != null)
                            RequestFinished(this, false);
                    }

                }
                catch (Exception ex)
                {
                    Uri uri = new Uri(url);
                    Console.WriteLine("Error during connect to server: " + uri.Host + ":" + uri.Port + "(exception:" + ex.Message + ")");
                    Disconnect();
                    if (RequestFinished != null)
                        RequestFinished(this, false);
                }
            }
            /// <summary>
            /// Prepares the request by combining several properties
            /// and returning a request(headers+body)
            /// </summary>
            /// <returns>a complete request to send to a server</returns>
            private string PrepareRequest()
            {
                //compile a request string
                Uri uri = new Uri(url);
                string request = "";
                request += request_method + " " + uri.AbsolutePath + " HTTP/1.1\r\n";
                request += "Content-Type: "+"text/xml; charset=\"utf-8\"\r\n";
                request += "SOAPACTION: \"" + soap_action + "\"\r\n";
                request += "User-Agent: Mozilla/4.0 (compatible; UPnP/1.0; Windows 9x)\r\n";
                request += "HOST: " + uri.Host + ":"+uri.Port+"\r\n";
                request += "Content-Length: " + body.Length + "\r\n";
                //request += "Connection: Keep-Alive\r\n";
                request += "Connection: close\r\n";
                request += "Cache-Control: no-cache\r\n";
                request += "Pragma: no-cache\r\n";
                //add custom headers here
                request += "\r\n";
                request += body;
                return (request);

            }    
            /// <summary>
            /// Callback to receive data from the server
            /// </summary>
            /// <param name="result">Async Result/State</param>
            private void OnReceive(IAsyncResult result)
            {
                Socket receive_socket = (Socket)result.AsyncState;
                if (!receive_socket.Connected) return;//TODO change to disconnect();
                try
                {
                    int received_bytes = receive_socket.EndReceive(result);
                    if (received_bytes > 0)
                    {
                        //string received_string = Encoding.ASCII.GetString(receive_buffer, 0, received_bytes);
                        string received_string = System.Text.Encoding.Default.GetString(receive_buffer, 0, received_bytes);
                        //Console.WriteLine("Received a string: "+received_string);
                        response += received_string;
                        AsyncCallback event_receive = new AsyncCallback(OnReceive);
                        receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                    }
                    else
                    {
                        Disconnect();
                        if (RequestFinished != null)
                            RequestFinished(this, true);
                    }

                }
                catch (Exception ex)
                {
                    Uri uri = new Uri(url);
                    Console.WriteLine("Error during connect to server: " + uri.Host + ":" + uri.Port + "(exception:" + ex.Message + ")");
                    Disconnect();
                    if (RequestFinished != null)
                        RequestFinished(this, false);
                }

            }
            private void SendRequest(string request)
            {
                string send_string = request;
                try
                {
                    //socket.Send(Encoding.UTF8.GetBytes(send_string), SocketFlags.None);
                    byte[] send_bytes = System.Text.Encoding.Default.GetBytes(send_string);
                    socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(SendRequestCallback), socket);
                }
                catch (Exception ex)
                {
                    Uri uri = new Uri(url);
                    Console.WriteLine("Error during sending request to server: " + uri.Host + ":" + uri.Port + "(exception:" + ex.Message + ")");
                    Disconnect();
                    if (RequestFinished != null)
                        RequestFinished(this, false);
                }
            }
            /// <summary>
            /// Callback for the send chat command
            /// </summary>
            /// <param name="ar">Async Result/State</param>
            private void SendRequestCallback(IAsyncResult ar)
            {
                Socket send_request_socket = (Socket)ar.AsyncState;
                try
                {
                    int bytes = send_request_socket.EndSend(ar);

                    //TODO check if bytes send == request.length
                }
                catch (Exception ex)
                {
                    Uri uri = new Uri(url);
                    Console.WriteLine("Error during sending request to server: " + uri.Host + ":" + uri.Port + "(exception:" + ex.Message + ")");
                    Disconnect();
                    if (RequestFinished != null)
                        RequestFinished(this, false);
                }
            }


            public void AbortRequest()
            {
                Disconnect();
            }

            public void StartRequest()
            {
                if (url != "" && body != "" && soap_action != "" && request_method != "" && !busy)
                    Connect();

            }
                        
            public delegate void RequestFinishedEventHandler(SOAP request,bool request_successful);
            public event RequestFinishedEventHandler RequestFinished;
       }

        /// <summary>
        /// an abstract upnp device class
        /// </summary>
        public class Device
        {

            protected string unique_service_name = "";
            public string UniqueServiceName
            {
                get
                {
                    return (unique_service_name);
                }
                set
                {
                    unique_service_name = value;
                }
            }

            protected string location = "";
            public string Location
            {
                get
                {
                    return (location);
                }
                set
                {
                    location = value;
                }
            }

            protected string host = "";
            public string Host
            {
                get
                {
                    return (host);
                }
                set
                {
                    host = value;
                }
            }

            protected int port = 80;
            public int Port
            {
                get
                {
                    return (port);
                }
                set
                {
                    port = value;
                }
            }

            protected int spec_version_major = 0;
            public int SpecVersionMajor
            {
                get
                {
                    return (spec_version_major);
                }
                set
                {
                    spec_version_major = value;
                }
            }

            protected int spec_version_minor = 0;
            public int SpecVersionMinor
            {
                get
                {
                    return (spec_version_minor);
                }
                set
                {
                    spec_version_minor = value;
                }
            }

            protected string server = "";
            public string Server
            {
                get
                {
                    return (server);
                }
                set
                {
                    server = value;
                }
            }

            protected string url_base = "";
            public string UrlBase
            {
                get
                {
                    return (url_base);
                }
                set
                {
                    url_base = value;
                }
            }

            protected string universal_unique_id = "";
            public string UniversalUniqueID
            {
                get
                {
                    return (universal_unique_id);
                }
                set
                {
                    universal_unique_id = value;
                }
            }
        }
        /// <summary>
        /// a class to represent an upnp device's subdevice
        /// </summary>
        public class SubDevice
        {
            /// <summary>
            /// a class to represent a service of a subdevice
            /// </summary>
            public class Service
            {

                protected string service_type = "";
                public string ServiceType
                {
                    get
                    {
                        return (service_type);
                    }
                    set
                    {
                        service_type = value;
                    }
                }

                protected string service_id = "";
                public string ServiceID
                {
                    get
                    {
                        return (service_id);
                    }
                    set
                    {
                        service_id = value;
                    }
                }

                /// <summary>
                /// Service Control Protocol Description Url
                /// </summary>
                protected string scpd_url = "";
                public string SCPDUrl
                {
                    get
                    {
                        return (scpd_url);
                    }
                    set
                    {
                        scpd_url = value;
                    }
                }

                protected string control_url = "";
                public string ControlUrl
                {
                    get
                    {
                        return (control_url);
                    }
                    set
                    {
                        control_url = value;
                    }
                }

                protected string event_sub_url = "";
                public string EventSubUrl
                {
                    get
                    {
                        return (event_sub_url);
                    }
                    set
                    {
                        event_sub_url = value;
                    }
                }

            }

            public Service GetServiceByType(string type)
            {
                foreach (SubDevice device in devices)
                {
                    Service temp = device.GetServiceByType(type);
                    if (temp != null) return (temp);
                }

                foreach (Service service in services)
                {
                    if (service.ServiceType == type)
                        return (service);
                }
                return (null);
            }


            protected List<Service> services = new List<Service>();
            public List<Service> Services
            {
                get
                {
                    return (services);
                }
            }
            protected List<SubDevice> devices = new List<SubDevice>();
            public List<SubDevice> Devices
            {
                get
                {
                    return (devices);
                }
            }


            protected string device_type = "";
            public string DeviceType
            {
                get
                {
                    return (device_type);
                }
                set
                {
                    device_type = value;
                }
            }

            protected string presentation_url = "";
            public string PresentationUrl
            {
                get
                {
                    return (presentation_url);
                }
                set
                {
                    presentation_url = value;
                }
            }

            protected string friendly_name = "";
            public string FriendlyName
            {
                get
                {
                    return (friendly_name);
                }
                set
                {
                    friendly_name = value;
                }
            }

            protected string manufacturer = "";
            public string Manufacturer
            {
                get
                {
                    return (manufacturer);
                }
                set
                {
                    manufacturer = value;
                }
            }

            protected string manufacturer_url = "";
            public string ManufacturerUrl
            {
                get
                {
                    return (manufacturer_url);
                }
                set
                {
                    manufacturer_url = value;
                }
            }

            protected string model_description = "";
            public string ModelDescription
            {
                get
                {
                    return (model_description);
                }
                set
                {
                    model_description = value;
                }
            }

            protected string model_name = "";
            public string ModelName
            {
                get
                {
                    return (model_name);
                }
                set
                {
                    model_name = value;
                }
            }

            protected string universal_unique_id= "";
            public string UniversalUniqueID
            {
                get
                {
                    return (universal_unique_id);
                }
                set
                {
                    universal_unique_id = value;
                }
            }


        }
        /// <summary>
     /// A class to use UPnP supporting Routers
     /// </summary>
        public class Router : Device
        {

            protected SubDevice root_device;
            public SubDevice RootDevice
            {
                get
                {
                    return (root_device);
                }
            }

            /*NOTIFY * HTTP/1.1
              HOST: 239.255.255.250:1900
              CACHE-CONTROL: max-age=3600
              LOCATION: http://192.168.0.1:80/Public_UPNP_gatedesc.xml
              NT: urn:schemas-upnp-org:service:WANIPConnection:1
              NTS: ssdp:alive
              SERVER: Ambit OS/1.0 UPnP/1.0 AMBIT-UPNP/1.0
              USN: uuid:c35df0ba-dd97-a13f-3af2-f7b1f2fa729b::urn:schemas-upnp-org:service:WANIPConnection:1
            */

            //TODO add cache management 
            //upnp checking expires or cache-control header value
            //to remove the router instance from the devices list
            //router will fire expired event to notify upnp

            /// <summary>
            /// TRUE if the router has updated status infos
            /// </summary>
            public bool HasStatusInfo
            {
                get
                {
                    return (false);
                }
            }
            /// <summary>
            /// TRUE if the basic router information has been gathered
            /// (router information is needed to control it or get the status)
            /// </summary>
            public bool HasInformation
            {
                get
                {
                    return (false);
                }
            }

            private string external_ip;

            public string ExternalIP
            {
                get { return external_ip; }
                set { external_ip = value; }
            }

            private long uptime;

            public long Uptime
            {
                get { return uptime; }
                set { uptime = value; }
            }

            private bool connected;

            public bool Connected
            {
                get { return connected; }
                set { connected = value; }
            }
	
	
            //protected string NotificationSubType NTS
            //protected string NotificationType NT

            //TODO move xml interpretation to upnp as static methods
            //Problem how get values back into router class
            //method shall return an router info class (info class base)
            //which to use is based on a parameter (but this way the methods still need to 'know' about all possible info classes)
            //at least some of them
            private WebClient information_wc;
            //private WebClient information_wc2;

            public void GatherInformation()
            {
                //get location url
                //interpret xml returned
                //and set values accordingly
                //find method urls of the router services
                Console.WriteLine("Starting to gather router information.");


                if (string.IsNullOrEmpty(location))
                {
                    if (InformationGathered != null)
                        InformationGathered(this, false);
                    return; //a location is really all we need here,but if its not there just return
                }
                if (!busy)
                {
                    information_wc = new WebClient();
                    if (information_wc.IsBusy)
                        Console.WriteLine("Damn the client is already busy.. wtf ?");
                    //information_wc.Headers.Add("Connection","close");
                    //information_wc.Headers.Add(HttpRequestHeader.KeepAlive, "close");
                    information_wc.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs e)
                    {
                        Console.WriteLine("download completed");
                        try
                        {
                            if (e.Cancelled)
                            {
                                if (InformationGathered != null)
                                    InformationGathered(this, false);
                                return;
                            }
                            if (e.Result.Length <= 0)
                            {
                                if (InformationGathered != null)
                                    InformationGathered(this, false);
                                return;
                            }
                            string page_string = "";
                            page_string = System.Text.Encoding.Default.GetString(e.Result);
                            information_wc.Dispose();
                            GetGatewayDescription(page_string);

                            //searching sub devices for a wan ip connections service
                            if (root_device != null)
                            {
                                SubDevice.Service wan_ip_connection = root_device.GetServiceByType("urn:schemas-upnp-org:service:WANIPConnection:1");
                                if (wan_ip_connection != null)
                                {
                                    Console.WriteLine("Found the wan ip connection service.");
                                    Console.WriteLine("service control url: " + url_base + wan_ip_connection.ControlUrl);
                                }
                            }

                            //download wanip description
                            //GetWanIPDescription(page_string);

                            if (InformationGathered != null)
                                InformationGathered(this, true);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception after download: " + ex.Message);
                            if (InformationGathered != null)
                                InformationGathered(this, false);
                            return;
                        }
                    };
                    busy = true;
                    try
                    {
                        Console.WriteLine("starting download of: "+location);
                        information_wc.DownloadDataAsync(new Uri(location));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception occured during download: " + ex.Message);
                        busy = false;
                        if (InformationGathered != null)
                            InformationGathered(this, false);
                    }
                }
            }

            private void GetGatewayDescription(string xml)
            {
                Console.WriteLine("getting gateway description");
                XmlDocument doc = new XmlDocument();
                try
                {
                    //Console.WriteLine("Starting to parse xml data.");
                    doc.LoadXml(xml);
                    XmlNodeList nodelist = doc.SelectNodes("/");
                    foreach (XmlNode node in nodelist)
                    {
                        if (node.HasChildNodes)
                        {
                            foreach (XmlNode child in node.ChildNodes)
                            {
                                if (child.Name.Equals("root")) ReadGatwayDescriptionRoot(child);
                            }
                        }
                    }
                    //Console.WriteLine("Finished parsing.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error reading xml device description: "+ex.Message);
                }
            }

            private void ReadGatwayDescriptionRoot(XmlNode node)
            {
                /*
                foreach (XmlAttribute attr in node.Attributes)
                {
                    //Console.WriteLine("attr:" + attr.Name + " - " + attr.Value);
                    if (attr.Name.Equals("Name")) name = attr.Value;
                    if (attr.Name.Equals("Address")) address = attr.Value;
                }
                */
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("specVersion")) ReadGatewaySpecVersion(child);
                        if (child.Name.Equals("URLBase")) url_base = child.InnerText;
                        if (child.Name.Equals("device"))
                        {
                            root_device = new SubDevice();
                            ReadGatewayDevice(child, root_device);
                        }
                    }
                }
            }

            private void ReadGatewayDevice(XmlNode node,SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("deviceType")) device.DeviceType = child.InnerText;
                        if (child.Name.Equals("presentationURL")) device.PresentationUrl = child.InnerText;
                        if (child.Name.Equals("friendlyName")) device.FriendlyName = child.InnerText;
                        if (child.Name.Equals("manufacturer")) device.Manufacturer = child.InnerText;
                        if (child.Name.Equals("manufacturerURL")) device.ManufacturerUrl = child.InnerText;
                        if (child.Name.Equals("modelDescription")) device.ModelDescription = child.InnerText;
                        if (child.Name.Equals("modelName")) device.ModelName = child.InnerText;
                        if (child.Name.Equals("UDN"))
                        {
                            device.UniversalUniqueID = child.InnerText;
                            if (device.UniversalUniqueID.StartsWith("uuid:", true, null))
                                device.UniversalUniqueID = device.UniversalUniqueID.Substring(5);
                        }
                        if (child.Name.Equals("serviceList")) ReadGatewayDeviceServiceList(child, device);
                        if (child.Name.Equals("deviceList")) ReadGatewayDeviceDeviceList(child, device);
                    }
                }
            }

            private void ReadGatewayDeviceDeviceList(XmlNode node, SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("device"))
                        {
                            SubDevice sub_device = new SubDevice();
                            ReadGatewayDevice(child, sub_device);
                            device.Devices.Add(sub_device);
                        }
                    }
                }
            }

            private void ReadGatewayDeviceServiceList(XmlNode node, SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("service")) ReadGatewayDeviceService(child, device);
                    }
                }
            }

            private void ReadGatewayDeviceService(XmlNode node, SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    SubDevice.Service service = new SubDevice.Service();
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("serviceType")) service.ServiceType = child.InnerText;
                        if (child.Name.Equals("serviceId"))  service.ServiceID = child.InnerText;
                        if (child.Name.Equals("SCPDURL"))  service.SCPDUrl = child.InnerText;
                        if (child.Name.Equals("controlURL")) service.ControlUrl = child.InnerText;
                        if (child.Name.Equals("eventSubURL")) service.EventSubUrl = child.InnerText;
                    }
                    device.Services.Add(service);
                }
            }

            private void ReadGatewaySpecVersion(XmlNode node)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        try
                        {
                            if (child.Name.Equals("major")) spec_version_major = int.Parse(child.InnerText);
                            if (child.Name.Equals("minor")) spec_version_minor = int.Parse(child.InnerText);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("error parsing spec version: " + ex.Message);
                        }
                    }
                }
            }

            /*
            private void GetWanIPDescription(string xml)
            {

            }
            */
            protected bool busy = false;
            /// <summary>
            /// Get the Status of the information gathering
            /// </summary>
            public bool IsBusy
            {
                get
                {
                    return (busy);
                }
            }
            /// <summary>
            /// Manually abort the gathering of router information
            /// </summary>
            public void AbortGathering()
            {
                if (busy)
                {
                    try
                    {
                        information_wc.CancelAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception occured during abort: " + ex.Message);
                    }
                    System.Threading.Thread.Sleep(10);
                    //wc.IsBusy
                    //wc = new WebClient();
                    busy = false;
                }
            }
            /// <summary>
            /// Updates the status informations
            /// and fires a status updated event if the the status changed
            /// </summary>
            public void UpdateStatusInfo()
            {
                bool fire_event = false;
                //TODO add a subscribe event and delete this stub

            }

            public delegate void InformationGatheredEventHandler(Router router, bool was_successful);
            public event InformationGatheredEventHandler InformationGathered;

            public delegate void StatusUpdatedEventHandler(Router router, bool was_successful);
            public event StatusUpdatedEventHandler StatusUpdated;

            public delegate void FetchExternalIPCompletedEventHandler(Router router, bool was_successful);
            public event FetchExternalIPCompletedEventHandler FetchExternalIPCompleted;

            public delegate void PortMappingCheckCompletedEventHandler(Router router, PortMapping pm,bool exists,bool was_successful);
            public event PortMappingCheckCompletedEventHandler PortMappingCheckCompleted;

            public class PortMapping
            {
                //public int Index=0;//TODO add generic indexing of all port mappings support
                public string RemoteHost;
                public int ExternalPort;
                public int InternalPort;
                public string Protocol;
                public string InternalClient;
                public string Description="";
                public long LeaseDuration=0;
                public bool Enabled=true;
            }

            public delegate void AddingPortMappingCompletedEventHandler(Router router, PortMapping pm, bool was_successful);
            public event AddingPortMappingCompletedEventHandler AddingPortMappingCompleted;

            public delegate void DeletingPortMappingCompletedEventHandler(Router router, PortMapping pm, bool was_successful);
            public event DeletingPortMappingCompletedEventHandler DeletingPortMappingCompleted;

            public delegate void ForcedTerminationCompletedEventHandler(Router router, bool was_successful);
            public event ForcedTerminationCompletedEventHandler ForcedTerminationCompleted;

            public delegate void ConnectionRequestCompletedEventHandler(Router router, bool was_successful);
            public event ConnectionRequestCompletedEventHandler ConnectionRequestCompleted;

            public void FetchExternalIP()
            {
                if (root_device != null)
                {
                    //searching sub devices for a wan ip connections service
                    SubDevice.Service wan_ip_connection = root_device.GetServiceByType("urn:schemas-upnp-org:service:WANIPConnection:1");
                    if (wan_ip_connection != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + wan_ip_connection.ControlUrl);
                        Console.WriteLine("service type: [" + wan_ip_connection.ServiceType + "]");
                        string soap_method = "GetExternalIPAddress";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + wan_ip_connection.ServiceType + "\">\r\n";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = wan_ip_connection.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + wan_ip_connection.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            /*<NewExternalIPAddress>...</NewExternalIPAddress>*/
                                            int external_ip_start = request_finished_soap.Response.IndexOf("<NewExternalIPAddress>");
                                            if (external_ip_start != -1)
                                            {
                                                external_ip = request_finished_soap.Response.Substring(external_ip_start + "<NewExternalIPAddress>".Length);
                                                int external_ip_end = external_ip.IndexOf("</NewExternalIPAddress>");
                                                if (external_ip_end != -1)
                                                {
                                                    external_ip = external_ip.Substring(0, external_ip_end);
                                                    successful = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (FetchExternalIPCompleted != null)
                                FetchExternalIPCompleted(this, successful);
                        };
                        soap.StartRequest();

                    }
                }
            }

            public void FetchStatus()
            {

            }

            public void CheckForExistingPortMapping(PortMapping pm)
            {
                if (pm.Protocol != "TCP" && pm.Protocol != "UDP")
                   PortMappingCheckCompleted(this, pm,false, false);

                if (root_device != null)
                {
                    //searching sub devices for a wan ip connections service
                    SubDevice.Service wan_ip_connection = root_device.GetServiceByType("urn:schemas-upnp-org:service:WANIPConnection:1");
                    if (wan_ip_connection != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + wan_ip_connection.ControlUrl);
                        Console.WriteLine("service type: [" + wan_ip_connection.ServiceType + "]");
                        string soap_method = "GetSpecificPortMappingEntry";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + wan_ip_connection.ServiceType + "\">\r\n";
                        soap_body += "<NewRemoteHost>" + pm.RemoteHost + "</NewRemoteHost>";
                        soap_body += "<NewExternalPort>" + pm.ExternalPort + "</NewExternalPort>";
                        soap_body += "<NewProtocol>" + pm.Protocol + "</NewProtocol>";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = wan_ip_connection.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + wan_ip_connection.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            bool exists = false;
                            if (request_successful)
                            {
                                Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            int values_found = 0;
                                            string internal_port = "0";
                                            int internal_port_start = request_finished_soap.Response.IndexOf("<NewInternalPort>");
                                            if (internal_port_start != -1)
                                            {
                                                internal_port = request_finished_soap.Response.Substring(internal_port_start + "<NewInternalPort>".Length);
                                                int internal_port_end = internal_port.IndexOf("</NewInternalPort>");
                                                if (internal_port_end != -1)
                                                {
                                                    internal_port = internal_port.Substring(0, internal_port_end);
                                                    values_found++;
                                                }
                                            }
                                            try
                                            {
                                                pm.InternalPort = int.Parse(internal_port);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine("error parsing internal port: "+ex.Message);
                                            }
                                            int internal_client_start = request_finished_soap.Response.IndexOf("<NewInternalClient>");
                                            if (internal_client_start != -1)
                                            {
                                                pm.InternalClient = request_finished_soap.Response.Substring(internal_client_start + "<NewInternalClient>".Length);
                                                int internal_client_end = pm.InternalClient.IndexOf("</NewInternalClient>");
                                                if (internal_client_end != -1)
                                                {
                                                    pm.InternalClient = pm.InternalClient.Substring(0, internal_client_end);
                                                    values_found++;
                                                }
                                            }
                                            string enabled = "0";
                                            int enabled_start = request_finished_soap.Response.IndexOf("<NewEnabled>");
                                            if (enabled_start != -1)
                                            {
                                                enabled = request_finished_soap.Response.Substring(enabled_start + "<NewEnabled>".Length);
                                                int enabled_end = enabled.IndexOf("</NewEnabled>");
                                                if (enabled_end != -1)
                                                {
                                                    enabled = enabled.Substring(0, enabled_end);
                                                    values_found++;
                                                }
                                            }
                                            if (enabled == "1")
                                                pm.Enabled = true;
                                            else pm.Enabled = false;

                                            int description_start = request_finished_soap.Response.IndexOf("<NewPortMappingDescription>");
                                            if (description_start != -1)
                                            {
                                                pm.Description = request_finished_soap.Response.Substring(description_start + "<NewPortMappingDescription>".Length);
                                                int description_end = pm.Description.IndexOf("</NewPortMappingDescription>");
                                                if (description_end != -1)
                                                {
                                                    pm.Description = pm.Description.Substring(0, description_end);
                                                    values_found++;
                                                }
                                            }

                                            string lease_duration = "0";
                                            int lease_duration_start = request_finished_soap.Response.IndexOf("<NewLeaseDuration>");
                                            if (lease_duration_start != -1)
                                            {
                                                lease_duration = request_finished_soap.Response.Substring(lease_duration_start + "<NewLeaseDuration>".Length);
                                                int lease_duration_end = lease_duration.IndexOf("</NewLeaseDuration>");
                                                if (lease_duration_end != -1)
                                                {
                                                    lease_duration = lease_duration.Substring(0, lease_duration_end);
                                                    values_found++;
                                                }
                                            }
                                            try
                                            {
                                                pm.LeaseDuration = int.Parse(lease_duration);
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine("error parsing lease duration: " + ex.Message);
                                            }

                                            if (values_found == 5) exists = true;
                                
                                        }
                                    }
                                }
                                successful = true;
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (PortMappingCheckCompleted != null)
                                PortMappingCheckCompleted(this, pm,exists, successful);
                        };
                        soap.StartRequest();

                    }
                }

            }

            public void CheckForExistingPortMapping(string remote_host, int external_port, string protocol)
            {
                PortMapping pm = new PortMapping();
                pm.RemoteHost = remote_host;
                pm.ExternalPort = external_port;
                pm.Protocol = protocol;
                CheckForExistingPortMapping(pm);
            }

            public void DeletePortMapping(PortMapping pm)
            {

                if (pm.Protocol != "TCP" && pm.Protocol != "UDP")
                    DeletingPortMappingCompleted(this, pm, false);

                if (root_device != null)
                {
                    //searching sub devices for a wan ip connections service
                    SubDevice.Service wan_ip_connection = root_device.GetServiceByType("urn:schemas-upnp-org:service:WANIPConnection:1");
                    if (wan_ip_connection != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + wan_ip_connection.ControlUrl);
                        Console.WriteLine("service type: [" + wan_ip_connection.ServiceType + "]");
                        string soap_method = "DeletePortMapping";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + wan_ip_connection.ServiceType + "\">\r\n";
                        soap_body += "<NewRemoteHost>" + pm.RemoteHost + "</NewRemoteHost>";
                        soap_body += "<NewExternalPort>" + pm.ExternalPort + "</NewExternalPort>";
                        soap_body += "<NewProtocol>" + pm.Protocol + "</NewProtocol>";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = wan_ip_connection.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + wan_ip_connection.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            successful = true;
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (DeletingPortMappingCompleted != null)
                                DeletingPortMappingCompleted(this, pm, successful);
                        };
                        soap.StartRequest();

                    }
                }

            }

            public void DeletePortMapping(string remote_host, int external_port, string protocol)
            {
                PortMapping pm = new PortMapping();
                pm.RemoteHost = remote_host;
                pm.ExternalPort = external_port;
                pm.Protocol = protocol;
                DeletePortMapping(pm);
            }

            public void AddPortMapping(PortMapping pm)
            {
                if (pm.Protocol != "TCP" && pm.Protocol != "UDP")
                    AddingPortMappingCompleted(this, pm, false);

                if (root_device != null)
                {
                    //searching sub devices for a wan ip connections service
                    SubDevice.Service wan_ip_connection = root_device.GetServiceByType("urn:schemas-upnp-org:service:WANIPConnection:1");
                    if (wan_ip_connection != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + wan_ip_connection.ControlUrl);
                        Console.WriteLine("service type: [" + wan_ip_connection.ServiceType + "]");
                        string soap_method = "AddPortMapping";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + wan_ip_connection.ServiceType + "\">\r\n";
                        soap_body += "<NewRemoteHost>" + pm.RemoteHost + "</NewRemoteHost>";
                        soap_body += "<NewExternalPort>" + pm.ExternalPort + "</NewExternalPort>";
                        soap_body += "<NewInternalClient>" + pm.InternalClient + "</NewInternalClient>";
                        soap_body += "<NewProtocol>" + pm.Protocol + "</NewProtocol>";
                        soap_body += "<NewInternalPort>" + pm.InternalPort + "</NewInternalPort>";
                        if (pm.Enabled)
                            soap_body += "<NewEnabled>1</NewEnabled>";
                        else soap_body += "<NewEnabled>0</NewEnabled>";
                        soap_body += "<NewPortMappingDescription>" + pm.Description + "</NewPortMappingDescription>";
                        soap_body += "<NewLeaseDuration>" + pm.LeaseDuration + "</NewLeaseDuration>";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = wan_ip_connection.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + wan_ip_connection.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            successful = true;
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (AddingPortMappingCompleted != null)
                                AddingPortMappingCompleted(this, pm, successful);
                        };
                        soap.StartRequest();

                    }
                }
            }

            public void AddPortMapping(string remote_host, int external_port, int internal_port, string protocol, string internal_client, string description, long lease_duration, bool enabled)
            {
                PortMapping pm = new PortMapping();
                pm.RemoteHost= remote_host;
                pm.ExternalPort = external_port;
                pm.InternalPort = internal_port;
                pm.Protocol= protocol;
                pm.InternalClient=internal_client;
                pm.Description=description;
                pm.LeaseDuration=lease_duration;
                pm.Enabled = enabled;
                AddPortMapping(pm);
            }

            public void AddPortMapping(string remote_host, int external_port, int internal_port, string protocol, string internal_client, string description, long lease_duration)
            {
                AddPortMapping(remote_host, external_port, internal_port, protocol, internal_client, description, lease_duration);
            }

            public void AddPortMapping(string remote_host, int external_port, int internal_port, string protocol, string internal_client, string description)
            {
                AddPortMapping(remote_host, external_port, internal_port, protocol, internal_client, description, 0, true);
            }

            public void AddPortMapping(string remote_host, int external_port, int internal_port, string protocol, string internal_client)
            {
                AddPortMapping(remote_host, external_port, internal_port, protocol, internal_client, "", 0, true);
            }

            public void ForceTermination()
            {
                if (root_device != null)
                {
                    //searching sub devices for a wan ip connections service
                    SubDevice.Service wan_ip_connection = root_device.GetServiceByType("urn:schemas-upnp-org:service:WANIPConnection:1");
                    if (wan_ip_connection != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + wan_ip_connection.ControlUrl);
                        Console.WriteLine("service type: [" + wan_ip_connection.ServiceType + "]");
                        string soap_method = "ForceTermination";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + wan_ip_connection.ServiceType + "\">\r\n";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = wan_ip_connection.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + wan_ip_connection.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps,StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                            successful = true;
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (ForcedTerminationCompleted != null)
                                ForcedTerminationCompleted(this, successful);
                        };
                        soap.StartRequest();
                    }
                }
            }

            public void RequestConnection()
            {
                if (root_device != null)
                {
                    //searching sub devices for a wan ip connections service
                    SubDevice.Service wan_ip_connection = root_device.GetServiceByType("urn:schemas-upnp-org:service:WANIPConnection:1");
                    if (wan_ip_connection != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + wan_ip_connection.ControlUrl);
                        Console.WriteLine("service type: [" + wan_ip_connection.ServiceType + "]");
                        string soap_method = "RequestConnection";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + wan_ip_connection.ServiceType + "\">\r\n";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = wan_ip_connection.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + wan_ip_connection.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps,StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                            successful = true;
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (ConnectionRequestCompleted != null)
                                ConnectionRequestCompleted(this, successful);
                        };
                        soap.StartRequest();
                    }
                }
            }
            
            public Router(string usn,string location,string server,string uuid)
            {
                this.unique_service_name = usn;
                this.location = location;
                this.server = server;
                this.universal_unique_id = uuid;
                Uri temp = new Uri(location);
                this.host = temp.Host;
                this.port = temp.Port;
                //scratched remark - give upnp class as parameter to use the upnp protocol functions
            }
        }

        /// <summary>
        /// Prototype for the Router Discovered Event Handler
        /// </summary>
        public event RouterDiscoveredEventHandler RouterDiscovered; 
        /// <summary>
        /// Prototype for the Router State Changed Event Handler
        /// </summary>
        public event RouterStateChangedEventHandler RouterStateChanged;
        /// <summary>
        /// Event handler that gets called
        /// when a UPnP Router was discovered in the lan
        /// </summary>
        /// <param name="router">the router that was discovered</param>
        public delegate void RouterDiscoveredEventHandler(Router router);
        /// <summary>
        /// Event handler that gets called
        /// when a state of a UPnP Router has changed
        /// </summary>
        /// <param name="router">the router which state has changed</param>
        public delegate void RouterStateChangedEventHandler(Router router);
        /// <summary>
        /// Start an UPnP service discovery
        /// </summary>
        public void StartDiscovery()
        {
            try
            {
                //string discovery_message = "M-SEARCH * HTTP/1.1\r\nHOST: " + upnp_udp_multicast_address + ":"+upnp_udp_port+"\r\nST: upnp:rootdevice\r\nMAN: \"ssdp:discover\"\r\nMX: 3\r\n\r\n\r\n";            
                //string discovery_message = "M-SEARCH * HTTP/1.1\r\nST: upnp:rootdevice\r\nMX: 3\r\nMAN: \"ssdp:discover\"\r\nHOST: 239.255.255.250:1900\r\n\r\n\r\n";
                string discovery_message = "M-SEARCH * HTTP/1.1\r\nST: ssdp:all\r\nMX: 3\r\nMAN: \"ssdp:discover\"\r\nHOST: 239.255.255.250:1900\r\n\r\n\r\n";

                Console.WriteLine("Starting Discovery by sending : " + discovery_message);
                IPEndPoint udp_discovery_endpoint = new IPEndPoint(IPAddress.Parse(upnp_udp_multicast_address), upnp_udp_port);
                byte[] send_bytes = System.Text.Encoding.Default.GetBytes(discovery_message);
                udp_send_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None, udp_discovery_endpoint, new AsyncCallback(StartDiscoveryCallback), udp_send_socket);
                //Thread.Sleep(100);
                //udp_send_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None, udp_discovery_endpoint, new AsyncCallback(StartDiscoveryCallback), udp_send_socket);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception during sending of discovery packet: "+ex.Message);
            }
        }
        /// <summary>
        /// Callback for the Discovery async send
        /// </summary>
        /// <param name="ar">Async Result/State</param>
        protected void StartDiscoveryCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            try
            {
                int bytes_sent = socket.EndSend(ar);

            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during sending of discovery packet: " + ex.Message);
            }
        }
        private Socket udp_socket = null;
        private Socket udp_send_socket = null;
        private object listening_lock = new Object();
        protected bool listening = false;
        private string ip = "127.0.0.1";

        /// <summary>
        /// TRUE if we bound our local udp socket and we are listening for packets/connections
        /// </summary>
        public bool IsListening
        {
            get
            {
                return (listening);
            }
        }
        /// <summary>
        /// the udp sockets receive buffer
        /// </summary>
        private byte[] receive_from_buffer = new byte[8192];
        /// <summary>
        /// the broadcast udp sockets receive buffer
        /// </summary>
        private byte[] broadcast_receive_from_buffer = new byte[8192];
        /// <summary>
        /// stores the ip address information of an unicasted packet
        /// </summary>
        private EndPoint receive_from_endpoint = (EndPoint)new IPEndPoint(IPAddress.None, 0);
        /// <summary>
        /// stores the ip address information of a broadcasted packet
        /// </summary>
        private EndPoint broadcast_receive_from_endpoint = (EndPoint)new IPEndPoint(IPAddress.None, 0);
        //private IPEndPoint receive_from_endpoint = new IPEndPoint(IPAddress.None, 0);
        private int upnp_udp_port = 1900;
        private string upnp_udp_multicast_address = "239.255.255.250";
        //TODO maybe better off if these are public

        public UPnP()
        {
            string host_name = Dns.GetHostName();
            IPHostEntry host_entry = Dns.GetHostEntry(host_name);
            if (host_entry.AddressList.Length == 0) return;//computer has not one network interface ;-( i bet this one will never a case anywhere, but better catch it *g*
            ip = host_entry.AddressList[0].ToString();

            SetupSockets();
        }
        
        private void SetupSockets()
        {
            lock (listening_lock)
            {
                if (!listening)
                {
                    listening = true;

                    if (udp_send_socket == null)
                    {
                        try
                        {
                            udp_send_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            IPEndPoint udp_send_local_endpoint = new IPEndPoint(IPAddress.Any, 0);
                            udp_send_socket.Bind(udp_send_local_endpoint);
                            udp_send_socket.Blocking = false;
                            //EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                            //EndPoint receive_from_endpoint = (EndPoint)new IPEndPoint(IPAddress.None, 0);
                            AsyncCallback event_receive_from = new AsyncCallback(OnReceiveFrom);
                            udp_send_socket.BeginReceiveFrom(receive_from_buffer, 0, receive_from_buffer.Length, SocketFlags.None, ref receive_from_endpoint, event_receive_from, udp_send_socket);
                            Console.WriteLine("Bound Sending UDP-Channel");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception opening sending unpnp udp port:" + ex.Message);
                        }
                    }
                    else Console.WriteLine("udp send socket already bound");

                    if (udp_socket == null)
                    {
                        try
                        {
                            udp_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            IPEndPoint udp_local_endpoint = new IPEndPoint(IPAddress.Any, upnp_udp_port);
                            udp_socket.EnableBroadcast = true;
                            udp_socket.ExclusiveAddressUse = false;
                            udp_socket.SetSocketOption(SocketOptionLevel.Udp, SocketOptionName.NoDelay, 1);
                            udp_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);
                            udp_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 4);
                            udp_socket.Bind(udp_local_endpoint);
                            udp_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(IPAddress.Parse(upnp_udp_multicast_address)));
                            udp_socket.Blocking = false;
                            //udp_socket.LingerState = new LingerOption(false, 0);
                            //EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                            //EndPoint broadcast_receive_from_endpoint = (EndPoint)new IPEndPoint(IPAddress.None, 0);
                            AsyncCallback event_receive_from = new AsyncCallback(OnBroadcastReceiveFrom);
                            udp_socket.BeginReceiveFrom(broadcast_receive_from_buffer, 0, broadcast_receive_from_buffer.Length, SocketFlags.None, ref broadcast_receive_from_endpoint, event_receive_from, udp_socket);
                            Console.WriteLine("Bound UDP-Channel to port: " + upnp_udp_port);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception opening local unpnp udp port:" + ex.Message);
                        }

                    }
                    else Console.WriteLine("udp port already in use :" + upnp_udp_port);
                }
                else Console.WriteLine("udp socket already listening");
            }
        }
        private void CloseSockets()
        {
            lock (listening_lock)
            {
                if (listening)
                {
                    listening = false;
                    try
                    {
                        if (udp_socket != null)
                        {
                            udp_socket.ReceiveTimeout = 0;
                            //udp_socket.Shutdown(SocketShutdown.Both);
                            //Thread.Sleep(10);
                            udp_socket.Close();
                            //Thread.Sleep(10);
                            udp_socket = null;
                            Thread.Sleep(100);
                            Console.WriteLine("Closed Listening udp socket.");
                        }
                        if (udp_send_socket != null)
                        {
                            udp_send_socket.ReceiveTimeout = 0;
                            //udp_socket.Shutdown(SocketShutdown.Both);
                            //Thread.Sleep(10);
                            udp_send_socket.Close();
                            //Thread.Sleep(10);
                            udp_send_socket = null;
                            Thread.Sleep(100);
                            Console.WriteLine("Closed sending udp socket.");
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error closing listening udp socket: " + ex.Message);
                    }
                }
            }

        }
        /// <summary>
        /// Callback to receive udp packets
        /// </summary>
        /// <param name="result">Async Result/State</param>
        private void OnReceiveFrom(IAsyncResult result)
        {
            if (!IsListening) return;
            if (udp_send_socket != null)
            {
                if (!udp_send_socket.IsBound) return;
                try
                {
                    if (udp_send_socket != ((Socket)result.AsyncState)) return;
                    Socket receive_from_socket = (Socket)result.AsyncState;
                    if (receive_from_socket == null) return;
                    //EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                    int received_bytes = udp_send_socket.EndReceiveFrom(result, ref receive_from_endpoint);
                    if (received_bytes > 0)
                    {
                        string received_string = System.Text.Encoding.Default.GetString(receive_from_buffer, 0, received_bytes);
                        InterpretReceivedString(received_string, false);
                    }
                    else Console.WriteLine("Empty packet received");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in ReceiveFrom callback: " + ex.Message);
                }
                try
                {
                    //EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                    //EndPoint receive_from_endpoint = (EndPoint)new IPEndPoint(IPAddress.None, 0);
                    AsyncCallback event_receive_from = new AsyncCallback(OnReceiveFrom);
                    udp_send_socket.BeginReceiveFrom(receive_from_buffer, 0, receive_from_buffer.Length, SocketFlags.None, ref receive_from_endpoint, event_receive_from, udp_send_socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fatal Error in ReceiveFrom callback: " + ex.Message);
                }


            }
            else Console.WriteLine("ReceiveFrom on udp sending socket aborted.");
        }
        /// <summary>
        /// Callback to receive udp packets
        /// </summary>
        /// <param name="result">Async Result/State</param>
        private void OnBroadcastReceiveFrom(IAsyncResult result)
        {
            if (!IsListening) return;
            if (udp_socket != null)
            {
                if (!udp_socket.IsBound) return;
                try
                {
                    if (udp_socket != ((Socket)result.AsyncState)) return;
                    Socket receive_from_socket = (Socket)result.AsyncState;
                    if (receive_from_socket == null) return;
                    //EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                    //EndPoint broadcast_receive_from_endpoint = (EndPoint)new IPEndPoint(IPAddress.None, 0);
                    int received_bytes = udp_socket.EndReceiveFrom(result, ref broadcast_receive_from_endpoint);

                    if (received_bytes > 0)
                    {
                        string received_string = System.Text.Encoding.Default.GetString(broadcast_receive_from_buffer, 0, received_bytes);
                        InterpretReceivedString(received_string,true);
                    }
                    else Console.WriteLine("Empty packet received");

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error in BroadcastReceiveFrom callback: " + ex.Message);
                }
                try
                {
                    //EndPoint broadcast_receive_from_endpoint = (EndPoint)new IPEndPoint(IPAddress.None, 0);
                    AsyncCallback event_broadcast_receive_from = new AsyncCallback(OnBroadcastReceiveFrom);
                    udp_socket.BeginReceiveFrom(broadcast_receive_from_buffer, 0, broadcast_receive_from_buffer.Length, SocketFlags.None, ref broadcast_receive_from_endpoint, event_broadcast_receive_from, udp_socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fatal Error in BroadcastReceiveFrom callback: " + ex.Message);
                }


            }
            else Console.WriteLine("ReceiveFrom on udp socket aborted.");
        }

        /// <summary>
        /// List of upnp devices in the lan
        /// (Cache)
        /// </summary>
        protected List<Device> devices = new List<Device>();
        /// <summary>
        /// Interpret a received string from the udp sockets
        /// </summary>
        /// <param name="received_string">the body of the udp packet</param>
        /// <param name="was_broadcasted">TRUE if it was received via broadcast</param>
        private void InterpretReceivedString(string received_string, bool was_broadcasted)
        {
            /*
            string message = "Received a";
            if (was_broadcasted)
            {
                message += " broadcasted unpnp string ";
                //message += "from :" + ((IPEndPoint)broadcast_receive_from_endpoint).Address.ToString();
                //message += ":" + ((IPEndPoint)broadcast_receive_from_endpoint).Port.ToString();
            }
            else
            {
                message += "n unicasted unpnp string ";
                //message += "from :" + ((IPEndPoint)receive_from_endpoint).Address.ToString();
                //message += ":" + ((IPEndPoint)receive_from_endpoint).Port.ToString();
            }
            message += ": ";
             */
            //Console.WriteLine("");
            //Console.WriteLine(message);
            //Console.WriteLine(received_string);
            //Console.WriteLine("");
            //Console.WriteLine("Header Lines:");
            string[] seps = {"\r\n"};
            string[] lines = received_string.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            string uuid = "";
            string urn = "";
            string server = "";
            string location = "";
            string usn = "";

            foreach (string line in lines)
            {
                //Console.WriteLine("line:" + line);
                if (line.StartsWith("SERVER:",true,null))
                {
                    server = line.Substring(8);
                }
                if (line.StartsWith("LOCATION:",true,null))
                {
                    location = line.Substring(10);
                }
                
                if (line.StartsWith("USN:",true,null))
                {
                    usn = line.Substring(5);
                    //Console.WriteLine("Found an Unique Service Name: " + usn);
                    //checking if it looks a router
                    int uuid_start = usn.IndexOf("uuid:");
                    int urn_start = usn.IndexOf("urn:");
                    if (uuid_start != -1)
                    {
                        uuid_start += 5;
                        int uuid_end = usn.Substring(uuid_start).IndexOf("::");
                        if (uuid_end != -1)
                        {
                            uuid = usn.Substring(uuid_start, uuid_end);
                            uuid = uuid.Trim();
                        }
                    }
                    if (urn_start != -1)
                    {
                        urn_start += 4;
                        //int urn_end = usn.Substring(urn_start).IndexOf(":");
                        //if (urn_end != -1)
                        //{
                            urn = usn.Substring(urn_start);
                            //urn = usn.Substring(urn_start, urn_end);
                            urn = urn.Trim();
                        //}
                    }
                }
            }


            if (urn != "" && uuid != "" && server!="" && location != "" && usn != "")
            {//found a usable packet for a router compare
                if (urn == "schemas-upnp-org:service:WANIPConnection:1")
                {//we found a router 
                    //check if its a new or to be updated entry
                    Device existing = GetDeviceByUUID(uuid);
                    if (existing != null)
                    {//we need to update a router entry
                        Router updated = (Router)existing;
                    }
                    else
                    {//we discovered a new router
                        Router r = new Router(usn,location,server,uuid);
                        devices.Add(r);
                        if (RouterDiscovered != null)
                            RouterDiscovered(r);
                    }
                }

            }

            //Console.WriteLine("");
            //Console.WriteLine("-- End of Message --");
            //Console.WriteLine("");
        }
        /// <summary>
        /// Get the device from the devices list with
        /// the uuid specified
        /// </summary>
        /// <param name="uuid">the uuid to look for</param>
        /// <returns>a device with the specified uuid or NULL</returns>
        private Device GetDeviceByUUID(string uuid)
        {
            foreach (Device dev in devices)
            {
                if (dev.UniversalUniqueID == uuid)
                    return (dev);
            }
            return (null);
        }
        //TODO add udp listening socket and http protocol stuff here
        #region Unit Testing
        /// <summary>
        /// Test to see if discovery works the way it should
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestDiscovery()
        {
            Console.WriteLine("Test to discover upnp devices.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            u.RouterDiscovered += delegate(Router r)
            {
                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                wait = false;
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 60))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("UPnP Device Discovery Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if gathering of Router information works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterInformationGathering()
        {
            Console.WriteLine("Test to gather information from a router.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            u.RouterDiscovered += delegate(Router r)
            {
                r.InformationGathered += delegate(Router ir,bool was_successful)
                {
                    if(was_successful)
                    {
                    Console.WriteLine("");
                    Console.WriteLine("-- Gathered router information --");
                    Console.WriteLine("SpecVersion: " + ir.SpecVersionMajor+"."+ir.SpecVersionMinor);
                    Console.WriteLine("URLBase: " + ir.UrlBase);
                    if (ir.RootDevice != null)
                    {
                        Console.WriteLine("presentationURL: " + ir.RootDevice.PresentationUrl);
                        Console.WriteLine("friendlyName: " + ir.RootDevice.FriendlyName);
                        Console.WriteLine("manufacturer: " + ir.RootDevice.Manufacturer);
                        Console.WriteLine("manufacturerURL: " + ir.RootDevice.ManufacturerUrl);
                        Console.WriteLine("modelDescription: " + ir.RootDevice.ModelDescription);
                        Console.WriteLine("modelName: " + ir.RootDevice.ModelName);
                        Console.WriteLine("Number of Sub Devices: " + ir.RootDevice.Devices.Count);
                        Console.WriteLine("Sub UUID: " + ir.RootDevice.UniversalUniqueID);
                    }
                    //Console.WriteLine("Server: " + r.Server);
                    Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                    Console.WriteLine("-- Gathered router information --");
                    Console.WriteLine("");
                    wait = false;
                }
                else Console.WriteLine("failed to gather router information");

                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 60))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("UPnP Router Information Gathering Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if Forced Termination of a Router connection works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterForceTermination()
        {
            Console.WriteLine("Test to disconnect a router.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            u.RouterDiscovered += delegate(Router r)
            {
                r.ForcedTerminationCompleted += delegate(Router fr,bool was_successful)
                {
                    if(was_successful)
                        Console.WriteLine("Disconnected Router.");
                    wait = !was_successful;
                };

                r.InformationGathered += delegate(Router ir,bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("SpecVersion: " + ir.SpecVersionMajor + "." + ir.SpecVersionMinor);
                        Console.WriteLine("URLBase: " + ir.UrlBase);
                        if (ir.RootDevice != null)
                        {
                            Console.WriteLine("presentationURL: " + ir.RootDevice.PresentationUrl);
                            Console.WriteLine("friendlyName: " + ir.RootDevice.FriendlyName);
                            Console.WriteLine("manufacturer: " + ir.RootDevice.Manufacturer);
                            Console.WriteLine("manufacturerURL: " + ir.RootDevice.ManufacturerUrl);
                            Console.WriteLine("modelDescription: " + ir.RootDevice.ModelDescription);
                            Console.WriteLine("modelName: " + ir.RootDevice.ModelName);
                            Console.WriteLine("Number of Sub Devices: " + ir.RootDevice.Devices.Count);
                            Console.WriteLine("Sub UUID: " + ir.RootDevice.UniversalUniqueID);
                            //Thread.Sleep(3000);
                            ir.ForceTermination();
                        }
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("");
                    }
                    else Console.WriteLine("failed to gather router information");
                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 300))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("UPnP Router Disconnecting Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if requesting a Router to connect works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterRequestConnection()
        {
            Console.WriteLine("Test to connect a router.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            u.RouterDiscovered += delegate(Router r)
            {
                r.ConnectionRequestCompleted += delegate(Router fr, bool was_successful)
                {
                    if(was_successful)
                        Console.WriteLine("Connected Router.");
                    wait = !was_successful;
                };

                r.InformationGathered += delegate(Router ir, bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("SpecVersion: " + ir.SpecVersionMajor + "." + ir.SpecVersionMinor);
                        Console.WriteLine("URLBase: " + ir.UrlBase);
                        if (ir.RootDevice != null)
                        {
                            Console.WriteLine("presentationURL: " + ir.RootDevice.PresentationUrl);
                            Console.WriteLine("friendlyName: " + ir.RootDevice.FriendlyName);
                            Console.WriteLine("manufacturer: " + ir.RootDevice.Manufacturer);
                            Console.WriteLine("manufacturerURL: " + ir.RootDevice.ManufacturerUrl);
                            Console.WriteLine("modelDescription: " + ir.RootDevice.ModelDescription);
                            Console.WriteLine("modelName: " + ir.RootDevice.ModelName);
                            Console.WriteLine("Number of Sub Devices: " + ir.RootDevice.Devices.Count);
                            Console.WriteLine("Sub UUID: " + ir.RootDevice.UniversalUniqueID);
                            //Thread.Sleep(3000);
                            ir.RequestConnection();
                        }
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("");
                    }
                    else Console.WriteLine("failed to gather router information");
                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 60))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("UPnP Router Connecting Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if fetching the external ip from a Router works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterFetchExternalIP()
        {
            Console.WriteLine("Test to fetch the external ip from a router.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            u.RouterDiscovered += delegate(Router r)
            {
                r.FetchExternalIPCompleted += delegate(Router fr, bool was_successful)
                {
                    if(was_successful)
                        Console.WriteLine("Fetched the external router: ["+fr.ExternalIP+"]");
                    wait = !was_successful;
                };

                r.InformationGathered += delegate(Router ir, bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("SpecVersion: " + ir.SpecVersionMajor + "." + ir.SpecVersionMinor);
                        Console.WriteLine("URLBase: " + ir.UrlBase);
                        if (ir.RootDevice != null)
                        {
                            Console.WriteLine("presentationURL: " + ir.RootDevice.PresentationUrl);
                            Console.WriteLine("friendlyName: " + ir.RootDevice.FriendlyName);
                            Console.WriteLine("manufacturer: " + ir.RootDevice.Manufacturer);
                            Console.WriteLine("manufacturerURL: " + ir.RootDevice.ManufacturerUrl);
                            Console.WriteLine("modelDescription: " + ir.RootDevice.ModelDescription);
                            Console.WriteLine("modelName: " + ir.RootDevice.ModelName);
                            Console.WriteLine("Number of Sub Devices: " + ir.RootDevice.Devices.Count);
                            Console.WriteLine("Sub UUID: " + ir.RootDevice.UniversalUniqueID);
                            //Thread.Sleep(3000);
                            ir.FetchExternalIP();
                        }
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("");
                    }
                    else Console.WriteLine("failed to gather router information");
                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 60))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("UPnP Router Fetching External IP Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if Adding a Port mapping on a Router works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterAddPortMapping()
        {
            Console.WriteLine("Test to add a portmapping on a router.");
            UPnP u = new UPnP();
            //u.SetupSockets();
            Console.WriteLine("our internal ip: "+ip);
            bool wait = true;
            bool test_failed = false;
            string test_failed_reason = "";

            u.RouterDiscovered += delegate(Router r)
            {
                int prots = 0;
                r.AddingPortMappingCompleted += delegate(Router apm,Router.PortMapping pm, bool was_successful)
                {
                    //Assert.IsTrue(was_successful, "Adding Portmapping failed."); 
                    if (was_successful)
                    {
                        Console.WriteLine("Added Portmapping.");
                        prots++;
                    }
                    else
                    {
                        test_failed_reason = "Adding Portmapping failed.";
                        test_failed = true;
                    }

                    if(prots==2)
                        wait = !was_successful;
                };

                r.InformationGathered += delegate(Router ir, bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("SpecVersion: " + ir.SpecVersionMajor + "." + ir.SpecVersionMinor);
                        Console.WriteLine("URLBase: " + ir.UrlBase);
                        if (ir.RootDevice != null)
                        {
                            Console.WriteLine("presentationURL: " + ir.RootDevice.PresentationUrl);
                            Console.WriteLine("friendlyName: " + ir.RootDevice.FriendlyName);
                            Console.WriteLine("manufacturer: " + ir.RootDevice.Manufacturer);
                            Console.WriteLine("manufacturerURL: " + ir.RootDevice.ManufacturerUrl);
                            Console.WriteLine("modelDescription: " + ir.RootDevice.ModelDescription);
                            Console.WriteLine("modelName: " + ir.RootDevice.ModelName);
                            Console.WriteLine("Number of Sub Devices: " + ir.RootDevice.Devices.Count);
                            Console.WriteLine("Sub UUID: " + ir.RootDevice.UniversalUniqueID);
                            //Thread.Sleep(3000);
                            ir.AddPortMapping("", 5588, 5588, "TCP", ip);
                            ir.AddPortMapping("", 5588, 5588, "UDP", ip);
                        }
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("");
                    }
                    else Console.WriteLine("failed to gather router information");
                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 300))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
                Assert.IsFalse(test_failed, "Test failed: " + test_failed_reason);
            }
            Console.WriteLine("UPnP Router Add Port Mapping Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if Deleting a Port mapping on a Router works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterDeletePortMapping()
        {
            Console.WriteLine("Test to delete a portmapping on a router.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            bool test_failed = false;
            string test_failed_reason = "";
            u.RouterDiscovered += delegate(Router r)
            {
                int prots = 0;
                r.DeletingPortMappingCompleted += delegate(Router apm, Router.PortMapping pm, bool was_successful)
                {
                    //Assert.IsTrue(was_successful, "Deleting Portmapping failed.");
                    if (was_successful)
                    {
                        Console.WriteLine("Deleted Portmapping.");
                        prots++;
                    }
                    else
                    {
                        test_failed_reason = "Deleting Portmapping failed.";
                        test_failed = true;
                    }
                    if (prots == 2)
                        wait = !was_successful;
                };

                r.InformationGathered += delegate(Router ir, bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("SpecVersion: " + ir.SpecVersionMajor + "." + ir.SpecVersionMinor);
                        Console.WriteLine("URLBase: " + ir.UrlBase);
                        if (ir.RootDevice != null)
                        {
                            Console.WriteLine("presentationURL: " + ir.RootDevice.PresentationUrl);
                            Console.WriteLine("friendlyName: " + ir.RootDevice.FriendlyName);
                            Console.WriteLine("manufacturer: " + ir.RootDevice.Manufacturer);
                            Console.WriteLine("manufacturerURL: " + ir.RootDevice.ManufacturerUrl);
                            Console.WriteLine("modelDescription: " + ir.RootDevice.ModelDescription);
                            Console.WriteLine("modelName: " + ir.RootDevice.ModelName);
                            Console.WriteLine("Number of Sub Devices: " + ir.RootDevice.Devices.Count);
                            Console.WriteLine("Sub UUID: " + ir.RootDevice.UniversalUniqueID);
                            //Thread.Sleep(3000);
                            ir.DeletePortMapping("", 5588, "TCP");
                            ir.DeletePortMapping("", 5588, "UDP");
                        }
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("");
                    }
                    else Console.WriteLine("failed to gather router information");
                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 300))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
                Assert.IsFalse(test_failed, "Test failed: " + test_failed_reason);
            }
            Console.WriteLine("UPnP Router Delete Port Mapping Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if checking if a Port mapping exists on a Router works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterCheckForExistingPortMapping()
        {
            Console.WriteLine("Test to check a portmapping on a router for its existance.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            bool test_failed = false;
            string test_failed_reason = "";

            u.RouterDiscovered += delegate(Router r)
            {
                int prots = 0;
                r.PortMappingCheckCompleted += delegate(Router apm, Router.PortMapping pm,bool exists, bool was_successful)
                {
                    if (was_successful)
                        Console.WriteLine("Checked Portmapping.");
                    //Assert.IsTrue(was_successful && exists, "Checking for existing Portmapping failed.");
                    if (was_successful && exists)
                    {
                        Console.WriteLine("-- Portmapping exists --");
                        Console.WriteLine("External Port: " + pm.ExternalPort);
                        Console.WriteLine("Internal Client: " + pm.InternalClient);
                        Console.WriteLine("Internal Port: " + pm.InternalPort);
                        Console.WriteLine("Protocol: " + pm.Protocol);
                        Console.WriteLine("Lease Duration: " + pm.LeaseDuration);
                        Console.WriteLine("Enabled: " + pm.Enabled);
                        Console.WriteLine("Description: " + pm.Description);
                        Console.WriteLine("-- Portmapping exists --");
                        prots++;
                    }
                    else
                    {
                        if (!exists)
                        {
                            test_failed_reason = "Portmapping doesn't exist";
                            test_failed = true;
                        }
                        if (!was_successful)
                        {
                            test_failed = true;
                            test_failed_reason = "Portmapping request failed. ";
                        }
                    }
                    if (prots == 2)
                        wait = !was_successful;
                };

                r.InformationGathered += delegate(Router ir, bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("SpecVersion: " + ir.SpecVersionMajor + "." + ir.SpecVersionMinor);
                        Console.WriteLine("URLBase: " + ir.UrlBase);
                        if (ir.RootDevice != null)
                        {
                            Console.WriteLine("presentationURL: " + ir.RootDevice.PresentationUrl);
                            Console.WriteLine("friendlyName: " + ir.RootDevice.FriendlyName);
                            Console.WriteLine("manufacturer: " + ir.RootDevice.Manufacturer);
                            Console.WriteLine("manufacturerURL: " + ir.RootDevice.ManufacturerUrl);
                            Console.WriteLine("modelDescription: " + ir.RootDevice.ModelDescription);
                            Console.WriteLine("modelName: " + ir.RootDevice.ModelName);
                            Console.WriteLine("Number of Sub Devices: " + ir.RootDevice.Devices.Count);
                            Console.WriteLine("Sub UUID: " + ir.RootDevice.UniversalUniqueID);
                            //Thread.Sleep(3000);
                            ir.CheckForExistingPortMapping("", 5588, "TCP");
                            ir.CheckForExistingPortMapping("", 5588, "UDP");
                        }
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("");
                    }
                    else Console.WriteLine("failed to gather router information");
                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 300))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
                Assert.IsFalse(test_failed, "Test failed: " + test_failed_reason);
            }
            Console.WriteLine("UPnP Router Check Port Mapping Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if status updating of Router information works
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterStatusUpdating()
        {
            Console.WriteLine("Test to update status from a router.");
            UPnP u = new UPnP();
            //u.SetupSockets();

            bool wait = true;
            u.RouterDiscovered += delegate(Router r)
            {
                r.StatusUpdated += delegate(Router ur,bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- router status updated --");
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + r.UniversalUniqueID);
                        Console.WriteLine("-- router status updated --");
                        Console.WriteLine("");
                        wait = false;
                    }
                    else Console.WriteLine("failed to update router status");

                };

                r.InformationGathered += delegate(Router ir,bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered router information --");
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + ir.UniversalUniqueID);
                        Console.WriteLine("-- Gathered router information --");
                        Console.WriteLine("");
                        ir.UpdateStatusInfo();
                    }
                    else Console.WriteLine("failed to gather router information");

                };
                r.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                Console.WriteLine("");
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("Server: " + r.Server);
                Console.WriteLine("Host: " + r.Host + ":" + r.Port);
                Console.WriteLine("UUID: " + r.UniversalUniqueID);
                Console.WriteLine("Location: " + r.Location);
                Console.WriteLine("Unique Service Name: " + r.UniqueServiceName);
                Console.WriteLine("-- Discovered a router --");
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
            };
            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 60))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("UPnP Router Updating Status Test successful.");

            u.CloseSockets();
        }
        #endregion
    }
}
