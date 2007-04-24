using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.IO;
using System.Timers;

//TODO add timed updating and event handler


namespace DCPlusPlus
{
    /// <summary>
    /// a very simple class to aggregate rss feed data
    /// supporting for now only Rss 2.0
    /// </summary>
    public class Rss
    {
        public class Channel
        {
            public class Item
            {
                public class TitledUrl
                {
                    private string title = "";

                    public string Title
                    {
                        get { return title; }
                        set { title = value; }
                    }
                    private string url = "";

                    public string Url
                    {
                        get { return url; }
                        set { url = value; }
                    }
                }
                public class RssEnclosure
                {
                    private string url = "";

                    public string Url
                    {
                        get { return url; }
                        set { url = value; }
                    }
                    private string content_type = "";

                    public string ContentType
                    {
                        get { return content_type; }
                        set { content_type = value; }
                    }
                    private long length=-1;

                    public long Length
                    {
                        get { return length; }
                        set { length = value; }
                    }
                }

                private string title = "";

                public string Title
                {
                    get { return title; }
                    set { title = value; }
                }
                private string description = "";

                public string Description
                {
                    get { return description; }
                    set { description = value; }
                }
                private string link = "";

                public string Link
                {
                    get { return link; }
                    set { link = value; }
                }
                private string author = "";

                public string Author
                {
                    get { return author; }
                    set { author = value; }
                }
                private List<string> categories= new List<string>();

                public List<string> Categories
                {
                    get { return categories; }
                    set { categories = value; }
                }
                private string comments = "";

                public string Comments
                {
                    get { return comments; }
                    set { comments = value; }
                }
                private string guid = "";

                public string GUID
                {
                    get { return guid; }
                    set { guid = value; }
                }
                private DateTime pub_date = DateTime.Now;

                public DateTime PubDate
                {
                    get { return pub_date; }
                    set { pub_date = value; }
                }
                private TitledUrl source= new TitledUrl();

                public TitledUrl Source
                {
                    get { return source; }
                    set { source = value; }
                }
                private RssEnclosure enclosure= new RssEnclosure();

                public RssEnclosure Enclosure
                {
                    get { return enclosure; }
                    set { enclosure = value; }
                }
            }

            public class RssImage
            {
                private string url;

                public string Url
                {
                    get { return url; }
                    set { url = value; }
                }
                private string title;

                public string Title
                {
                    get { return title; }
                    set { title = value; }
                }
                private string link;

                public string Link
                {
                    get { return link; }
                    set { link = value; }
                }
                private int width;

                public int Width
                {
                    get { return width; }
                    set { width = value; }
                }
                private int height;

                public int Height
                {
                    get { return height; }
                    set { height = value; }
                }
                private string description;

                public string Description
                {
                    get { return description; }
                    set { description = value; }
                }
            }

            private string title = "";

            public string Title
            {
                get { return title; }
                set { title = value; }
            }
            private string link = "";

            public string Link
            {
                get { return link; }
                set { link = value; }
            }
            private string description = "";

            public string Description
            {
                get { return description; }
                set { description = value; }
            }
            private string language = "";

            public string Language
            {
                get { return language; }
                set { language = value; }
            }
            private string copyright = "";

            public string Copyright
            {
                get { return copyright; }
                set { copyright = value; }
            }
            private DateTime pub_date= DateTime.Now;

            public DateTime PubDate
            {
                get { return pub_date; }
                set { pub_date = value; }
            }

            private RssImage image = new RssImage();

            public RssImage Image
            {
                get { return image; }
                set { image = value; }
            }

            private List<Item> items = new List<Item>();

            public List<Item> Items
            {
                get { return items; }
                set { items = value; }
            }
            private string managing_editor = "";

            public string ManagingEditor
            {
                get { return managing_editor; }
                set { managing_editor = value; }
            }
            private string webmaster = "";

            public string WebMaster
            {
                get { return webmaster; }
                set { webmaster = value; }
            }
            private DateTime last_build_date = DateTime.Now;

            public DateTime LastBuildDate
            {
                get { return last_build_date; }
                set { last_build_date = value; }
            }
            private string docs = "";

            public string Docs
            {
                get { return docs; }
                set { docs = value; }
            }
            private string generator = "";

            public string Generator
            {
                get { return generator; }
                set { generator = value; }
            }
            private int ttl = 350;

            public int TTL
            {
                get { return ttl; }
                set { ttl = value; }
            }
            private List<string> categories = new List<string>();

            public List<string> Categories
            {
                get { return categories; }
                set { categories = value; }
            }
        }
        private List<Channel> channels = new List<Channel>();

        public List<Channel> Channels
        {
            get { return channels; }
            set { channels = value; }
        }
        private string version = "";

        public string Version
        {
            get { return version; }
            set { version = value; }
        }
        private string url="";
        /// <summary>
        /// the url of the rss feed
        /// </summary>
        public string Url
        {
            get { return url; }
            set { url = value; }
        }
        private bool auto_relocation_check = false;

        public bool AutoRelocationCheck
        {
            get { return auto_relocation_check; }
            set { auto_relocation_check = value; }
        }

        private int update_interval = 1800; //default 30 minutes update interval

        public int UpdateInterval
        {
            get { return update_interval; }
            set { update_interval = value; }
        }

        /// <summary>
        /// Event handler that gets called
        /// when the feed was updated
        /// </summary>
        public event FeedUpdatedEventHandler FeedUpdated;
        /// <summary>
        /// Prototype for the Feed Updated Event Handler
        /// </summary>
        /// <param name="feed">the feed that was updated</param>
        public delegate void FeedUpdatedEventHandler(Rss feed);

        public void FireFeedUpdated()
        {
            if (FeedUpdated != null)
                FeedUpdated(this);
        }
        private WebClient wc;
        protected bool busy = false;
        /// <summary>
        /// Get the Status of the feed fetching
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return (busy);
            }
        }
        /// <summary>
        /// Get the feed channel information
        /// </summary>
        public void FetchFeed()
        {
            //get location url
            //interpret xml returned
            //and set values accordingly
            //find method urls of the router services
            Console.WriteLine("Fetching feed.");


            if (string.IsNullOrEmpty(url))
            {
                return; //a location is really all we need here,but if its not there just return
            }
            if (!busy)
            {
                wc = new WebClient();
                if (wc.IsBusy)
                    Console.WriteLine("Damn the client is already busy.. wtf ?");
                wc.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs e)
                {
                    Console.WriteLine("download completed");
                    try
                    {
                        if (e.Cancelled)
                        {
                            return;
                        }
                        if (e.Result.Length <= 0)
                        {
                            return;
                        }
                        string page_string = "";
                        page_string = System.Text.Encoding.Default.GetString(e.Result);
                        wc.Dispose();
                        ReadFeedFromXml(page_string);
                        Console.WriteLine("Feed read.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception after download: " + ex.Message);
                        return;
                    }
                };
                busy = true;
                try
                {
                    Console.WriteLine("starting download of: " + url);
                    wc.DownloadDataAsync(new Uri(url));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured during download: " + ex.Message);
                    busy = false;
                }

                if (update_timer != null)
                {
                    update_timer.Stop();
                    update_timer.Dispose();
                    update_timer.Close();
                }
                update_timer = new Timer(update_interval * 1000);
                update_timer.AutoReset = true;
                update_timer.Elapsed += delegate(object sender, ElapsedEventArgs e)
                {
                    FetchFeed();
                };
                update_timer.Start();
            }


        }
        public void FetchFeed(string rss_url)
        {
            url = rss_url;
            FetchFeed();
        }

        private void ReadFeedFromXml(string xml)
        {
            Console.WriteLine("getting channel");

            //string tmp = xml;
         /*   for(int i =0; i< xml.Length;i++)
            {
                if (xml[i] != 0x09 && xml[i] != 0x0a && xml[i] != 0x0d &&
                    (
                    (xml[i] < 0x020) || (xml[i] > 0x07e)//0x0d7ff) //||
                    //(xml[i] < 0x0e000) || (xml[i] > 0x0fffd) 
                    )
                    //|| ((xml[i] < 0x010000) || (xml[i] > 0x010ffff))
                    )
                    xml = xml.Remove(i,1);
                
            }
            */
            //Console.WriteLine("xml: "+xml);
            
            //xml = xml.Replace("&amp;", "$$$_amp;_$$$");
            //xml = xml.Replace("&", "&amp;");
            //xml = xml.Replace( "$$$_amp;_$$$","&amp;");



            XmlDocument doc = new XmlDocument();
            try
            {
                //Console.WriteLine("Starting to parse xml data.");
                doc.LoadXml(xml);
                XmlNodeList nodelist = doc.SelectNodes("/");
                foreach (XmlNode node in nodelist)
                {
                    if (node.HasChildNodes)
                    {
                        foreach (XmlNode child in node.ChildNodes)
                        {
                            if (child.Name.Equals("rss", StringComparison.CurrentCultureIgnoreCase)) ReadRss(child);
                        }
                    }
                }
                //Console.WriteLine("Finished parsing.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("error reading xml: " + ex.Message);
            }
            if (FeedUpdated != null)
                FeedUpdated(this);
        }

        private void ReadRss(XmlNode node)
        {

            foreach (XmlAttribute attr in node.Attributes)
            {
                if (attr.Name.Equals("version", StringComparison.CurrentCultureIgnoreCase)) version = attr.Value;
            }
            
            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name.Equals("channel", StringComparison.CurrentCultureIgnoreCase)) ReadChannel(child);
                }
            }
        }

        private void ReadChannel(XmlNode node)
        {
            Channel channel = new Channel();

            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name.Equals("title", StringComparison.CurrentCultureIgnoreCase)) channel.Title = child.InnerText;
                    if (child.Name.Equals("link", StringComparison.CurrentCultureIgnoreCase)) channel.Link = child.InnerText;
                    if (child.Name.Equals("description", StringComparison.CurrentCultureIgnoreCase)) channel.Description = child.InnerText;
                    if (child.Name.Equals("language", StringComparison.CurrentCultureIgnoreCase)) channel.Language = child.InnerText;
                    if (child.Name.Equals("copyright", StringComparison.CurrentCultureIgnoreCase)) channel.Copyright = child.InnerText;
                    if (child.Name.Equals("managingEditor", StringComparison.CurrentCultureIgnoreCase)) channel.ManagingEditor = child.InnerText;
                    if (child.Name.Equals("webMaster", StringComparison.CurrentCultureIgnoreCase)) channel.WebMaster = child.InnerText;
                    try
                    {
                        //if (child.Name.Equals("pubDate")) channel.PubDate = DateTime.Parse(child.InnerText);
                        //if (child.Name.Equals("lastBuildDate")) channel.LastBuildDate = DateTime.Parse(child.InnerText);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("error parsing channel pubDate/lastBuildDate: " + ex.Message);
                    }
                    if (child.Name.Equals("category", StringComparison.CurrentCultureIgnoreCase)) channel.Categories.Add(child.InnerText);
                    if (child.Name.Equals("generator", StringComparison.CurrentCultureIgnoreCase)) channel.Generator = child.InnerText;
                    if (child.Name.Equals("docs", StringComparison.CurrentCultureIgnoreCase)) channel.Docs = child.InnerText;
                    try
                    {
                        if (child.Name.Equals("ttl", StringComparison.CurrentCultureIgnoreCase)) channel.TTL = int.Parse(child.InnerText);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("error parsing channel ttl: " + ex.Message);
                    }

                    if (child.Name.Equals("image", StringComparison.CurrentCultureIgnoreCase)) ReadRssImage(child, channel);
                    if (child.Name.Equals("item", StringComparison.CurrentCultureIgnoreCase)) ReadItem(child, channel);
                }
            }
            channels.Add(channel);
        }

        private void ReadRssImage(XmlNode node,Channel channel)
        {
            Channel.RssImage image = new Channel.RssImage();
            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name.Equals("url", StringComparison.CurrentCultureIgnoreCase)) image.Url = child.InnerText;
                    if (child.Name.Equals("title", StringComparison.CurrentCultureIgnoreCase)) image.Title = child.InnerText;
                    if (child.Name.Equals("link", StringComparison.CurrentCultureIgnoreCase)) image.Link = child.InnerText;
                    try
                    {
                        if (child.Name.Equals("width", StringComparison.CurrentCultureIgnoreCase)) image.Width = int.Parse(child.InnerText);
                        if (child.Name.Equals("height", StringComparison.CurrentCultureIgnoreCase)) image.Height = int.Parse(child.InnerText);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("error parsing image width/height: " + ex.Message);
                        return;
                    }
                    if (child.Name.Equals("description", StringComparison.CurrentCultureIgnoreCase)) image.Description = child.InnerText;
                }
            }
            channel.Image = image;
        }

        private void ReadItem(XmlNode node,Channel channel)
        {
            Channel.Item item = new Channel.Item();
            if (node.HasChildNodes)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    if (child.Name.Equals("title", StringComparison.CurrentCultureIgnoreCase)) item.Title = child.InnerText;
                    if (child.Name.Equals("link", StringComparison.CurrentCultureIgnoreCase)) item.Link = child.InnerText;
                    if (child.Name.Equals("description", StringComparison.CurrentCultureIgnoreCase)) item.Description = child.InnerText;
                    if (child.Name.Equals("author", StringComparison.CurrentCultureIgnoreCase)) item.Author = child.InnerText;
                    if (child.Name.Equals("category", StringComparison.CurrentCultureIgnoreCase)) item.Categories.Add(child.InnerText);
                    if (child.Name.Equals("comments", StringComparison.CurrentCultureIgnoreCase)) item.Comments = child.InnerText;
                    if (child.Name.Equals("enclosure", StringComparison.CurrentCultureIgnoreCase))
                    {
                        Channel.Item.RssEnclosure enclosure = new Channel.Item.RssEnclosure();
                        foreach (XmlAttribute attr in child.Attributes)
                        {
                            if (attr.Name.Equals("url", StringComparison.CurrentCultureIgnoreCase))
                            {
                                enclosure.Url = attr.Value;
                                if (auto_relocation_check)
                                {//TODO check if this gets into gc soon after using it
                                    UrlRelocationCheck urc = new UrlRelocationCheck(enclosure.Url);
                                    urc.FoundRelocatedUrl += delegate(UrlRelocationCheck found_urc)
                                    {
                                        enclosure.Url = urc.RelocatedUrl;
                                    };
                                    urc.CheckUrl();
                                }
                            }

                            try
                            {
                                if (attr.Name.Equals("length", StringComparison.CurrentCultureIgnoreCase)) enclosure.Length = int.Parse(attr.Value);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error parsing enclosure length: " + ex.Message);
                            }
                            if (attr.Name.Equals("type", StringComparison.CurrentCultureIgnoreCase)) enclosure.ContentType = attr.Value;
                        }
                        item.Enclosure = enclosure;
                    }
                    if (child.Name.Equals("source",StringComparison.CurrentCultureIgnoreCase))
                    {
                        Channel.Item.TitledUrl source = new Channel.Item.TitledUrl();
                        foreach (XmlAttribute attr in child.Attributes)
                        {
                            if (attr.Name.Equals("url", StringComparison.CurrentCultureIgnoreCase)) source.Url = attr.Value;
                        }
                        source.Title = child.InnerText;
                        item.Source = source;
                    }
                    if (child.Name.Equals("guid", StringComparison.CurrentCultureIgnoreCase)) item.GUID = child.InnerText;
                    try
                    {
                        //if (child.Name.Equals("pubDate")) channel.PubDate = DateTime.ParseExact(child.InnerText, "ddd, dd MMM yyyy HH:mm:ss zzz", null);
                        //channel.PubDate = Convert.ToDateTime(child.InnerText); 
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("error parsing item pubDate("+child.InnerText+"): " + ex.Message);
                    }
                }
            }
            channel.Items.Add(item);
        }

        private Timer update_timer = null;

        public Rss(string rss_url, bool activate_relocation_check)
        {
            auto_relocation_check = activate_relocation_check;
            url = rss_url;
            FetchFeed();
        }
        
        public Rss(string rss_url)
        {
            url = rss_url;
            FetchFeed();
        }

        public Rss(bool activate_relocation_check)
        {
            auto_relocation_check = activate_relocation_check;
        }

        public Rss()
        {
            url = "";
        }
    }
}
