using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace DCPlusPlus
{
    public class Peer : Connection
    {
        public delegate void ConnectedEventHandler(Peer peer);
        public event ConnectedEventHandler Connected;

        public delegate void DisconnectedEventHandler(Peer peer);
        public event DisconnectedEventHandler Disconnected;

        public delegate void HandShakeCompletedEventHandler(Peer peer);
        public event HandShakeCompletedEventHandler HandShakeCompleted;

        public delegate void CompletedEventHandler(Peer peer);
        public event CompletedEventHandler Completed;

        public delegate void DataReceivedEventHandler(Peer peer);
        public event DataReceivedEventHandler DataReceived;

        protected string peer_nick = "unknown";
        public string PeerNick
        {
            get
            {
                return (peer_nick);
            }
            set
            {
                peer_nick = value;
            }
        }

        private void StartReceiving()
        {
            try
            {
                if (socket.Connected)
                {
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, socket);
                }
                else Console.WriteLine("Connection to peer aborted.");

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during connect to peer: "+ex.Message);
            }

        }

        private int start_tick = 0;
        private FileStream out_stream=null;

        protected long bytes_already_downloaded = 0;
        public long BytesAlreadyDownloaded
        {
            get
            {
                return (bytes_already_downloaded);
            }
        }

        protected long bytes_downloaded = 0;
        public long BytesDownloaded
        {
            get
            {
                return (bytes_downloaded);
            }
        }

        protected float speed = 0;
        public float Speed
        {
            get{
                return (speed);
            }
        }

        protected ConnectionDirection direction = ConnectionDirection.Download;
        public ConnectionDirection Direction
        {
            get
            {
                return (direction);
            }
        }

        protected bool is_transfering = false;
        public bool IsTransfering
        {
            get
            {
                return (is_transfering);
            }
        }


        protected Queue.QueueEntry queue_entry = new Queue.QueueEntry();
        public Queue.QueueEntry QueueEntry
        {
            get
            {
                return (queue_entry);
            }
        }

        protected Queue.QueueEntry.Source source = new Queue.QueueEntry.Source();
        public Queue.QueueEntry.Source Source
        {
            get
            {
                return (source);
            }
        }

        private bool first_bytes_received = false;



        /*
        
        protected long filelength = 0;
        public long FileLength
        {
            get
            {
                return (filelength);
            }
        }

        protected string output_filename="";
        public string OutputFilename
        {
            get
            {
                return (output_filename);
            }

        }

        protected string filename = "";
        public string Filename
        {
            get
            {
                return (filename);
            }
        }
        */
        //protected string input_filename="";

        private void OnReceive(IAsyncResult result)
        {
            Socket receive_socket = (Socket)result.AsyncState;
            try
            {
                if (!receive_socket.Connected)
                {
                    try
                    {
                        if (queue_entry.Filesize > 0 && bytes_downloaded+bytes_already_downloaded == queue_entry.Filesize)
                        {
                            try
                            {
                                if (Completed != null)
                                    Completed(this);
                                return;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in Completed Event: " + ex.Message);
                            }
                        }
                        if (first_bytes_received)
                            queue_entry.IsInUse = false;
                        Console.WriteLine("Connection to peer dropped.");

                        if (Disconnected != null)
                            Disconnected(this);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception in Disconnected Event: " + ex.Message);
                    }


                }
                int received_bytes = receive_socket.EndReceive(result);
                if (received_bytes > 0)
                {
                    if (is_transfering)
                    {
                        if (!first_bytes_received)
                        {
                            if(queue_entry.TryToClaimEntry())
                            {
                                first_bytes_received = true;
                            }
                            else Disconnect();

                        }

                        bytes_downloaded += received_bytes;
                        //TODO quite buggy divide by zero prob
                        speed = (float)(((float)bytes_downloaded / 1024.0f) / ((float)(System.Environment.TickCount - start_tick) / 1000.0f));
                        //Console.WriteLine("Received " + received_bytes + " bytes of data. with a speed of: " + (((System.Environment.TickCount - start_tick) / 1000) / (bytes_downloaded / 1024)) + " KB/s");

                        if (out_stream == null) //if we do not append we still have to create the output file
                            out_stream = new FileStream(queue_entry.OutputFilename, FileMode.Create, FileAccess.Write, System.IO.FileShare.ReadWrite);

                        out_stream.Write(receive_buffer, 0, received_bytes);
                        out_stream.Flush();

                        try
                        {
                            if (DataReceived != null)
                                DataReceived(this);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception in DataReceived Event: " + ex.Message);
                        }

                        if (queue_entry.Filesize > 0 && bytes_downloaded+bytes_already_downloaded == queue_entry.Filesize)
                        {
                            try
                            {
                                if (Completed != null)
                                    Completed(this);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in Completed Event: " + ex.Message);
                            }

                            SendCommand("Send");
                            Disconnect();
                        }
                    }
                    else
                    {
                        //string received_string = Encoding.ASCII.GetString(receive_buffer, 0, received_bytes);
                        string received_string = System.Text.Encoding.Default.GetString(receive_buffer, 0, received_bytes);
                        //Console.WriteLine("Received a string: "+received_string);
                        //interpret string and act accordingly
                        InterpretReceivedString(received_string);

                    }
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                }
                else
                {
                    try
                    {
                        if (queue_entry.Filesize > 0 && bytes_downloaded+bytes_already_downloaded == queue_entry.Filesize)
                        {
                            try
                            {
                                if (Completed != null)
                                    Completed(this);
                                return;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in Completed Event: " + ex.Message);
                            }
                        }
                        if (first_bytes_received)
                            queue_entry.IsInUse = false;
                        Console.WriteLine("Connection to peer dropped.");

                        if (Disconnected != null)
                            Disconnected(this);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception in Disconnected Event: " + ex.Message);
                    }

                }

            }
            catch (Exception ex)
            {
                    try
                    {
                        if (queue_entry.Filesize > 0 && bytes_downloaded+bytes_already_downloaded == queue_entry.Filesize)
                        {
                            try
                            {
                                if (Completed != null)
                                    Completed(this);
                                return;
                            }
                            catch (Exception ex2)
                            {
                                Console.WriteLine("Exception in Completed Event: " + ex2.Message);
                            }
                        }
                        if (first_bytes_received)
                            queue_entry.IsInUse = false;
                        Console.WriteLine("Error receiving data from Peer: " + ex.Message);
                        if (Disconnected != null)
                            Disconnected(this);

                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine("Exception in Disconnected Event: " + ex2.Message);
                    }
                
            }

        }

        public enum ConnectionDirection
        {
            //Incoming,Outgoing
            Upload,Download
        }


        public void StartDownload()
        {
            direction = ConnectionDirection.Download;
            long start_pos = 1;
            if (File.Exists(queue_entry.OutputFilename))
            {
                FileInfo fi = new FileInfo(queue_entry.OutputFilename);
                if (fi.Length >= queue_entry.Filesize)
                {//abort , file is complete or something else may happened here
                    Disconnect();
                    return;
                }
                start_pos = fi.Length + 1;
                bytes_already_downloaded =fi.Length;
                out_stream = new FileStream(queue_entry.OutputFilename, FileMode.Append, FileAccess.Write, System.IO.FileShare.ReadWrite);
            }
            else start_pos = 1;
            SendCommand("Get", source.Filename + "$" + start_pos);
            SendCommand("Send");
        }

        
        public void StartDownload(Queue.QueueEntry.Source source, Queue.QueueEntry entry)
        {
            this.queue_entry = entry;
            this.source = source;
            StartDownload();
        }

        
        
        public void StartDownload(string filename, string output_filename,long output_file_length)
        {
            //this.output_filename = output_filename;
            //this.filename = filename;
            queue_entry = new Queue.QueueEntry();
            queue_entry.OutputFilename = output_filename;
            queue_entry.Filesize = output_file_length;
            source = new Queue.QueueEntry.Source();
            source.Filename = filename;
            StartDownload();
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
                if (received_strings[i].StartsWith("$")) InterpretCommand(received_strings[i]);
                //else Console.WriteLine("Received a non command line: " + received_strings[i]);
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
                }

                switch (command)
                {
                    case "Direction":
                        //Console.WriteLine("Direction command received: " + parameter);
                        break;
                    case "MyNick":
                        peer_nick = parameters[0];
                        //Console.WriteLine("peer nick: "+peer_nick);
                        //handshake complete
                        break;


                    case "Supports":
                        break;

                    case "MaxedOut":
                        Disconnect();
                        break;

                    case "GetListLen":
                        //Console.WriteLine("GetListLen command received: " + parameter);
                        break;
                    case "Get":
                        //Console.WriteLine("Get command received: " + parameter);
                        break;
                    case "Send":
                        //Console.WriteLine("Send command received: " + parameter);
                        break;
                    case "FileLength":
                        //Console.WriteLine("FileLength command received: "+parameter);
                        try 
                        { 
                            long filelength = long.Parse(parameters[0]);
                            if (filelength != queue_entry.Filesize) Disconnect();//fail safe to secure downloads a bit 
                        }
                        catch (Exception ex) { Console.WriteLine("Error parsing file length: " + ex.Message); }
                        is_transfering = true;
                        start_tick = System.Environment.TickCount;
                        bytes_downloaded = 0;
                        break;

                    case "Key":
                        //Console.WriteLine("Key command received: " + parameter);
                        //Random rnd = new Random();
                        //SendCommand("Direction","Download "+rnd.Next(49999).ToString());
                        //SendCommand("GetListLen");
                        try
                        {
                            if (HandShakeCompleted != null)
                                HandShakeCompleted(this);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception in Handshake Event: " + ex.Message);
                        }

                        //SendCommand("Get", "extras series\\Extras - S01E05 [digitaldistractions].avi$1");
                        //SendCommand("Get", "MyList.DcLst$1");
                        //SendCommand("Get", "files.xml.bz2$1");
                        //out_stream = System.IO.File.Create("temp.avi");
                        //out_stream = new FileStream("temp.avi", FileMode.Create, FileAccess.Write, System.IO.FileShare.ReadWrite);
                        //SendCommand("Send");
                        break;

                    case "Lock":
                        //Console.WriteLine("Lock Command received: " + parameter);
                        if (parameters.Length > 1)
                        {
                            string key = parameters[0];
                            //Console.WriteLine("Key: " + key);
                            if (key.StartsWith("EXTENDEDPROTOCOL"))
                            {
                                is_extended_protocol = true;
                                //Console.WriteLine("Peer is using the dc++ protocol enhancements.");
                            }

                            string decoded_key = LockToKey2(key);

                            //StartHandShake();
                            SendCommand("Direction", "Download " + (49999).ToString());
                        
                            //Console.WriteLine("SendingDecoded key: " + decoded_key);
                            SendCommand("Key", decoded_key);


                        }
                        break;
                    default:
                        Console.WriteLine("Unknown Command received: " + command + ", Parameter: " + parameter);
                        break;
                }
            }
            else Console.WriteLine("Error interpreting command: " + received_command);
        }

        public Peer(Socket client,string nick)
        {
            this.nick = nick;
            this.socket = client;
            StartReceiving();
            //StartHandShake();
        }

        public Peer(Socket client)
        {
            this.socket = client;
            StartReceiving();
        }

        public Peer(string dst_ip, int dst_port)
        {
            ip = dst_ip;
            port = dst_port;
        }

        public Peer(string address)
        {
            string tmp = address;
            int port_start = tmp.IndexOf(":");
            if (port_start != -1)
            {
                int tmp_port = 411;
                string tmp_port_string = tmp.Substring(port_start + 1);
                try
                {
                    tmp_port = int.Parse(tmp_port_string);
                }
                catch (Exception)
                {
                    Console.WriteLine("error parsing port : " + tmp_port_string);
                }

                tmp = tmp.Substring(0, port_start);
                port = tmp_port;
            }
            ip = tmp; //ip or adress property?
        }

        public Peer()
        {
            ip = "";
            port = 0;
            socket = null;
        }


        public void StartHandShake()
        {
            SendCommand("MyNick",nick);
            string key=CreateKey(false);
            //Console.WriteLine("sending lock: '"+key+"'");
            SendCommand("Lock",key);
        }

        public void Disconnect()
        {
            if (socket != null)
            {
                if (socket.Connected)
                {
                    try{
                    if (Disconnected != null)
                        Disconnected(this);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception in Disconnected Event: " + ex.Message);
                }

                    this.socket.Close();
                }
                else Console.WriteLine("This peer is already disconnected");
            }
            else Console.WriteLine("This socket is unused -> no disconnect needed.");
        }

        public void Connect()
        {
            if (!is_connected)
            {
                try
                {
                    //Disconnect();//if still connected , disconnect first
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
                    AsyncCallback event_connect = new AsyncCallback(OnConnect);
                    socket.BeginConnect(endpoint, event_connect, socket);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to Peer: " + ip + ":" + port);
                    //if (Error != null)
                    //    Error(this, "Exception during connect: " + ex.Message, ErrorCodes.Exception);

                }
            }

        }

        public void ConnectTo(string dst_ip,int dst_port)
        {
            ip = dst_ip;
            port = dst_port;
            Connect();
        }

        private void OnConnect(IAsyncResult result)
        {
            Socket connect_socket = (Socket)result.AsyncState;

            try
            {
                if (connect_socket.Connected)
                {
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    connect_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, connect_socket);
                    Console.WriteLine("Successfully connected to peer: " + ip+":"+port);
                    try
                    {
                        if (Connected != null)
                            Connected(this);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception in Connected event: " + ex.Message);
                        //if (Error != null)
                        //    Error(this, "Exception in Connected event: " + ex.Message, ErrorCodes.Exception);
                    }

                    is_connected = true;
                }
                else
                {
                    Console.WriteLine("Unable to connect to peer");
                    //if (Error != null)
                    //    Error(this, "Unable to connect to peer.", ErrorCodes.UnableToConnect);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during connect to peer.");
                //if (Error != null)
                 //   Error(this, "Error during connect to peer: " + ex.Message, ErrorCodes.Exception);
            }


        }

    }
}
