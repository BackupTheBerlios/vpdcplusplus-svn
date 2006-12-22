using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DCPlusPlus
{
    public class ListeningSockets
    {
        public delegate void SearchResultEventHandler(object sender, SearchResults.SearchResult result);

        public event SearchResultEventHandler SearchResultReceived;

        public delegate bool PeerConnectedEventHandler(object sender, Peer peer);

        public event PeerConnectedEventHandler PeerConnected;

        protected string ip = "";
        public string IP
        {
            get
            {
                return (ip);
            }
        }

        protected string external_ip = "";
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

        protected int tcp_port = 0;
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

        protected int max_tcp_connections = 10;
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

        protected int max_udp_connections = 10;
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
        }

        public ListeningSockets()
        {
            SetupListeningSocket();
        }

        ~ListeningSockets()
        {
            CloseListeningSocket();
        }

        protected bool listening = false;
        public bool IsListening
        {
            get
            {
                return (listening);
            }
        }

        private Socket tcp_socket = null;
        private IAsyncResult tcp_callback = null;

        private Socket udp_socket = null;
        private byte[] receive_from_buffer = new byte[1024];
        private IPEndPoint receive_from_endpoint = new IPEndPoint(IPAddress.None, 0);

        public void UpdateConnectionSettings()
        {
            if (listening)
                CloseListeningSocket();
            SetupListeningSocket();
      
        }


        public void Close()
        {
            CloseListeningSocket();
        }


        private void CloseListeningSocket()
        {
            //close the listening socket if openened
            if (listening)
            {
                listening = false;
                try
                {
                    if (udp_socket != null)
                    {
                        udp_socket.ReceiveTimeout = 0;
                        udp_socket.Shutdown(SocketShutdown.Both);
                        Thread.Sleep(10);
                        udp_socket.Close();
                        Thread.Sleep(10);
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
                        Thread.Sleep(10);
                        tcp_socket = null;
                        Thread.Sleep(10);
                        Console.WriteLine("Closed Listening tcp socket.");
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error closing listening socket: "+ex.Message);
                }
            }
        }

        private void SetupListeningSocket()
        {
            //if ip is nullorempty
            //if ports == 0
            //determine local ip address
            //select random ports
            //
            // setup socket accordingly

            if (!listening)
            {
                if (tcp_socket == null)
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
                else Console.WriteLine("tcp port already in use :" + tcp_port);
                if (udp_socket == null)
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
                else Console.WriteLine("udp port already in use :" + udp_port);

                listening = true;
            }
        }

        private void OnReceiveFrom(IAsyncResult result)
        {
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
                                SearchResultReceived(this, result);
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

        private void OnAccept(IAsyncResult result)
        {
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
                            if (!PeerConnected(this, new_peer)) client.Close();//if no slots avail just close connection
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
    }
}
