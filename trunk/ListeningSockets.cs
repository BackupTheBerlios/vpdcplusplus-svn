using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;

namespace DCPlusPlus
{
    /// <summary>
    /// creates a local bound udp socket
    /// and tcp socket to accept connections/packets
    /// fires events upon a connected peer
    /// or a search result received via udp
    /// </summary>
    [TestFixture]
    public class ListeningSockets
    {
        /// <summary>
        /// Prototype for the Search Result Received Event Handler
        /// </summary>
        /// <param name="result">the search result received via udp</param>
        public delegate void SearchResultReceivedEventHandler(SearchResults.SearchResult result);
        /// <summary>
        /// Event handler that gets called
        /// when a search result was received via udp
        /// </summary>
        public event SearchResultReceivedEventHandler SearchResultReceived;
        /// <summary>
        /// Event handler that gets called
        /// when a peer connected to our local tcp listener
        /// </summary>
        public event Peer.ConnectedEventHandler PeerConnected;
        protected string ip = "";
        /// <summary>
        /// the internal ip address
        /// </summary>
        public string IP
        {
            get
            {
                return (ip);
            }
        }
        protected string external_ip = "";
        /// <summary>
        /// the external ip address
        /// (TODO move this to a more suitable place like the Client class)
        /// </summary>
        public string ExternalIP
        {
            get
            {
                return (external_ip);
            }
            set
            {
                external_ip = value;
            }
        }
        protected int tcp_port = 0;//TODO change this to a certain port as default,else we may clutter up a routers upnp port mappings
        /// <summary>
        /// the tcp port we want to use 
        /// set to 0 for automatic free port selection of the os
        /// </summary>
        public int TcpPort
        {
            get
            {
                return (tcp_port);
            }
            set
            {
                tcp_port = value;
            }
        }
        protected int udp_port = 0;
        /// <summary>
        /// the udp port we want to use 
        /// set to 0 for automatic free port selection of the os
        /// </summary>
        public int UdpPort
        {
            get
            {
                return (udp_port);
            }
            set
            {
                udp_port = value;
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
        /* this is one for the books lol a max connections int for a connectionless protocol
         * think first before writing ;-)
        protected int max_udp_connections = 100;
        /// <summary>
        /// 
        /// </summary>
        public int MaxUdpConnections
        {
            get
            {
                return (max_udp_connections);
            }
            set
            {
                max_udp_connections = value;
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        public int MaxConnections
        {
            get
            {
                return (max_tcp_connections + max_udp_connections);
            }
            set
            {
                max_udp_connections = value / 2;
                max_tcp_connections = value / 2;
            }
        }*/
        /// <summary>
        /// ListeningSockets Constructor
        /// (Gets the local ip and sets up 
        /// the local tcp and udp socket)
        /// </summary>
        public ListeningSockets()
        {
            UpdateIP();
            //SetupListeningSocket();//TODO MAYBE REACTIVATE ... decide it pez !! ;-)
        }
        /// <summary>
        /// Updates the local ip address
        /// stores it in the IP property
        /// </summary>
        private void UpdateIP()
        {
            string host_name = Dns.GetHostName();
            IPHostEntry host_entry = Dns.GetHostEntry(host_name);
            if (host_entry.AddressList.Length == 0) return;//computer has not one network interface ;-( i bet this one will never a case anywhere, but better catch it *g*
            ip = host_entry.AddressList[0].ToString();
        }
        protected bool listening = false;
        /// <summary>
        /// TRUE if we bound our local sockets and we are listening for packets/connections
        /// </summary>
        public bool IsListening
        {
            get
            {
                return (listening);
            }
        }
        /// <summary>
        /// the tcp_socket we bind to our local port
        /// specified with TcpPort
        /// </summary>
        private Socket tcp_socket = null;
        /// <summary>
        /// the result of socket.BeginAccept
        /// [unused at the moment]
        /// </summary>
        private IAsyncResult tcp_callback = null;
        /// <summary>
        /// the udp_socket we bind to our local port
        /// specified with UdpPort
        /// </summary>
        private Socket udp_socket = null;
        /// <summary>
        /// the udp sockets receive buffer
        /// </summary>
        private byte[] receive_from_buffer = new byte[1024];
        /// <summary>
        /// stores the ip address information of a received packet
        /// [unused]
        /// </summary>
        private IPEndPoint receive_from_endpoint = new IPEndPoint(IPAddress.None, 0);
        /// <summary>
        /// Updates the sockets,
        /// needs to be called
        /// after the ports have changed
        /// </summary>
        public void UpdateConnectionSettings()
        {
            if (listening)
                CloseListeningSocket();
            SetupListeningSocket();
        }
        /// <summary>
        /// 
        /// TODO, heard some rumors that deconstructors are not supported
        /// </summary>
        ~ListeningSockets()
        {
            CloseListeningSocket();
        }
        /// <summary>
        /// Just another name for CloseListeningSockets()
        /// </summary>
        public void Close()
        {
            CloseListeningSocket();
        }
        /// <summary>
        /// Close the udp and tcp socket
        /// and stop listening for packets/connections
        /// </summary>
        private void CloseListeningSocket()
        {
            //close the listening socket if openened
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
                            Thread.Sleep(10);
                            Console.WriteLine("Closed Listening udp socket.");
                        }
                        if (tcp_socket != null)
                        {
                            //int temp_timeout = tcp_socket.ReceiveTimeout;
                            //tcp_socket.Shutdown(SocketShutdown.Both);
                            //tcp_socket
                            tcp_socket.ReceiveTimeout = 0;
                            tcp_socket.Close();
                            //Thread.Sleep(10);
                            tcp_socket = null;
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
        /// <summary>
        /// lock used to make this class thread safe
        /// </summary>
        private object listening_lock = new Object();
        /// <summary>
        /// Open the tcp and udp ports
        /// and start listen for packets and connections
        /// </summary>
        private void SetupListeningSocket()
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
                    if (tcp_socket == null)
                    {
                        try
                        {
                            tcp_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                            IPEndPoint tcp_local_endpoint = new IPEndPoint(IPAddress.Any, tcp_port);
                            tcp_socket.Bind(tcp_local_endpoint);
                            tcp_port = ((IPEndPoint)tcp_socket.LocalEndPoint).Port;
                            tcp_socket.Blocking = false;
                            //tcp_socket.LingerState = new LingerOption(false, 0);
                            tcp_socket.Listen(max_tcp_connections);
                            AsyncCallback event_accept = new AsyncCallback(OnAccept);
                            tcp_callback = tcp_socket.BeginAccept(event_accept, tcp_socket);
                            Console.WriteLine("Bound listening tcp socket to port: " + tcp_port);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception opening local peer tcp port:"+ex.Message);
                        }
                    }
                    else Console.WriteLine("tcp port already in use :" + tcp_port);
                    if (udp_socket == null)
                    {
                        try
                        {
                            udp_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                            IPEndPoint udp_local_endpoint = new IPEndPoint(IPAddress.Any, udp_port);
                            udp_socket.Bind(udp_local_endpoint);
                            udp_port = ((IPEndPoint)udp_socket.LocalEndPoint).Port;
                            udp_socket.Blocking = false;
                            //udp_socket.LingerState = new LingerOption(false, 0);
                            EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                            AsyncCallback event_receive_from = new AsyncCallback(OnReceiveFrom);
                            udp_socket.BeginReceiveFrom(receive_from_buffer, 0, receive_from_buffer.Length, SocketFlags.None, ref temp_receive_from_endpoint, event_receive_from, udp_socket);
                            Console.WriteLine("Bound UDP-Channel to port: " + udp_port);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception opening local peer udp port:"+ex.Message);
                        }

                    }
                    else Console.WriteLine("udp port already in use :" + udp_port);
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
            if (udp_socket != null)
            {
                if (!udp_socket.IsBound) return;
                try
                {
                    if (udp_socket != ((Socket)result.AsyncState)) return;
                    Socket receive_from_socket = (Socket)result.AsyncState;
                    if (receive_from_socket == null) return;
                    //if (!receive_from_socket.IsBound) return;
                    //Socket receive_from_socket = (Socket)result;
                    //Console.WriteLine("udp packet received.");
                    EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                    //Console.WriteLine("udp packet end start.");
                    int received_bytes = udp_socket.EndReceiveFrom(result, ref temp_receive_from_endpoint);
                    //Console.WriteLine("udp packet end end.");

                    if (received_bytes > 0)
                    {
                        //string received_string = Encoding.ASCII.GetString(receive_from_buffer, 0, received_bytes);
                        string received_string = System.Text.Encoding.Default.GetString(receive_from_buffer, 0, received_bytes);
                        //Console.WriteLine("received data in packet: " + received_string);
                        InterpretReceivedString(received_string);
                    }
                    else Console.WriteLine("Empty packet received");

                    //Console.WriteLine("udp packet begin start.");
                    //Console.WriteLine("udp packet begin end.");
                }
                //catch (ObjectDisposedException oex)
                //{
                //}
                catch (Exception ex)
                {
                    Console.WriteLine("Error in ReceiveFrom callback: " + ex.Message);
                }
                try
                {
                    EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
                    AsyncCallback event_receive_from = new AsyncCallback(OnReceiveFrom);
                    udp_socket.BeginReceiveFrom(receive_from_buffer, 0, receive_from_buffer.Length, SocketFlags.None, ref temp_receive_from_endpoint, event_receive_from, udp_socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fatal Error in ReceiveFrom callback: " + ex.Message);
                }

            
            }
            else Console.WriteLine("ReceiveFrom on udp socket aborted.");
        }
        /// <summary>
        /// Interpret a received string.
        /// this will split the string into single commands and
        /// call InterpretCommand() with them
        /// (TODO maybe add a received string buffer to fight incomplete packets
        ///  ,but its complicated . it would need a buffer for each ip:port combo,not very practical)
        /// </summary>
        /// <param name="received_string">the data received via udp</param>
        private void InterpretReceivedString(string received_string)
        {
            // possible strings
            //$command|
            //<chat>
            //| 
            string[] received_strings = received_string.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < received_strings.Length; i++)
            {
                //if (received_strings[i].StartsWith("<")) Console.WriteLine("chat message on hub: " + name + " - " + received_strings[i]);
                if (received_strings[i].StartsWith("$")) InterpretCommand(received_strings[i]);
                else Console.WriteLine("Received a non command line: " + received_strings[i]);
            }
         
        }
        /// <summary>
        /// Interpret a single command 
        /// in this case only $SR search results received via udp
        /// </summary>
        /// <param name="received_command">the command to interpret</param>
        private void InterpretCommand(string received_command)
        {
            int command_end = received_command.IndexOf(" ");
            if (command_end != -1)
            {
                string command = received_command.Substring(1, command_end - 1);
                string parameter = received_command.Substring(command_end + 1);
                string[] parameters = parameter.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                //Console.WriteLine("Command: '" + command + "' ,Parameter("+parameters.Length+"): '" + parameter + "'");

                switch (command)
                {
                    case "SR":
                        //Console.WriteLine("Search result received: " + parameter);
                        SearchResults.SearchResult result = new SearchResults.SearchResult();
                        result.ResultLine = parameter;
                        try
                        {
                            if (SearchResultReceived != null)
                                SearchResultReceived( result);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception in event handler: " + ex.Message);
                        }

                        break;


                    default:
                        Console.WriteLine("Unknown Command received: " + command + ", Parameter: " + parameter);
                        break;
                }
            }
            else Console.WriteLine("Error interpreting command: " + received_command);
        }
        /// <summary>
        /// Callback to accept tcp connections
        /// </summary>
        /// <param name="result">Async Result/State</param>
        private void OnAccept(IAsyncResult result)
        {
            if (!IsListening) return;
            if (tcp_socket != null)
            {
                //if(
                if (!tcp_socket.IsBound) return;
                if (!result.IsCompleted) return;
                try
                {

                    if (tcp_socket != ((Socket)result.AsyncState)) return;
                    Socket accept_socket = (Socket)result.AsyncState;
                    if (accept_socket == null) return;
                    if (!accept_socket.IsBound) return;
                    //if (!accept_socket.Connected) return;
                    //Console.WriteLine("trying end accept.");
                    //accept_socket.a
                    Socket client = tcp_socket.EndAccept(result);
                    if (!client.Connected) return;
                    //Console.WriteLine("new client connected.");
                    Peer new_peer = new Peer(client);
                    try
                    {
                        if (PeerConnected != null)
                        {
                            PeerConnected(new_peer);
                        }
                    
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception in Peer Connected Event: " + ex.Message);
                    }
                            
                    //accept_socket.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error accepting connection: " + ex.Message);
                }
                try
                {
                    AsyncCallback event_accept = new AsyncCallback(OnAccept);
                    tcp_socket.BeginAccept(event_accept, tcp_socket);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Fatal Error accepting connection: " + ex.Message);
                }


            }
            else Console.WriteLine("Accept on tcp socket aborted.");
        }
        /// <summary>
        /// Reply to a search from a user via udp
        /// (active connection of peer user required)
        /// </summary>
        /// <param name="result_name">the filename of the share found</param>
        /// <param name="filesize">the filesize of the share</param>
        /// <param name="hub">the hub the user is connected to</param>
        /// <param name="search">a whole lot of parameters of the search initiated by a remote user (including ip and port,which we will need here)</param>
        public void SearchReply(string result_name, long filesize, Hub hub, Hub.SearchParameters search)
        {
            try
            {
            string temp_hub = hub.Name;
            if (search.HasTTH) temp_hub = "TTH:" + search.tth;
            string reply = "$SR " + hub.Nick + " " + result_name + (char)0x05 + filesize + " 1/1" + (char)0x05 + temp_hub + " (" + hub.IP + ":" + hub.Port + ")|";
            Console.WriteLine("Replying to active search: " + reply);
            IPEndPoint udp_reply_endpoint = new IPEndPoint(IPAddress.Parse(search.ip), search.port);
            //EndPoint temp_receive_from_endpoint = (EndPoint)receive_from_endpoint;
            
            byte[] send_bytes = System.Text.Encoding.Default.GetBytes(reply);
            udp_socket.BeginSendTo(send_bytes, 0, send_bytes.Length, SocketFlags.None,udp_reply_endpoint, new AsyncCallback(SearchReplyCallback), udp_socket);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Exception during sending of SearchReply to: "+search.ip+":"+search.port+" : "+ex.Message);
            }
        }
        /// <summary>
        /// Callback for SearchReply async send
        /// </summary>
        /// <param name="ar">Async Result/State</param>
        protected void SearchReplyCallback(IAsyncResult ar)
        {
            Socket search_reply_socket = (Socket)ar.AsyncState;
            try
            {
                int bytes_sent = search_reply_socket.EndSend(ar);

            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during sending of SearchReply: " + ex.Message);
            }
        }
        #region Unit Testing
        /// <summary>
        /// Test to see if opening and closing of sockets works
        /// </summary>
        [Test]
        public void TestOpenClose()
        {
            Console.WriteLine("Test to open and close listening sockets.");
            ListeningSockets ls = new ListeningSockets();
            ls.SetupListeningSocket();
            ls.CloseListeningSocket();
            Console.WriteLine("Opening and Closing Sockets Test successful.");

            /*bool wait = true;
            u.RouterDiscovered += delegate(Router r)
            {
                wait = false;
            };
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
            }*/
        }
        /// <summary>
        /// Test to see if opening and NOT closing of sockets works
        /// </summary>
        [Test]
        public void TestOpenWithoutClosing()
        {
            Console.WriteLine("Test to open and NOT close listening sockets.");
            ListeningSockets ls = new ListeningSockets();
            ls.SetupListeningSocket();
            Console.WriteLine("Opening and NOT Closing Sockets Test successful.");
        }
        /// <summary>
        /// Test to see if opening and NOT closing of sockets works using a defined port pair
        /// </summary>
        [Test]
        public void TestOpenWithoutClosingAndPortsSpecified()
        {
            Console.WriteLine("Test to open and NOT close listening sockets(using specified ports).");
            ListeningSockets ls = new ListeningSockets();
            ls.UdpPort = 5000;
            ls.TcpPort = 5000;
            ls.SetupListeningSocket();
            Console.WriteLine("Opening and NOT Closing Sockets Test successful.");
        }
        #endregion
    }
}
