using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using NUnit.Framework;
using System.Threading;
using System.Net;
using System.IO;


namespace DCPlusPlus
{
    /// <summary>
    /// a very simple webserver
    /// </summary>
    [TestFixture]
    public class MiniWebServer
    {
        /// <summary>
        /// a class to interact with a connecected web client
        /// </summary>
        public class Client
        {
            private Socket socket = null;
            private byte[] receive_buffer = new byte[8192];
            private bool disconnected = false;
            private object disconnected_lock = new object();
            public void StartReceiving()
            {
                try
                {
                    if (socket.Connected)
                    {
                        AsyncCallback event_receive = new AsyncCallback(OnReceive);
                        //receive_buffer = new byte[32768];
                        //Console.WriteLine("Starting receiving of data.");
                        socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, socket);
                    }
                    else
                    {
                        Console.WriteLine("Connection to web client aborted.");
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error during starting of receiving data from web client: " + ex.Message);
                }

            }

            private string request_string = "";

            public string RequestString
            {
                get { return request_string; }
                set { request_string = value; }
            }

            public void Disconnect()
            {
                lock (disconnected_lock)
                {
                    disconnected = true;
                    socket.ReceiveTimeout = 0;
                    socket.Close();
                    socket = null;
                }
            }

            
            private void OnReceive(IAsyncResult result)
            {
                lock (disconnected_lock)
                {
                    if (disconnected) return;
                }

                try
                {
                    if (socket == null) return;
                    Socket receive_socket = (Socket)result.AsyncState;
                    //Console.WriteLine("Connection socket.");
                    if (receive_socket.Connected)
                    {
                        int received_bytes = receive_socket.EndReceive(result);
                        //Console.WriteLine("Received " + received_bytes + " bytes so far.");
                        if (received_bytes > 0)
                        {
                            string received_string = System.Text.Encoding.Default.GetString(receive_buffer, 0, received_bytes);
                            //Console.WriteLine("Received this from a client: " + received_string);
                            request_string +=received_string;
                            if (received_string.IndexOf("\r\n\r\n")!=-1)
                            {//header finished TODO add timeout and a better detection for a complete header 

                                Request request = new Request(request_string,this);
                                if (request.IsValid)
                                {
                                    if (RequestReceived != null)
                                        RequestReceived(this, request);
                                }
                                else Console.WriteLine("Received an invalid request.");
                            }
                            AsyncCallback event_receive = new AsyncCallback(OnReceive);
                            receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                        }
                        else
                        {
                            Console.WriteLine("Connection to web client dropped.");
                        }
                    }
                    else Console.WriteLine("web client not connected.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception during receive of data: " + ex.Message);
                }
            }

            public void TellNotFound()
            {
                /*HTTP/1.1 200 OK
                SERVER: Ambit OS/1.0 UPnP/1.0 AMBIT-UPNP/1.0
                EXT:
                LOCATION: http://192.168.0.1:80/Public_UPNP_gatedesc.xml
                CACHE-CONTROL: max-age=3600
                ST: upnp:rootdevice
                USN: uuid:c5d399f6-750e-a039-334c-7af0d49396f3::upnp:rootdevice
                */
                Answer("HTTP/1.1 404 Not Found\r\nConnection: Close\r\n\r\n");
            }

            public void Answer(byte[] content, string type)
            {
                if (socket != null)
                {
                    if (!socket.Connected) return;
                    try
                    {
                        string header_string = "HTTP/1.1 200 OK\r\nSERVER: MiniWebServer\r\nContent-Type: "+type+"\r\nContent-Length: " + content.Length + "\r\nConnection: Close\r\n\r\n";
                        byte[] header = System.Text.Encoding.Default.GetBytes(header_string);
                        byte[] send_bytes = new byte[header.Length + content.Length];
                        header.CopyTo(send_bytes, 0);
                        content.CopyTo(send_bytes, header.Length);

                        socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(AnswerCallback), socket);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error sending answer to web client: " + e.Message);
                    }
                }

            }

            public void Answer(string content, string type)
            {
                Answer("HTTP/1.1 200 OK\r\nSERVER: MiniWebServer\r\nContent-Type: " + type + "\r\nContent-Length: " + content.Length + "\r\nConnection: Close\r\n\r\n" + content);
            }


            public void Answer(string answer)
            {
                if (socket != null)
                {
                    if (!socket.Connected) return;
                    try
                    {
                        byte[] send_bytes = System.Text.Encoding.Default.GetBytes(answer);
                        socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(AnswerCallback), socket);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error sending answer to web client: " + e.Message);
                    }
                }

            }
            //TODO add event to let the system know the request was finished

            /// <summary>
            /// Async Callback for the Answer
            /// (gets called when the send is completed)
            /// </summary>
            /// <param name="ar">Async Result/State </param>
            protected void AnswerCallback(IAsyncResult ar)
            {
                Socket answer_socket = (Socket)ar.AsyncState;
                try
                {
                    int bytes = answer_socket.EndSend(ar);
                    //Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("exception during send of answer: " + ex.Message);
                }
            }

            public delegate void RequestReceivedEventHandler(Client client,Request request);
            public event RequestReceivedEventHandler RequestReceived;

            public Client(Socket client_socket)
            {
                this.socket = client_socket;
                //this.socket.Blocking = false;
            }

        }
        /// <summary>
        /// a class that represents all important fields of a web request
        /// and a link to the originating web client
        /// </summary>
        public class Request
        {
            private string url;

            public string Url
            {
                get { return url; }
                set { url = value; }
            }

            private string method;

            public string Method
            {
                get { return method; }
                set { method = value; }
            }

            private string version;

            public string Version
            {
                get { return version; }
                set { version = value; }
            }
	
            private bool is_valid=true;

            public bool IsValid
            {
                get { return is_valid; }
                set { is_valid = value; }
            }

            private Client request_client=null;

            public Client RequestClient
            {
                get { return request_client; }
            }

	
            private WebHeaderCollection headers= new WebHeaderCollection();

            public WebHeaderCollection Headers
            {
                get { return headers; }
                set { headers = value; }
            }
	
	
            public Request(string request_string,Client request_client)
            {
                if (request_client == null) 
                    is_valid = false;
                this.request_client = request_client;
                //Console.WriteLine("Initiating Request from request string: "+request_string);
                string[] seps = {"\r\n"};
                string[] header_lines = request_string.Split(seps,StringSplitOptions.RemoveEmptyEntries);
                if (header_lines.Length > 0)
                {
                    string[] seps2 ={ " " };
                    string[] request_line_parts = header_lines[0].Split(seps2, StringSplitOptions.RemoveEmptyEntries);
                    if (request_line_parts.Length >= 3)
                    {
                        method = request_line_parts[0];
                        url = request_line_parts[1];
                        version = request_line_parts[2];

                        int i = 0;
                        foreach (string line in header_lines) //i know i should skip the first line , but this is so convenient
                        {//read header values into our headers list
                            if (line == "\r\n\r\n")
                                break;
                            if (i++ != 0)
                            {//skipping the first line which is not a header
                                int colon_pos = line.IndexOf(":");
                                if (colon_pos != -1)
                                {
                                    headers.Add(line.Substring(0, colon_pos), line.Substring(colon_pos + 1).Trim());
                                }
                                else
                                {
                                    Console.WriteLine("invalid header line without a colon");
                                    is_valid = false;
                                }

                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("first request line had not enough fields");
                        is_valid = false;
                    }
                }
                else
                {
                    Console.WriteLine("header is empty");
                    is_valid = false;
                }
            }
        }

        public delegate void RequestReceivedEventHandler(MiniWebServer server, Request request);
        public event RequestReceivedEventHandler RequestReceived;

        private int port;

        public int Port
        {
            get { return port; }
            set { port = value; }
        }
        protected bool listening = false;
        /// <summary>
        /// TRUE if we bound our local socket and we are waiting for connections
        /// </summary>
        public bool IsListening
        {
            get
            {
                return (listening);
            }
        }
        protected int max_tcp_connections = 100;
        /// <summary>
        /// Maximum number of tcp connections in listening queue before discarding them
        /// </summary>
        public int MaxTcpConnections
        {
            get
            {
                return (max_tcp_connections);
            }
            set
            {
                max_tcp_connections = value;
            }
        }

        private Socket socket = null;
        /// <summary>
        /// lock used to make this class thread safe
        /// </summary>
        private object listening_lock = new Object();
        /// <summary>
        /// Updates the sockets,
        /// needs to be called
        /// after the port has been changed
        /// </summary>
        public void UpdateConnectionSettings()
        {
            if (listening)
                CloseListeningSocket();
            SetupListeningSocket();
        }

        public void SetupListeningSocket()
        {
            //if ip is nullorempty
            //if ports == 0
            //determine local ip address
            //select random ports
            //
            // setup socket accordingly
            lock (listening_lock)
            {
                if (!listening)
                {
                    listening = true;
                    if (socket == null)
                    {
                        try
                        {
                            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            IPEndPoint tcp_local_endpoint = new IPEndPoint(IPAddress.Any, port);
                            socket.Bind(tcp_local_endpoint);
                            port = ((IPEndPoint)socket.LocalEndPoint).Port;
                            socket.Blocking = false;
                            //tcp_socket.LingerState = new LingerOption(false, 0);
                            socket.Listen(max_tcp_connections);
                            AsyncCallback event_accept = new AsyncCallback(OnAccept);
                            socket.BeginAccept(event_accept, socket);
                            Console.WriteLine("Bound listening tcp socket to port: " + port);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception opening local tcp port:" + ex.Message);
                        }
                    }
                    else Console.WriteLine("tcp port already in use :" + port);
                }
            }
        }
        public void CloseListeningSocket()
        {
            //close the listening socket if openened
            lock (listening_lock)
            {
                if (listening)
                {
                    listening = false;
                    try
                    {
                        if (socket != null)
                        {
                            //int temp_timeout = tcp_socket.ReceiveTimeout;
                            //tcp_socket.Shutdown(SocketShutdown.Both);
                            //tcp_socket
                            socket.ReceiveTimeout = 0;
                            socket.Close();
                            //Thread.Sleep(10);
                            socket = null;
                            Thread.Sleep(10);
                            Console.WriteLine("Closed Listening tcp socket.");
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error closing listening socket: " + ex.Message);
                    }
                }
            }

        }
        public void Close()
        {
            CloseListeningSocket();
        }

        private List<Client> clients = new List<Client>();

        /// <summary>
        /// Callback to accept tcp connections
        /// </summary>
        /// <param name="result">Async Result/State</param>
        private void OnAccept(IAsyncResult result)
        {
            if (!IsListening) return;
            if (socket != null)
            {
                if (!socket.IsBound) return;
                if (!result.IsCompleted) return;
                try
                {
                    if (socket != ((Socket)result.AsyncState)) return;
                    Socket accept_socket = (Socket)result.AsyncState;
                    if (accept_socket == null) return;
                    if (!accept_socket.IsBound) return;
                    Socket client_socket = socket.EndAccept(result);
                    if (!client_socket.Connected) return;
                    Console.WriteLine("new client connected.");
                    Client client = new Client(client_socket);
                    client.RequestReceived += delegate(Client requester, Request request)
                    {
                        if (RequestReceived != null)
                            RequestReceived(this, request);
                    };
                    clients.Add(client);
                    client.StartReceiving();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error accepting connection: " + ex.Message);
                }
                try
                {
                    AsyncCallback event_accept = new AsyncCallback(OnAccept);
                    socket.BeginAccept(event_accept, socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fatal Error accepting connection: " + ex.Message);
                }


            }
            else Console.WriteLine("Accept on tcp socket aborted.");
        }


        public MiniWebServer()
        {

        }

        #region Unit Testing
        /// <summary>
        /// Test to see if opening and closing the mini web server works as expected
        /// </summary>
        [Test]
        public void TestMiniWebServerOpenClose()
        {
            Console.WriteLine("Test to Open/Close the Mini Web Server.");
            MiniWebServer server = new MiniWebServer();
            server.SetupListeningSocket();
            server.CloseListeningSocket();
            Console.WriteLine("Mini Web Server Open/Close Test successful.");

        }
        /// <summary>
        /// Test to see if the mini web server acts expected when a request was received
        /// (this test needs your help by browsing to http://your-ip/
        /// ,the test will end after 2 minutes)
        /// </summary>
        [Test]
        public void TestMiniWebServerRequest()
        {
            Console.WriteLine("Test to see if requesting works.");
            MiniWebServer server = new MiniWebServer();
            server.Port = 80;
            server.SetupListeningSocket();
  
            bool wait = true;
            server.RequestReceived += delegate(MiniWebServer request_server,MiniWebServer.Request request)
            {
                Console.WriteLine("Request received: ");
                Console.WriteLine("URL: "+request.Url);
                Console.WriteLine("Method: "+request.Method);
                Console.WriteLine("Version: "+request.Version);
                Console.WriteLine("Headers:");
                foreach (string key in request.Headers.Keys)
                {
                    Console.WriteLine("["+key+"]"+":["+request.Headers.Get(key)+"]");
                }
                //request.RequestClient.TellNotFound();
                
                //request.RequestClient.Answer("HTTP/1.0 302 Moved Temporarily\r\nSERVER: MiniWebServer\r\nLOCATION: http://192.168.0.1:80/Public_UPNP_gatedesc.xml\r\n\r\n");
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

                    //request.RequestClient.Answer("HTTP/1.1 200 OK\r\nSERVER: MiniWebServer\r\nContent-Type: text/plain\r\nContent-Length: " + page.Length + "\r\nConnection: Close\r\n\r\n" + page);

                //Thread.Sleep(300);
                //request.RequestClient.Disconnect();
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                //wait = false;
            };
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0,120))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    Assert.Fail("Operation took too long");
                    wait = false;
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("");
            server.CloseListeningSocket();
            Console.WriteLine("Mini Web Server Request Test successful.");
 
        }
        /// <summary>
        /// Test to see if the mini web server acts expected when a request was received
        /// (running automatically)
        /// </summary>
        [Test]
        public void TestMiniWebServerAutoRequest()
        {
            Console.WriteLine("Test to see if requesting works.");
            MiniWebServer server = new MiniWebServer();
            server.Port = 80;
            server.SetupListeningSocket();
            int page_len = 0;
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
                //request.RequestClient.TellNotFound();

                //request.RequestClient.Answer("HTTP/1.0 302 Moved Temporarily\r\nSERVER: MiniWebServer\r\nLOCATION: http://192.168.0.1:80/Public_UPNP_gatedesc.xml\r\n\r\n");
                if (request.Url == "/")
                {
                    string page = "";
                    //string type = "text/plain";
                    page = "<html>\n<head>\n<title>MiniWebServer Test Page</title>\n</head>\n<body bgcolor=\"#333355\">Test Page of the Miniwebserver running on port: " + server.Port + "<br><a href=\"/test.mp3\">Test Mp3</a></body>\n</html>\n";
                    string type = "text/html";
                    page_len = page.Length;
                    request.RequestClient.Answer(page, type);
                }
                else if (request.Url == "/test.mp3")
                {
                    byte[] mp3 = File.ReadAllBytes("..\\..\\..\\TestDateien\\test.mp3");
                    string type = "audio/mpeg";
                    page_len = mp3.Length;
                    request.RequestClient.Answer(mp3, type);
                }

                //request.RequestClient.Answer("HTTP/1.1 200 OK\r\nSERVER: MiniWebServer\r\nContent-Type: text/plain\r\nContent-Length: " + page.Length + "\r\nConnection: Close\r\n\r\n" + page);

                //Thread.Sleep(300);
                //request.RequestClient.Disconnect();
                //Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                //wait = false;
            };

            WebClient wc = new WebClient();
            wc.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs e)
            {
                if(!e.Cancelled)
                {
                    Assert.IsTrue(page_len == e.Result.Length, "Test failed: received an incomplete page");
                    wait = false;
                }
            };
            wc.DownloadDataAsync(new Uri("http://127.0.0.1/test.mp3"));

            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 120))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    Assert.Fail("Operation took too long");
                    wait = false;
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("");
            server.CloseListeningSocket();
            Console.WriteLine("Mini Web Server Request Test successful.");

        }

        #endregion

    }
}
