using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using NUnit.Framework;
using System.Threading;
using System.Net;
using System.IO;
using System.Reflection;


//TODO
// add resume support
// fix waiting bug if some other request takes too much time to process
// react correctly on connection close header

namespace DCPlusPlus
{
    /// <summary>
    /// a very simple webserver
    /// </summary>
    [TestFixture]
    public class MiniWebServer
    {
        static private string http_version="HTTP/1.1";

        static public string HttpVersion
        {
            get { return http_version; }
            set { http_version = value; }
        }
        private string web_directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName)+"\\web" ;

        public string WebDirectory
        {
            get { return web_directory; }
            set { web_directory = value; }
        }



        static private string mime_types_file = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName) +"\\web\\mime.types";

        static public string MimeTypesFile
        {
            get { return mime_types_file; }
            set { mime_types_file = value; }
        }
        /// <summary>
        /// Get the Mime-Type for a file extension
        /// </summary>
        /// <param name="filename">the filename or the extension (starting with a .)</param>
        /// <returns>the best matching mime type found</returns>
        static public string GetMimeType(string filename)
        {
            try
            {
                string extension = Path.GetExtension(filename);
                string[] mime_types = File.ReadAllLines(mime_types_file);
                foreach (string mime_type in mime_types)
                {
                    string[] seps = { " " };
                    string[] fields = mime_type.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                    if (fields.Length == 2)
                    {
                        if (fields[0] == extension)
                            return (fields[1]);
                    }

                }
                return ("application/octet-stream");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to get mime type: " + ex.Message);
                return ("application/octet-stream");
            }
        }
        /// <summary>
        /// a class to interact with a connecected web client
        /// </summary>
        public class Client
        {
            private Socket socket = null;
            private byte[] receive_buffer = new byte[8192];
            private bool disconnected = false;
            private bool close_connection = false;
            //private object disconnected_lock = new object();
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

            public void ResetRequest()
            {
                request_finished = false;
                header_string = "";
                //request_string = "";
                body_string = "";
                send_bytes_length = 0;

                file_real_filesize = -1;
                file_answer_transfered_bytes = 0;
                if(file_answer_stream != null)
                    file_answer_stream.Close();
                if (close_connection)
                    Disconnect();
            }

            private bool request_finished=false;

            public bool RequestFinished
            {
                get { return request_finished; }
                set { request_finished = value; }
            }


            private object answering_lock = new object();
            private bool answering = false;

            public bool Answering
            {
                get { return answering; }
            }


            private string request_string;

            public string RequestString
            {
                get { return request_string; }
                set { request_string = value; }
            }
	
            private string header_string = "";

            public string HeaderString
            {
                get { return header_string; }
                set { header_string = value; }
            }

            private string body_string = "";

            public string BodyString
            {
                get { return body_string; }
                set { body_string = value; }
            }

            public void Disconnect()
            {
                //lock (disconnected_lock)
                //{
                    disconnected = true;
                    //socket.ReceiveTimeout = 0;
                    if(socket!=null)
                        socket.Close();
                    socket = null;
                //}
            }

            
            private void OnReceive(IAsyncResult result)
            {
                //lock (disconnected_lock)
                //{
                    if (disconnected) return;
                //}

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
                            request_string += received_string;
                            int header_end = request_string.IndexOf("\r\n\r\n");
                            //lock (answering_lock)
                            //{
                            //}
                                if (header_end != -1 && !answering)
                                {//header finished TODO add timeout and a better detection for a complete header 
                                    //now try to get body string if content-length + method = POST
                                    //maybe wait for rest of body
                                    request_finished = true;
                                    header_string = request_string.Substring(0, header_end);
                                    if (header_string.StartsWith("POST"))
                                    {
                                        string[] seps = { "\r\n" };
                                        string[] lines = header_string.Split(seps, StringSplitOptions.None);
                                        int content_length = 0;
                                        foreach (string line in lines)
                                        {
                                            if (line.StartsWith("Content-Length:", true, null))
                                            {
                                                try
                                                {
                                                    content_length = int.Parse(line.Substring("Content-Length:".Length).Trim());
                                                }
                                                catch (Exception ex)
                                                {
                                                    Console.WriteLine("error parsing content-length: " + ex.Message);
                                                }
                                            }
                                        }

                                        //check if body_string.Length == content-length if given else just parse the request
                                        //if != wait for more data
                                        if (content_length != 0)
                                        {
                                            if (request_string.Substring(header_end + "\r\n\r\n".Length).Length >= content_length)
                                            {
                                                body_string = request_string.Substring(header_end + "\r\n\r\n".Length, content_length);
                                            }
                                            else request_finished = false;
                                        }
                                        else
                                        { //this needs to be more robust for a perfect environment to handle even oldest clients
                                            //maybe with some kind of timeout to ensure all body data was received
                                            body_string = request_string.Substring(header_end + "\r\n\r\n".Length);
                                        }
                                    }

                                    if (request_finished)
                                    {
                                        answering = true;
                                        Request request = new Request(header_string, body_string, this);
                                        if (request.IsValid)
                                        {
                                            Console.WriteLine("a valid request was received.");
                                            string connection = request.Headers.Get("Connection");
                                            if(!string.IsNullOrEmpty(connection) && connection.Equals("Close",StringComparison.CurrentCultureIgnoreCase))
                                                close_connection = true;
                                            if (RequestReceived != null)
                                                RequestReceived(this, request);
                                        }
                                        else
                                        {
                                            Console.WriteLine("Received an invalid request.");
                                            Disconnect();
                                            //TODO TellError here
                                        }
                                        //ResetRequest();
                                        if (header_end + 4 + body_string.Length < request_string.Length)
                                            request_string = request_string.Remove(0, header_end + 4 + body_string.Length);
                                        else request_string = "";
                                        //Console.WriteLine("next request:"+request_string);
                                    }
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


            private string auth_realm="vpDCPlusPlus";

            public string AuthRealm
            {
                get { return auth_realm; }
                set { auth_realm = value; }
            }
	

            public void TellToAuthenticate()
            {
                request_string = "";
                string body = "<html><head><title>401 Not allowed</title></head><body><h1>401 Not allowed</h1><p>The access to this resource has been denied; The client didn't authenticate.</p></body></html>";
                string header = MiniWebServer.HttpVersion + " 401 Nicht zugelassen\r\nWWW-Authenticate: Basic realm=\"" + auth_realm + "\"\r\nContent-type: text/html\r\nContent-Length: " + body.Length + "\r\n\r\n";//Connection: Close\r\n
                Answer(header+body);
            }

            public void TellNotFound()
            {
                request_string = "";
                string body = "<html><head><title>404 File not found</title></head><body><h1>404 File not found</h1><p>The resource was not found on this server; The client has specified an invalid resource.</p></body></html>";
                string header = MiniWebServer.HttpVersion + " 404 Not Found\r\nContent-type: text/html\r\nContent-Length: " + body.Length + "\r\n\r\n";//Connection: Close\r\n
                Answer(header + body);
                    
            }

            public void TellNewLocation(string url)
            {
                request_string = "";
                string body = "<html><head><title>302 Moved Temporarily</title></head><body><h1>302 Moved Temporarily</h1><p>The resource has been moved temporarily;If the client doesn't load the resource in a few seconds ,you may find it <a href=\"" + url + "\">here</a>.</p></body></html>";
                string header = MiniWebServer.HttpVersion + " 302 Moved Temporarily\r\nLocation: " + url + "\r\nContent-type: text/html\r\nContent-Length: " + body.Length + "\r\n\r\n";//Connection: Close\r\n
                Answer(header + body);
            }


            private int send_bytes_length = 0;

            private int file_answer_timeout = 25; 
            private int file_answer_buffer_size=2048;
            private FileStream file_answer_stream = null;
            private long file_real_filesize = -1;
            private long file_answer_transfered_bytes = 0;
            private int file_answer_header_length = 0;

            public void FileAnswer(string filename)
            {
                FileAnswer(filename,-1);
            }
            
            public void FileAnswer(string filename,long real_filesize)
            {


                if (socket != null && File.Exists(filename))
                {
                    if (!socket.Connected) return;
                    try
                    {
                        file_answer_stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (file_answer_stream.CanRead)
                        {
                            if (real_filesize == -1)
                                file_real_filesize = file_answer_stream.Length;
                            else file_real_filesize = real_filesize;
                            send_bytes_length = file_answer_buffer_size;
                            if (file_answer_stream.Length < send_bytes_length)
                                send_bytes_length = (int)file_answer_stream.Length;
                            if (file_real_filesize < send_bytes_length)
                                send_bytes_length = (int)file_real_filesize;
                            byte[] body_bytes = new byte[send_bytes_length];
                            file_answer_stream.Read(body_bytes, 0, send_bytes_length);
                            //send_bytes_length = send_bytes.Length;
                            //add header in front of the answer
                            //substract the header size from the first answer_callback bytes value
                            string type = MiniWebServer.GetMimeType(filename);
                            //Content-Disposition: attachment; filename=genome.jpeg;
                            //   Content-Disposition: inline
                            //   Content-Description: just a small picture of me
                            //Content-Disposition: inline; filename=\""+Path.GetFileName(filename)+"\";\r\n
                            string header_string = MiniWebServer.HttpVersion + " 200 OK\r\nSERVER: MiniWebServer\r\nContent-Type: " + type + "\r\nContent-Length: " + file_real_filesize + "\r\n\r\n";//nConnection: Close\r\n
                            byte[] header = System.Text.Encoding.Default.GetBytes(header_string);
                            file_answer_header_length = header.Length;
                            send_bytes_length = header.Length + send_bytes_length;
                            byte[] send_bytes = new byte[send_bytes_length];
                            header.CopyTo(send_bytes, 0);
                            body_bytes.CopyTo(send_bytes, header.Length);


                            socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(FileAnswerCallback), socket);
                        }
                        else TellNotFound();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error sending file answer to web client: " + e.Message);
                    }
                }
                else
                    TellNotFound();
            }

            protected void FileAnswerCallback(IAsyncResult ar)
            {
                Socket file_answer_socket = (Socket)ar.AsyncState;
                if (socket == null) return;
                try
                {
                    int bytes = file_answer_socket.EndSend(ar);
                    
                    //check if send_bytes_length == bytes ?

                    //add bytes to total number of transfered bytes
                    file_answer_transfered_bytes += bytes;
                    //check if total_number == real_filesize
                    if (file_answer_transfered_bytes == file_real_filesize+file_answer_header_length)
                    {
                        ResetRequest();
                        //lock (answering_lock)
                        //{
                            answering = false;
                        //}
                        return;
                    }
                    //read next values into temp buffer
                    //if no values available wait for 250ms periods until timeout was hit or
                    //new values could be read 
                    //if timeout happened just disconnect
                    send_bytes_length = 0;
                    int temp_timeout = 0;
                    byte[] send_bytes = new byte[file_answer_buffer_size];
                    while (send_bytes_length == 0 && temp_timeout < (file_answer_timeout * 1000))
                    {
                        send_bytes_length = file_answer_stream.Read(send_bytes, 0,file_answer_buffer_size);
                        if (send_bytes_length == 0)
                        {
                            Thread.Sleep(250);
                            temp_timeout += 250;
                        }
                    }
                    if (send_bytes_length == 0)
                    {//reading timed out
                        Disconnect();//TODO in keep alive connections maybe not the correct reaction
                    }
                    //send temp buffer
                    socket.BeginSend(send_bytes, 0, send_bytes_length, SocketFlags.None, new AsyncCallback(FileAnswerCallback), socket);


                    //Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("exception during send of file answer: " + ex.Message);
                }
            }

            public void Answer(byte[] send_bytes)
            {
                if (socket != null)
                {
                    if (!socket.Connected) return;
                    try
                    {
                        send_bytes_length = send_bytes.Length;
                        socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(AnswerCallback), socket);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error sending answer to web client: " + e.Message);
                    }
                }
            }

            public void Answer(byte[] content, string type)
            {
                string header_string = MiniWebServer.HttpVersion + " 200 OK\r\nSERVER: MiniWebServer\r\nContent-Type: " + type + "\r\nContent-Length: " + content.Length + "\r\n\r\n";//nConnection: Close\r\n
                byte[] header = System.Text.Encoding.Default.GetBytes(header_string);
                byte[] send_bytes = new byte[header.Length + content.Length];
                header.CopyTo(send_bytes, 0);
                content.CopyTo(send_bytes, header.Length);
                Answer(send_bytes);
            }

            public void Answer(string content, string type)
            {
                Answer(MiniWebServer.HttpVersion + " 200 OK\r\nSERVER: MiniWebServer\r\nContent-Type: " + type + "\r\nContent-Length: " + content.Length + "\r\n\r\n" + content);//Connection: Close\r\n
            }

            public void Answer(string answer)
            {
                byte[] send_bytes = System.Text.Encoding.Default.GetBytes(answer);
                Answer(send_bytes);
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
                    ResetRequest();
                    //lock (answering_lock)
                    //{
                        answering = false;
                    //}
                    //Console.WriteLine(send_bytes_length + " Bytes scheduled and "+bytes+" Bytes were delivered.");
                    //Disconnect();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("exception during send of answer: " + ex.Message);
                }
            }

            public delegate void RequestReceivedEventHandler(Client client, Request request);
            public event RequestReceivedEventHandler RequestReceived;

            private string ip;

            public string IP
            {
                get { return ip; }
                set { ip = value; }
            }
            private int port;

            public int Port
            {
                get { return port; }
                set { port = value; }
            }

            public Client(Socket client_socket)
            {
                this.socket = client_socket;
                this.ip = ((IPEndPoint)client_socket.RemoteEndPoint).Address.ToString();
                this.port = ((IPEndPoint)client_socket.RemoteEndPoint).Port;
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

            private string body;

            public string Body
            {
                get { return body; }
                set { body = value; }
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

            public class ValuePair
            {
                public string key = "";
                public string value = "";
            }

            private List<ValuePair> query_values = new List<ValuePair>();

            public List<ValuePair> QueryValues
            {
                get { return query_values; }
                set { query_values = value; }
            }

            private List<ValuePair> post_values = new List<ValuePair>();

            public List<ValuePair> PostValues
            {
                get { return post_values; }
                set { post_values = value; }
            }
	
            private WebHeaderCollection headers= new WebHeaderCollection();

            public WebHeaderCollection Headers
            {
                get { return headers; }
                set { headers = value; }
            }

            private string auth_username="";

            public string AuthUsername
            {
                get { return auth_username; }
                set { auth_username = value; }
            }


            private string auth_password="";

            public string AuthPassword
            {
                get { return auth_password; }
                set { auth_password = value; }
            }
	
            /// <summary>
            /// check the basic authentication header contents 
            /// against the specified credentials
            /// </summary>
            /// <param name="username">the username the client should have specified to fetch a resource</param>
            /// <param name="password">the password of the user</param>
            /// <returns>TRUE if credentials match</returns>
            public bool CheckAuthentication(string username, string password)
            {
                if (auth_username == username && auth_password == password)
                    return (true);
                return (false);
            }

	
            public Request(string header_string,string body_string,Client request_client)
            {
                if (request_client == null) 
                    is_valid = false;
                this.request_client = request_client;
                //Console.WriteLine("Initiating Request from request string: "+request_string);
                string[] seps = {"\r\n"};
                string[] header_lines = header_string.Split(seps,StringSplitOptions.None);
                if (header_lines.Length > 0)
                {
                    string[] seps2 ={ " " };
                    string[] header_line_parts = header_lines[0].Split(seps2, StringSplitOptions.RemoveEmptyEntries);
                    if (header_line_parts.Length >= 3)
                    {
                        method = header_line_parts[0];
                        url = header_line_parts[1];
                        version = header_line_parts[2];

                        int i = 0;
                        foreach (string line in header_lines) //i know i should skip the first line , but this is so convenient
                        {//read header values into our headers list
                            if (line == "\r\n")
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

                        //retrieve values from post body if type== x-www-form-url-encoded
                        //get query values from url if present ?
                        body = body_string;
                        //check if post and query values were given in body
                        string content_type = headers.Get("Content-Type");
                        if (content_type == "application/x-www-form-urlencoded")
                        {
                            //Console.WriteLine("body: " + body_string);
                            ParseValues(body_string, true);
                        }
                        //check if url contains query values
                        int query_start=url.IndexOf("?");
                        if (query_start != -1)
                        {
                            if (query_start + 1 < url.Length)
                            {
                                string query = url.Substring(query_start + 1);
                                ParseValues(query, false);
                            }
                            url = url.Substring(0, query_start);
                        }
                        

                        //check headers for basic authentification
                        string auth_value = headers.Get("Authorization");
                        if (!string.IsNullOrEmpty(auth_value))
                        {
                            string[] seps3 = { " "};
                            string[] fields = auth_value.Split(seps3,StringSplitOptions.RemoveEmptyEntries);
                            if (fields.Length == 2)
                            {
                                byte[] decode = Convert.FromBase64String(fields[1]);
                                string creds = System.Text.Encoding.Default.GetString(decode);
                                //Console.WriteLine("creds: "+creds);
                                int colon_pos2 = creds.IndexOf(":");
                                if (colon_pos2 != -1)
                                {
                                    auth_username = creds.Substring(0, colon_pos2);
                                    auth_password = creds.Substring(colon_pos2 + 1);
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

            private string GetValue(string key, List<ValuePair> values)
            {
                foreach (ValuePair pair in values)
                {
                    if (pair.key == key)
                        return (pair.value);
                }
                return ("");
            }

            public string GetPostValue(string key)
            {
                return (GetValue(key,post_values));
            }
            public string GetQueryValue(string key)
            {
                return (GetValue(key,query_values));
            }


            private void ParseValues(string value_string, bool are_post_values)
            {
                //Console.WriteLine("values: " + value_string);
                string[] seps = { "&", ";" };
                string[] pairs = value_string.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                foreach (string pair in pairs)
                {
                    string[] seps2 = { "=" };
                    string[] parts = pair.Split(seps2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {//found a key/value pair
                        ValuePair vp = new ValuePair();
                        vp.key = parts[0];
                        vp.value = UnEscape(parts[1]);

                        if (are_post_values)
                            post_values.Add(vp);
                        else
                            query_values.Add(vp);
                    }
                }
            }

            private string UnEscape(string url_encoded_string)
            {
                string decoded = "";
                int i = 0;
                while (i < url_encoded_string.Length)
                {
                    if (url_encoded_string[i] == '%')
                    {//found an encoded letter
                        i++;
                        string encoded = url_encoded_string.Substring(i,2);
                        i++;
                        int num = int.Parse(encoded, System.Globalization.NumberStyles.AllowHexSpecifier);
                        char c = (char)num;
                        decoded += c.ToString();
                    }
                    else decoded += url_encoded_string[i];
                    i++;
                }
                return (decoded);
            }

        }

        public delegate void RequestReceivedEventHandler(MiniWebServer server, Request request);
        public event RequestReceivedEventHandler RequestReceived;
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
        private string ip;

        public string IP
        {
            get { return ip; }
            set { ip = value; }
        }
        private int port=0;

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
        /// <summary>
        /// Mini Web Server Constructor
        /// </summary>
        public MiniWebServer()
        {
            UpdateIP();
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
                if (!e.Cancelled && e.Result != null)
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
            wc.Dispose();
            Console.WriteLine("Mini Web Server Request Test successful.");

        }
        /// <summary>
        /// Test to see if the mini web server acts expected when a request was received
        /// using the file answer method
        /// (running automatically)
        /// </summary>
        [Test]
        public void TestMiniWebServerAutoFileAnswerRequest()
        {
            Console.WriteLine("Test to see if requesting using the file answer method works.");
            MiniWebServer server = new MiniWebServer();
            server.Port = 80;
            server.SetupListeningSocket();
            long page_len = 0;
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
                    FileInfo fi = new FileInfo("..\\..\\..\\TestDateien\\test.mp3");
                    page_len = fi.Length;
                    request.RequestClient.FileAnswer("..\\..\\..\\TestDateien\\test.mp3");
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
                if (!e.Cancelled && e.Result != null)
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
            wc.Dispose();
            Console.WriteLine("Mini Web Server Request using File Answer Method Test successful.");

        }
        #endregion
    }
}
