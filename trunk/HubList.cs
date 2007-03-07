using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using ICSharpCode.SharpZipLib;
using System.IO;


// todo :
// maybe change it to not clear hubs on a fetch ..so hublists can add up
// maybe name,address,columns better be put in list< >
// maybe not ;-)

// add checks so we can use uncompressed lists too ;-)
// add async readxml handler
// maybe a fetching queue ... but why the heck.. an client app can handle that with multiple hublists

namespace DCPlusPlus
{
    /// <summary>
    /// a class to retrieve hublists from an url
    /// downloads the compressed hublist,
    /// uncompresses it ,interprets the xml contents
    /// and creates a list of Hub classes
    /// </summary>
    [TestFixture]
    public class HubList
    {
        /// <summary>
        /// Prototype for the Completed Event Handler
        /// </summary>
        /// <param name="hub_list">the hublist that completed a fetch</param>
        public delegate void CompletedEventHandler(HubList hub_list);
        /// <summary>
        /// Prototxype for teh Progress Changed Event Handler
        /// </summary>
        /// <param name="hub_list">the hublist which progress has changed</param>
        public delegate void ProgressChangedEventHandler(HubList hub_list);
        /// <summary>
        /// Prototype for the Unable To Fetch Event Handler
        /// </summary>
        /// <param name="hub_list">the hublist that was unable to fetch a request</param>
        public delegate void UnableToFetchEventHandler(HubList hub_list); //our new error handler
        protected Connection.ErrorCodes error_code = Connection.ErrorCodes.NoErrorYet;
        /// <summary>
        /// Get the error code of the failed Fetch
        /// </summary>
        public Connection.ErrorCodes ErrorCode
        {
            get
            {
                return (error_code);
            }
        }
        protected int percentage=0;
        /// <summary>
        /// Get the actual progress percentage of the fetch
        /// </summary>
        public int Percentage
        {
            get
            {
                return (percentage);
            }
        }
        protected string url;
        /// <summary>
        /// Get/Set the the url of the hublist
        /// </summary>
        public string Url
        {
            get
            {
                return (url);
            }
            set
            {
                url = value;
            }

        }
        protected List<Hub> hubs = new List<Hub>();
        /// <summary>
        /// A list of hubs of this hublist
        /// </summary>
        public List<Hub> Hubs
        {
            get
            {
                return (hubs);
            }

        }
        protected bool busy = false;
        /// <summary>
        /// TRUE if a fetch is currently in progress
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return (busy);
            }
        }
        protected string name = "";
        /// <summary>
        /// Get the name of the hublist
        /// </summary>
        public string Name
        {
            get
            {
                return (name);
            }
        }
        protected string address = "";
        /// <summary>
        /// get the address of the hublist ?? (why is there a field for this in a hublist?)
        /// </summary>
        public string Address
        {
            get
            {
                return (address);
            }
        }
        /// <summary>
        /// a class describing one column of a hublist
        /// </summary>
        public class Column
        {
            protected string name;
            /// <summary>
            /// Get/Set the header name of the column
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

            protected string type;
            /// <summary>
            /// Get/Set the type of the column
            /// </summary>
            public string Type
            {
                get
                {
                    return (type);
                }
                set
                {
                    type = value;
                }
            }
        }
        protected List<Column> columns= new List<Column>();
        /// <summary>
        /// Get a list of columns in this hublist
        /// </summary>
        public List<Column> Columns
        {
            get
            {
                return (columns);
            }

        }
        /// <summary>
        /// Event handler that gets called
        /// when a hublist fetch was completed successfully
        /// </summary>
        public event CompletedEventHandler Completed;
        /// <summary>
        /// Event handler that gets called
        /// when the progress of a fetch changed
        /// </summary>
        public event ProgressChangedEventHandler ProgressChanged;
        /// <summary>
        /// Event handler that gets called
        /// when a hublist fetch was unabble to complete
        /// </summary>
        public event UnableToFetchEventHandler UnableToFetch;
        /// <summary>
        /// our webclient instance that handles all the downloading work
        /// </summary>
        private WebClient wc = new WebClient();
        /// <summary>
        /// Hublist Constructor
        /// </summary>
        public HubList()
        {
            Url = "";
            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            wc.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCallback);
        }
        /// <summary>
        /// Hublist Constructor
        /// </summary>
        /// <param name="HubListUrl">initialize the instance with an url of the hublist to be fetched</param>
        public HubList(string HubListUrl)
        {
            Url = HubListUrl;
            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            wc.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCallback);
        }
        /// <summary>
        /// Start fetching a hublist
        /// needs url to point to a valid hublist to complete successfully
        /// [Non Blocking]
        /// </summary>
        public void FetchHubs()
        {
            if (!busy && !string.IsNullOrEmpty(url))
            {
                busy = true;
                //hubs.Clear();
                columns.Clear();
                percentage = 0;
                if (ProgressChanged != null)
                    ProgressChanged(this);
                try
                {
                    wc.DownloadDataAsync(new Uri(url));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured during download: " + ex.Message);
                    error_code = Connection.ErrorCodes.Exception;
                    if (UnableToFetch != null)
                        UnableToFetch(this);
                    busy = false;
                }
            }
        }
        /// <summary>
        /// Start fetching a hublist
        /// [Non Blocking]
        /// </summary>
        /// <param name="url">the url of the hublist</param>
        public void FetchHubs(string url)
        {
            this.url = url;
            FetchHubs();
        }
        /// <summary>
        /// Abort a fetch of a hublist
        /// </summary>
        public void AbortFetch()
        {
            try
            {
                wc.CancelAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured during abort: " + ex.Message);
            }
            
            busy = false;

        }
        /// <summary>
        /// Async callback for webclients get file operation
        /// ,gets called if the file was retrieved successfully
        /// </summary>
        /// <param name="sender">event sending webclient instance</param>
        /// <param name="e">event arguments of the download operation</param>
        private void DownloadFileCallback(object sender, DownloadDataCompletedEventArgs e)
        {
            try
            {
                if (e.Cancelled) return;
                if (e.Result == null || e.Result.Length == 0)
                {
                    error_code = Connection.ErrorCodes.UrlNotFound;
                    Console.WriteLine("Error downloading hublist.");
                    if (UnableToFetch != null)
                        UnableToFetch(this);
                    return;
                }
                //Console.WriteLine("Error:"+e.Error.Data.Values.ToString());
                //if its a bz2 uncompress the data stream
                //Stream input = new StreamReader(e.Result);
                byte[] input_bytes = e.Result;
                ProcessDownloadedBytes(input_bytes);
            }
            catch (Exception ex)
            {
                error_code = Connection.ErrorCodes.Exception;
                Console.WriteLine("exception during hublist fetch: " + ex.Message);
                if (UnableToFetch != null)
                    UnableToFetch(this);
                busy = false;
            }

        }
        /// <summary>
        /// Process the downloaded bytes from the webserver
        /// (uncompressing and reading of xml hublist)
        /// [non blocking]
        /// </summary>
        /// <param name="input_bytes">array of bytes received from a webserver</param>
        private void ProcessDownloadedBytes(byte[] input_bytes)
        {
            ProcessDownloadedBytesHandler pdbh = new ProcessDownloadedBytesHandler(ProcessDownloadedBytesAsync);
            IAsyncResult result = pdbh.BeginInvoke(input_bytes, new AsyncCallback(ProcessDownloadedBytesFinished), pdbh);
        }
        /// <summary>
        /// Private Prototype for the Process Downloaded Bytes Async Handler
        /// </summary>
        /// <param name="input_bytes">array of bytes received from a webserver</param>
        private delegate void ProcessDownloadedBytesHandler(byte[] input_bytes);
        /// <summary>
        /// Callback of Process Downloaded Bytes async operation
        /// </summary>
        /// <param name="result">Async Result/State</param>
        private void ProcessDownloadedBytesFinished(IAsyncResult result)
        {
            ProcessDownloadedBytesHandler pdbh = (ProcessDownloadedBytesHandler)result.AsyncState;
            pdbh.EndInvoke(result);
            busy = false;
            if (Completed != null)
                Completed(this);
        }
        /// <summary>
        /// Process the downloaded bytes from the webserver
        /// (uncompressing and reading of xml hublist)
        /// </summary>
        /// <param name="input_bytes">array of bytes received from a webserver</param>
        private void ProcessDownloadedBytesAsync(byte[] input_bytes)
        {
            MemoryStream input = new MemoryStream(input_bytes);
            MemoryStream output = new MemoryStream();
            ASCIIEncoding ascii = new ASCIIEncoding();
            UTF8Encoding utf = new UTF8Encoding();
            UnicodeEncoding unicode = new UnicodeEncoding();

            try
            {
                ICSharpCode.SharpZipLib.BZip2.BZip2.Decompress(input, output);
            }
            catch (Exception ex)
            {
                error_code = Connection.ErrorCodes.Exception;
                Console.WriteLine("Error uncompressing hublist: " + ex.Message);
                if (UnableToFetch != null)
                    UnableToFetch(this);
                busy = false;
                return;
            }
            input.Flush();
            byte[] out_data = output.GetBuffer();
            //string hubs_string = ascii.GetString(out_data);

            string hubs_string = System.Text.Encoding.Default.GetString(out_data);
            //string hubs_string = utf.GetString(out_data);
            int end = hubs_string.IndexOf((char)0);
            if (end != -1) hubs_string = hubs_string.Remove(end);
            //string hubs_string = unicode.GetString(out_data);
            for (int i = 0; i < 0x1f; i++)
                if (i != 0x09 && i != 0x0a && i != 0x0d) hubs_string = hubs_string.Replace((char)i, ' ');//"&#x00"+i+";"

            hubs_string = hubs_string.Replace("&", "&amp;");
            bool inside_quotes = false;
            for (int i = 0; i < hubs_string.Length; i++)
            {
                if (hubs_string[i] == '\"' && inside_quotes == false)
                {
                    inside_quotes = true;
                }
                else if (hubs_string[i] == '\"' && inside_quotes == true)
                {
                    inside_quotes = false;
                }

                if (inside_quotes && hubs_string[i] == '<')
                {
                    hubs_string = hubs_string.Remove(i, 1);
                    hubs_string = hubs_string.Insert(i, "&lt;");
                }

                if (inside_quotes && hubs_string[i] == '>')
                {
                    hubs_string = hubs_string.Remove(i, 1);
                    hubs_string = hubs_string.Insert(i, "&gt;");
                }

                if (inside_quotes && hubs_string[i] == '')
                {
                    hubs_string = hubs_string.Remove(i, 1);
                    hubs_string = hubs_string.Insert(i, " ");
                }

            }
            //hubs_string = hubs_string.Replace("&", "&amp;");
            //Console.WriteLine(hubs_string);
            //File.WriteAllText("hublist.uncompressed.xml", hubs_string);
            //output.Position = 0;
            ReadXmlStringAsync(hubs_string);
            /*busy = false;
            if (Completed != null)
                Completed(this);*/

        }
        /// <summary>
        /// Async callback for webclients get file operation
        /// ,gets called when the progress of the download changes
        /// </summary>
        /// <param name="sender">event sending webclient instance</param>
        /// <param name="e">event arguments of the download operation</param>
        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            percentage = e.ProgressPercentage;// TODO scale down to something like 70% if download has finished
            if (ProgressChanged != null)
                ProgressChanged(this);

        }
        /// <summary>
        /// Private Prototype for the Read Xml String Async Handler
        /// </summary>
        /// <param name="xml">the xml representation of a hublist</param>
        private delegate bool ReadXmlStringHandler(string xml);
        /// <summary>
        /// Read a hublist from a xml string
        /// </summary>
        /// <param name="xml">the xml representation of a hublist</param>
        private bool ReadXmlStringAsync(string xml)
        {
            XmlDocument doc = new XmlDocument();
            bool try_again = false;
            try
            {
                //Console.WriteLine("Starting to parse xml data.");
                doc.LoadXml(xml);
                //Console.WriteLine("Finished parsing.");

            }
            catch (XmlException xe)
            {

                string error_message = "Unexpected end of file has occurred. The following elements are not closed: ";
                if (xe.Message.StartsWith(error_message))
                {
                    string tags = xe.Message.Substring(error_message.Length);
                    int end = tags.IndexOf(".");
                    if (end != -1)
                    {
                        tags = tags.Substring(0, end);
                        int last = 0;
                        int split = 0;
                        while ((split = tags.IndexOf(",", split)) != -1)
                        {
                            string tag = tags.Substring(last, split);
                            last = split + 1;
                            split++;
                            //int line = xe.LineNumber;
                            tag = tag.Trim();
                            xml = xml + "</" + tag + ">\r\n";
                        }
                        string last_tag = tags.Substring(last);
                        last_tag = last_tag.Trim();
                        xml = xml + "</" + last_tag + ">\r\n";

                    }
                    try_again = true;
                }
                else
                {
                    error_code = Connection.ErrorCodes.Exception;
                    Console.WriteLine("xml exception:" + xe.Message);
                    if (UnableToFetch != null)
                        UnableToFetch(this);
                    busy = false;
                    return (false);
                }

            }

            if (try_again)
            {
                try
                {
                    //Console.WriteLine("Starting to parse xml data for a second time.");
                    doc.LoadXml(xml);
                    //Console.WriteLine("Finished parsing.");

                }
                catch (XmlException xe)
                {
                    Console.WriteLine("xml exception:" + xe.Message);
                    return (false);
                }


            }

            //TODO change this to a non xpath workarround version
            try
            {
                XmlNodeList nodelist = doc.SelectNodes("/");
                foreach (XmlNode node in nodelist)
                {
                    if (node.HasChildNodes)
                    {
                        foreach (XmlNode child in node.ChildNodes)
                        {
                            if (child.Name.Equals("Hublist")) ReadHubList(child);
                        }
                    }

                }

            }
            catch (XmlException xmle)
            {
                error_code = Connection.ErrorCodes.Exception;
                Console.WriteLine(xmle.Message);
                if (UnableToFetch != null)
                    UnableToFetch(this);
                busy = false;
                return (false);

            }

            return (true);
        }
        /// <summary>
        /// Read a hublist from a xml string
        /// [non blocking]
        /// </summary>
        /// <param name="xml">the xml representation of a hublist</param>
        /// <returns>TRUE if the xml string contained a valid hublist</returns>
        public void ReadXmlString(string xml)
        {
            ReadXmlStringHandler rxsh = new ReadXmlStringHandler(ReadXmlStringAsync);
            IAsyncResult result = rxsh.BeginInvoke(xml, new AsyncCallback(ReadXmlStringFinished), rxsh);
        }
        /// <summary>
        /// Callback of ReadXmlString async operation
        /// </summary>
        /// <param name="result">Async Result/State</param>
        private void ReadXmlStringFinished(IAsyncResult result)
        {
            ReadXmlStringHandler rxsh = (ReadXmlStringHandler)result.AsyncState;
            bool ret = rxsh.EndInvoke(result);
            busy = false;
            if (Completed != null)
                Completed(this);
        }
        /// <summary>
        /// Read hublist name and address
        /// and starts reading of the hubs tree
        /// </summary>
        /// <param name="node">xml node to begin reading from</param>
        /// <returns>reserved</returns>
        private bool ReadHubList(XmlNode node)
        {
                foreach (XmlAttribute attr in node.Attributes)
                {
                    //Console.WriteLine("attr:" + attr.Name + " - " + attr.Value);
                    if (attr.Name.Equals("Name")) name = attr.Value;
                    if (attr.Name.Equals("Address")) address = attr.Value;
                }

                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("Hubs")) ReadHubs(child);
                    }
                }

            return (true);
        }
        /// <summary>
        /// Read Hubs
        /// and starts reading of columns if present
        /// </summary>
        /// <param name="node">xml node to begin reading from</param>
        /// <returns>reserved</returns>
        private bool ReadHubs(XmlNode node)
        {
            //Console.WriteLine("Reading Hubs Tree");
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("Hub")) ReadHub(child);
                        if (child.Name.Equals("Columns")) ReadColumns(child);
                        Thread.Sleep(1);//simple way to decrease cpu lag of this large loop but at cost of time
                    }
                }

            return (true);
       }
        /// <summary>
        /// Read Columns
        /// </summary>
       /// <param name="node">xml node to begin reading from</param>
       /// <returns>reserved</returns>
        private bool ReadColumns(XmlNode node)
        {
            //Console.WriteLine("Reading Columns Tree");
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("Column")) ReadColumn(child);
                    }
                }

            return (true);
        }
        /// <summary>
        /// Read values of a single column
        /// </summary>
        /// <param name="node">xml node to read from</param>
        /// <returns>reserved</returns>
        private bool ReadColumn(XmlNode node)
        {
            //Console.WriteLine("Reading Column information");
            Column column = new Column();
            foreach (XmlAttribute attr in node.Attributes)
            {
                //Console.WriteLine("attr:" + attr.Name + " - " + attr.Value);
                if (attr.Name.Equals("Name")) column.Name = attr.Value;
                if (attr.Name.Equals("Type")) column.Type = attr.Value;
            }
            columns.Add(column);
            return (true);
        }
        /// <summary>
        /// Read values of a single hub
        /// </summary>
        /// <param name="node">xml node to read from</param>
        /// <returns>reserved</returns>
        private bool ReadHub(XmlNode node)
        {
            //Console.WriteLine("Reading Hub information");
            Hub hub = new Hub();
            foreach (XmlAttribute attr in node.Attributes)
            {
                try
                {
                    //Console.WriteLine("attr:" + attr.Name + " - " + attr.Value);
                    if (attr.Name.Equals("Name")) hub.Name = attr.Value;
                    if (attr.Name.Equals("Address")) hub.Address = attr.Value;
                    if (attr.Name.Equals("Description")) hub.Description = attr.Value;
                    if (attr.Name.Equals("Country")) hub.Country = attr.Value;
                    if (attr.Name.Equals("IP")) hub.IP = attr.Value;
                    if (attr.Name.Equals("Users") && !string.IsNullOrEmpty(attr.Value)) hub.Users = long.Parse(attr.Value);
                    if (attr.Name.Equals("Shared") && !string.IsNullOrEmpty(attr.Value)) hub.Shared = long.Parse(attr.Value);
                    if (attr.Name.Equals("Minshare") && !string.IsNullOrEmpty(attr.Value)) hub.MinShare = long.Parse(attr.Value);
                    if (attr.Name.Equals("Minslots") && !string.IsNullOrEmpty(attr.Value)) hub.MinSlots = int.Parse(attr.Value);
                    if (attr.Name.Equals("Maxhubs") && !string.IsNullOrEmpty(attr.Value)) hub.MaxHubs = int.Parse(attr.Value);
                    if (attr.Name.Equals("Maxusers") && !string.IsNullOrEmpty(attr.Value)) hub.MaxUsers = long.Parse(attr.Value);
                    if (attr.Name.Equals("Port") && !string.IsNullOrEmpty(attr.Value)) hub.Port = int.Parse(attr.Value);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception reading Hub-Values: "+e.Message);
                    return (false);
                }
            }
            bool unique = true;
            foreach (Hub search in hubs)
            {
                if (search.Address == hub.Address)
                {
                    //Console.WriteLine("duplicate hub entry found: "+hub.Name);
                    unique = false;
                }
            }
            if(unique)
                hubs.Add(hub);
            return (true);
        }

#region Unit Testing
        /// <summary>
        /// Test to see if a hublist download works as expected
        /// </summary>
        [Test]
        public void TestHubListDownload()
        {
            Console.WriteLine("Test to download a hublist.");
            bool wait = true;
            HubList hublist = new HubList("http://www.hublist.co.uk/hublist.xml.bz2");
            //HubList hublist = new HubList("http://www.hublist.org/PublicHubList.xml.bz2");
            hublist.Completed += delegate(HubList hub_list)
            {
                Console.WriteLine("");
                Console.WriteLine("Fetch Completed (Hubs found : " + hub_list.Hubs.Count + ")");
                wait = false;
            };
            hublist.FetchHubs();
            //hublist.FetchHubs("http://www.hublist.org/PublicHubList.xml.bz2"); 
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 30))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("Hublist Download Test successful.");

        }
        /// <summary>
        /// Test to see if a failed hublist download doesnt
        /// crash of throw uncatched exceptions
        /// </summary>
        [Test]
        public void TestHubListDownloadFailWrongUrl()
        {
            Console.WriteLine("Test to download a hublist (wrong url).");
            bool wait = true;
            HubList hublist = new HubList("http://ww.hublist.co.uk/hublist.xml.bz2");
            //HubList hublist = new HubList("http://www.hublist.org/PublicHubList.xml.bz2");
            hublist.Completed += delegate(HubList hub_list)
            {
                Console.WriteLine("");
                Console.WriteLine("Fetch Completed (Hubs found : " + hub_list.Hubs.Count + ")");
                Assert.Fail("Failed at failing ;-(");
            };

            hublist.UnableToFetch += delegate(HubList hub_list_unable)
            {
                Console.WriteLine("");
                Console.WriteLine("Unable to fetch hublist: "+ hub_list_unable.Address);
                wait = false;
            };
            hublist.FetchHubs();
            //hublist.FetchHubs("http://www.hublist.org/PublicHubList.xml.bz2"); 
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 30))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("Failed Hublist Download Test successful.");

        }
#endregion
    }
}
