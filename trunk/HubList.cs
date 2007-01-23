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


namespace DCPlusPlus
{


    [TestFixture]
    public class HubList
    {
        public delegate void CompletedEventHandler(HubList hub_list);
        public delegate void ProgressChangedEventHandler(HubList hub_list, int percentage);
        public delegate void UnableToFetchHandler(HubList hub_list, int ErrorCode, string ErrorMessage); //our new error handler
        

        protected string url;
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
        public List<Hub> Hubs
        {
            get
            {
                return (hubs);
            }

        }

        protected bool busy = false;
        public bool IsBusy
        {
            get
            {
                return (busy);
            }
        }

        protected string name = "";
        public string Name
        {
            get
            {
                return (name);
            }
        }

        protected string address = "";
        public string Address
        {
            get
            {
                return (address);
            }
        }

        public class Column
        {
            protected string name;
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
        public List<Column> Columns
        {
            get
            {
                return (columns);
            }

        }

        public event CompletedEventHandler Completed;
        public event ProgressChangedEventHandler ProgressChanged;
        public event ErrorEventHandler Error;

        private WebClient wc = new WebClient();
        
        public HubList()
        {
            Url = "";
            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            wc.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCallback);

        }

        public HubList(string HubListUrl)
        {
            Url = HubListUrl;
            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            wc.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCallback);
        }

        public void FetchHubs()
        {
            FetchHubs(Url);
        }

        public void FetchHubs(string Url)
        {
            if (!busy)
            {
                busy = true;
                //hubs.Clear();
                columns.Clear();
                if (ProgressChanged != null)
                    ProgressChanged(this, 0);
                try
                {
                    wc.DownloadDataAsync(new Uri(Url));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured during download: " + ex.Message);
                }

            }
        }

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

        private void DownloadFileCallback(object sender, DownloadDataCompletedEventArgs e)
        {
            if (e.Cancelled) return;
            //Console.WriteLine("Error:"+e.Error.Data.Values.ToString());
            //if its a bz2 uncompress the data stream
            //Stream input = new StreamReader(e.Result);
            MemoryStream input = new MemoryStream(e.Result);
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
                Console.WriteLine("Error uncompressing hublist: "+ex.Message);
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
                if(i!=0x09 && i!=0x0a && i!=0x0d)hubs_string = hubs_string.Replace((char)i, ' ');//"&#x00"+i+";"
            
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
                    hubs_string = hubs_string.Insert(i,"&lt;");
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
            ReadXmlString(hubs_string);
            busy = false;
            if (Completed != null)
                Completed(this);

        }

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
         /*
            Console.WriteLine("{0}    downloaded {1} of {2} bytes. {3} % complete...",
                (string)e.UserState,
                e.BytesReceived,
                e.TotalBytesToReceive,
                e.ProgressPercentage);
          */
            if (ProgressChanged != null)
                ProgressChanged(this, e.ProgressPercentage);

        }

        public bool ReadXmlString(string xml)
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
                    Console.WriteLine("xml exception:" + xe.Message);
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
                Console.WriteLine(xmle.Message);
                return (false);

            }
            
            return (true);
        }

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

        private bool ReadHubs(XmlNode node)
        {
            //Console.WriteLine("Reading Hubs Tree");
                if (node.HasChildNodes)
                {
                    foreach (XmlNode child in node.ChildNodes)
                    {
                        if (child.Name.Equals("Hub")) ReadHub(child);
                        if (child.Name.Equals("Columns")) ReadColumns(child);
                    }
                }

            return (true);
       }

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



#endregion
    }
}
