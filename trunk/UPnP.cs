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
        /// Get a string in between two border strings
        /// </summary>
        /// <param name="search">the string to search in</param>
        /// <param name="start_border">the front border</param>
        /// <param name="end_border">the end border</param>
        /// <returns></returns>
        public static string GetStringInbetween(string search, string start_border, string end_border)
        {
            string ret = "";
            int start = search.IndexOf(start_border);
            if (start != -1)
            {
                ret = search.Substring(start + start_border.Length);
                int end = ret.IndexOf(end_border);
                if (end != -1)
                {
                    ret = ret.Substring(0, end);
                }
            }
            return (ret);
        }
        /// <summary>
        /// Get an int in between two border strings
        /// </summary>
        /// <param name="search">the string to search in</param>
        /// <param name="start_border">the front border</param>
        /// <param name="end_border">the end border</param>
        /// <returns></returns>
        public static int GetIntInbetween(string search, string start_border, string end_border)
        {
            int ret = -1;
            string ret_string = "-1";
            int start = search.IndexOf(start_border);
            if (start != -1)
            {
                ret_string = search.Substring(start + start_border.Length);
                int end = ret_string.IndexOf(end_border);
                if (end != -1)
                {
                    ret_string = ret_string.Substring(0, end);
                    try
                    {
                        ret = int.Parse(ret_string);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("error parsing int in between: "+ex.Message);
                    }
                }
            }
            return (ret);
        }

        /// <summary>
        /// a class to help handling soap requests
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
                        if (socket != null)//&& socket.Connected
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
                        Console.WriteLine("Error during disconnect from server: " + uri.Host + ":" + uri.Port+"(exception: "+ex.Message);
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
                try
                {
                    Uri uri = new Uri(url);
                    busy = true;
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    AsyncCallback event_host_resolved = new AsyncCallback(OnHostResolve);
                    Dns.BeginGetHostEntry(uri.DnsSafeHost, event_host_resolved, socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to server:(exception:" + ex.Message + ")");
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
                if (!receive_socket.Connected)
                {
                    return;//TODO change to disconnect();
                }
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
                    Console.WriteLine("Error during receive from server: " + uri.Host + ":" + uri.Port + "(exception:" + ex.Message + ")");
                    Disconnect();
                    if (RequestFinished != null)
                        RequestFinished(this, false);
                }

            }
            private void SendRequest(string request)
            {
                string send_string = request;
                if (!socket.Connected) return;
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
                    if (send_request_socket == null || !send_request_socket.Connected) return;

                    int bytes = send_request_socket.EndSend(ar);

                    //TODO check if bytes send == request.length
                }
                catch (Exception ex)
                {
                    Uri uri = new Uri(url);
                    Console.WriteLine("Error during ending send request to server: " + uri.Host + ":" + uri.Port + "(exception:" + ex.Message + ")");
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
        /// an (abstract,not yet) upnp device class
        /// </summary>
        public class Device
        {
            public enum DeviceTypes
            {
                /// <summary>
                /// the device is a binary light
                /// </summary>
                BinaryLight,
                /// <summary>
                /// the device is a media renderer
                /// </summary>
                MediaRenderer,
                /// <summary>
                /// the device is a router
                /// </summary>
                Router,
                /// <summary>
                /// the device is a media browser
                /// </summary>
                MediaBrowser,
                /// <summary>
                /// the device is unknown
                /// </summary>
                Unkown
            }

            protected DeviceTypes device_type = DeviceTypes.Unkown;

            public DeviceTypes DeviceType
            {
                get { return device_type; }
                set { device_type = value; }
            }
	
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

            protected SubDevice root_device=null;
            public SubDevice RootDevice
            {
                get
                {
                    return (root_device);
                }
            }
            /// <summary>
            /// TRUE if the basic device information has been gathered
            /// (device information is needed to control it or get the status)
            /// </summary>
            public bool HasInformation
            {
                get
                {
                    return (!(root_device==null));
                }
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
                Console.WriteLine("Starting to gather device information.");


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
                            //Console.WriteLine("xml_page_string:\n"+page_string);
                            //we set the url base to the default value retrieved from location
                            //in case the device wont send one
                            Uri temp_location = new Uri(location);
                            url_base = "http://"+temp_location.Host +":"+ temp_location.Port+"/";
                            GetDeviceDescription(page_string);
                            /*
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
                            */
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
                        Console.WriteLine("starting download of: " + location);
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
            //TODO maybe urls can get screwed up sometimes the way it is now (base url / or no / )
            private string GetFixedURL(string url)
            {//checks if url needs some rebuilding
                return (url);
            }

            private void GetDeviceDescription(string xml)
            {
                Console.WriteLine("getting device description");
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
                                if (child.Name.Equals("root")) ReadDeviceDescriptionRoot(child);
                            }
                        }
                    }
                    //Console.WriteLine("Finished parsing.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("error reading xml device description: " + ex.Message);
                }
            }

            private void ReadDeviceDescriptionRoot(XmlNode node)
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
                        if (child.Name.Equals("specVersion")) ReadSpecVersion(child);
                        if (child.Name.Equals("URLBase"))
                        {
                            url_base = child.InnerText;
                            if (!url_base.EndsWith("/")) url_base += "/";
                        }

                        if (child.Name.Equals("device"))
                        {
                            root_device = new SubDevice();
                            ReadDevice(child, root_device);
                        }
                    }
                }
            }

            private void ReadDevice(XmlNode node, SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("deviceType")) device.DeviceType = child.InnerText;
                        if (child.Name.Equals("presentationURL"))
                        {
                            device.PresentationUrl = child.InnerText;
                            if (device.PresentationUrl.StartsWith("/"))
                                device.PresentationUrl = device.PresentationUrl.Substring(1);
                        }
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
                        if (child.Name.Equals("serviceList")) ReadDeviceServiceList(child, device);
                        if (child.Name.Equals("deviceList")) ReadDeviceDeviceList(child, device);
                    }
                }
            }

            private void ReadDeviceDeviceList(XmlNode node, SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("device"))
                        {
                            SubDevice sub_device = new SubDevice();
                            ReadDevice(child, sub_device);
                            device.Devices.Add(sub_device);
                        }
                    }
                }
            }

            private void ReadDeviceServiceList(XmlNode node, SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("service")) ReadDeviceService(child, device);
                    }
                }
            }

            private void ReadDeviceService(XmlNode node, SubDevice device)
            {
                if (node.HasChildNodes)
                {
                    SubDevice.Service service = new SubDevice.Service();
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("serviceType")) service.ServiceType = child.InnerText;
                        if (child.Name.Equals("serviceId")) service.ServiceID = child.InnerText;
                        if (child.Name.Equals("SCPDURL"))
                        {
                            service.SCPDUrl = child.InnerText;
                            if (service.SCPDUrl.StartsWith("/"))
                                service.SCPDUrl = service.SCPDUrl.Substring(1);
                        }
                        if (child.Name.Equals("controlURL"))
                        {
                            service.ControlUrl = child.InnerText;
                            if (service.ControlUrl.StartsWith("/"))
                                service.ControlUrl = service.ControlUrl.Substring(1);
                        }
                        if (child.Name.Equals("eventSubURL"))
                        {
                            service.EventSubUrl = child.InnerText;
                            if(service.EventSubUrl.StartsWith("/"))
                                service.EventSubUrl = service.EventSubUrl.Substring(1);
                        }
                    }
                    device.Services.Add(service);
                }
            }

            private void ReadSpecVersion(XmlNode node)
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

            public delegate void InformationGatheredEventHandler(Device device, bool was_successful);
            public event InformationGatheredEventHandler InformationGathered;

        }
        /// <summary>
        /// a class to handle local upnp (fake) devices
        /// </summary>
        public abstract class LocalDevice : Device
        {
            /// <summary>
            /// the number of local devices
            /// </summary>
            static public int LocalDevicesCount = 0;
            protected int device_id;
            /// <summary>
            /// the device id
            /// </summary>
            public int DeviceID
            {
                get { return device_id; }
                set { device_id = value; }
            }

            public abstract string Control(MiniWebServer.Request request,string service_id);
            public abstract string Event(MiniWebServer.Request request,string service_id);

            public LocalDevice()
            {
                device_id = LocalDevice.LocalDevicesCount++;
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
            public Service GetServiceByID(string service_id)
            {
                foreach (SubDevice device in devices)
                {
                    Service temp = device.GetServiceByID(service_id);
                    if (temp != null) return (temp);
                }

                foreach (Service service in services)
                {
                    if (service.ServiceID == service_id)
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
            
            protected string model_number = "";
            public string ModelNumber
            {
                get { return model_number; }
                set { model_number = value; }
            }

            private string model_url = "";

            public string ModelUrl
            {
                get { return model_url; }
                set { model_url = value; }
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
        /// this implements a media browser 
        /// </summary>
        public class MediaBrowser : LocalDevice // TODO change to LocalDevice subclass of Device
        {
            /* TODO 
             * use upnp class to handle its presence announcing and updating of cache infos
             */
            /// <summary>
            /// a class to represent a browse result returned to an upnp client
            /// </summary>
            public class BrowseResult
            {
                private int number_returned = 0;
	            public int NumberReturned
	            {
		            get { return number_returned;}
		            //set { number_returned = value;}
            	}
                private int total_matches = 0;

            	public int TotalMatches
	            {
		            get { return total_matches;}
		            set { total_matches = value;}
	            }
                private int update_id = 0;
    
            	public int UpdateID
	            {
		            get { return update_id;}
		            set { update_id = value;}
	            }

                public void AddFolder(string title, string id, string parent_id, bool searchable, bool restricted, int child_count, string write_status, long storage_used)
                {
                    //didl += 
                    didl += "<container id=\""+id+"\" ";
                    if(searchable)
                        didl += "searchable=\"1\"";
                    else didl += "searchable=\"0\"";
                    didl += " parentID=\""+parent_id+"\" ";
                    if (restricted)
                        didl += "restricted=\"1\"";
                    else didl += "restricted=\"0\"";
                    didl += " childCount=\""+child_count+"\">";
                    didl += "<dc:title>"+title+"</dc:title>";
                    didl += "<upnp:class>object.container.storageFolder</upnp:class>";
                    didl += "<upnp:storageUsed>"+storage_used+"</upnp:storageUsed>";
                    didl += "<upnp:writeStatus>"+write_status+"</upnp:writeStatus>";
                    didl += "</container>";
                    /*
                    didl += "<container id=\"0000000000000001\" searchable=\"1\" parentID=\"0\" restricted=\"0\" childCount=\"2\">";
                    didl += "<dc:title>Winamp Media Library</dc:title>";
                    didl += "<upnp:class>object.container.storageFolder</upnp:class>";
                    didl += "<upnp:storageUsed>-1</upnp:storageUsed>";
                    didl += "<upnp:writeStatus>UNKNOWN</upnp:writeStatus>";
                    didl += "</container>";*/
                    //didl += ;
                    number_returned++;
                }
                public void AddItem(string title,string creator,string url,string content_type,string id,string parent_id,bool restricted,long size,string write_status,string res_title)
                {
                    didl += "<item id=\""+id+"\" parentID=\""+parent_id+"\" ";
                    if(restricted)
                    didl += "restricted=\"1\"";
                    else didl += "restricted=\"0\"";
                    didl+=">";
                    didl += "<dc:title>"+title+"</dc:title>";
                    didl += "<upnp:class>object.item</upnp:class>";
                    didl += "<dc:creator>"+creator+"</dc:creator>";
                    didl += "<upnp:writeStatus>"+write_status+"</upnp:writeStatus>";
                    //didl += "<res protocolInfo=\"http-get:*:" + content_type + ":*\" size=\"" + size + "\" importUri=\"" + url + "\">" + res_title + "</res>";
                    didl += "<res protocolInfo=\"http-get:*:" + content_type + ":*\" size=\"" + size + "\" importUri=\"" + url + "\">" + url + "</res>";
                    didl += "</item>";
                    /*
                    didl += "<item id=\"000000000000164E\" parentID=\"0\" restricted=\"0\">";
                    didl += "<dc:title>Test Phat Mp3</dc:title>";
                    didl += "<upnp:class>object.item</upnp:class>";
                    didl += "<dc:creator>pez2001</dc:creator>";
                    didl += "<upnp:writeStatus>UNKNOWN</upnp:writeStatus>";
                    didl += "<res protocolInfo=\"http-get:*:audio/mpeg:*\" size=\"1\" importUri=\"http://www.voyagerproject.de/stuff/phat_sweet_drill_mix.wav.mp3\">http://www.voyagerproject.de/stuff/phat_sweet_drill_mix.wav.mp3</res>";
                    didl += "</item>";*/
                    number_returned++;
                }
                private string didl = "<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\">";
                public string Didl
                {
                    get
                    {//compile didl string
                        return (didl + "</DIDL-Lite>");

                    }
                }

                public BrowseResult()
                {

                }

            }



            public override string Event(MiniWebServer.Request request, string service_id)
            {
                //TODO change to void method and send request answer inline
                //TODO maybe remove request parameter and change to soap_action + body strings
                return ("");
            }

            public override string Control(MiniWebServer.Request request, string service_id)
            {
                SubDevice.Service control_service = root_device.GetServiceByID(service_id);
                Console.WriteLine("opened control url of service :" + control_service.ServiceType);
                //Console.WriteLine("body:" + request.Body);
                Console.WriteLine("soap action:" + request.Headers.Get("SOAPACTION"));
                char[] trims = { '"'};
                if (request.Headers.Get("SOAPACTION").Trim().Trim(trims) == "urn:schemas-upnp-org:service:ContentDirectory:1#Browse")
                {
                    string soap_answer = "";
                    soap_answer += "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
                    soap_answer += "<s:Envelope s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\" xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">";
                    soap_answer += "<s:Body>";
                    soap_answer += "<u:Browse xmlns:u=\"urn:" + control_service.ServiceType + "\">";
                    //soap_answer += "<ObjectID>0</ObjectID>";
                    //soap_answer += "<BrowseFlag>BrowseDirectChildren</BrowseFlag>";
                    //soap_answer += "<Filter>*</Filter>";
                    //soap_answer += "<StartingIndex>0</StartingIndex>";
                    //soap_answer += "<RequestedCount>10</RequestedCount>";
                    //soap_answer += "<SortCriteria />";
                    //input values
                    string object_id = UPnP.GetStringInbetween(request.Body, "<ObjectID>", "</ObjectID>");
                    string browse_flag = UPnP.GetStringInbetween(request.Body, "<BrowseFlag>", "</BrowseFlag>");
                    string filter = UPnP.GetStringInbetween(request.Body, "<Filter>", "</Filter>");
                    int starting_index = UPnP.GetIntInbetween(request.Body, "<StartingIndex>", "</StartingIndex>");
                    int requested_count = UPnP.GetIntInbetween(request.Body, "<RequestedCount>", "</RequestedCount>");
                    string sort_criteria = UPnP.GetStringInbetween(request.Body, "<SortCriteria>", "</SortCriteria>");
                    if (starting_index == -1)
                        starting_index = 0;
                    if (requested_count == -1)
                        requested_count = 0;
                    //output values
                    string didl = "";
                    int total_matches = 0;
                    int number_returned = 0;
                    int update_id = 0;
                    if (BrowseRequestReceived != null)
                    {
                        BrowseResult result = new BrowseResult();
                        result = BrowseRequestReceived(this, object_id, browse_flag, starting_index, requested_count, sort_criteria);
                        didl = result.Didl;
                        total_matches = result.TotalMatches;
                        number_returned = result.NumberReturned;
                        update_id = result.UpdateID;
                    }
                    soap_answer += "<Result>" + didl + "</Result>";
                    soap_answer += "<NumberReturned>" + number_returned + "</NumberReturned>";
                    soap_answer += "<TotalMatches>" + total_matches + "</TotalMatches>";
                    soap_answer += "<UpdateID>" + update_id + "</UpdateID>";
                    soap_answer += "</u:Browse>";
                    soap_answer += "</s:Body>";
                    soap_answer += "</s:Envelope>";
                    return (soap_answer);
                }
                //TODO add a real soap error response here
                return("");
            }

            /// <summary>
            /// Event handler that gets called
            /// when a browse request was received
            /// </summary>
            public event BrowseRequestReceivedEventHandler BrowseRequestReceived;
            /// <summary>
            /// Prototype for the Browse Request Received Event Handler
            /// </summary>
            /// <param name="media_browser">the media browser which is going to be browsed by the media renderer</param>
            /// <param name="object_id">the object id to browse</param>
            /// <param name="browse_flag">the browsing flags</param>
            /// <param name="starting_index">the index to start returning entries from</param>
            /// <param name="requested_count">the maximum number of entries the media renderer wants to receive</param>
            /// <param name="sort_criteria"></param>
            /// <returns>the browse information the application wants to return to the media renderer</returns>
            public delegate BrowseResult BrowseRequestReceivedEventHandler(MediaBrowser media_browser, string object_id, string browse_flag, int starting_index, int requested_count, string sort_criteria);
            /// <summary>
            /// Media Browser Constructor
            /// ,fills the devices properties and values with default values
            /// </summary>
            public MediaBrowser()
            {
                device_type = DeviceTypes.MediaBrowser;
                //host = "";
                //location = "";
                //port = 0;
                //server = "";
                //url_base = "";
                spec_version_major = 1;
                spec_version_minor = 0;
                unique_service_name = "schemas-upnp-org:device:MediaServer:1";
                universal_unique_id = "aabbaadd-0000-0000-0000-00000000000"+device_id;//TODO change to FormatStringVersion to add zeros in front of device_id if less than 1000
                root_device = new SubDevice();
                root_device.DeviceType = "schemas-upnp-org:device:MediaServer:1";//urn:
                root_device.FriendlyName = "vpMediaServer";
                root_device.Manufacturer="Voyager Project";
                root_device.ManufacturerUrl="http://www.voyagerproject.org";
                root_device.ModelDescription="Provides access to local and remote files.";
                root_device.ModelName="vpMediaServer";
                root_device.ModelNumber = "0.1";
                root_device.ModelUrl = "http://www.voyagerproject.org";
                root_device.PresentationUrl = "http://www.voyagerproject.org";
                root_device.UniversalUniqueID = "a310a1fd-e86f-42f1-b5b5-fcc59e8b66ff";//uuid:
                SubDevice.Service connection_manager = new SubDevice.Service();
                SubDevice.Service content_directory = new SubDevice.Service();
                connection_manager.ServiceType = "schemas-upnp-org:service:ConnectionManager:1";
                connection_manager.ServiceID = "a310a1fd-e86f-42f1-b5b5-fcc59e8b67ff";
                connection_manager.ControlUrl = "";
                connection_manager.EventSubUrl = "";
                connection_manager.SCPDUrl = "";
                content_directory.ServiceType = "schemas-upnp-org:service:ContentDirectory:1";
                content_directory.ServiceID = "a310a1fd-e86f-42f1-b5b5-fcc59e8b68ff";
                content_directory.ControlUrl = "";
                content_directory.EventSubUrl = "";
                content_directory.SCPDUrl = "";
                root_device.Services.Add(connection_manager);
                root_device.Services.Add(content_directory);

            }
        }
        /// <summary>
        /// a class to use an upnp binary light
        /// </summary>
        public class BinaryLight : Device
        {
            /// <summary>
            /// Enumeration of all possible Power States
            /// </summary>
            public enum PowerStates
            {
                /// <summary>
                /// the light is switched on
                /// </summary>
                On,
                /// <summary>
                /// the light is switched off
                /// </summary>
                Off,
                /// <summary>
                /// the light status is unknown
                /// </summary>
                Unknown
            }
            private PowerStates status;

            public PowerStates Status
            {
                get { return status; }
                set { status = value; }
            }


            private byte load_level;

            public byte LoadLevel
            {
                get { return load_level; }
                set { load_level = value; }
            }
            private byte min_level;

            public byte MinLevel
            {
                get { return min_level; }
                set { min_level = value; }
            }
            public delegate void GettingStatusCompletedEventHandler(BinaryLight binary_light, bool was_successful);
            public event GettingStatusCompletedEventHandler GettingStatusCompleted;
            public delegate void SwitchingPowerCompletedEventHandler(BinaryLight binary_light, bool was_successful);
            public event SwitchingPowerCompletedEventHandler SwitchingPowerCompleted;
            public delegate void GettingMinLevelCompletedEventHandler(BinaryLight binary_light, bool was_successful);
            public event GettingMinLevelCompletedEventHandler GettingMinLevelCompleted;
            public delegate void GettingLoadLevelCompletedEventHandler(BinaryLight binary_light, bool was_successful);
            public event GettingLoadLevelCompletedEventHandler GettingLoadLevelCompleted;
            public delegate void SettingLoadLevelCompletedEventHandler(BinaryLight binary_light, bool was_successful);
            public event SettingLoadLevelCompletedEventHandler SettingLoadLevelCompleted;

            public void SetLoadLevel(byte level)
            {
                if (root_device != null)
                {
                    //searching sub devices for dimming service
                    SubDevice.Service dimming = root_device.GetServiceByType("urn:schemas-upnp-org:service:DimmingService:1");
                    if (dimming != null)
                    {
                        Console.WriteLine("Found the dimming service.");
                        Console.WriteLine("service control url: " + url_base + dimming.ControlUrl);
                        Console.WriteLine("service type: [" + dimming.ServiceType + "]");
                        string soap_method = "SetLoadLevelTarget";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + dimming.ServiceType + "\">\r\n";
                        soap_body += "<NewLoadLevelTarget>"+level.ToString()+"</NewLoadLevelTarget>";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = dimming.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + dimming.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            successful = true;
                                            load_level = level;
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (SettingLoadLevelCompleted != null)
                                SettingLoadLevelCompleted(this, successful);
                        };
                        soap.StartRequest();

                    }
                }

            }
            public void GetLoadLevel()
            {
                if (root_device != null)
                {
                    //searching sub devices for a dimming service
                    SubDevice.Service dimming = root_device.GetServiceByType("urn:schemas-upnp-org:service:DimmingService:1");
                    if (dimming != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + dimming.ControlUrl);
                        Console.WriteLine("service type: [" + dimming.ServiceType + "]");
                        string soap_method = "GetLoadLevelStatus";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + dimming.ServiceType + "\">\r\n";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = dimming.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + dimming.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            int load_level_start = request_finished_soap.Response.IndexOf("<RetLoadLevelStatus>");
                                            if (load_level_start != -1)
                                            {
                                                string load_level_string = request_finished_soap.Response.Substring(load_level_start + "<RetLoadLevelStatus>".Length);
                                                int load_level_end = load_level_string.IndexOf("</RetLoadLevelStatus>");
                                                if (load_level_end != -1)
                                                {
                                                    load_level_string = load_level_string.Substring(0, load_level_end);
                                                    try
                                                    {
                                                        load_level = Byte.Parse(load_level_string);
                                                        successful = true;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Console.WriteLine("error parsing load level: " + ex.Message);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (GettingLoadLevelCompleted != null)
                                GettingLoadLevelCompleted(this, successful);
                        };
                        soap.StartRequest();
                    }
                }
            }
            public void GetMinLevel()
            {
                if (root_device != null)
                {
                    //searching sub devices for a dimming service
                    SubDevice.Service dimming = root_device.GetServiceByType("urn:schemas-upnp-org:service:DimmingService:1");
                    if (dimming != null)
                    {
                        Console.WriteLine("Found the wan ip connection service.");
                        Console.WriteLine("service control url: " + url_base + dimming.ControlUrl);
                        Console.WriteLine("service type: [" + dimming.ServiceType + "]");
                        string soap_method = "GetMinLevel";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + dimming.ServiceType + "\">\r\n";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = dimming.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + dimming.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            int load_level_start = request_finished_soap.Response.IndexOf("<MinLevel>");
                                            if (load_level_start != -1)
                                            {
                                                string load_level_string = request_finished_soap.Response.Substring(load_level_start + "<MinLevel>".Length);
                                                int load_level_end = load_level_string.IndexOf("</MinLevel>");
                                                if (load_level_end != -1)
                                                {
                                                    load_level_string = load_level_string.Substring(0, load_level_end);
                                                    try
                                                    {
                                                        load_level = Byte.Parse(load_level_string);
                                                        successful = true;
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        Console.WriteLine("error parsing min level: " + ex.Message);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (GettingMinLevelCompleted != null)
                                GettingMinLevelCompleted(this, successful);
                        };
                        soap.StartRequest();
                    }
                }
            }
            public void GetStatus()
            {
                if (root_device != null)
                {
                    //searching sub devices for a power switching service
                    SubDevice.Service power_switch = root_device.GetServiceByType("urn:schemas-upnp-org:service:SwitchPower:1");
                    if (power_switch != null)
                    {
                        Console.WriteLine("Found the power switch service.");
                        Console.WriteLine("service control url: " + url_base + power_switch.ControlUrl);
                        Console.WriteLine("service type: [" + power_switch.ServiceType + "]");
                        string soap_method = "GetStatus";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + power_switch.ServiceType + "\">\r\n";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = power_switch.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + power_switch.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            int stats_start = request_finished_soap.Response.IndexOf("<ResultStatus>");
                                            if (stats_start != -1)
                                            {
                                                string status_string = request_finished_soap.Response.Substring(stats_start + "<ResultStatus>".Length);
                                                int status_end = status_string.IndexOf("</ResultStatus>");
                                                if (status_end != -1)
                                                {
                                                    status_string = status_string.Substring(0, status_end);
                                                    if (status_string == "1")
                                                        status = PowerStates.On;
                                                    else status = PowerStates.Off;

                                                    successful = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (GettingStatusCompleted != null)
                                GettingStatusCompleted(this, successful);
                        };
                        soap.StartRequest();
                    }
                }
            }
            public void SwitchPower()
            {
                if (status == PowerStates.Unknown)
                {//TODO get light status and act accordingly

                }
                else if (status == PowerStates.On)
                    SwitchPower(PowerStates.Off);
                else SwitchPower(PowerStates.On);
            }
            public void SwitchPower(PowerStates state)
            {
                if (root_device != null)
                {
                    //searching sub devices for power switching service
                    SubDevice.Service power_switch = root_device.GetServiceByType("urn:schemas-upnp-org:service:SwitchPower:1");
                    if (power_switch != null)
                    {
                        Console.WriteLine("Found the power switch service.");
                        Console.WriteLine("service control url: " + url_base + power_switch.ControlUrl);
                        Console.WriteLine("service type: [" + power_switch.ServiceType + "]");
                        string soap_method = "SetTarget";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + power_switch.ServiceType + "\">\r\n";
                        if (state == PowerStates.On)
                            soap_body += "<newTargetValue>1</newTargetValue>";
                        else soap_body += "<newTargetValue>0</newTargetValue>";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = power_switch.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + power_switch.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
                                string[] seps = { "\r\n" };
                                string[] lines = request_finished_soap.Response.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                                if (lines.Length > 0)
                                {
                                    if (lines[0] == "HTTP/1.1 200 OK")
                                    {
                                        if (request_finished_soap.Response.IndexOf(soap_method + "Response") != -1)
                                        {
                                            successful = true;
                                            status = state;
                                        }
                                    }
                                }
                            }
                            else Console.WriteLine("Soap Request failed.");
                            if (SwitchingPowerCompleted != null)
                                SwitchingPowerCompleted(this, successful);
                        };
                        soap.StartRequest();

                    }
                }
            }

            public BinaryLight(string usn, string location, string server, string uuid)
            {
                this.unique_service_name = usn;
                this.location = location;
                this.server = server;
                this.universal_unique_id = uuid;
                Uri temp = new Uri(location);
                this.host = temp.Host;
                this.port = temp.Port;
                this.device_type = DeviceTypes.BinaryLight;
            }
        }
        /// <summary>
        /// a class to use an upnp media renderer
        /// </summary>
        public class MediaRenderer : Device
        {

            public delegate void SettingPlaybackUrlCompletedEventHandler(MediaRenderer media_renderer, bool was_successful);
            public event SettingPlaybackUrlCompletedEventHandler SettingPlaybackUrlCompleted;

            public void SetPlaybackUrl(string playback_url)
            {
                if (root_device != null)
                {
                    //searching sub devices for a av transport service
                    SubDevice.Service transport = root_device.GetServiceByType("urn:schemas-upnp-org:service:AVTransport:1");
                    if (transport != null)
                    {
                        Console.WriteLine("Found the av transport service.");
                        Console.WriteLine("service control url: " + url_base + transport.ControlUrl);
                        Console.WriteLine("service type: [" + transport.ServiceType + "]");
                        string soap_method = "SetAVTransportURI";
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + transport.ServiceType + "\">\r\n";
                        soap_body += "<InstanceID>0</InstanceID>";
                        soap_body += "<CurrentURI>" + playback_url + "</CurrentURI>";
                        soap_body += "<CurrentURIMetaData></CurrentURIMetaData>";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = transport.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + transport.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
                            if (SettingPlaybackUrlCompleted != null)
                                SettingPlaybackUrlCompleted(this, successful);
                        };
                        soap.StartRequest();

                    }
                }
            }


            public delegate void PressingButtonCompletedEventHandler(MediaRenderer media_renderer, Button pressed,bool was_successful);
            public event PressingButtonCompletedEventHandler PressingButtonCompleted;

            public enum Button
            {
                /// <summary>
                /// the play button of the media renderer
                /// </summary>
                Play,
                /// <summary>
                /// the stop button of the media renderer
                /// </summary>
                Stop,
                /// <summary>
                /// the next button of the media renderer
                /// </summary>
                Next,
                /// <summary>
                /// the previous button of the media renderer
                /// </summary>
                Previous
            }

            public void Press(Button action)
            {
                if (root_device != null)
                {
                    //searching sub devices for a av transport service
                    SubDevice.Service transport = root_device.GetServiceByType("urn:schemas-upnp-org:service:AVTransport:1");
                    if (transport != null)
                    {
                        Console.WriteLine("Found the av transport service.");
                        Console.WriteLine("service control url: " + url_base + transport.ControlUrl);
                        Console.WriteLine("service type: [" + transport.ServiceType + "]");
                        
                        string soap_method = Enum.GetName(typeof(Button), action);
                        string soap_body = "<?xml version=\"1.0\"?>\r\n";
                        soap_body += "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\r\n";
                        soap_body += "<s:Body>\r\n";
                        soap_body += "<m:" + soap_method + " xmlns:m=\"" + transport.ServiceType + "\">\r\n";
                        soap_body += "<InstanceID>0</InstanceID>";
                        if(action== Button.Play)
                            soap_body += "<Speed>1</Speed>";
                        soap_body += "</m:" + soap_method + ">\r\n";
                        soap_body += "</s:Body>\r\n";
                        soap_body += "</s:Envelope>\r\n";
                        string soap_action = transport.ServiceType + "#" + soap_method;
                        SOAP soap = new SOAP(url_base + transport.ControlUrl, soap_body, soap_action);
                        soap.RequestFinished += delegate(SOAP request_finished_soap, bool request_successful)
                        {
                            bool successful = false;
                            if (request_successful)
                            {
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
                            if (PressingButtonCompleted != null)
                                PressingButtonCompleted(this,action, successful);
                        };
                        soap.StartRequest();

                    }
                }
            }

            public MediaRenderer(string usn,string location,string server,string uuid)
            {
                this.unique_service_name = usn;
                this.location = location;
                this.server = server;
                this.universal_unique_id = uuid;
                Uri temp = new Uri(location);
                this.host = temp.Host;
                this.port = temp.Port;
                this.device_type = DeviceTypes.MediaRenderer;
            }
        }
        /// <summary>
        /// A class to use UPnP supporting Routers
        /// </summary>
        public class Router : Device
        {
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
	
            /// <summary>
            /// Updates the status informations
            /// and fires a status updated event if the the status changed
            /// </summary>
            public void UpdateStatusInfo()
            {
                bool fire_event = false;
                //TODO add a subscribe event and delete this stub

            }
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
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
                                //Console.WriteLine("Soap Response: \n" + request_finished_soap.Response);
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
            //TODO maybe put this somehow into Device
            public Router(string usn,string location,string server,string uuid)
            {
                this.unique_service_name = usn;
                this.location = location;
                this.server = server;
                this.universal_unique_id = uuid;
                Uri temp = new Uri(location);
                this.host = temp.Host;
                this.port = temp.Port;
                this.device_type = DeviceTypes.Router;
                //scratched remark - give upnp class as parameter to use the upnp protocol functions
            }
        }
        /// <summary>
        /// Event handler that gets called
        /// when a UPnP Device was discovered in the lan
        /// </summary>
        public event DeviceDiscoveredEventHandler DeviceDiscovered;
        /// <summary>
        /// Prototype for the Device Discovered Event Handler
        /// </summary>
        /// <param name="device">the device that was discovered</param>
        public delegate void DeviceDiscoveredEventHandler(Device device);
       
        /*
        /// <summary>
        /// Event handler that gets called
        /// when a state of a UPnP Router has changed
        /// </summary>
        public event RouterStateChangedEventHandler RouterStateChanged;
        /// <summary>
        /// Prototype for the Router State Changed Event Handler
        /// </summary>
        /// <param name="router">the router which state has changed</param>
        public delegate void RouterStateChangedEventHandler(Router router);
         */
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

                //Console.WriteLine("Starting Discovery by sending : " + discovery_message);
                IPEndPoint udp_discovery_endpoint = new IPEndPoint(IPAddress.Parse(upnp_udp_multicast_address), upnp_udp_port);
                byte[] send_bytes = System.Text.Encoding.Default.GetBytes(discovery_message);
                udp_send_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None, udp_discovery_endpoint, new AsyncCallback(StartDiscoveryCallback), udp_send_socket);
                //Thread.Sleep(100);
                //udp_send_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None, udp_discovery_endpoint, new AsyncCallback(StartDiscoveryCallback), udp_send_socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during sending of discovery packet: " + ex.Message);
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

        private void ReplyDevice(LocalDevice device, IPEndPoint destination)
        {
            try
            {
                string announce_message = "HTTP/1.1 200 OK\r\nCache-Control:max-age=120\r\nLocation:http://" + local_device_handler.IP + ":" + local_device_handler.Port + "/device/" + device.DeviceID + "/root.xml\r\nNT:urn:" + device.UniqueServiceName + "\r\nUSN:uuid:" + device.UniversalUniqueID + "::urn:" + device.UniqueServiceName + "\r\nST: uuid:"+device.UniversalUniqueID+"\r\nServer:NT/5.0 UPnP/1.0\r\n\r\n\r\n";

                //IPEndPoint udp_announce_endpoint = new IPEndPoint(IPAddress.Parse(upnp_udp_multicast_address), upnp_udp_port);
                byte[] send_bytes = System.Text.Encoding.Default.GetBytes(announce_message);
                udp_send_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None, destination, new AsyncCallback(AnnounceDeviceCallback), udp_send_socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during sending of device announce packet: " + ex.Message);
            }

        }

        /// <summary>
        /// Announce a device to other upnp aware applications
        /// </summary>
        /// <param name="device">the device to broadcast</param>
        public void AnnounceDevice(LocalDevice device)
        {
            try
            {
                string announce_message = "NOTIFY * HTTP/1.1\r\nHOST:239.255.255.250:1900\r\nCache-Control:max-age=120\r\nLocation:http://" + local_device_handler.IP + ":" + local_device_handler.Port + "/device/" + device.DeviceID + "/root.xml\r\nNT:urn:" + device.UniqueServiceName + "\r\nUSN:uuid:" + device.UniversalUniqueID + "::urn:" + device.UniqueServiceName + "\r\nNTS:ssdp:alive\r\nServer:NT/5.0 UPnP/1.0\r\n\r\n\r\n";

                IPEndPoint udp_announce_endpoint = new IPEndPoint(IPAddress.Parse(upnp_udp_multicast_address), upnp_udp_port);
                byte[] send_bytes = System.Text.Encoding.Default.GetBytes(announce_message);
                udp_send_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None, udp_announce_endpoint, new AsyncCallback(AnnounceDeviceCallback), udp_send_socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during sending of device announce packet: " + ex.Message);
            }

        }
        /// <summary>
        /// Callback for the Announce Device async send
        /// </summary>
        /// <param name="ar">Async Result/State</param>
        protected void AnnounceDeviceCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            try
            {
                int bytes_sent = socket.EndSend(ar);

            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during sending of device announce packet: " + ex.Message);
            }
        }
        /// <summary>
        /// Announce a service to other upnp aware applications
        /// </summary>
        /// <param name="device">the device to broadcast</param>
        public void AnnounceDeviceService(LocalDevice device,SubDevice.Service service)
        {
            try
            {
                string announce_message = "NOTIFY * HTTP/1.1\r\nHOST:239.255.255.250:1900\r\nCache-Control:max-age=120\r\nLocation:http://" + local_device_handler.IP + ":" + local_device_handler.Port + "/device/"+device.DeviceID+"/root.xml\r\nNT: urn:"+service.ServiceType+"\r\nUSN:uuid:"+service.ServiceID+"::urn:"+service.ServiceType+"\r\nNTS:ssdp:alive\r\nServer:NT/5.0 UPnP/1.0\r\n\r\n\r\n";

                IPEndPoint udp_announce_endpoint = new IPEndPoint(IPAddress.Parse(upnp_udp_multicast_address), upnp_udp_port);
                byte[] send_bytes = System.Text.Encoding.Default.GetBytes(announce_message);
                udp_send_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None, udp_announce_endpoint, new AsyncCallback(AnnounceDeviceServiceCallback), udp_send_socket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during sending of device service announce packet: " + ex.Message);
            }

        }
        /// <summary>
        /// Callback for the Announce Device Service async send
        /// </summary>
        /// <param name="ar">Async Result/State</param>
        protected void AnnounceDeviceServiceCallback(IAsyncResult ar)
        {
            Socket socket = (Socket)ar.AsyncState;
            try
            {
                int bytes_sent = socket.EndSend(ar);

            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during sending of device service announce packet: " + ex.Message);
            }
        }

        public void AnnounceDeviceServices(LocalDevice device)
        {
            foreach (SubDevice.Service service in device.RootDevice.Services)
            {
                AnnounceDeviceService(device, service);
            }
        }
        /// <summary>
        /// Announce all local devices
        /// </summary>
        public void AnnounceLocalDevices()
        {
            foreach (LocalDevice device in local_devices)
            {
                AnnounceDevice(device);
                AnnounceDeviceServices(device);
            }

        }

        public void AddLocalDevice(LocalDevice device)
        {
            if (local_devices.Count == 0)
                StartLocalDeviceHandler();
            local_devices.Add(device);
            AnnounceDevice(device);
            AnnounceDeviceServices(device);

        }

        public void RemoveLocalDevice(LocalDevice device)
        {
            local_devices.Remove(device);
            if (local_devices.Count == 0)
                StopLocalDeviceHandler();
            //TODO let other upnp devices know that this device has gone offline
        }

        private object local_device_handler_started_lock = new object();
        private bool local_device_handler_started = false;
        /// <summary>
        /// starts the mini web server to handle local device requests
        /// if not already running
        /// </summary>
        private void StartLocalDeviceHandler()
        {
            lock (local_device_handler_started_lock)
            {
                if (!local_device_handler_started)
                {
                    local_device_handler_started = true;
                    local_device_handler = new MiniWebServer();
                    local_device_handler.SetupListeningSocket();
                    local_device_handler.RequestReceived += delegate(MiniWebServer request_server, MiniWebServer.Request request)
                        {
                            Console.WriteLine("Request received: ");
                            Console.WriteLine("URL: " + request.Url);
                            Console.WriteLine("Method: " + request.Method);
                            Console.WriteLine("Version: " + request.Version);
                            Console.WriteLine("Headers:");
                            foreach (string key in request.Headers.Keys)
                            {
                                Console.WriteLine("[" + key + "]" + ":[" + request.Headers.Get(key) + "]");
                            }


                            int device_id = -1;
                            string device_id_string = "-1";
                            int device_id_start = request.Url.IndexOf("/device/");
                            if (device_id_start != -1)
                            {
                                device_id_string = request.Url.Substring(device_id_start + "/device/".Length);
                                int device_id_end = device_id_string.IndexOf("/");
                                if (device_id_end != -1)
                                {
                                    device_id_string = device_id_string.Substring(0, device_id_end);
                                    Console.WriteLine("device id string: " + device_id_string);
                                }
                            }
                            try
                            {
                                device_id = int.Parse(device_id_string);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error parsing device id: " + ex.Message);
                            }

                            if (device_id != -1)
                            {
                                if (device_id >= local_devices.Count)
                                    request.RequestClient.TellNotFound();

                                string service_id = "";
                                int service_id_start = request.Url.IndexOf("/service/");
                                if (service_id_start != -1)
                                {
                                    service_id = request.Url.Substring(service_id_start + "/service/".Length);
                                    int service_id_end = service_id.IndexOf("/");
                                    if (service_id_end != -1)
                                    {
                                        service_id = service_id.Substring(0, service_id_end);
                                        Console.WriteLine("service id: " + service_id);
                                    }
                                }

                                if (service_id != "")
                                {
                                    if (request.Url.EndsWith("/scpd.xml"))
                                    {
                                        //TODO add correct scpd of service in here
                                        Console.WriteLine("Clients want the service description xml. -> oops ;-)");
                                    }
                                    else if (request.Url.EndsWith("/control"))
                                    {
                                        request.RequestClient.Answer(local_devices[device_id].Control(request, service_id), "text/xml");
                                    }
                                    else if (request.Url.EndsWith("/event"))
                                    {
                                        SubDevice.Service control_service = local_devices[device_id].RootDevice.GetServiceByID(service_id);
                                        Console.WriteLine("opened event url of service :" + control_service.ServiceType);
                                        request.RequestClient.Answer(local_devices[device_id].Event(request, service_id), "text/xml");
                                    }
                                }
                                else if (request.Url.EndsWith("/root.xml"))
                                {
                                    string root_description = "";
                                    root_description += "<?xml version=\"1.0\" encoding=\"utf-8\"?>";
                                    root_description += "<root xmlns=\"urn:schemas-upnp-org:device-1-0\">";
                                    root_description += "<specVersion>";
                                    root_description += "<major>" + local_devices[device_id].SpecVersionMajor + "</major>";
                                    root_description += "<minor>" + local_devices[device_id].SpecVersionMinor + "</minor>";
                                    root_description += "</specVersion>";
                                    root_description += "<device>";
                                    root_description += "<deviceType>urn:" + local_devices[device_id].RootDevice.DeviceType + "</deviceType>";
                                    //root_description += "<INMPR03>1.0</INMPR03>";
                                    root_description += "<friendlyName>" + local_devices[device_id].RootDevice.FriendlyName + "</friendlyName>";
                                    root_description += "<manufacturer>" + local_devices[device_id].RootDevice.Manufacturer + "</manufacturer>";
                                    root_description += "<manufacturerURL>" + local_devices[device_id].RootDevice.ManufacturerUrl + "</manufacturerURL>";
                                    root_description += "<modelDescription>" + local_devices[device_id].RootDevice.ModelDescription + "</modelDescription>";
                                    root_description += "<modelName>" + local_devices[device_id].RootDevice.ModelName + "</modelName>";
                                    root_description += "<modelNumber>" + local_devices[device_id].RootDevice.ModelNumber + "</modelNumber>";
                                    root_description += "<modelURL>" + local_devices[device_id].RootDevice.ModelUrl + "</modelURL>";
                                    root_description += "<UDN>uuid:" + local_devices[device_id].RootDevice.UniversalUniqueID + "</UDN>";
                                    root_description += "<serviceList>";
                                    foreach (SubDevice.Service service in local_devices[device_id].RootDevice.Services)
                                    {
                                        root_description += "<service>";
                                        root_description += "<serviceType>urn:" + service.ServiceType + "</serviceType>";
                                        root_description += "<serviceId>urn:" + service.ServiceID + "</serviceId>";
                                        root_description += "<SCPDURL>/device/" + device_id + "/service/" + service.ServiceID + "/scpd.xml</SCPDURL>";
                                        root_description += "<controlURL>/device/" + device_id + "/service/" + service.ServiceID + "/control</controlURL>";
                                        root_description += "<eventSubURL>/device/" + device_id + "/service/" + service.ServiceID + "/event</eventSubURL>";
                                        root_description += "</service>";
                                    }
                                    root_description += "</serviceList>";
                                    root_description += "</device>";
                                    root_description += "</root>";
                                    request.RequestClient.Answer(root_description, "text/xml");
                                }
                                else request.RequestClient.TellNotFound();


                            }
                            else request.RequestClient.TellNotFound();
                        };
                }
            }
        }
        /// <summary>
        /// stops the mini web server to handle local device requests
        /// if running
        /// </summary>
        private void StopLocalDeviceHandler()
        {
            lock (local_device_handler_started_lock)
            {
                if (local_device_handler_started)
                {
                    local_device_handler_started = false;
                    local_device_handler.CloseListeningSocket();
                    local_device_handler = null;
                }
            }
        }

        private Socket udp_socket = null;
        private Socket udp_send_socket = null;
        private object listening_lock = new Object();
        protected bool listening = false;
        private string ip = "127.0.0.1";
        private MiniWebServer local_device_handler;

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

            //SetupSockets();
        }
        
        public void SetupSockets()
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
        public void CloseSockets()
        {
            StopLocalDeviceHandler();
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
        /// List of upnp devices handled locally
        /// </summary>
        protected List<LocalDevice> local_devices = new List<LocalDevice>();
        /// <summary>
        /// List of upnp devices in the lan
        /// (Cache)
        /// </summary>
        protected List<Device> devices = new List<Device>();
        /// <summary>
        /// Clear the device list
        /// </summary>
        public void ClearDevicesList()
        {
            devices.Clear();
        }
        /// <summary>
        /// Interpret a received string from the udp sockets
        /// </summary>
        /// <param name="received_string">the body of the udp packet</param>
        /// <param name="was_broadcasted">TRUE if it was received via broadcast</param>
        private void InterpretReceivedString(string received_string, bool was_broadcasted)
        {
            
            string message = "Received a";
            IPEndPoint received_from;
            if (was_broadcasted)
            {
                message += " broadcasted unpnp string ";
                message += "from :" + ((IPEndPoint)broadcast_receive_from_endpoint).Address.ToString();
                message += ":" + ((IPEndPoint)broadcast_receive_from_endpoint).Port.ToString();
                received_from = (IPEndPoint)broadcast_receive_from_endpoint;
            }
            else
            {
                message += "n unicasted unpnp string ";
                message += "from :" + ((IPEndPoint)receive_from_endpoint).Address.ToString();
                message += ":" + ((IPEndPoint)receive_from_endpoint).Port.ToString();
                received_from = (IPEndPoint)receive_from_endpoint;
            }
            message += ": ";
            /* 
            Console.WriteLine("");
            Console.WriteLine(message);
            Console.WriteLine(received_string);
            Console.WriteLine("");
            //Console.WriteLine("Header Lines:");
             */

            //Console.WriteLine(received_string);
            string[] seps = {"\r\n"};
            string[] lines = received_string.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            string uuid = "";
            string urn = "";
            string server = "";
            string location = "";
            string usn = "";
            string mx = "";
            string st = "";
            string nt = "";
            string man = "";
            bool discovery_request = false;

            foreach (string line in lines)
            {


                //Console.WriteLine("line:" + line);
                if (line.StartsWith("MX:", true, null))
                {
                    mx = line.Substring("MX:".Length).Trim();
                }
                if (line.StartsWith("MAN:", true, null))
                {
                    man = line.Substring("MAN:".Length).Trim();
                }
                if (line.StartsWith("NT:", true, null))
                {
                    nt = line.Substring("NT:".Length).Trim();
                }
                if (line.StartsWith("ST:", true, null))
                {
                    st = line.Substring("ST:".Length).Trim();
                }
                if (line == "M-SEARCH * HTTP/1.1")
                {
                    discovery_request = true;
                }

                if (line.StartsWith("SERVER:", true, null))
                {
                    server = line.Substring(8);
                }
                if (line.StartsWith("LOCATION:", true, null))
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

            if (discovery_request)
            {//received a discovery request 
                //check header options
                //and answer if needed
                Console.WriteLine("checking local devices for service matching the search request.");
                foreach (LocalDevice device in local_devices)
                {
                    ReplyDevice(device, received_from);
                }

                //TODO Add response discovery method


            }
            else if (urn != "" && uuid != "" && server != "" && location != "" && usn != "" && !discovery_request)
            {//found a usable packet for a device urn compare
                if (urn == "schemas-upnp-org:service:WANIPConnection:1") //TODO maybe change this to InternetGatewayDevice
                {//we found a router 
                    //check if its a new or to be updated entry
                    Device existing = GetDeviceByUUID(uuid);
                    if (existing != null)
                    {//we need to update a router entry
                        Router updated = (Router)existing;
                    }
                    else
                    {//we discovered a new router
                        Router r = new Router(usn, location, server, uuid);
                        devices.Add(r);
                        if (DeviceDiscovered != null)
                            DeviceDiscovered(r);
                    }
                }
                else if (urn == "schemas-upnp-org:device:MediaRenderer:1")
                {//we found a media renderer
                    //check if its a new or to be updated entry
                    Device existing = GetDeviceByUUID(uuid);
                    if (existing != null)
                    {//we need to update a router entry
                        MediaRenderer updated = (MediaRenderer)existing;
                    }
                    else
                    {//we discovered a new media renderer
                        MediaRenderer mr = new MediaRenderer(usn, location, server, uuid);
                        devices.Add(mr);
                        if (DeviceDiscovered != null)
                            DeviceDiscovered(mr);
                    }
                }
                else if (urn == "schemas-upnp-org:device:BinaryLight:1")
                {//we found a binary light
                    //check if its a new or to be updated entry
                    Device existing = GetDeviceByUUID(uuid);
                    if (existing != null)
                    {//we need to update a router entry
                        BinaryLight updated = (BinaryLight)existing;
                    }
                    else
                    {//we discovered a new media renderer
                        BinaryLight bl = new BinaryLight(usn, location, server, uuid);
                        devices.Add(bl);
                        if (DeviceDiscovered != null)
                            DeviceDiscovered(bl);
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
        /// (an upnp device in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestDiscovery()
        {
            Console.WriteLine("Test to discover supported upnp devices.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            int found = 0;
            u.DeviceDiscovered += delegate(Device d)
            {
                string message_border = "-- Discovered a " + Enum.GetName(typeof(Device.DeviceTypes), d.DeviceType) + " --";
                Console.WriteLine("");
                Console.WriteLine(message_border);
                Console.WriteLine("Server: " + d.Server);
                Console.WriteLine("Host: " + d.Host + ":" + d.Port);
                Console.WriteLine("UUID: " + d.UniversalUniqueID);
                Console.WriteLine("Location: " + d.Location);
                Console.WriteLine("Unique Service Name: " + d.UniqueServiceName);
                Console.WriteLine(message_border);
                Console.WriteLine("");
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                //wait = false;
                found++;

            };

            u.StartDiscovery();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 15))
                {
                    if (found == 0)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("Operation took too long");
                        Assert.Fail("Operation took too long");
                    }
                    wait = false;
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("");
            if (found > 0) Console.WriteLine("Found " + found + " UPnP Devices.");
            Assert.IsTrue(found>0,"Test failed: No devices found.");
            Console.WriteLine("UPnP Device Discovery Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if gathering of device information works
        /// (an upnp device in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestInformationGathering()
        {
            Console.WriteLine("Test to gather information from a device.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                d.InformationGathered += delegate(Device dig, bool was_successful)
                {
                    if (was_successful)
                    {
                        Console.WriteLine("");
                        Console.WriteLine("-- Gathered device information --");
                        Console.WriteLine("Device Type: "+Enum.GetName(typeof(Device.DeviceTypes),dig.DeviceType));
                        Console.WriteLine("SpecVersion: " + dig.SpecVersionMajor + "." + dig.SpecVersionMinor);
                        Console.WriteLine("URLBase: " + dig.UrlBase);
                        if (dig.RootDevice != null)
                        {
                            Console.WriteLine("presentationURL: " + dig.RootDevice.PresentationUrl);
                            Console.WriteLine("friendlyName: " + dig.RootDevice.FriendlyName);
                            Console.WriteLine("manufacturer: " + dig.RootDevice.Manufacturer);
                            Console.WriteLine("manufacturerURL: " + dig.RootDevice.ManufacturerUrl);
                            Console.WriteLine("modelDescription: " + dig.RootDevice.ModelDescription);
                            Console.WriteLine("modelName: " + dig.RootDevice.ModelName);
                            Console.WriteLine("Number of Sub Devices: " + dig.RootDevice.Devices.Count);
                            Console.WriteLine("Sub UUID: " + dig.RootDevice.UniversalUniqueID);
                        }
                        //Console.WriteLine("Server: " + r.Server);
                        Console.WriteLine("UUID: " + dig.UniversalUniqueID);
                        Console.WriteLine("-- Gathered device information --");
                        Console.WriteLine("");
                        wait = false;
                    }
                    else Console.WriteLine("failed to gather device information");

                };
                d.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                string message_border = "-- Discovered a " + Enum.GetName(typeof(Device.DeviceTypes), d.DeviceType) + " --";
                Console.WriteLine("");
                Console.WriteLine(message_border);
                Console.WriteLine("Server: " + d.Server);
                Console.WriteLine("Host: " + d.Host + ":" + d.Port);
                Console.WriteLine("UUID: " + d.UniversalUniqueID);
                Console.WriteLine("Location: " + d.Location);
                Console.WriteLine("Unique Service Name: " + d.UniqueServiceName);
                Console.WriteLine(message_border);
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
            Console.WriteLine("UPnP Device Information Gathering Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if discovery of a media renderer works the way it should
        /// (an upnp media renderer in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestMediaRendererDiscovery()
        {
            Console.WriteLine("Test to discover an upnp media renderer.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.MediaRenderer)
                {
                    MediaRenderer mr = (MediaRenderer)d;
                    Console.WriteLine("");
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("Server: " + mr.Server);
                    Console.WriteLine("Host: " + mr.Host + ":" + mr.Port);
                    Console.WriteLine("UUID: " + mr.UniversalUniqueID);
                    Console.WriteLine("Location: " + mr.Location);
                    Console.WriteLine("Unique Service Name: " + mr.UniqueServiceName);
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("");
                    //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                    wait = false;
                }
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
            Console.WriteLine("UPnP Media Renderer Discovery Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if gathering of Media Renderer information works
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestMediaRendererInformationGathering()
        {
            Console.WriteLine("Test to gather information from a media renderer.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.MediaRenderer)
                {
                    MediaRenderer mr = (MediaRenderer)d;
                    mr.InformationGathered += delegate(Device dimr, bool was_successful)
                    {
                        MediaRenderer imr = (MediaRenderer)dimr;
                        if (was_successful)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("-- Gathered media renderer information --");
                            Console.WriteLine("SpecVersion: " + imr.SpecVersionMajor + "." + imr.SpecVersionMinor);
                            Console.WriteLine("URLBase: " + imr.UrlBase);
                            if (imr.RootDevice != null)
                            {
                                Console.WriteLine("presentationURL: " + imr.RootDevice.PresentationUrl);
                                Console.WriteLine("friendlyName: " + imr.RootDevice.FriendlyName);
                                Console.WriteLine("manufacturer: " + imr.RootDevice.Manufacturer);
                                Console.WriteLine("manufacturerURL: " + imr.RootDevice.ManufacturerUrl);
                                Console.WriteLine("modelDescription: " + imr.RootDevice.ModelDescription);
                                Console.WriteLine("modelName: " + imr.RootDevice.ModelName);
                                Console.WriteLine("Number of Sub Devices: " + imr.RootDevice.Devices.Count);
                                Console.WriteLine("Sub UUID: " + imr.RootDevice.UniversalUniqueID);
                            }
                            //Console.WriteLine("Server: " + r.Server);
                            Console.WriteLine("UUID: " + imr.UniversalUniqueID);
                            Console.WriteLine("-- Gathered media renderer information --");
                            Console.WriteLine("");
                            wait = false;
                        }
                        else Console.WriteLine("failed to gather media renderer information");
                    };

                    mr.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                    Console.WriteLine("");
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("Server: " + mr.Server);
                    Console.WriteLine("Host: " + mr.Host + ":" + mr.Port);
                    Console.WriteLine("UUID: " + mr.UniversalUniqueID);
                    Console.WriteLine("Location: " + mr.Location);
                    Console.WriteLine("Unique Service Name: " + mr.UniqueServiceName);
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("");
                }
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
            Console.WriteLine("UPnP Media Renderer Information Gathering Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if setting a playback url of a media renderer works
        /// (an upnp media renderer in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestMediaRendererSetPlaybackUrl()
        {
            Console.WriteLine("Test to set a playback url of a media renderer.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.MediaRenderer)
                {
                    MediaRenderer mr = (MediaRenderer)d;
                    mr.SettingPlaybackUrlCompleted += delegate(MediaRenderer sp, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Set a playback url.");
                        //wait = !was_successful;
                    };
                    mr.PressingButtonCompleted += delegate(MediaRenderer sp, MediaRenderer.Button pressed, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Pressed the " + Enum.GetName(typeof(MediaRenderer.Button), pressed) + " button.");
                        wait = !was_successful;
                    };

                    mr.InformationGathered += delegate(Device dimr, bool was_successful)
                    {
                        MediaRenderer mir = (MediaRenderer)dimr;
                        if (was_successful)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("-- Gathered media renderer information --");
                            Console.WriteLine("SpecVersion: " + mir.SpecVersionMajor + "." + mir.SpecVersionMinor);
                            Console.WriteLine("URLBase: " + mir.UrlBase);
                            if (mir.RootDevice != null)
                            {
                                Console.WriteLine("presentationURL: " + mir.RootDevice.PresentationUrl);
                                Console.WriteLine("friendlyName: " + mir.RootDevice.FriendlyName);
                                Console.WriteLine("manufacturer: " + mir.RootDevice.Manufacturer);
                                Console.WriteLine("manufacturerURL: " + mir.RootDevice.ManufacturerUrl);
                                Console.WriteLine("modelDescription: " + mir.RootDevice.ModelDescription);
                                Console.WriteLine("modelName: " + mir.RootDevice.ModelName);
                                Console.WriteLine("Number of Sub Devices: " + mir.RootDevice.Devices.Count);
                                Console.WriteLine("Sub UUID: " + mir.RootDevice.UniversalUniqueID);
                                //Thread.Sleep(3000);
                                mir.SetPlaybackUrl("http://www.voyagerproject.de/stuff/phat_sweet_drill_mix.wav.mp3");
                                Thread.Sleep(1000);
                                mir.Press(MediaRenderer.Button.Play);
                            }
                            //Console.WriteLine("Server: " + r.Server);
                            Console.WriteLine("UUID: " + mir.UniversalUniqueID);
                            Console.WriteLine("-- Gathered media renderer information --");
                            Console.WriteLine("");
                        }
                        else Console.WriteLine("failed to gather media renderer information");
                    };
                    mr.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                    Console.WriteLine("");
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("Server: " + mr.Server);
                    Console.WriteLine("Host: " + mr.Host + ":" + mr.Port);
                    Console.WriteLine("UUID: " + mr.UniversalUniqueID);
                    Console.WriteLine("Location: " + mr.Location);
                    Console.WriteLine("Unique Service Name: " + mr.UniqueServiceName);
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("");
                    //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                }
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
            Console.WriteLine("UPnP Media Renderer Set Playback URL Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if switching off a binary light works
        /// (an upnp binary light in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestBinaryLightSwitchPowerOff()
        {
            Console.WriteLine("Test to switch the power of a binary light off.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.BinaryLight)
                {
                    BinaryLight bl = (BinaryLight)d;
                    bl.SwitchingPowerCompleted += delegate(BinaryLight sp, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Switched some light off.");
                        wait = !was_successful;
                    };

                    bl.InformationGathered += delegate(Device dibl, bool was_successful)
                    {
                        BinaryLight blig = (BinaryLight)dibl;
                        if (was_successful)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("-- Gathered binary light information --");
                            Console.WriteLine("SpecVersion: " + blig.SpecVersionMajor + "." + blig.SpecVersionMinor);
                            Console.WriteLine("URLBase: " + blig.UrlBase);
                            if (blig.RootDevice != null)
                            {
                                Console.WriteLine("presentationURL: " + blig.RootDevice.PresentationUrl);
                                Console.WriteLine("friendlyName: " + blig.RootDevice.FriendlyName);
                                Console.WriteLine("manufacturer: " + blig.RootDevice.Manufacturer);
                                Console.WriteLine("manufacturerURL: " + blig.RootDevice.ManufacturerUrl);
                                Console.WriteLine("modelDescription: " + blig.RootDevice.ModelDescription);
                                Console.WriteLine("modelName: " + blig.RootDevice.ModelName);
                                Console.WriteLine("Number of Sub Devices: " + blig.RootDevice.Devices.Count);
                                Console.WriteLine("Sub UUID: " + blig.RootDevice.UniversalUniqueID);
                                blig.SwitchPower(BinaryLight.PowerStates.Off);
                            }
                            //Console.WriteLine("Server: " + r.Server);
                            Console.WriteLine("UUID: " + blig.UniversalUniqueID);
                            Console.WriteLine("-- Gathered binary light information --");
                            Console.WriteLine("");
                        }
                        else Console.WriteLine("failed to gather binary light information");
                    };
                    bl.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                    Console.WriteLine("");
                    Console.WriteLine("-- Discovered a binary light --");
                    Console.WriteLine("Server: " + bl.Server);
                    Console.WriteLine("Host: " + bl.Host + ":" + bl.Port);
                    Console.WriteLine("UUID: " + bl.UniversalUniqueID);
                    Console.WriteLine("Location: " + bl.Location);
                    Console.WriteLine("Unique Service Name: " + bl.UniqueServiceName);
                    Console.WriteLine("-- Discovered a binary light --");
                    Console.WriteLine("");
                    //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                }
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
            Console.WriteLine("UPnP Binary Light Switch Power Off Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if switching on a binary light works
        /// (an upnp binary light in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestBinaryLightSwitchPowerOn()
        {
            Console.WriteLine("Test to switch the power of a binary light on.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.BinaryLight)
                {
                    BinaryLight bl = (BinaryLight)d;
                    bl.SwitchingPowerCompleted += delegate(BinaryLight sp, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Switched some light on.");
                        wait = !was_successful;
                    };

                    bl.InformationGathered += delegate(Device dibl, bool was_successful)
                    {
                        BinaryLight blig = (BinaryLight)dibl;
                        if (was_successful)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("-- Gathered binary light information --");
                            Console.WriteLine("SpecVersion: " + blig.SpecVersionMajor + "." + blig.SpecVersionMinor);
                            Console.WriteLine("URLBase: " + blig.UrlBase);
                            if (blig.RootDevice != null)
                            {
                                Console.WriteLine("presentationURL: " + blig.RootDevice.PresentationUrl);
                                Console.WriteLine("friendlyName: " + blig.RootDevice.FriendlyName);
                                Console.WriteLine("manufacturer: " + blig.RootDevice.Manufacturer);
                                Console.WriteLine("manufacturerURL: " + blig.RootDevice.ManufacturerUrl);
                                Console.WriteLine("modelDescription: " + blig.RootDevice.ModelDescription);
                                Console.WriteLine("modelName: " + blig.RootDevice.ModelName);
                                Console.WriteLine("Number of Sub Devices: " + blig.RootDevice.Devices.Count);
                                Console.WriteLine("Sub UUID: " + blig.RootDevice.UniversalUniqueID);
                                blig.SwitchPower(BinaryLight.PowerStates.On);
                            }
                            //Console.WriteLine("Server: " + r.Server);
                            Console.WriteLine("UUID: " + blig.UniversalUniqueID);
                            Console.WriteLine("-- Gathered binary light information --");
                            Console.WriteLine("");
                        }
                        else Console.WriteLine("failed to gather binary light information");
                    };
                    bl.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                    Console.WriteLine("");
                    Console.WriteLine("-- Discovered a binary light --");
                    Console.WriteLine("Server: " + bl.Server);
                    Console.WriteLine("Host: " + bl.Host + ":" + bl.Port);
                    Console.WriteLine("UUID: " + bl.UniversalUniqueID);
                    Console.WriteLine("Location: " + bl.Location);
                    Console.WriteLine("Unique Service Name: " + bl.UniqueServiceName);
                    Console.WriteLine("-- Discovered a binary light --");
                    Console.WriteLine("");
                    //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                }
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
            Console.WriteLine("UPnP Binary Light Switch Power On Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if discovery of a router works the way it should
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterDiscovery()
        {
            Console.WriteLine("Test to discover an upnp router.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
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
                }
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
            Console.WriteLine("UPnP Router Discovery Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if announcing of a device works the way it should
        /// </summary>
        [Test]
        public void TestAnnounceDevice()
        {
            Console.WriteLine("Test to announce an upnp device.");
            UPnP u = new UPnP();
            u.SetupSockets();
            MediaBrowser mb = new MediaBrowser();

            bool wait = true;
            //     wait = false;
            u.AnnounceDevice(mb);
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
            Console.WriteLine("UPnP Device Announcing Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if adding a local device works the way it should
        /// </summary>
        [Test]
        public void TestAddLocalDevice()
        {
            Console.WriteLine("Test to add a local upnp device.");
            UPnP u = new UPnP();
            u.SetupSockets();
            MediaBrowser mb = new MediaBrowser();
            bool wait = true;
            //     wait = false;
            mb.RootDevice.FriendlyName = "vpMediaTestserver";
            mb.BrowseRequestReceived += delegate(MediaBrowser media_browser,string object_id,string browse_flag,int starting_index,int requested_count,string sort_criteria)
            {
                Console.WriteLine("Browse request received: \nobject_id: "+object_id+"\nbrowse_flag: "+browse_flag+"\nstarting_index: "+starting_index+"\nrequested_count: "+requested_count+"\nsort_criteria: "+sort_criteria+"\n");
                MediaBrowser.BrowseResult result = new MediaBrowser.BrowseResult();
                if (object_id == "0")
                {
                    result.TotalMatches = 3;
                    result.AddFolder("Queue", "queue", "0", true, false, 3, "UNKNOWN", -1);
                    result.AddItem("test item", "pez2001", "http://www.voyagerproject.de/stuff/phat_sweet_drill_mix.wav.mp3", "audio/mpeg", "test", "0", false, -1, "UNKNOWN", "test item");
                    result.AddItem("test item 2", "pez2001", "http://www.voyagerproject.org/wp-content/uploads/2006/05/treiwund%20+%20graf%20contra2.mp3", "audio/mpeg", "test", "0", false, -1, "UNKNOWN", "test item");
                }
                else if (object_id == "queue")
                {
                    result.TotalMatches = 3;
                    result.AddFolder("Root", "0", "0", true, false, 3, "UNKNOWN", -1);
                    result.AddItem("test item b", "pez2001", "http://www.voyagerproject.de/stuff/phat_sweet_drill_mix.wav.mp3", "audio/mpeg", "test", "0", false, -1, "UNKNOWN", "test item");
                    result.AddItem("test item b 2", "pez2001", "http://www.voyagerproject.org/wp-content/uploads/2006/05/treiwund%20+%20graf%20contra2.mp3", "audio/mpeg", "test", "0", false, -1, "UNKNOWN", "test item");
                }
                return (result);
            };
            u.AddLocalDevice(mb);
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
            Console.WriteLine("UPnP Local Device Add Test successful.");

            u.CloseSockets();
        }
        /// <summary>
        /// Test to see if gathering of Router information works
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterInformationGathering()
        {
            Console.WriteLine("Test to gather information from a router.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterForceTermination()
        {
            Console.WriteLine("Test to disconnect a router.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
                    r.ForcedTerminationCompleted += delegate(Router fr, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Disconnected Router.");
                        wait = !was_successful;
                    };

                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterRequestConnection()
        {
            Console.WriteLine("Test to connect a router.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;

                    r.ConnectionRequestCompleted += delegate(Router fr, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Connected Router.");
                        wait = !was_successful;
                    };

                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterFetchExternalIP()
        {
            Console.WriteLine("Test to fetch the external ip from a router.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
                    r.FetchExternalIPCompleted += delegate(Router fr, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Fetched the external router: [" + fr.ExternalIP + "]");
                        wait = !was_successful;
                    };

                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterAddPortMapping()
        {
            Console.WriteLine("Test to add a portmapping on a router.");
            UPnP u = new UPnP();
            u.SetupSockets();
            Console.WriteLine("our internal ip: "+ip);
            bool wait = true;
            bool test_failed = false;
            string test_failed_reason = "";

            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
                    int prots = 0;
                    r.AddingPortMappingCompleted += delegate(Router apm, Router.PortMapping pm, bool was_successful)
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

                        if (prots == 2)
                            wait = !was_successful;
                    };

                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterDeletePortMapping()
        {
            Console.WriteLine("Test to delete a portmapping on a router.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            bool test_failed = false;
            string test_failed_reason = "";
            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
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

                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterCheckForExistingPortMapping()
        {
            Console.WriteLine("Test to check a portmapping on a router for its existance.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
            bool test_failed = false;
            string test_failed_reason = "";

            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
                    int prots = 0;
                    r.PortMappingCheckCompleted += delegate(Router apm, Router.PortMapping pm, bool exists, bool was_successful)
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

                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// (an upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestRouterStatusUpdating()
        {
            Console.WriteLine("Test to update status from a router.");
            UPnP u = new UPnP();
            u.SetupSockets();

            bool wait = true;
             u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.Router)
                {
                    Router r = (Router)d;
                    r.StatusUpdated += delegate(Router ur, bool was_successful)
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

                    r.InformationGathered += delegate(Device dir, bool was_successful)
                    {
                        Router ir = (Router)dir;
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
                }
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
        /// <summary>
        /// Test to see if setting a playback url of a media renderer connection works
        /// in conjunction with a mini web server
        /// (an upnp media renderer in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestMediaRendererSetPlaybackUrlWithMiniWebServer()
        {
            Console.WriteLine("Test to set a playback url of a media renderer with a mini web server.");
            UPnP u = new UPnP();
            u.SetupSockets();
            MiniWebServer server = new MiniWebServer();
            server.Port = 80;
            server.SetupListeningSocket();

            bool wait = true;
            server.RequestReceived += delegate(MiniWebServer request_server, MiniWebServer.Request request)
            {
                Console.WriteLine("Request received: ");
                Console.WriteLine("URL: " + request.Url);
                Console.WriteLine("Method: " + request.Method);
                Console.WriteLine("Version: " + request.Version);
                Console.WriteLine("Headers:");
                foreach (string key in request.Headers.Keys)
                {
                    Console.WriteLine("[" + key + "]" + ":[" + request.Headers.Get(key) + "]");
                }
                if (request.Url == "/")
                {
                    string page = "";
                    //string type = "text/plain";
                    page = "<html>\n<head>\n<title>MiniWebServer Test Page</title>\n</head>\n<body bgcolor=\"#333355\">Test Page of the Miniwebserver running on port: " + server.Port + "<br><a href=\"/test.mp3\">Test Mp3</a></body>\n</html>\n";
                    string type = "text/html";
                    request.RequestClient.Answer(page, type);
                }
                else if (request.Url == "/test.mp3")
                {
                    byte[] mp3 = File.ReadAllBytes("..\\..\\..\\TestDateien\\test.mp3");
                    string type = "audio/mpeg";
                    request.RequestClient.Answer(mp3, type);
                }
            };

            u.DeviceDiscovered += delegate(Device d)
            {
                if (d.DeviceType == Device.DeviceTypes.MediaRenderer)
                {
                    MediaRenderer mr = (MediaRenderer)d;
                    mr.SettingPlaybackUrlCompleted += delegate(MediaRenderer sp, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Set a playback url.");
                        //wait = !was_successful;
                    };
                    mr.PressingButtonCompleted += delegate(MediaRenderer sp, MediaRenderer.Button pressed, bool was_successful)
                    {
                        if (was_successful)
                            Console.WriteLine("Pressed the " + Enum.GetName(typeof(MediaRenderer.Button), pressed) + " button.");
                        wait = !was_successful;
                    };

                    mr.InformationGathered += delegate(Device dimr, bool was_successful)
                    {
                        MediaRenderer mir = (MediaRenderer)dimr;
                        if (was_successful)
                        {
                            Console.WriteLine("");
                            Console.WriteLine("-- Gathered media renderer information --");
                            Console.WriteLine("SpecVersion: " + mir.SpecVersionMajor + "." + mir.SpecVersionMinor);
                            Console.WriteLine("URLBase: " + mir.UrlBase);
                            if (mir.RootDevice != null)
                            {
                                Console.WriteLine("presentationURL: " + mir.RootDevice.PresentationUrl);
                                Console.WriteLine("friendlyName: " + mir.RootDevice.FriendlyName);
                                Console.WriteLine("manufacturer: " + mir.RootDevice.Manufacturer);
                                Console.WriteLine("manufacturerURL: " + mir.RootDevice.ManufacturerUrl);
                                Console.WriteLine("modelDescription: " + mir.RootDevice.ModelDescription);
                                Console.WriteLine("modelName: " + mir.RootDevice.ModelName);
                                Console.WriteLine("Number of Sub Devices: " + mir.RootDevice.Devices.Count);
                                Console.WriteLine("Sub UUID: " + mir.RootDevice.UniversalUniqueID);
                                //Thread.Sleep(3000);
                                mir.SetPlaybackUrl("http://" + ip + "/test.mp3");
                                Thread.Sleep(1000);
                                mir.Press(MediaRenderer.Button.Play);
                            }
                            //Console.WriteLine("Server: " + r.Server);
                            Console.WriteLine("UUID: " + mir.UniversalUniqueID);
                            Console.WriteLine("-- Gathered media renderer information --");
                            Console.WriteLine("");
                        }
                        else Console.WriteLine("failed to gather media renderer information");
                    };
                    mr.GatherInformation();//TODO this could also be done by upnp after it fired the discovered event

                    Console.WriteLine("");
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("Server: " + mr.Server);
                    Console.WriteLine("Host: " + mr.Host + ":" + mr.Port);
                    Console.WriteLine("UUID: " + mr.UniversalUniqueID);
                    Console.WriteLine("Location: " + mr.Location);
                    Console.WriteLine("Unique Service Name: " + mr.UniqueServiceName);
                    Console.WriteLine("-- Discovered a media renderer --");
                    Console.WriteLine("");
                    //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                }
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
            Console.WriteLine("UPnP Media Renderer Set Playback URL With Mini Web Server Test successful.");

            u.CloseSockets();
        }
        #endregion
    }
}
