using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace DCPlusPlus
{

    /// <summary>
    /// it's sole purpose is to check urls for relocation header replies
    /// and change the url to the relocation url via an event
    /// </summary>
    public class UrlRelocationCheck
    {
        private string url = "";
        /// <summary>
        /// the url to check for relocation
        /// </summary>
        public string Url
        {
            get { return url; }
            set { url = value; }
        }
        private string relocated_url = "";
        /// <summary>
        /// the relocation url found
        /// </summary>
	    public string RelocatedUrl
	    {
    		get { return relocated_url;}
		    set { relocated_url = value;}
	    }
        private string mime_type = "";
        /// <summary>
        /// the mime type if returned from the web server
        /// </summary>
        public string MimeType
        {
            get { return mime_type; }
            set { mime_type = value; }
        }
        private bool is_busy = false;
        /// <summary>
        /// is the the url checker busy?
        /// </summary>
	    public bool IsBusy
	    {
		    get { return is_busy;}
	    	set { is_busy = value;}
    	}
        /// <summary>
        /// Event handler that gets called
        /// when a relocated url was found
        /// </summary>
        public event FoundRelocatedUrlEventHandler FoundRelocatedUrl;
        /// <summary>
        /// Prototype for the Found Relocated Url Event Handler
        /// </summary>
        public delegate void FoundRelocatedUrlEventHandler(UrlRelocationCheck url_checker);
        /// <summary>
        /// Event handler that gets called
        /// when a mime type was found
        /// </summary>
        public event FoundMimeTypeEventHandler FoundMimeType;
        /// <summary>
        /// Prototype for the Found Mime Type Event Handler
        /// </summary>
        public delegate void FoundMimeTypeEventHandler(UrlRelocationCheck url_checker);

        /// <summary>
        /// the socket for the web server connection
        /// </summary>
        private Socket socket = null;
        /// <summary>
        /// Start the url relocation check
        /// </summary>
        /// <param name="check_url">the url to check for a relocation</param>
        public void CheckUrl(string check_url)
        {
            url = check_url;
            CheckUrl();
        }
        /// <summary>
        /// Start the url relocation check
        /// </summary>
        public void CheckUrl()
        {
            Connect();
        }
        /// <summary>
        /// The Receive buffer used by socket 
        /// </summary>
        protected byte[] receive_buffer = null;
        private bool disconnected = true;
        private string reply_string = "";

        private void SendRequest()
        {

            if (socket != null)
            {
                if (!socket.Connected) return;
                try
                {
                    Uri uri = new Uri(url);
                    string request = "GET "+uri.PathAndQuery+" HTTP/1.1\r\n";
                    //request += "Host: "+uri.Host+":"+uri.Port+"\r\n";
                    request += "Host: "+uri.Host+"\r\n";
                    request += "User-Agent: Wget/1.10.2\r\n";
                    request += "Accept: */*\r\n";
                    request += "\r\n";
                    byte[] send_bytes = System.Text.Encoding.Default.GetBytes(request);
                    //send_bytes_length = send_bytes.Length;
                    socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(SendRequestCallback), socket);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error sending request to web server: " + e.Message);
                }
            }

        }
        /// <summary>
        /// Async Callback for the send request
        /// (gets called when the send is completed)
        /// </summary>
        /// <param name="ar">Async Result/State </param>
        private void SendRequestCallback(IAsyncResult ar)
        {
            Socket send_request_socket = (Socket)ar.AsyncState;
            try
            {
                int bytes = send_request_socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during send of request: " + ex.Message);
            }
        }

        private void Disconnect()
        {
            disconnected = true;
            if (socket != null)
            {
                socket.Close();
            }
            else Console.WriteLine("This socket is unused -> no disconnect needed.");
            is_busy = false;
            reply_string = "";
        }
        private void Connect()
        {
            if (is_busy)
            {
                Disconnect();
            }
            if (url != "")
            {
                try
                {
                    Uri uri = new Uri(url);
                    is_busy = true;
                    //Disconnect();//if still connected , disconnect first
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    AsyncCallback event_host_resolved = new AsyncCallback(OnHostResolve);
                    Dns.BeginGetHostEntry(uri.Host, event_host_resolved, socket);
                    
                    //IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port);
                    //AsyncCallback event_connect = new AsyncCallback(OnConnect);
                    //socket.BeginConnect(endpoint, event_connect, socket);
                    socket.ReceiveTimeout = 500;
                    socket.SendTimeout = 500;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to : " + url + "(exception:" + ex.Message + ")");
                    Disconnect();
                }
            }
        }
        /// <summary>
        /// Callback for hostname resolving
        /// </summary>
        /// <param name="result">Async Result/State</param>
        private void OnHostResolve(IAsyncResult result)
        {
            Socket resolve_socket = (Socket)result.AsyncState;
            try
            {
                Uri uri = new Uri(url);
                IPHostEntry ip_entry = Dns.EndGetHostEntry(result);
                if (ip_entry != null && ip_entry.AddressList.Length > 0)
                {
                    //ip = ip_entry.AddressList[0].ToString(); // correct the ip string
                    IPEndPoint endpoint = new IPEndPoint(ip_entry.AddressList[0], uri.Port);
                    AsyncCallback event_connect = new AsyncCallback(OnConnect);
                    socket.BeginConnect(endpoint, event_connect, socket);
                }
                else
                {
                    Console.WriteLine("Unable to connect to web server (address:" + url + ")");
                    Disconnect();
                }

            }
            catch (SocketException sex)
            {
                if (sex.ErrorCode == 11001) //TODO i know , or correctly i dont know ...
                {
                    Console.WriteLine("Error during Address resolve of web server (address:" + url + ")");
                    Disconnect();
                }
                else
                {
                    Console.WriteLine("Error during Address resolve of web server (address:" + url + "): "+sex.Message);
                    Disconnect();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during Address resolve of web server (address:" + url + "): "+ex.Message);
                Disconnect();
            }
        }
        private void OnConnect(IAsyncResult result)
        {
            Socket connect_socket = (Socket)result.AsyncState;
            try
            {
                if (connect_socket.Connected)
                {
                    disconnected = false;
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_buffer = new byte[32768];
                    connect_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, connect_socket);
                    SendRequest();
                    //Console.WriteLine("Successfully connected to web server: " + url);
                }
                else
                {
                    Console.WriteLine("Error during connect to url: " + url);
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during connect to url: " + url + "(exception:" + ex.Message + ").");
                Disconnect();
            }
        }
        private void OnReceive(IAsyncResult result)
        {
            if (disconnected) 
                return;
            try
            {
                if (socket == null) return;
                Socket receive_socket = (Socket)result.AsyncState;
                if (receive_socket.Connected)
                {
                    int received_bytes = receive_socket.EndReceive(result);
                    if (received_bytes > 0)
                    {
                        string received_string = System.Text.Encoding.Default.GetString(receive_buffer, 0, received_bytes);
                        //Console.WriteLine("Received this from a client: " + received_string);
                        reply_string += received_string;
                        int header_end = reply_string.IndexOf("\r\n\r\n");
                        if (header_end != -1)
                        {
                            string header_string = reply_string.Substring(0, header_end);
                         
                 //string body = "<html><head><title>302 Moved Temporarily</title></head><body><h1>302 Moved Temporarily</h1><p>The resource has been moved temporarily;If the client doesn't load the resource in a few seconds ,you may find it <a href=\""+url+"\">here</a>.</p></body></html>";
                //string header = MiniWebServer.HttpVersion + " 302 Moved Temporarily\r\nLocation: " + url + "\r\nContent-type: text/html\r\nContent-Length: " + body.Length + "\r\n\r\n";//Connection: Close\r\n

                            if (header_string.StartsWith("HTTP/1.1 302") || header_string.StartsWith("HTTP/1.0 302") || header_string.StartsWith("HTTP/1.1 301") || header_string.StartsWith("HTTP/1.0 301"))
                            {
                                string[] seps = { "\r\n" };
                                string[] lines = header_string.Split(seps, StringSplitOptions.None);
                                foreach (string line in lines)
                                {
                                    if (line.StartsWith("Location:", true, null))
                                    {
                                        relocated_url = line.Substring("Location:".Length).Trim();
                                    }
                                    if (line.StartsWith("Content-Type:", true, null))
                                    {
                                        mime_type = line.Substring("Content-Type:".Length).Trim();
                                    }
                                }
                                Disconnect();
                                if (relocated_url != "")
                                {
                                    if (FoundRelocatedUrl != null)
                                        FoundRelocatedUrl(this);
                                }
                                else if (mime_type != "")
                                {
                                    if (FoundMimeType != null)
                                        FoundMimeType(this);
                                }
                            }
                            if (relocated_url != "" || mime_type != "")
                                Console.WriteLine("Found this relocated url: " + relocated_url + "\nThis mime-type: " + mime_type);
                            //else Console.WriteLine("no relocation or no mime type detected.");
                            return;
                        }

                        AsyncCallback event_receive = new AsyncCallback(OnReceive);
                        receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                    }
                    else
                    {
                        Console.WriteLine("Connection to web server dropped.");
                    }
                }
                else Console.WriteLine("web server not connected.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during receive of data from web server: " + ex.Message);
            }
        }
        /// <summary>
        /// the url relocation check constructor
        /// </summary>
        /// <param name="check_url">the url to check for relocation</param>
        public UrlRelocationCheck(string check_url)
        {
            url = check_url;
        }
        /// <summary>
        /// the url relocation check constructor
        /// </summary>
        public UrlRelocationCheck()
        {

        }
        /// <summary>
        /// the url relocation check constructor
        /// </summary>
        /// <param name="check_url">the url to check for relocation</param>
        /// <param name="handler">the handler to call when a relocation was determined</param>
        public UrlRelocationCheck(string check_url, FoundRelocatedUrlEventHandler handler)
        {
            url = check_url;
            FoundRelocatedUrl += handler;
            CheckUrl();
        }

    }
}
