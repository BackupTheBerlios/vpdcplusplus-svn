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
 * add support for user infos beside his name ;-)
 */

namespace DCPlusPlus
{
    /// <summary>
    /// a class to manage information and the connection
    /// to a single hub
    /// </summary>
    [TestFixture]
    public class Hub : Connection
    {
        /// <summary>
        /// Prototype for the Search Result Received Event Handler
        /// </summary>
        /// <param name="hub">the hub on which the search result was found</param>
        /// <param name="result">the search result received</param>
        public delegate void SearchResultReceivedEventHandler(Hub hub, SearchResults.SearchResult result);
        /// <summary>
        /// Event handler that gets called
        /// when a search result was received via the hub connection (passive result)
        /// </summary>
        public event SearchResultReceivedEventHandler SearchResultReceived;
        /// <summary>
        /// Prototype for the Search Received Event Handler
        /// </summary>
        /// <param name="hub">the hub on which the search was issued</param>
        /// <param name="search">the search parameters received</param>
        public delegate void SearchReceivedEventHandler(Hub hub, SearchParameters search);
        /// <summary>
        /// Event handler that gets called
        /// when a search was received
        /// (another user wants to find something)
        /// </summary>
        public event SearchReceivedEventHandler SearchReceived;
        /// <summary>
        /// Prototype for the Main Chat Line Received Event Handler
        /// </summary>
        /// <param name="hub">the hub on which the chat was send</param>
        /// <param name="line">the chat line received</param>
        public delegate void MainChatLineReceivedEventHandler(Hub hub, ChatLine line);
        /// <summary>
        /// Event handler that gets called
        /// when a chat message was received from a hub
        /// </summary>
        public event MainChatLineReceivedEventHandler MainChatLineReceived;
        /// <summary>
        /// Prototype for the Private Chat Line Received Event Handler
        /// </summary>
        /// <param name="hub">the hub on which the private message was send</param>
        /// <param name="line">the private message</param>
        public delegate void PrivateChatLineReceivedEventHandler(Hub hub, ChatLine line);
        /// <summary>
        /// Event handler that gets called
        /// when a private message was received from a hub
        /// </summary>
        public event PrivateChatLineReceivedEventHandler PrivateChatLineReceived;
        /// <summary>
        /// Prototype for the User Quit Event Handler
        /// </summary>
        /// <param name="hub">the hub from which the user disconnected</param>
        /// <param name="username">the user that went offline</param>
        public delegate void UserQuitEventHandler(Hub hub, string username);
        /// <summary>
        /// Event handler that gets called
        /// when a user was disconnected from a hub
        /// </summary>
        public event UserQuitEventHandler UserQuit;
        /// <summary>
        /// Prototype for the User Joined Event Handler
        /// </summary>
        /// <param name="hub">the hub to which the user connected to</param>
        /// <param name="username">the user that went online</param>
        public delegate void UserJoinedEventHandler(Hub hub, string username);
        /// <summary>
        /// Event handler that gets called
        /// when a user joined a hub
        /// </summary>
        public event UserJoinedEventHandler UserJoined;
        /// <summary>
        /// Prototype for the Logged In Event Handler
        /// </summary>
        /// <param name="hub">the hub that you successfully logged in</param>
        public delegate void LoggedInEventHandler(Hub hub);
        /// <summary>
        /// Event handler that gets called
        /// when the client logged in a hub
        /// </summary>
        public event LoggedInEventHandler LoggedIn;
        /// <summary>
        /// Prototype for the Password Requested Event Handler
        /// </summary>
        /// <param name="hub">the hub which asks for a password</param>
        public delegate string PasswordRequestedEventHandler(Hub hub);
        /// <summary>
        /// Event handler that gets called
        /// when a hub requested a password
        /// to use the specified username
        /// should return the password if it is known
        /// </summary>
        public event PasswordRequestedEventHandler PasswordRequested;
        /// <summary>
        /// Prototype for the Move Forced Event Handler
        /// </summary>
        /// <param name="src_hub">the hub that wants us to move</param>
        /// <param name="dst_hub">the destination the source hub has suggested</param>
        public delegate void MoveForcedEventHandler(Hub src_hub, Hub dst_hub);
        /// <summary>
        /// Event handler that gets called
        /// when a hub wants us to connect to another hub
        /// </summary>
        public event MoveForcedEventHandler MoveForced;
        /// <summary>
        /// Prototype for the Connect To Me Event Handler
        /// </summary>
        /// <param name="hub">the hub on which the request was issued on</param>
        /// <param name="connection">the connection endpoint information</param>
        public delegate void ConnectToMeEventHandler(Hub hub, Peer connection);
        /// <summary>
        /// Event handler that gets called
        /// when connection request was received
        /// </summary>
        public event ConnectToMeEventHandler ConnectToMeReceived;
        /// <summary>
        /// Prototype for the Disconnected Event Handler
        /// </summary>
        /// <param name="hub">the hub that was disconnected</param>
        public delegate void DisconnectedEventHandler(Hub hub);
        /// <summary>
        /// Event handler that gets called
        /// when a hub was disconnected
        /// </summary>
        public event DisconnectedEventHandler Disconnected;
        /// <summary>
        /// Prototype for the Connected Event Handler
        /// </summary>
        /// <param name="hub">the hub we connected to</param>
        public delegate void ConnectedEventHandler(Hub hub);
        /// <summary>
        /// Event handler that gets called
        /// when a hub connection was established
        /// </summary>
        public event ConnectedEventHandler Connected;
        /// <summary>
        /// Prototype for the Unable To Connect Event Handler
        /// </summary>
        /// <param name="hub">the hub we failed to connect to</param>
        public delegate void UnableToConnectEventHandler(Hub hub);
        /// <summary>
        /// Event handler that gets called
        /// when a connection to a hub was unable to be established
        /// </summary>
        public event UnableToConnectEventHandler UnableToConnect;
        /// <summary>
        /// a filename filesize pair
        /// </summary>
        public class FileParameters
        {
            /// <summary>
            /// the filename of the parameter
            /// </summary>
            public string filename;
            /// <summary>
            /// the filesize of the parameter
            /// </summary>
            public long filesize;
        }
        /// <summary>
        /// the parameters needed for a search
        /// </summary>
        public class SearchParameters
        {
            /// <summary>
            /// the connection mode of the search
            /// </summary>
            public ConnectionMode mode;
            /// <summary>
            /// the term to search for
            /// </summary>
            public string search_string;
            /// <summary>
            /// TRUE if the search is restricted to a specific size range
            /// </summary>
            public bool size_restricted;
            /// <summary>
            /// TRUE if the search is restricted to a max size
            /// </summary>
            public bool is_max_size;
            /// <summary>
            /// the size of the size restriction,only used if size_restricted is set to TRUE
            /// </summary>
            public long size;
            /// <summary>
            /// the specific filetype to search for ,default will be ANY
            /// </summary>
            public SearchFileType file_type;
            /// <summary>
            /// the ip to return the search results to
            /// </summary>
            public string ip;
            /// <summary>
            /// the port to return the search results to
            /// </summary>
            public int port;
            /// <summary>
            /// the search user
            /// </summary>
            public string username;
            /// <summary>
            /// TRUE if the search is for a tth
            /// </summary>
            public bool HasTTH
            {
                get
                {
                    return (!string.IsNullOrEmpty(tth));
                }
            }
            /// <summary>
            /// the tth to search for
            /// </summary>
            public string tth = "";
            /// <summary>
            /// SearchParameters Constructor
            /// </summary>
            public SearchParameters()
            {
            }
            /// <summary>
            /// SearchParameters Constructor
            /// </summary>
            /// <param name="mode">the connection mode of the search</param>
            /// <param name="search_string">the term to search for</param>
            /// <param name="size_restricted">TRUE if the search is restricted to a specific size range</param>
            /// <param name="is_max_size">TRUE if the search is restricted to a max size</param>
            /// <param name="size">the size of the size restriction,only used if size_restricted is set to TRUE</param>
            /// <param name="file_type">the specific filetype to search for ,default will be ANY</param>
            /// <param name="tth">the tth to search for</param>
            /// <param name="ip"></param>
            /// <param name="port"></param>
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
            /// <summary>
            /// SearchParameters Constructor
            /// </summary>
            /// <param name="search_string">the term to search for</param>
            /// <param name="size_restricted">TRUE if the search is restricted to a specific size range</param>
            /// <param name="is_max_size">TRUE if the search is resricted to a max size</param>
            /// <param name="size">the size of the size restriction,only used if size_restricted is set to TRUE</param>
            /// <param name="file_type">the specific filetype to search for ,default will be ANY</param>
            /// <param name="tth">the tth to search for</param>
            /// <param name="username">the search user</param>
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
        protected string name = "";
        /// <summary>
        /// the hub name
        /// </summary>
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
        /// <summary>
        /// the hub address
        /// </summary>
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
        /// <summary>
        /// the hub description
        /// </summary>
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
        /// <summary>
        /// the country in which the hubs resides in
        /// </summary>
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
        /// <summary>
        /// the number of users connected to the hub
        /// (as replied from a hublist)
        /// TODO support returning of UserList.Count
        /// </summary>
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
        /// <summary>
        /// the number of shared bytes on the hub
        /// </summary>
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
        /// <summary>
        /// the minimum amount of shared bytes a client needs to connect to this hub
        /// </summary>
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
        /// <summary>
        /// the minimum amount of open slots a client needs to connect to this hub
        /// </summary>
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
        /// <summary>
        /// the maximum number of hubs a client can be connected to to connect to this hub
        /// </summary>
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
        /// <summary>
        /// the maximum number of users this hub supports
        /// </summary>
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
        /// <summary>
        /// the topic of the hub
        /// </summary>
        public string Topic
        {
            get
            {
                return (topic);
            }

        }
        protected bool is_grabbed = false;
        /// <summary>
        /// TRUE if a client has added some event handlers to this hub
        /// </summary>
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
        /// <summary>
        /// TRUE if we are logged into this hub
        /// </summary>
        public bool IsLoggedIn
        {
            get
            {
                return (is_logged_in);
            }

        }
        protected bool auto_reconnect = false;
        /// <summary>
        /// TODO add auto reconnect feature
        /// [not implemented]
        /// </summary>
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
        /// <summary>
        /// the maximum chat history length
        /// if reached chat lines will be discarded
        /// </summary>
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
        /// <summary>
        /// a single line of chat
        /// </summary>
        public class ChatLine
        {
            /// <summary>
            /// the user who send the message
            /// </summary>
            public string username = "unknown";
            /// <summary>
            /// the message body
            /// </summary>
            public string message = "";
            /// <summary>
            /// ChatLine Constructor
            /// </summary>
            /// <param name="username">the user who send the message</param>
            /// <param name="message">the message body</param>
            public ChatLine(string username, string message)
            {
                this.username = username;
                this.message = message;
            }
        }
        protected List<ChatLine> chat_history = new List<ChatLine>();
        /// <summary>
        /// the chat history of the hub
        /// (Todo change this to a enumeration
        /// at the moment it can throw list modified exceptions)
        /// </summary>
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
        //TODO create a thread safe enumerator for this
        /// <summary>
        /// the users connected to this hub
        /// (Todo change this to a enumeration
        /// at the moment it can throw list modified exceptions)
        /// </summary>
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
        /// <summary>
        /// the operators connected to this hub
        /// (Todo change this to a enumeration
        /// at the moment it can throw list modified exceptions)
        /// TODO add operator events and make them more usable as sources
        /// </summary>
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
        /// <summary>
        /// set it to the external ip of the client pc
        /// it will we used for active connection requests
        /// </summary>
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
        /// <summary>
        /// the version of the client
        /// </summary>
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
        /// <summary>
        /// the version of the client that is used in the myinfo tag
        /// </summary>
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
        /// <summary>
        /// the name of the client
        /// </summary>
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
        /// <summary>
        /// the email address of the user
        /// </summary>
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
        protected string my_description = "";
        /// <summary>
        /// the user description
        /// </summary>
        public string MyDescription
        {
            get
            {
                return (my_description);
            }
            set
            {
                my_description = value;
            }
        }
        protected long my_share_size = 0;
        /// <summary>
        /// the total number of shared bytes by the client
        /// </summary>
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
        /// <summary>
        /// the local tcp port used by the client 
        /// to accept remote connections
        /// </summary>
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
        /// <summary>
        /// the local udp port used by the client
        /// to receive udp search results
        /// </summary>
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
        /// <summary>
        /// Enumeration of Old ConnectionSpeeds 
        /// </summary>
        public enum ConnectionSpeed
        {
            /// <summary>
            /// use this if you are connected to the internet 
            /// with an old 28.8k modem
            /// </summary>
            kbps_28_8,// = "28.8Kbps",
            /// <summary>
            /// use this if you are connected to the internet 
            /// with an old 33.6k modem
            /// </summary>
            kbps_33_6,//  = "33.6Kbps",
            /// <summary>
            /// use this if you are connected to the internet 
            /// with an old 56k modem
            /// </summary>
            kbps_56,//  = "56Kbps",
            /// <summary>
            /// use this if you are connected to the internet 
            /// via a satellite connection
            /// </summary>
            satellite,//  = "Satellite",
            /// <summary>
            /// use this if you are connected to the internet 
            /// via isdn
            /// </summary>
            isdn,//  = "ISDN",
            /// <summary>
            /// use this if you are connected to the internet 
            /// with a dsl modem
            /// </summary>
            dsl,//  = "DSL",
            /// <summary>
            /// use this if you are connected to the internet 
            /// with a cable modem
            /// </summary>
            cable,//  = "Cable",
            /// <summary>
            /// use this if you are connected to the internet 
            /// via a lan connection
            /// </summary>
            lan_t1,//  = "LAN(T1)",
            /// <summary>
            /// use this if you are connected to the internet 
            /// via a lan connection
            /// </summary>
            lan_t3,//  = "LAN(T3)",
            /// <summary>
            /// use this if you are connected to the internet 
            /// with a modem
            /// </summary>
            modem,//  = "Modem"
        }
        protected string my_connection_speed = "0.02";
        /// <summary>
        /// the connection speed of the client pc to the internet
        /// </summary>
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
        /// <summary>
        /// Enumeration of all possible connection modes for a hub
        /// </summary>
        public enum ConnectionMode
        {
            /// <summary>
            /// connection is active
            /// </summary>
            Active,
            /// <summary>
            /// connection is passive
            /// </summary>
            Passive 
            //,Socks5
        }
        //Socks5 is not implemented atm
        protected ConnectionMode my_connection_mode = ConnectionMode.Passive;
        /// <summary>
        /// the connection mode to use on this hub
        /// this will decide how to connect to peers
        /// active or passive
        /// </summary>
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
        /// <summary>
        /// Hub Constructor
        /// this will initialize some values to default
        /// </summary>
        public Hub()
        {
            is_connecting = false;
            is_connected = false;
            is_extended_protocol = false;
            is_logged_in = false;
            socket = null;
            port = 411;
            //Disconnect();
        }
        /// <summary>
        /// Reconnect the hub
        /// </summary>
        public void Reconnect()
        {
            Disconnect();
            Connect();
        }
        /// <summary>
        /// Disconnect the hub
        /// </summary>
        public override void Disconnect()
        {
            if (is_connected || is_connecting)
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
                    }
                    if (is_connected)
                    {
                        if (Disconnected != null)
                            Disconnected(this);
                    }else if (is_connecting)
                    {
                        if (UnableToConnect != null)
                            UnableToConnect(this);
                    }
                    is_connecting = false;
                    is_connected = false;
                    is_extended_protocol = false;
                    is_logged_in = false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error disconnecting Hub: " + name + "(exception:" + ex.Message + ")");
                    error_code = ErrorCodes.Exception;
                }
            }
        }
        /// <summary>
        /// Connect to the hub
        /// </summary>
        public void Connect()
        {
            if (is_connecting)
            {//better handling of fast user retries
                error_code = ErrorCodes.UserDisconnect;
                Disconnect();
            }
            if (!is_connected)
            {
                //Console.WriteLine("Connecting to Hub: "+name);
                try
                {
                    is_connecting = true;
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    AsyncCallback event_host_resolved = new AsyncCallback(OnHostResolve);
                    Dns.BeginGetHostEntry(address, event_host_resolved, socket);
                    //IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(address),port);
                    //IPHostEntry ip = Dns.GetHostEntry(address);
                }
                catch (Exception ex)
                {
                    error_code = ErrorCodes.Exception;
                    Console.WriteLine("Error connecting to Hub: " + name + "(exception:" + ex.Message + ")");
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
                        Disconnect();
                    }

            }

            catch (SocketException sex)
            {
                if (sex.ErrorCode == 11001) //TODO i know , or correctly i dont know ...
                {
                    error_code = ErrorCodes.UnableToConnect;
                    Console.WriteLine("Error during Address resolve of Hub: " + name + "(address:" + address + ")");
                    Disconnect();
                }
                else
                {
                    error_code = ErrorCodes.UnableToConnect;
                    Console.WriteLine("Error during Address resolve of Hub: " + name + "(address:" + address + ")");
                    Disconnect();
                }

            }
            catch (Exception ex)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error during Address resolve of Hub: " + name + "(address:" + address +",exception:"+ex.Message+")");
                Disconnect();
            }
        }
        /// <summary>
        /// Callback for hub connecting
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
                    try
                    {
                        if (Connected != null)
                            Connected(this);
                    }
                    catch (Exception ex)
                    {
                        error_code = ErrorCodes.Exception;
                        Console.WriteLine("Exception in Connected event: " + ex.Message);
                        Disconnect();
                    }
                    is_connecting = false;
                    is_connected = true;
                }
                else
                {
                    error_code = ErrorCodes.UnableToConnect;
                    Console.WriteLine("Unable to connect to server: " + name);
                    Disconnect();
                }

            }
            catch (Exception ex)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error during connect to Hub: " + name + "(exception:" + ex.Message + ")");
                Disconnect();
            }
        }
        /// <summary>
        /// Callback to receive data from the hub
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
                    //interpret string and act accordingly
                    InterpretReceivedString(received_string);
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                }
                else
                {
                    Disconnect();
                }

            }
            catch (Exception ex)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error receiving data from Hub: " + name + "(exception:" + ex.Message + ")");
                Disconnect();
            }

        }
        /// <summary>
        /// Send a chat message to the hub
        /// </summary>
        /// <param name="message">the message you want to tell the other hub users</param>
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
                Disconnect();
            }
        }
        /// <summary>
        /// Callback for the send chat command
        /// </summary>
        /// <param name="ar">Async Result/State</param>
        private void SendChatMessageCallback(IAsyncResult ar)
        {
            Socket send_chat_message_socket = (Socket)ar.AsyncState;
            try
            {
                int bytes = send_chat_message_socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                error_code = ErrorCodes.Exception;
                Console.WriteLine("Error during sending chat message to Hub: " + name + "(exception:" + ex.Message + ")");
                Disconnect();
            }
        }
        /// <summary>
        /// Send a private message to a hub user
        /// </summary>
        /// <param name="message">the message to send</param>
        /// <param name="username">to which user you want to send it</param>
        public void SendChatMessage(string message, string username)
        {
            SendCommand("To: " + username + " From: " + nick + " $<" + nick + "> " + message);
        }
        /// <summary>
        /// Send a private message to a hub user
        /// that will be shown in his hub main chat window
        /// </summary>
        /// <param name="message">the message to send</param>
        /// <param name="username">to which user you want to send it</param>
        /// <param name="show_in_main_chat">must be set to TRUE</param>
        public void SendChatMessage(string message, string username,bool show_in_main_chat)
        {
            if (show_in_main_chat)
                SendCommand("MCTo: " + username + " $" + nick + " " + message);
            else SendChatMessage(message, username);
        }
        /// <summary>
        /// Enumeration of File Types used in a search request
        /// </summary>
        public enum SearchFileType
        {
            /// <summary>
            /// Searches for any file
            /// </summary>
            any = 1,
            /// <summary>
            /// Searches only for audio files
            /// </summary>
            audio = 2,
            /// <summary>
            /// Searches only for archieves
            /// </summary>
            compressed = 3,
            /// <summary>
            /// Searches only for documents
            /// </summary>
            documents = 4,
            /// <summary>
            /// Searches only for executables
            /// </summary>
            executables = 5,
            /// <summary>
            /// Searches only for pictures
            /// </summary>
            pictures = 6,
            /// <summary>
            /// Searches only for videos
            /// </summary>
            video = 7,
            /// <summary>
            /// Searches only for folders
            /// </summary>
            folders = 8,
            /// <summary>
            /// Searches only for tths
            /// </summary>
            tth = 9
        }
        /// <summary>
        /// Search for something on this hub
        /// </summary>
        /// <param name="search_string">the term you want to search for</param>
        public void Search(string search_string)
        {
            Search(search_string,false,false,0,SearchFileType.any);
        }
        /// <summary>
        /// Search for something on this hub
        /// </summary>
        /// <param name="search_string">the term you want to search for</param>
        /// <param name="size_restricted">TRUE if you want to restrict your search to a specific size range</param>
        /// <param name="is_max_size">TRUE if you want to size restrict your search to a max size</param>
        /// <param name="size">the size you want to use in your size resstriction,only used if size_restricted is set to TRUE</param>
        /// <param name="file_type">the specific filetype to search for ,default will be ANY</param>
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
        /// <summary>
        /// Search for something on this hub
        /// </summary>
        /// <param name="sp">all search parameters in one single parameter</param>
        public void Search(SearchParameters sp)
        {
            Search(sp.search_string, sp.size_restricted, sp.is_max_size, sp.size, sp.file_type);
        }
        /// <summary>
        /// Search for a tth on this hub
        /// </summary>
        /// <param name="search_tth">the tth to search for</param>
        /// <param name="is_tth">Must be TRUE,or else a normal search would be started</param>
        public void Search(string search_tth, bool is_tth)
        {
            if (!is_tth)
                Search(search_tth);
            else
            {
                Search("TTH:" + search_tth,false,false,0,SearchFileType.tth);
            }
        }
        /// <summary>
        /// Reply to a search from a user 
        /// (only used if the connection is passive)
        /// </summary>
        /// <param name="result_name">the filename of the share found</param>
        /// <param name="filesize">the filesize of the share</param>
        /// <param name="search">a whole lot of parameters of the search initiated by a remote user (including ip and port,which we will need here)</param>
        public void SearchReply(string result_name, long filesize, SearchParameters search)
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
        /// <summary>
        /// Send a password to the hub
        /// </summary>
        /// <param name="password">the correct password</param>
        public void SendPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return;
            SendCommand("MyPass", password);
        }
        /// <summary>
        /// Get the info for a specific user
        /// this will send GetInfo to the hub
        /// (TODO no event for this kind of received data implemented)
        /// </summary>
        /// <param name="username">the user from which to get info from</param>
        public void GetUserInfo(string username)
        {//TODO finish this 
        
            SendCommand("GetInfo",username+" "+nick);
        }
        /// <summary>
        /// Send a connection request to a specific user
        /// this will automatically select the correct command
        /// according to the hubs connection mode
        /// </summary>
        /// <param name="username">the user to connect to</param>
        public void SendConnectToMe(string username)
        {
            if (my_connection_mode == ConnectionMode.Active)
                SendCommand("ConnectToMe", username + " " + my_ip + ":" + my_tcp_port);
            else SendCommand("RevConnectToMe",nick + " " + username);
        }
        /// <summary>
        /// Send a connection request to a specific user Version 2
        /// (actually use this for something or at least investigate what this was meant for)
        /// </summary>
        /// <param name="username">the user to connect to</param>
        public void SendConnectToMeV2(string username)
        {
            SendCommand("ConnectToMe", nick + " " + username + " " + my_ip + ":" + my_tcp_port);
        }
        /// <summary>
        /// Send the client information but only if an update is needed
        /// </summary>
        public void SendMyInfo()
        {
            string temp_connection_mode = "";
            //check if info changed and a myinfo command is actually needed
            if (my_connection_mode == Hub.ConnectionMode.Active) temp_connection_mode = "A";
            else if (my_connection_mode == Hub.ConnectionMode.Passive) temp_connection_mode = "P";
            //else if (my_connection_mode == Hub.ConnectionMode.Socks5) temp_connection_mode = "5";
            //SendCommand("MyINFO", "$ALL " + parameters[0] + " <" + my_name + " V:" + my_tag_version + ",M:" + temp_connection_mode + ",H:0/0/0,S:2>$ $Cable1$" + my_email + "$" + my_share_size.ToString() + "$");
            //TODO add user flag support and hubs accounting
            SendCommand("MyINFO", "$ALL " + nick + " "+my_description+"<" + my_name + " V:" + my_tag_version + ",M:" + temp_connection_mode + ",H:1/2/2,S:2>$ $"+my_connection_speed+(char)0x01+"$" + my_email + "$" + my_share_size.ToString() + "$");
            //                                                                                                                                             ,O:0           
        }
        /// <summary>
        /// Add a chat line to the chat history of a hub
        /// if the chat history max length is != 0
        /// it will automatically remove the first lines added if needed
        /// (FIFO buffer)
        /// </summary>
        /// <param name="username">the user who wrote the chat message</param>
        /// <param name="message">the message contents</param>
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
        /// <summary>
        /// Add a user to the userlist and
        /// fires an user joined event
        /// </summary>
        /// <param name="username">the user connected to the hub</param>
        private void UserListAdd(string username)
        {
            user_list.Add(username);
            try
            {
                if (UserJoined != null)
                    UserJoined(this, username);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in UserJoined: " + ex.Message);
            }
        }
        /// <summary>
        /// Remove a user from the userlist and
        /// fires an user quit event
        /// </summary>
        /// <param name="username">the user that disconnected from the hub</param>
        private void UserListRemove(string username)
        {
            user_list.Remove(username);
            try
            {
                if (UserQuit != null)
                    UserQuit(this, username);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in UserJoined: " + ex.Message);
            }
        }
        /// <summary>
        /// Clear the userlist 
        /// this will UserListRemove() them first
        /// so a lot of events maybe triggered by this
        /// </summary>
        private void UserListClear()
        {
            if(user_list.Count>0)
                foreach (string temp_user in user_list)
                {
                    UserListRemove(temp_user);
                }
            user_list.Clear();
        }
        /// <summary>
        /// a temporary fifo buffer to fight packet fragmentation and incomplete commands 
        /// that packet fragmentation introduces
        /// </summary>
        private string received_string_buffer = "";
        /// <summary>
        /// Interpret a received string.
        /// this will split the string into single commands and
        /// call InterpretCommand() with them
        /// </summary>
        /// <param name="received_string">the data received from the hub</param>
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
                            message = message.TrimStart(' ');
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
        /// <summary>
        /// Interpret a single command 
        /// </summary>
        /// <param name="received_command">the command to interpret</param>
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
                            SendCommand("GetNickList");
                            SendMyInfo();
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
                            UserListAdd(parameters[0]);
                        }
                        break;

                    case "Quit":
                        //Console.WriteLine("User "+parameters[0]+" has left Hub: "+name);
                        UserListRemove(parameters[0]);
                        break;

                    case "NickList":
                        Console.WriteLine("NickList Message received.");
                        UserListClear();
                        string[] temp_users = parameters[0].Split("$$".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        foreach (string temp_user in temp_users)
                        {
                            UserListAdd(temp_user);
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

                    case "HubTopic":
                        //topic = parameters[0];
                        topic = parameter;
                        break;

                    case "UserCommand":
                        //Console.WriteLine("User Command received: "+parameter);
                        //TODO support user context menu entries
                        break;

                    case "Search":
                        //Console.WriteLine("Search Command received: "+parameter);
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
                        Console.WriteLine("UserIP Message received: " + parameter);
                        break;

                    case "MCTo:":
                        string mcto_username = parameters[1].Substring(1); //to skip the leading $
                        int mcto_message_start = parameters[0].Length + parameters[1].Length + 2;
                        if (mcto_message_start < parameter.Length)
                        {
                            string mcto_message = parameter.Substring(mcto_message_start);
                            AddChatToHistory(mcto_username, mcto_message);
                        }
                        break;

                    case "To:":
                        //Console.WriteLine("Private Message received: " + parameter);
                        string to_username = parameters[2];
                        int to_message_start = parameters[0].Length + parameters[1].Length + parameters[2].Length + parameters[3].Length + 4;
                        if (to_message_start < parameter.Length )
                        {
                            string to_message = parameter.Substring(to_message_start);
                            ChatLine to_message_line = new ChatLine(to_username, to_message);
                            if (PrivateChatLineReceived != null)
                                PrivateChatLineReceived(this, to_message_line);
                        }
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
                        Console.WriteLine("LogedIn Message received: " + parameter);
                        //what the hell is this and who forgot to take some english lessons ?
                        break;
                    case "MyINFO":
                        //Console.WriteLine("MyINFO Message received: " + parameter);
                        UserListAdd(parameters[1]);
                        break;
                    case "GetPass":
                        Console.WriteLine("GetPass Message received: " + parameter);
                        if (PasswordRequested != null)
                        {
                            string password = PasswordRequested(this);
                            SendPassword(password);
                        }
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
                                SendCommand("Supports", "UserCommand TTHSearch NoGetINFO NoHello ");
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
        /// <summary>
        /// Copy the hub's values into a new hub instance and return it
        /// this will not duplicate a connection / socket - only values
        /// </summary>
        /// <returns>a new hub instance containing all values of the original hub</returns>
        public Hub Copy()
        {
            Hub ret = new Hub();
            ret.address = this.address;
            ret.auto_reconnect = this.auto_reconnect;
            ret.country = this.country;
            ret.description = this.description;
            ret.ip = this.ip;
            ret.is_connecting = false;
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
        /// <summary>
        /// Remove the event handlers from the hub
        /// </summary>
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
            is_grabbed = false;
        }
        #region Unit Testing
        /// <summary>
        /// Test to connect to a local hub
        /// </summary>
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
