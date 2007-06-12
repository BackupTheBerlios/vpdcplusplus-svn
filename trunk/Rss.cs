using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.IO;
using System.Timers;
using NUnit.Framework;


namespace DCPlusPlus
{
    /// <summary>
    /// a very simple class to aggregate rss feed data
    /// supporting for now only Rss 2.0
    /// </summary>
    [TestFixture]
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
        /// when the feed not read/found
        /// </summary>
        public event FeedNotReadEventHandler FeedNotRead;
        /// <summary>
        /// Prototype for the Feed Not Read/Found Event Handler
        /// </summary>
        /// <param name="feed">the feed that was not read/found</param>
        public delegate void FeedNotReadEventHandler(Rss feed);
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

        /// <summary>
        /// Event handler that gets called
        /// when a channel in the feed was updated
        /// </summary>
        public event ChannelUpdatedEventHandler ChannelUpdated;
        /// <summary>
        /// Prototype for the Channel Updated Event Handler
        /// </summary>
        /// <param name="feed">the feed that was updated</param>
        /// <param name="channel">the channel that was updated</param>
        public delegate void ChannelUpdatedEventHandler(Rss feed, Channel channel);
        /// <summary>
        /// Event handler that gets called
        /// when a channel in the feed was added
        /// </summary>
        public event ChannelAddedEventHandler ChannelAdded;
        /// <summary>
        /// Prototype for the Channel Added Event Handler
        /// </summary>
        /// <param name="feed">the feed the channel belongs to</param>
        /// <param name="channel">the channel that was added</param>
        public delegate void ChannelAddedEventHandler(Rss feed, Channel channel);
        /// <summary>
        /// Event handler that gets called
        /// when an item was added to the feed 
        /// </summary>
        public event ItemAddedEventHandler ItemAdded;
        /// <summary>
        /// Prototype for the Item Added Event Handler
        /// </summary>
        /// <param name="feed">the feed the item belongs to</param>
        /// <param name="channel">the channel the item belongs to</param>
        /// <param name="item">the item that was added</param>
        public delegate void ItemAddedEventHandler(Rss feed, Channel channel,Channel.Item item);
        private System.Threading.Thread reading_thread = null;
        public void StopReading()
        {
            if (reading_thread != null)
            {
                reading_thread.Abort();
                reading_thread = null;
            }
        }

        public void CleanUp()
        {
            StopReading();
            FeedNotRead = null;
            FeedUpdated = null;
            ChannelUpdated = null;
            ChannelAdded = null;
            ItemAdded = null;
            if (update_timer != null)
            {
                update_timer.Stop();
                update_timer.Dispose();
                update_timer.Close();
            }
            Console.WriteLine("Cleaned up Rss Feed");
        }

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
        private bool feed_updated = false;

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
                FetchFeed();
            }
            else
            {
                if (FeedNotRead != null)
                    FeedNotRead(this);
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
            if (string.IsNullOrEmpty(url))
                return; //a location is really all we need here,but if its not there just return
            if (!busy)
            {
                feed_updated = false;
                //Console.WriteLine("Fetching feed.");
                wc = new WebClient();
                if (wc.IsBusy)
                    Console.WriteLine("Damn the client is already busy.. wtf ?");
                wc.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs e)
                {
                    //Console.WriteLine("download completed");
                    try
                    {
                        if (e.Cancelled)
                        {
                            CheckForRetry();
                            return;
                        }
                        if (e.Result == null || e.Result.Length <= 0)
                        //if (e.Result.Length <= 0)
                            {
                                CheckForRetry();
                                return;
                            }
                        string page_string = "";
                        page_string = System.Text.Encoding.Default.GetString(e.Result);
                        wc.Dispose();
                        if(page_string=="")
                        {
                            CheckForRetry();
                            return;
                        }
                        reading_thread = new System.Threading.Thread(delegate()
                        {
                            if (ReadFeedFromXml(page_string))
                            {
                                //Console.WriteLine("Feed read.");
                                busy = false;
                                retries = 0;
                                if (FeedUpdated != null && feed_updated)
                                    FeedUpdated(this);
                            }
                            else
                            {
                                Console.WriteLine("Feed not read.");
                                busy = false;
                                CheckForRetry();
                            }
                            reading_thread = null;
                        });
                        reading_thread.Start();

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception after download: " + ex.Message);
                        CheckForRetry();
                        return;
                    }
                };
                busy = true;
                try
                {
                    //Console.WriteLine("starting download of: " + url);
                    wc.DownloadDataAsync(new Uri(url));
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured during download: " + ex.Message);
                    busy = false;
                    CheckForRetry();
                    return;
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

        private bool ReadFeedFromXml(string xml)
        {
            //Console.WriteLine("getting channel");

            //some simple validity check

            if (xml.IndexOf("rss", StringComparison.CurrentCultureIgnoreCase) == -1)
                return (false);
            if (xml.IndexOf("channel", StringComparison.CurrentCultureIgnoreCase) == -1)
                return (false);

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
                return (false);
            }
            return (true);
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
                    if (child.Name.Equals("title", StringComparison.CurrentCultureIgnoreCase)) channel.Title =child.InnerText;
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

            Channel found = ChannelExists(channel);
            if (found != null)  //check if the channel is already in our list
            {
                //if updating an already existing channel
                //check each item seperately and compare if possible
                //by guid (if no guid exists check by item fields)
                //add unique new items to the existing channel
                //using insert(0);to put it in front
                //fire new_item and the channel_updated event
                //
                if (ChannelHasGuids(found) && ChannelHasGuids(channel))
                {
                    //easy money we can compare each item by its guid
                    bool channel_updated = false;
                    foreach (Channel.Item item in channel.Items)
                    {
                        //we now compare each item of channel against the found channels items
                        //by guid
                        if (ChannelItemExistsByGuid(found, item.GUID))
                        {
                            //this item already exist no adding needed
                            //Console.WriteLine("the channel already has an item with that guid");

                        }
                        else
                        {
                            //this is a new item , add and set feed_updated/channel_updated to true
                            channel_updated = true;
                            feed_updated = true;
                            found.Items.Insert(0, item);
                            if (ItemAdded != null)
                                ItemAdded(this, found, item);
                        }

                    }
                    if (ChannelUpdated != null && channel_updated)
                        ChannelUpdated(this, found);
                }
                else
                {
                    //damn have to spend some money to compare by every single property of the item
                    bool channel_updated = false;
                    foreach (Channel.Item item in channel.Items)
                    {
                        //we now compare each item of channel against the found channels items
                        //by guid
                        if (ChannelItemExistsByEtc(found, item))
                        {
                            //this item already exist no adding needed
                            //Console.WriteLine("the channel already has an item alike");
                        }
                        else
                        {
                            //this is a new item , add and set feed_updated/channel_updated to true
                            channel_updated = true;
                            feed_updated = true;
                            found.Items.Insert(0, item);
                            //Console.WriteLine("Adding new item: "+item.Title);
                            if (ItemAdded != null)
                                ItemAdded(this, found, item);
                        }

                    }
                    if (ChannelUpdated != null && channel_updated)
                        ChannelUpdated(this, found);

                    channel.Items.Clear();
                    channel = null;
                }


            }
            else
            {
                //if new just add and fire the new_channel event
                feed_updated = true;
                channels.Add(channel);
                if (ChannelAdded != null)
                    ChannelAdded(this, channel);
            }
            


        }
        /// <summary>
        /// Checks if a channel has an item like the item specified
        /// </summary>
        /// <param name="channel">the channel which items to check</param>
        /// <param name="item">the item to compare with</param>
        /// <returns>TRUE if the channel has an item alike the item specified</returns>
        private bool ChannelItemExistsByEtc(Channel channel,Channel.Item item)
        {
            //TODO change to ignore cases
            foreach (Channel.Item i in channel.Items)
            {
                if ( (i.Title == item.Title) 
                    //&& (i.PubDate == item.PubDate) 
                    && (i.Link == item.Link) 
                    && (i.Description == item.Description) 
                    && (i.Comments == item.Comments) 
                    && (i.Author == item.Author))
                    return (true);
            }
            return (false);

        }

        /// <summary>
        /// Checks if a channel has an item with the guid specified
        /// </summary>
        /// <param name="channel">the channel which items to check</param>
        /// <param name="guid">the guid to compare to</param>
        /// <returns>TRUE if the channel has an item with the specified guid</returns>
        private bool ChannelItemExistsByGuid(Channel channel, string guid)
        {
            //TODO change to ignore cases
            foreach (Channel.Item item in channel.Items)
            {
                if (item.GUID == guid)
                    return (true);
            }
            return (false);
        }
        /// <summary>
        /// Checks if a channel has item guids to make differentiation possible
        /// (all items need to have a guid or else this check fails)
        /// </summary>
        /// <param name="channel">the channel to check</param>
        /// <returns>TRUE if the channel has items with guids</returns>
        private bool ChannelHasGuids(Channel channel)
        {
            bool item_has_no_guid = false;
            foreach (Channel.Item item in channel.Items)
            {
                if (item.GUID == "")
                    item_has_no_guid = true;
            }
            return (!item_has_no_guid);
            //return (false);
        }

        /// <summary>
        /// Checks if a channel already exists in the channels list
        /// </summary>
        /// <param name="channel">the channel to check for</param>
        /// <returns>returns the channel that already exists , or NULL if the channel is unique</returns>
        private Channel ChannelExists(Channel channel)
        {
            //TODO change to ignore cases
            //comparing by title ,etc
            foreach (Channel c in channels)
            {
                if ((c.Title == channel.Title)
                    && (c.Copyright == channel.Copyright)
                    && (c.Description == channel.Description)
                    && (c.Docs == channel.Docs)
                    && (c.Generator == channel.Generator)
                    && (c.Language == channel.Language)
                    && (c.Link == channel.Link)
                    && (c.ManagingEditor == channel.ManagingEditor)
                    && (c.WebMaster == channel.WebMaster))
                    return (c);
            }
            return (null);
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
                    if (child.Name.Equals("description", StringComparison.CurrentCultureIgnoreCase)) item.Description = XmlStrings.FromXmlString(child.InnerText);
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
                                        enclosure.Url = found_urc.RelocatedUrl;

                                        //found_urc.Url = found_urc.RelocatedUrl;
                                        //found_urc.MimeType = "";
                                        //found_urc.RelocatedUrl = "";
                                        //found_urc.CheckUrl();
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

        #region Unit Testing
        /// <summary>
        /// Test to see if fetching rss feeds works
        /// </summary>
        [Test]
        public void TestFetchingRssFeed()
        {
            Console.WriteLine("Test to check if fetching a rss feed works.");
            bool wait = true;
            Rss feed = new Rss("http://www.voyagerproject.org/feed/");
            feed.FeedUpdated += delegate(Rss updated_feed)
            {
                Console.WriteLine("");
                Console.WriteLine("Rss Feed fetched (" + updated_feed.Channels.Count + ")");
                wait = false;
            };
            feed.FetchFeed();
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
            Assert.IsTrue(feed.Channels.Count > 0, "no channels found.");
            Console.WriteLine("Fetching Rss Feed Test successful.");
        }
        /// <summary>
        /// Test to see if fetching rss feeds works
        /// </summary>
        [Test]
        public void TestUpdatingRssFeed()
        {
            Console.WriteLine("Test to check if updating a rss feed works.");
            bool wait = true;
            int items_num = 0;
            int updates = 0;
            Rss feed = new Rss("http://www.voyagerproject.org/feed/");
            feed.FeedUpdated += delegate(Rss updated_feed)
            {
                updates++;
                Console.WriteLine("");
                Console.WriteLine("Rss Feed fetched (" + updated_feed.Channels.Count + ")");
                items_num = updated_feed.Channels[0].Items.Count;
                updated_feed.FetchFeed();
                //wait = false;
            };
            feed.FetchFeed();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 2))
                {
                    Assert.IsTrue(feed.Channels.Count > 0, "no channels found.");
                    Assert.IsTrue(feed.Channels.Count == 1, "too many channels found.");
                    Assert.IsTrue(feed.Channels[0].Items.Count == items_num, "items num changed.");
                    wait = false;
                }
                Assert.IsTrue(updates <= 1, "too many updates.");
                Console.Write(".");
                System.Threading.Thread.Sleep(250);
            }
            Console.WriteLine("Updating Rss Feed Test successful.");
        }
        #endregion
    }
}
