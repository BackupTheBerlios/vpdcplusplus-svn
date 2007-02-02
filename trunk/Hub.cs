using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.IO;
using NUnit.Framework;


/*
  TODO 
 * add OnCommandHandler which apps can overwrite and if they dont want to act on a certain command
 * it calls DefaultCommandHandler
 * 
 */

namespace DCPlusPlus
{
    [TestFixture]
    public class Hub : Connection
    {
        public delegate void SearchResultEventHandler(Hub hub, SearchResults.SearchResult result);
        public event SearchResultEventHandler SearchResultReceived;


        public class FileParameters
        {
            public string filename;
            public long filesize;
        }

        public class SearchParameters
        {
            public ConnectionMode mode;
            public string search_string;
            public bool size_restricted;
            public bool is_max_size;
            public long size;
            public SearchFileType file_type;
            public string ip;
            public int port;
            public string username;
            public bool HasTTH
            {
                get
                {
                    return (!string.IsNullOrEmpty(tth));
                }
            }
            public string tth = "";

            public SearchParameters()
            {
            }
            public SearchParameters(ConnectionMode mode, string search_string, bool size_restricted, bool is_max_size, int size, SearchFileType file_type,string tth, string ip, int port)
            {
                this.mode = ConnectionMode.Active;
                this.search_string = search_string;
                this.size_restricted = size_restricted;
                this.is_max_size = is_max_size;
                this.size = size;
                this.file_type = file_type;
                this.ip = ip;
                this.port = port;
                this.username = "";
                this.tth = tth;
            }
            public SearchParameters(string search_string, bool size_restricted, bool is_max_size, int size, SearchFileType file_type, string tth,string username)
            {
                this.mode = ConnectionMode.Passive;
                this.search_string = search_string;
                this.size_restricted = size_restricted;
                this.is_max_size = is_max_size;
                this.size = size;
                this.file_type = file_type;
                this.ip = "";
                this.port = 0;
                this.tth = tth;
                this.username = username;
            }

        }
        
        public delegate void SearchEventHandler(Hub hub,SearchParameters search);
        public event SearchEventHandler SearchReceived;

        public delegate void MainChatLineReceivedEventHandler(Hub hub, ChatLine line);
        public event MainChatLineReceivedEventHandler MainChatLineReceived;

        public delegate void UserQuitEventHandler(Hub hub, string username);
        public event UserQuitEventHandler UserQuit;

        public delegate void UserJoinedEventHandler(Hub hub, string username);
        public event UserJoinedEventHandler UserJoined;

        public delegate void LoggedInEventHandler(Hub hub);
        public event LoggedInEventHandler LoggedIn;

        public delegate void MoveForcedEventHandler(Hub src_hub, Hub dst_hub);
        public event MoveForcedEventHandler MoveForced;

        public delegate void ConnectToMeEventHandler(Hub hub, Peer connection);
        public event ConnectToMeEventHandler ConnectToMeReceived;

        public delegate void DisconnectedEventHandler(Hub hub);
        public event DisconnectedEventHandler Disconnected;

        public delegate void ConnectedEventHandler(Hub hub);
        public event ConnectedEventHandler Connected;

        public delegate void UnableToConnectEventHandler(Hub hub);
        public event UnableToConnectEventHandler UnableToConnect;

      
        protected string name = "";
        public string Name
        {
            get
            {
                return (name);
            }
            set
            {
                name = value;
            }
        }

        protected string address = "";
        public string Address
        {
            get
            {
                return (address);
            }
            set
            {
            
                //check if port is included in adress
                string tmp = value;
                int port_start = tmp.IndexOf(":");
                if (port_start != -1)
                {
                    int tmp_port = 411;
                    string tmp_port_string = tmp.Substring(port_start+1);
                    try
                    {
                        tmp_port = int.Parse(tmp_port_string);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("error parsing port : "+tmp_port_string);
                    }

                    tmp = tmp.Substring(0, port_start);
                    port = tmp_port;
                }
                
                address = tmp;
            }
        }

        protected string description = "";
        public string Description
        {
            get
            {
                return (description);
            }
            set
            {
                description = value;
            }
        }

        protected string country = "";
        public string Country
        {
            get
            {
                return (country);
            }
            set
            {
                country = value;
            }
        }

         protected long users = 0;
        public long Users
        {
            get
            {
                return (users);
            }
            set
            {
                users = value;
            }
        }

        protected long shared = 0;
        public long Shared
        {
            get
            {
                return (shared);
            }
            set
            {
                shared = value;
            }
        }

        protected long min_share = 0;
        public long MinShare
        {
            get
            {
                return (min_share);
            }
            set
            {
                min_share = value;
            }
        }

        protected int min_slots = 0;
        public int MinSlots
        {
            get
            {
                return (min_slots);
            }
            set
            {
                min_slots = value;
            }
        }

        protected int max_hubs = 0;
        public int MaxHubs
        {
            get
            {
                return (max_hubs);
            }
            set
            {
                max_hubs = value;
            }
        }

        protected long max_users = 0;
        public long MaxUsers
        {
            get
            {
                return (max_users);
            }
            set
            {
                max_users = value;
            }
        }


        //TODO if nick differs and already loggedin send validatenick again to change nickname

        protected string topic = "";
        public string Topic
        {
            get
            {
                return (topic);
            }

        }


        protected bool is_grabbed = false;
        public bool IsGrabbedByClient
        {
            get
            {
                return (is_grabbed);
            }
            set
            {
                is_grabbed = value;
            }
        }

        protected bool is_logged_in= false;
        public bool IsLoggedIn
        {
            get
            {
                return (is_logged_in);
            }

        }

        protected bool auto_reconnect = false;
        public bool AutoReconnect
        {
            get 
            {
                return (auto_reconnect);
            }
            set 
            {
                auto_reconnect = value;
            }
        }

        //TODO change to a userinfo holding class list instead of just the username


        protected int chat_history_max_length = 0;
        public int ChatHistoryMaxLength
        {
            get
            {
                return (chat_history_max_length);
            }
            set
            {
                chat_history_max_length = value;
            }
        }

        public class ChatLine
        {
            public string username = "unknown";
            public string message = "";
            public ChatLine(string username, string message)
            {
                this.username = username;
                this.message = message;
            }
        }


        protected List<ChatLine> chat_history = new List<ChatLine>();
        public List<ChatLine> ChatHistory
        {
            get
            {
                return (chat_history);
            }
            set
            {
                chat_history = value;
            }
        }


        protected List<string> user_list = new List<string>();
        public List<string> UserList
        {
            get
            {
                return (user_list);
            }
            set
            {
                user_list = value;
            }
        }

        protected List<string> op_list = new List<string>();
        public List<string> OperatorList
        {
            get
            {
                return (op_list);
            }
            set
            {
                op_list = value;
            }
        }

        protected string my_ip = "";
        public string MyIP
        {
            get
            {
                return (my_ip);
            }
            set
            {
                my_ip = value;
            }
        }

        protected string my_version = "1,0091";
        public string MyVersion
        {
            get
            {
                return (my_version);
            }
            set
            {
                my_version = value;
            }
        }

        protected string my_tag_version = "0.698";
        public string MyTagVersion
        {
            get
            {
                return (my_tag_version);
            }
            set
            {
                my_tag_version = value;
            }
        }

        protected string my_name = "c#++";
        public string MyName
        {
            get
            {
                return (my_name);
            }
            set
            {
                my_name = value;
            }
        }

        protected string my_email = "unknown@unknown.net";
        public string MyEmail
        {
            get
            {
                return (my_email);
            }
            set
            {
                my_email = value;
            }
        }

        protected long my_share_size = 0;
        public long MyShareSize
        {
            get
            {
                return (my_share_size);
            }
            set
            {
                my_share_size = value;
            }
        }

        protected int my_tcp_port = 0;
        public int MyTcpPort
        {
            get
            {
                return (my_tcp_port);
            }
            set
            {
                my_tcp_port = value;
            }
        }

        protected int my_udp_port = 0;
        public int MyUdpPort
        {
            get
            {
                return (my_udp_port);
            }
            set
            {
                my_udp_port = value;
            }

        }

        public enum ConnectionSpeed
        {

            //28.8Kbps, 33.6Kbps, 56Kbps,
            ISDN, Modem, DSL, Cable, Satellite,T1,T3
            //, LAN(T1), LAN(T3),
            
            //, LAN(T1), LAN(T3)
        }

        protected string my_connection_speed = "unknown";
        public string MyConnectionSpeed
        {
            get
            {
                return (my_connection_speed);
            }
            set
            {
                my_connection_speed = value;
            }

        }

        public enum ConnectionMode
        {
            Active,Passive//,Socks5
        }
        //Socks5 is not implemented atm

        protected ConnectionMode my_connection_mode = ConnectionMode.Passive;
        public ConnectionMode MyConnectionMode
        {
            get
            {
                return (my_connection_mode);
            }
            set
            {
                my_connection_mode = value;
            }

        }
  
        public Hub()
        {
            is_connected = false;
            is_extended_protocol = false;
            is_logged_in = false;
            socket = null;
            port = 411;
            //Disconnect();
        }

        public void Reconnect()
        {
            Disconnect();
            Connect();
        }
        
        public void Disconnect()
        {
            try
            {
                if (socket != null && socket.Connected)
                {
                    //if(receive_operation!=null) socket //socket.EndReceive(receive_operation);
                    socket.Shutdown(SocketShutdown.Both);
                    Thread.Sleep(10);
                    socket.Close();
                    socket = null;
                    //receive_operation = null;
                }
                is_connected = false;
                is_extended_protocol = false;
                is_logged_in = false;
                try
                {
                    if (Disconnected != null)
                        Disconnected(this);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception in Disconnect event: " + ex.Message);
                    error_code = ErrorCodes.Exception;
                    if (UnableToConnect != null)
                        UnableToConnect(this);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error disconnecting Hub: " + name + "(exception:" + ex.Message + ")");
                error_code = ErrorCodes.Exception;
                if (UnableToConnect != null)
                    UnableToConnect(this);

            }

        }

        public void Connect()
        {
            if (!is_connected)
            {
                //Console.WriteLine("Connecting to Hub: "+name);
                try
                {
                    //Disconnect();//if still connected , disconnect first

                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    //IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(address),port);
                    AsyncCallback event_host_resolved = new AsyncCallback(OnHostResolve);
                    Dns.BeginGetHostEntry(address, event_host_resolved, socket);
                    //IPHostEntry ip = Dns.GetHostEntry(address);
                }
                catch (Exception ex)
                {
                    error_code = ErrorCodes.Exception;
                    Console.WriteLine("Error connecting to Hub: " + name + "(exception:" + ex.Message + ")");
                    if (UnableToConnect != null)
                        UnableToConnect(this);

                }
            }
        }

        private void OnHostResolve(IAsyncResult result)
        {
            Socket resolve_socket = (Socket)result.AsyncState;
            try
            {
                    IPHostEntry ip_entry = Dns.EndGetHostEntry(result);
                    if (ip_entry != null && ip_entry.AddressList.Length > 0)
                    {
                        ip = ip_entry.AddressList[0].ToString(); // correct the ip string
                        IPEndPoint endpoint = new IPEndPoint(ip_entry.AddressList[0], port);
                        AsyncCallback event_connect = new AsyncCallback(OnConnect);
                        socket.BeginConnect(endpoint, event_connect, socket);
                    }
                    else
                    {
                        Console.WriteLine("Unable to connect to server: " + name + "(address:" + address + ")");
                        error_code = ErrorCodes.UnableToConnect;
                        if (UnableToConnect != null)
                            UnableToConnect(this);
                    }

            }

            catch (SocketException sex)
            {
                if (sex.ErrorCode == 11001)
                {
                    error_code = ErrorCodes.UnableToConnect;
                    Console.WriteLine("Error during Address resolve of Hub: " + name + "(address:" + address + ")");
                    if (UnableToConnect != null)
                        UnableToConnect(this);
                }
                else
                {
                    error_code = ErrorCodes.UnableToConnect;
                    Console.WriteLine("Error during Address resolve of Hub: " + name + "(address:" + address + ")");
                    if (UnableToConnect != null)
                        UnableToConnect(this);
                }

            }
            catch (Exception ex)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error during Address resolve of Hub: " + name + "(address:" + address +",exception:"+ex.Message+")");
                if (UnableToConnect != null)
                    UnableToConnect(this);

            }


        }

        //private IAsyncResult receive_operation = null;

        private void OnConnect(IAsyncResult result)
        {
            Socket connect_socket = (Socket)result.AsyncState;

            try
            {
                if (connect_socket.Connected)
                {
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    connect_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, connect_socket);
                    //Console.WriteLine("Successfully connected to Hub: " + name);
                    try
                    {
                        if (Connected != null)
                            Connected(this);
                    }
                    catch (Exception ex)
                    {
                        error_code = ErrorCodes.Exception;
                        Console.WriteLine("Exception in Connected event: " + ex.Message);
                        if (UnableToConnect != null)
                            UnableToConnect(this);
                    }

                    is_connected = true;
                }
                else
                {
                    error_code = ErrorCodes.UnableToConnect;
                    Console.WriteLine("Unable to connect to server: " + name);
                    if (UnableToConnect != null)
                        UnableToConnect(this);
                }

            }
            catch (Exception ex)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error during connect to Hub: " + name + "(exception:" + ex.Message + ")");
                if (UnableToConnect != null)
                    UnableToConnect(this);
            }


        }

        //TODO counter possible packet splits in messages

        private void OnReceive(IAsyncResult result)
        {
            Socket receive_socket = (Socket)result.AsyncState;
            if (!receive_socket.Connected) return;
            try
            {
                int received_bytes = receive_socket.EndReceive(result);
                if (received_bytes > 0)
                {
                    //string received_string = Encoding.ASCII.GetString(receive_buffer, 0, received_bytes);
                    string received_string = System.Text.Encoding.Default.GetString(receive_buffer, 0, received_bytes);
                    //Console.WriteLine("Received a string: "+received_string);
                    //interpret string and act accordingly
                    InterpretReceivedString(received_string);
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                }
                else
                {
                    is_connected = false;
                    //Console.WriteLine("Connection to Hub: " + name + " dropped.");
                    try
                    {
                        if (Disconnected != null)
                            Disconnected(this);
                    }
                    catch (Exception ex)
                    {
                        error_code = ErrorCodes.Exception;
                        Console.WriteLine("Exception in Disconnect event: " + ex.Message);
                    }

                }

            }
            catch (Exception ex)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error receiving data from Hub: " + name + "(exception:" + ex.Message + ")");
                try
                {
                    is_connected = false;
                    if (Disconnected != null)
                        Disconnected(this);
                }
                catch (Exception ex2)
                {
                    error_code = ErrorCodes.Exception;
                    Console.WriteLine("Exception in Disconnect event: " + ex2.Message);
                }

            }

        }

        public void SendChatMessage(string message)
        {

            string send_string = "<" + nick + "> " + message + "|";

            try
            {
                //socket.Send(Encoding.UTF8.GetBytes(send_string), SocketFlags.None);
                byte[] send_bytes = System.Text.Encoding.Default.GetBytes(send_string);
                socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(SendChatMessageCallback), socket);
            }
            catch (Exception e)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error sending chat message to Hub: " + name + "(exception:" + e.Message + ")");
            }
        }

        private void SendChatMessageCallback(IAsyncResult ar)
        {
            Socket send_chat_message_socket = (Socket)ar.AsyncState;
            try
            {
                int bytes = send_chat_message_socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during send of chat: " + ex.Message);
            }
        }

        public void SendChatMessage(string message, string user)
        {
            SendCommand("To: " + user + " From: " + nick + " $<" + nick + "> " + message);
        }

        public void SendChatMessage(string message, string user,bool show_in_main_chat)
        {
            SendCommand("MCTo: " + user + " $" + nick + " " + message);
        }

        public enum SearchFileType
        {
            any = 1,audio = 2,compressed = 3 ,documents = 4,executables = 5,pictures = 6,video = 7,folders = 8,tth = 9
        }

        public void Search(string search_string)
        {
            Search(search_string,false,false,0,SearchFileType.any);
        }

        public void Search(string search_string, bool size_restricted, bool is_max_size, long size, SearchFileType file_type)
        {
            //"Search Hub:[DE]Test F?F?0?1?extras"
            //string send_string = "<" + nick + "> " + message + "|";
            string parameter = "";
            if (my_connection_mode == ConnectionMode.Active)
            {
                parameter = my_ip + ":" + my_udp_port.ToString() + " ";
            }
            else if (my_connection_mode == ConnectionMode.Passive)
                {
                    parameter = "Hub:" + nick + " ";
                }
                if (size_restricted)
                    parameter += "T?";
                else parameter += "F?";
                if (is_max_size)
                    parameter += "T?";
                else parameter += "F?";
            
            parameter += size.ToString()+"?";
            parameter += ((int)file_type).ToString()+"?";
            parameter += search_string.Replace(' ','$');
            SendCommand("Search",parameter);
        }

        public void Search(string search_tth, bool is_tth)
        {
            if (!is_tth)
                Search(search_tth);
            else
            {
                Search("TTH:" + search_tth,false,false,0,SearchFileType.tth);
            }
        }

        public void SearchReply(string result_name,long filesize, SearchParameters search)
        {
            if (search.mode == ConnectionMode.Passive)
            {
                string temp_hub = name;
                if (search.HasTTH) temp_hub = "TTH:" + search.tth;
                string reply_parameter = nick + " " + result_name + (char)0x05 + filesize + " 1/1" + (char)0x05 + temp_hub + " (" + ip + ":" + port + ")" + (char)0x05 + search.username;
                Console.WriteLine("Replying to passive search: " + reply_parameter);
                SendCommand("SR", reply_parameter);
            } 
        }

        
        public void GetUserInfo(string user)
        {//TODO finish this 
        
            SendCommand("GetInfo",user+" "+nick);
        }

        public void SendConnectToMe(string username)
        {
            if (my_connection_mode == ConnectionMode.Active)
                SendCommand("ConnectToMe", username + " " + my_ip + ":" + my_tcp_port);
            else SendCommand("RevConnectToMe",nick + " " + username);
        }

        public void SendConnectToMeV2(string username)
        {
            SendCommand("ConnectToMe", nick + " " + username + " " + my_ip + ":" + my_tcp_port);
        }

        public void SendMyInfo()
        {
            string temp_connection_mode = "";
            //check if info changed and a myinfo command is actually needed
            if (my_connection_mode == Hub.ConnectionMode.Active) temp_connection_mode = "A";
            else if (my_connection_mode == Hub.ConnectionMode.Passive) temp_connection_mode = "P";
            //else if (my_connection_mode == Hub.ConnectionMode.Socks5) temp_connection_mode = "5";
            //SendCommand("MyINFO", "$ALL " + parameters[0] + " <" + my_name + " V:" + my_tag_version + ",M:" + temp_connection_mode + ",H:0/0/0,S:2>$ $Cable1$" + my_email + "$" + my_share_size.ToString() + "$");
            SendCommand("MyINFO", "$ALL " + nick + " <" + my_name + " V:" + my_tag_version + ",M:" + temp_connection_mode + ",H:0/0/0,S:2,O:0>$ $Cable1$" + my_email + "$" + my_share_size.ToString() + "$");
        }

        private void AddChatToHistory(string username, string message)
        {
            if (chat_history_max_length != 0)
            {//using max history -> delete first line added to list
                if (chat_history.Count > chat_history_max_length)
                    chat_history.RemoveAt(0);
            }
            ChatLine cline = new ChatLine(username, message);
            chat_history.Add(cline);
            if (MainChatLineReceived != null)
                MainChatLineReceived(this,cline);

        }
        
        
        private string received_string_buffer = "";

        private void InterpretReceivedString(string received_string)
        {
            // possible strings
            //$command|
            //<chat>
            //| 
            received_string_buffer += received_string;
            //if (received_string_buffer.IndexOf("|") == -1) return;//incomplete command
            int last_command_marker = received_string_buffer.LastIndexOf("|");
            if (last_command_marker != -1)
            {
                string command_strings = received_string_buffer.Substring(0, last_command_marker);

                //received_string_buffer = received_string_buffer.Substring(last_command_marker);
                received_string_buffer = received_string_buffer.Remove(0, last_command_marker);
                string[] received_strings = command_strings.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < received_strings.Length; i++)
                {
                    if (received_strings[i].StartsWith("<"))
                    {
                        int user_end_marker = received_strings[i].IndexOf(">");
                        if (user_end_marker != -1)
                        {
                            string user = received_strings[i].Substring(1, user_end_marker - 1);
                            string message = "";
                            if ((user_end_marker + 1) < received_strings[i].Length) 
                                message = received_strings[i].Substring(user_end_marker+1);
                            AddChatToHistory(user, message);
                        }
                        else Console.WriteLine("Received a wrong chat line: " + received_strings[i]);
                        //Console.WriteLine("chat message on hub: " + name + " - " + received_strings[i]);
                    }
                    else
                    {
                        if (received_strings[i].StartsWith("$")) InterpretCommand(received_strings[i]);
                        //else Console.WriteLine("Received a non command line: " + received_strings[i]);
                    }
                }
            }
         
        }

        private void InterpretCommand(string received_command)
        {
            int command_end = received_command.IndexOf(" ");
            if (command_end == -1) command_end = received_command.Length;

            if (command_end != -1)
            {
                string parameter = "";
                string[] parameters ={ };
                string command = received_command.Substring(1);
                if (command_end != received_command.Length)
                {
                    command = received_command.Substring(1, command_end - 1);
                    parameter = received_command.Substring(command_end + 1);
                    parameters = parameter.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                    //Console.WriteLine("Command: '" + command + "' ,Parameter(" + parameters.Length + "): '" + parameter + "'");
                }
                switch (command)
                {
                    case "HubName" :
                        //Console.WriteLine("Hubname Command received: " + parameter);
                        name = parameter;
                        //fire nameChange event
                        break;

                    case "Hello" :
                        //Console.WriteLine("Hello Command received: " + parameters[0]);
                        if (!is_logged_in)
                        {
                            is_logged_in = true;
                            SendCommand("Version", my_version);
                            SendMyInfo();
                            SendCommand("GetNickList");
                            //Console.WriteLine("Logged in Hub: "+name);
                            try
                            {
                                if (LoggedIn != null)
                                    LoggedIn(this);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in LoggedIn: " + ex.Message);
                            }

                        }
                        else
                        {//new user announced by server
                            //Console.WriteLine("User "+parameters[0]+" has joined Hub: "+name);
                            user_list.Add(parameters[0]);
                            try
                            {
                                if (UserJoined != null)
                                    UserJoined(this, parameters[0]);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in UserJoined: " + ex.Message);
                            }
                        }
                        break;

                    case "ConnectToMe":
                            try
                            {
                                string peer_address = "";
                                if (parameters.Length == 2)
                                {
                                    peer_address = parameters[1];
                                }
                                else if (parameters.Length == 3)
                                {
                                    peer_address = parameters[2];
                                }
                                else break;
                                Peer peer = new Peer(peer_address); //add username also, to counter possible network attacks 
                                if (ConnectToMeReceived != null)
                                    ConnectToMeReceived(this, peer);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in ConnectToMe EventHandler: " + ex.Message);
                            }
                        break;

                    case "RevConnectToMe":
                        SendConnectToMe(parameters[0]);
                        break;

                    case "NickList":
                        user_list.Clear();
                        string[] temp_users = parameters[0].Split("$$".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        foreach (string temp_user in temp_users)
                        {
                            user_list.Add(temp_user);
                            if (UserJoined != null)
                                UserJoined(this, temp_user);
                        }
                        break;

                    case "OpList":
                        op_list.Clear();
                        string[] temp_ops = parameters[0].Split("$$".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        foreach (string temp_op in temp_ops)
                        {
                            op_list.Add(temp_op);
                        }
                        break;

                    case "HubTopic":
                        //topic = parameters[0];
                        topic = parameter;
                        break;

                    case "UserCommand":
                        //Console.WriteLine("User Command received: "+parameter);
                        //TODO support user context menu entries
                        break;

                    case "Search":
                        Console.WriteLine("Search Command received: "+parameter);
                        SearchParameters search = new SearchParameters();
                        if(parameters[0].StartsWith("Hub:"))
                        {
                            search.mode = ConnectionMode.Passive;
                            int username_start = parameters[0].IndexOf(":");
                            if (username_start == -1 || username_start + 1 > parameters[0].Length) break;
                            search.username = parameters[0].Substring(username_start + 1);
                        }
                        else
                        {
                            search.mode = ConnectionMode.Active;
                            int port_start = parameters[0].IndexOf(":");
                            if (port_start == -1 || port_start + 1 > parameters[0].Length) break;
                            search.ip = parameters[0].Substring(0,port_start);
                            try
                            {
                                search.port = int.Parse(parameters[0].Substring(port_start + 1));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error parsing port in search: " + ex.Message);
                                break;
                            }
                        }

                        char[] seps ={ '?' };
                        string[] search_parameters = parameters[1].Split(seps,StringSplitOptions.RemoveEmptyEntries);
                        if (search_parameters.Length < 4) break;
                        if (search_parameters[0] == "F")
                            search.size_restricted = false;
                        else search.size_restricted = true;
                        if (search_parameters[1] == "F")
                            search.is_max_size = false;
                        else search.is_max_size = true;
                        try
                        {
                            search.size = long.Parse(search_parameters[2]);
                            search.file_type = (SearchFileType)int.Parse(search_parameters[3]);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("error parsing ints in search: " + ex.Message);
                            break;
                        }
                        if (search_parameters[4].StartsWith("TTH:") && search.file_type == SearchFileType.tth)
                            search.tth = search_parameters[4].Substring(4);
                        else search.search_string = search_parameters[4];

                        if (SearchReceived != null)
                            SearchReceived(this,search);
                        break;

                    case "Supports":
                        supports = (string[])parameters.Clone();
                        break;

                    case "UserIP":

                        break;

                    case "To:":

                        break;



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

                    case "LogedIn":
                        //what the hell is this and who forgot to take some english lessons ?
                        break;
                    case "MyINFO":
                    case "GetPass":
                        break;

                    case "ForceMove":
                        //Console.WriteLine("FORCE MOVE NOT IMPLEMENTED");
                        if (MoveForced != null)
                        {
                            Hub dst_hub = this.Copy();
                            MoveForced(this, dst_hub);
                        }
                        break;


                    case "ValidateDenide":
                        Console.WriteLine("Nick: "+parameters[0]+" on Hub: " + name + " is already in use.");
                        break;



                    case "HubIsFull":
                        Console.WriteLine("Hub: " + name + " is full.");
                        Disconnect();
                        break;


                    case "Quit":
                        //Console.WriteLine("User "+parameters[0]+" has left Hub: "+name);
                        user_list.Remove(parameters[0]);
                        try
                        {
                            if (UserQuit != null)
                                UserQuit(this, parameters[0]);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception in UserJoined: " + ex.Message);
                        }

                        break;

                    
                    case "Lock" :
                        //Console.WriteLine("Lock Command received: "+parameter);
                        //int key_end = parameter.IndexOf(" ");
                        //if (key_end != -1)
                        //{
                            //string key = parameter.Substring(0, key_end);
                        if (parameters.Length > 1)
                        {
                            string key = parameters[0];
                            //Console.WriteLine("Key: " + key);
                            if (key.StartsWith("EXTENDEDPROTOCOL"))
                            {
                                is_extended_protocol = true;
                                //Console.WriteLine("Hub is using the dc++ protocol enhancements.");
                                //SendCommand("Supports", "UserCommand NoGetINFO NoHello UserIP2 TTHSearch ZPipe0 GetZBlock ");
                                SendCommand("Supports", "UserCommand TTHSearch ");
                            }

                            //string decoded_key = MyLockToKey(key);
                            string decoded_key = L2K(key);
                            //Console.WriteLine("Decoded key: " + decoded_key);
                            SendCommand("Key" , decoded_key);
                            SendCommand("ValidateNick", nick);


                        }
                        break;
                    default:
                        Console.WriteLine("Unknown Command received: " + command + ", Parameter: " + parameter);
                        break;
                }
            }
            else Console.WriteLine("Error interpreting command: " + received_command);
        }

        public Hub Copy()
        {
            Hub ret = new Hub();
            ret.address = this.address;
            ret.auto_reconnect = this.auto_reconnect;
            ret.country = this.country;
            ret.description = this.description;
            ret.ip = this.ip;
            ret.is_connected = false;
            ret.is_extended_protocol = false;
            ret.is_grabbed = this.is_grabbed;
            ret.is_logged_in = false;
            ret.Connected = this.Connected;
            ret.Disconnected = this.Disconnected;
            ret.UnableToConnect = this.UnableToConnect;
            ret.LoggedIn = this.LoggedIn;
            ret.SearchResultReceived = this.SearchResultReceived;
            ret.UserJoined = this.UserJoined;
            ret.UserQuit = this.UserQuit;
            ret.MoveForced = this.MoveForced;
            ret.max_hubs = 0;
            ret.max_users = 0;
            ret.min_share = 0;
            ret.min_slots = 0;
            ret.my_connection_mode = this.my_connection_mode;
            ret.my_connection_speed = this.my_connection_speed;
            ret.my_email = this.my_email;
            ret.my_ip = this.my_ip;
            ret.my_name = this.my_name;
            ret.my_share_size = this.my_share_size;
            ret.my_tag_version = this.my_tag_version;
            ret.my_tcp_port = this.my_tcp_port;
            ret.my_udp_port = this.my_udp_port;
            ret.my_version = this.my_version;
            ret.name = this.name;
            ret.nick = this.nick;
            ret.op_list = new List<string>();
            ret.port = this.port;
            ret.shared = 0;
            ret.topic = this.topic;
            ret.user_list = new List<string>();
            ret.UserList = new List<string>();
            ret.users = 0;
            return (ret);
        }

        public void Ungrab()
        {
            Connected = null;
            Disconnected = null;
            UnableToConnect = null;
            LoggedIn = null;
            SearchResultReceived = null;
            UserJoined = null;
            UserQuit = null;
            MoveForced = null;
        }



        #region Unit Testing
        [Test]
        public void TestLocalHubConnect()
        {
            Console.WriteLine("Test to connect to a local hub (remember to start some hub before).");
            bool wait = true;
            Hub hub = new Hub();
            hub.Address = "localhost";
            hub.Connected += delegate(Hub connected)
            {
                Console.WriteLine("Hub Connected");
                //Assert.IsTrue(!string.IsNullOrEmpty(external_ip), "no ip address fetched");
                //wait = false;
            };
            hub.LoggedIn += delegate(Hub logged_in)
            {
                Console.WriteLine("Hub Logged in");
                wait = false;
            };
            hub.Disconnected += delegate(Hub disconnected)
            {
                if (wait)
                {
                    Console.WriteLine("Test failed : Hub disconnected.");
                    Assert.Fail("Test failed : Hub disconnected.");
                }
            };
            hub.UnableToConnect += delegate(Hub error)
            {
                Console.WriteLine("Test failed : Unable to connect to");
            };
            hub.IsGrabbedByClient = true;
            hub.Connect();
            Console.WriteLine("Waiting for hub events.");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 10))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            hub.Disconnect();
            Console.WriteLine("Local Hub Connect Test successful.");
        }
        #endregion



    }
}
