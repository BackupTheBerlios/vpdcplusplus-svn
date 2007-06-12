using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using System.Timers;

namespace DCPlusPlus
{

    /// <summary>
    /// it's sole purpose is to check urls for relocation header replies
    /// and change the url to the relocation url via an event
    /// </summary>
    [TestFixture]
    public class UrlRelocationCheck
    {

        //TODO switch arch to a queue based manager class to not overload connection attempts on xp machines

        public static List<UrlRelocationCheck> Queue = new List<UrlRelocationCheck>();
        private static object QueueLock = new object();
        public static int MaxAttemptsNum = 1;
        public static int Attempts = 0;
        private static object AttemptsLock = new object();
        private static Timer QueueTimer = null;
        public static int QueueUpdateInterval = 75;
        public static void StopQueueTimer()
        {
            if (QueueTimer != null)
            {
                QueueTimer.Stop();
                QueueTimer.Dispose();
                QueueTimer.Close();
                QueueTimer = null;
            }
        }

        //TODO add static close method and bool is_closing to make it impossible that any check will be done afterwards
        //TODO add retries of errror to connect


        private int retries = 0;

        public int Retries
        {
            get { return retries; }
            //set { retries = value; }
        }

        private int max_retries = 3;

        public int MaxRetries
        {
            get { return max_retries; }
            set { max_retries = value; }
        }


        private void CheckForRetry()
        {
            if (retries++ < max_retries)
            {
                CheckUrl();
            }
            else
            {
                if (UrlNotFound != null)
                    UrlNotFound(this);
                CleanUp();
            }

        }

        private void CleanUp()
        {
            FoundMimeType = null;
            FoundRelocatedUrl = null;
            UrlNotFound = null;
        }


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
        /// when an url was not found
        /// </summary>
        public event UrlNotFoundEventHandler UrlNotFound;
        /// <summary>
        /// Prototype for the Url Not Found Event Handler
        /// </summary>
        public delegate void UrlNotFoundEventHandler(UrlRelocationCheck url_checker);
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
            //check if attempt<max_attempts
            //if not add urc to queue
            //and start a timer until there are no open urc's left
            //
            //if attempts slot left just connect
            /*
            if (QueueTimer != null)
            {
                QueueTimer.Stop();
                QueueTimer.Dispose();
                QueueTimer.Close();
            }
            */
            if (url == "")
            {
                Console.WriteLine("empty url to check ?");
                return;
            }
            bool attempts_left = false;
            lock (AttemptsLock)
            {
                if (Attempts < MaxAttemptsNum)
                    attempts_left = true;
            }
            if (attempts_left)
            {
                Attempts++;
            }
            else
            {
                lock (QueueLock)
                {
                    Queue.Add(this);
                }
                if (QueueTimer == null)
                {
                    Console.WriteLine("Attempts overflow: starting queue timer");
                    QueueTimer = new Timer(QueueUpdateInterval);
                    QueueTimer.AutoReset = true;
                    QueueTimer.Elapsed += delegate(object sender, ElapsedEventArgs e)
                    {//an interesting question is if this block is locked ? ;-)
                        //Console.WriteLine("queue timer interval elapsed\nAttempts running: " + Attempts + "\nItems in Queue: " + Queue.Count);
                        lock (QueueLock)
                        {
                            if (Attempts < MaxAttemptsNum && Queue.Count > 0)
                            {
                                //now use up all free slots by activating new checks and increase the attempts num
                                int end = MaxAttemptsNum - Attempts;
                                if (end > Queue.Count)
                                    end = Queue.Count;
                                //Console.WriteLine("checking " + end + " queue items.");
                                for (int i = 0; i < end; i++)
                                {
                                    //lock (AttemptsLock)
                                    //{
                                    Attempts++;
                                    //}
                                    Queue[i].Connect();
                                }
                                Queue.RemoveRange(0, end);
                            }
                            if (Queue.Count == 0)
                            {
                                Console.WriteLine("finished all urls in the relocation check queue.\nStopping Timer.");
                                QueueTimer.Stop();
                                QueueTimer.Dispose();
                                QueueTimer.Close();
                                QueueTimer = null;
                                //GC.Collect();
                            }
                        }
                    };
                    QueueTimer.Start();
                }
                return;
            }
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
                    string request = "GET " + uri.PathAndQuery + " HTTP/1.1\r\n";
                    //string request = "HEAD " + uri.PathAndQuery + " HTTP/1.0\r\n";
                    //string request = "HEAD " + uri.PathAndQuery + " HTTP/1.1\r\n";
                    //request += "Host: "+uri.Host+":"+uri.Port+"\r\n";
                    request += "Host: "+uri.Host+"\r\n";
                    request += "User-Agent: Wget/1.10.2\r\n";
                    request += "Accept: */*\r\n";
                    request += "Connection: Close\r\n";
                    request += "\r\n";
                    byte[] send_bytes = System.Text.Encoding.Default.GetBytes(request);
                    //send_bytes_length = send_bytes.Length;
                    socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(SendRequestCallback), socket);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error sending request to web server: " + e.Message);
                    Disconnect();
                    CheckForRetry();
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
                Disconnect();
                CheckForRetry();
            }
        }

        private void Disconnect()
        {
            disconnected = true;
            if (socket != null)
            {
                socket.Close();
            }
            //else Console.WriteLine("This socket is unused -> no disconnect needed.");
            socket = null;
            is_busy = false;
            reply_string = "";
            //lock (AttemptsLock)
            //{
                Attempts--;
            //}

        }
        private void Connect()
        {
            if (is_busy)
            {
                return;
                //Disconnect();
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
                    CheckForRetry();
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
                    CheckForRetry();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during Address resolve of web server (address:" + url + "): "+ex.Message);
                Disconnect();
                CheckForRetry();
            }
        }
        private void OnConnect(IAsyncResult result)
        {
            if (socket == null) return;
            Socket connect_socket = (Socket)result.AsyncState;
            try
            {
                if (connect_socket.Connected)
                {
                    disconnected = false;
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_buffer = new byte[4096];
                    connect_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, connect_socket);
                    SendRequest();
                    //Console.WriteLine("Successfully connected to web server: " + url);
                }
                else
                {
                    Console.WriteLine("Error during connect to url: " + url);
                    Disconnect();
                    CheckForRetry();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during connect to url: " + url + "(exception:" + ex.Message + ").");
                Disconnect();
                CheckForRetry();
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

                            if (header_string.StartsWith("HTTP/1.1 3", StringComparison.CurrentCultureIgnoreCase) || header_string.StartsWith("HTTP/1.0 3", StringComparison.CurrentCultureIgnoreCase)) 
                            //if (header_string.StartsWith("HTTP/1.1 302") || header_string.StartsWith("HTTP/1.0 302") || header_string.StartsWith("HTTP/1.1 301") || header_string.StartsWith("HTTP/1.0 301"))
                            {
                                string[] seps = { "\r\n" };
                                string[] lines = header_string.Split(seps, StringSplitOptions.None);
                                foreach (string line in lines)
                                {
                                    if (line.StartsWith("Location:", true, null))
                                    {
                                        relocated_url = line.Substring("Location:".Length).Trim();
                                    }
                                    /*if (line.StartsWith("Content-Type:", true, null))
                                    {
                                        mime_type = line.Substring("Content-Type:".Length).Trim();
                                    }*/
                                }
                                if (relocated_url != "")
                                {
                                    //now check if the relocated url is relative 
                                    //and if add the request host to the beginning of the string
                                    if (relocated_url.StartsWith("/"))
                                    {
                                        try
                                        {
                                            Uri uri = new Uri(url);
                                            //Uri uri2 = new Uri(relocated_url);
                                            //if (!uri2.IsAbsoluteUri)
                                            //{
                                            relocated_url = "http://" + uri.Host + ":" + uri.Port + relocated_url;
                                            //  else 
                                            //      relocated_url = uri.Host + ":" + uri.Port + uri.AbsolutePath + "/" + relocated_url;
                                            //}

                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("error building url: " + ex.Message);

                                        }
                                    }
                                    string temp_url = relocated_url;
                                    if (FoundRelocatedUrl != null)
                                        FoundRelocatedUrl(this);
                                    url = temp_url;
                                    if (url == "")
                                        Console.WriteLine("WTF ????");
                                    relocated_url = "";
                                    mime_type = "";
                                    CheckUrl();
                                }


                                /*else if (mime_type != "")
                                {
                                    if (FoundMimeType != null)
                                        FoundMimeType(this);
                                }*/
                            }
                            else if (header_string.StartsWith("HTTP/1.1 2", StringComparison.CurrentCultureIgnoreCase) || header_string.StartsWith("HTTP/1.0 2", StringComparison.CurrentCultureIgnoreCase))
                            {
                                string[] seps = { "\r\n" };
                                string[] lines = header_string.Split(seps, StringSplitOptions.None);
                                foreach (string line in lines)
                                {
                                    if (line.StartsWith("Content-Type:", true, null))
                                    {
                                        mime_type = line.Substring("Content-Type:".Length).Trim();
                                    }
                                }
                                if (mime_type != "")
                                {
                                    if (FoundMimeType != null)
                                        FoundMimeType(this);
                                }

                                CleanUp();

                            }

                            //if (mime_type != "")
                            //    Console.WriteLine("Found this mime-type: " + mime_type);
                            //if (relocated_url != "")
                            //    Console.WriteLine("Found this relocated url: " + relocated_url);
                            //else Console.WriteLine("no relocation or no mime type detected.");
                            Disconnect();
                            return;
                        }

                        AsyncCallback event_receive = new AsyncCallback(OnReceive);
                        receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                    }
                    else
                    {
                        Console.WriteLine("Connection to web server dropped.");
                        Disconnect();
                        CheckForRetry();
                    }
                }
                else
                {
                    Console.WriteLine("web server not connected.");
                    Disconnect();
                    CheckForRetry();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception during receive of data from web server: " + ex.Message);
                Disconnect();
                CheckForRetry();
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
        /// <summary>
        /// the url relocation check constructor
        /// </summary>
        /// <param name="check_url">the url to check for relocation</param>
        /// <param name="handler">the handler to call when a relocation was determined</param>
        /// <param name="mime_handler">the handler to call when a mime type was determined</param>
        public UrlRelocationCheck(string check_url, FoundRelocatedUrlEventHandler handler, FoundMimeTypeEventHandler mime_handler)
        {
            url = check_url;
            FoundRelocatedUrl += handler;
            FoundMimeType += mime_handler;
            CheckUrl();
        }

        #region Unit Testing
        //TODO add mime test and bugfix mime recognition

        /// <summary>
        /// Test to see if our url relocation check works
        /// </summary>
        [Test]
        public void TestUrlRelocationCheck()
        {
            Console.WriteLine("Test to check if url relocation works.");
            bool wait = true;
            UrlRelocationCheck urc = new UrlRelocationCheck("http://www.podtrac.com/pts/redirect.mp3/aolradio.podcast.aol.com/sn/SN-078.mp3");
            urc.FoundRelocatedUrl += delegate(UrlRelocationCheck urc_found)
            {
                Console.WriteLine("");
                Console.WriteLine("Url Relocation Check Completed (" + urc_found.RelocatedUrl + ")");
                /*if (urc_found.Url != urc_found.RelocatedUrl)
                {
                    urc_found.Url = urc_found.RelocatedUrl;
                    urc_found.MimeType = "";
                    urc_found.RelocatedUrl = "";
                    urc_found.CheckUrl();
                }*/
                Assert.IsTrue(urc_found.RelocatedUrl == "http://aolradio.podcast.aol.com/sn/SN-078.mp3", "wrong relocation url found.");
                wait = false;
            };
            urc.CheckUrl();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 35))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                System.Threading.Thread.Sleep(250);
            }
            Assert.IsTrue(UrlRelocationCheck.Queue.Count == 0, "queue still containing entries.");
            Assert.IsTrue(UrlRelocationCheck.Attempts == 0, "attempts still running.");
            Console.WriteLine("Url Relocation Check Test successful.");
        }
        /// <summary>
        /// Test to see if our url relocation check works with a wrong url
        /// </summary>
        [Test]
        public void TestUrlRelocationCheckWrongUrl()
        {
            Console.WriteLine("Test to check if url relocation works with a wrong url.");
            bool wait = true;
            UrlRelocationCheck urc = new UrlRelocationCheck("http://www.podt.cmpt/SN-078.mp3");
            urc.FoundRelocatedUrl += delegate(UrlRelocationCheck urc_found)
            {
                Console.WriteLine("");
                Console.WriteLine("Url Relocation Check Completed (" + urc_found.RelocatedUrl + ")");
                /*if (urc_found.Url != urc_found.RelocatedUrl)
                {
                    urc_found.Url = urc_found.RelocatedUrl;
                    urc_found.MimeType = "";
                    urc_found.RelocatedUrl = "";
                    urc_found.CheckUrl();
                }*/
                //Assert.IsTrue(urc_found.RelocatedUrl == "http://aolradio.podcast.aol.com/sn/SN-078.mp3", "wrong relocation url found.");
                //wait = false;
            };
            urc.UrlNotFound += delegate(UrlRelocationCheck urc_not_found)
            {
                Console.WriteLine("");
                Console.WriteLine("Url Not Found check completed (" + urc_not_found.Url + ")");
                wait = false;
            };
            urc.CheckUrl();
            Console.WriteLine("Waiting for data");
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
                System.Threading.Thread.Sleep(250);
            }
            Console.WriteLine("running attempts: " + UrlRelocationCheck.Attempts + " still in queue: " + UrlRelocationCheck.Queue.Count);
            Assert.IsTrue(UrlRelocationCheck.Queue.Count == 0, "queue still containing entries.");
            Assert.IsTrue(UrlRelocationCheck.Attempts == 0, "attempts still running.");
            Console.WriteLine("Wrong Url Relocation Check Test successful.");
        }
        /// <summary>
        /// Test to see if our url relocation check works with a non relocated url
        /// </summary>
        [Test]
        public void TestUrlRelocationCheckNonRelocationUrl()
        {
            Console.WriteLine("Test to check if url relocation works with a non relocation url.");
            bool wait = true;
            UrlRelocationCheck urc = new UrlRelocationCheck("http://freshmeat.net/");
            urc.FoundRelocatedUrl += delegate(UrlRelocationCheck urc_found)
            {
                Console.WriteLine("");
                Console.WriteLine("Url Relocation Check Completed (" + urc_found.RelocatedUrl + ")");
                /*if (urc_found.Url != urc_found.RelocatedUrl)
                {
                    urc_found.Url = urc_found.RelocatedUrl;
                    urc_found.MimeType = "";
                    urc_found.RelocatedUrl = "";
                    urc_found.CheckUrl();
                }*/
                //Assert.IsTrue(urc_found.RelocatedUrl == "http://aolradio.podcast.aol.com/sn/SN-078.mp3", "wrong relocation url found.");
                wait = false;
            };
            urc.CheckUrl();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 40))
                {
                    Console.WriteLine("");
                    //Console.WriteLine("Operation took too long");
                    wait = false;
                    //Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                System.Threading.Thread.Sleep(250);
            }
            Console.WriteLine("running attempts: " + UrlRelocationCheck.Attempts + " still in queue: " + UrlRelocationCheck.Queue.Count);
            Assert.IsTrue(UrlRelocationCheck.Queue.Count == 0, "queue still containing entries.");
            Assert.IsTrue(UrlRelocationCheck.Attempts == 0, "attempts still running.");
            Console.WriteLine("Non Relocation Url Relocation Check Test successful.");
        }
        /// <summary>
        /// Test to see if our url relocation check under regression works
        /// </summary>
        [Test]
        public void RegressionTestUrlRelocationCheck()
        {
            int max_num = 1000;
            Console.WriteLine("Regression Test to check if url relocation works.");
            int wait = 0;
            List<UrlRelocationCheck> urc_list = new List<UrlRelocationCheck>();
            for (int i = 0; i < max_num; i++)
            {
                UrlRelocationCheck urc = new UrlRelocationCheck("http://www.podtrac.com/pts/redirect.mp3/aolradio.podcast.aol.com/sn/SN-078.mp3");
                urc.FoundRelocatedUrl += delegate(UrlRelocationCheck urc_found)
                {
                    Console.WriteLine("");
                    Assert.IsTrue(urc_found.RelocatedUrl == "http://aolradio.podcast.aol.com/sn/SN-078.mp3", "wrong relocation url found.");
                    wait++;
                    Console.WriteLine("Url Relocation Check " + wait + " of " + max_num + " Completed (" + urc_found.RelocatedUrl + ")");
                    /*if (urc_found.Url != urc_found.RelocatedUrl)
                    {
                        urc_found.Url = urc_found.RelocatedUrl;
                        urc_found.MimeType = "";
                        urc_found.RelocatedUrl = "";
                        urc_found.CheckUrl();
                    }*/
                };
                urc_list.Add(urc);
            }
            for (int i = 0; i < max_num; i++)
            {
                urc_list[i].CheckUrl();
            }
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait < max_num)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, max_num / 4))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long -> " + wait + " of " + max_num + " tests (running attempts: " + UrlRelocationCheck.Attempts + " still in queue: " + UrlRelocationCheck.Queue.Count + ")completed");
                    wait = max_num;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                System.Threading.Thread.Sleep(250);
            }
            Assert.IsTrue(UrlRelocationCheck.Queue.Count == 0, "queue still containing entries.");
            Assert.IsTrue(UrlRelocationCheck.Attempts == 0, "attempts still running.");
            Console.WriteLine("Url Relocation Check Regression Test successful.");
        }
        /// <summary>
        /// Test to see if our url relocation check under regression x2 works
        /// </summary>
        [Test]
        public void DoubleRegressionTestUrlRelocationCheck()
        {
            int max_num = 1000;
            Console.WriteLine("Double Regression Test to check if url relocation works.");
            int wait = 0;
            List<UrlRelocationCheck> urc_list = new List<UrlRelocationCheck>();
            for (int i = 0; i < max_num; i++)
            {
                UrlRelocationCheck urc = new UrlRelocationCheck("http://www.podtrac.com/pts/redirect.mp3/aolradio.podcast.aol.com/sn/SN-078.mp3");
                urc.FoundRelocatedUrl += delegate(UrlRelocationCheck urc_found)
                {
                    Console.WriteLine("");
                    Assert.IsTrue(urc_found.RelocatedUrl == "http://aolradio.podcast.aol.com/sn/SN-078.mp3", "wrong relocation url found.");
                    wait++;
                    Console.WriteLine("Url Relocation Check " + wait + " of " + max_num + " Completed (" + urc_found.RelocatedUrl + ")");
                    /*if (urc_found.Url != urc_found.RelocatedUrl)
                    {
                        urc_found.Url = urc_found.RelocatedUrl;
                        urc_found.MimeType = "";
                        urc_found.RelocatedUrl = "";
                        urc_found.CheckUrl();
                    }*/
                };
                urc_list.Add(urc);
            }
            for (int i = 0; i < max_num / 4; i++)
            {
                urc_list[i].CheckUrl();
            }
            int remaining = (max_num / 4) * 3;
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait < max_num)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, max_num / 4))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long -> " + wait + " of " + max_num + " tests (running attempts: " + UrlRelocationCheck.Attempts + " still in queue: " + UrlRelocationCheck.Queue.Count + ")completed");
                    wait = max_num;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                if (remaining > 0)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        urc_list[i].CheckUrl();
                        remaining--;
                    }
                }
                System.Threading.Thread.Sleep(50);
            }
            Assert.IsTrue(UrlRelocationCheck.Queue.Count == 0, "queue still containing entries.");
            Assert.IsTrue(UrlRelocationCheck.Attempts == 0, "attempts still running.");
            Console.WriteLine("Url Relocation Check Double Regression Test successful.");
        }
        #endregion
    }
}
