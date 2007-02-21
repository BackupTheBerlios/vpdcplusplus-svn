using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Collections;

namespace DCPlusPlus
{
    [Serializable]
    public class Queue : ICollection<Queue.QueueEntry>
    {

        public class QueueEntry
        {
            public class Source
            {
                public delegate void SourceStatusChangedEventHandler(Source source);
                public event SourceStatusChangedEventHandler SourceStatusChanged;

                protected string user_name = "";
                public string UserName
                {
                    get
                    {
                        return (user_name);
                    }
                    set
                    {
                        user_name = value;
                    }
                }
                protected string filename = "";
                public string Filename
                {
                    get
                    {
                        return (filename);
                    }
                    set
                    {
                        filename = value;
                    }
                }
                //TODO add last connection attempt time field
                protected bool is_online = false;
                [XmlIgnoreAttribute]
                public bool IsOnline
                {
                    get
                    {
                        //lock (source_lock)
                        //{
                        return (is_online);
                        //}
                    }
                    set
                    {
                        //lock (source_lock)
                        //{
                        is_online = value;
                        if (SourceStatusChanged != null)
                            SourceStatusChanged(this);
                        //}
                    }
                }
                //TODO maybe scratch this .. or replace it with a find hub of user function
                protected Hub hub = null;
                [XmlIgnoreAttribute]
                public Hub Hub
                {
                    get
                    {
                        return (hub);
                    }
                    set
                    {
                        hub = value;
                    }
                }
                public bool HasHub
                {
                    get
                    {
                        if (hub != null) return (true);
                        else return (false);
                    }
                }
                /*
                protected string ip = "";
                [XmlIgnoreAttribute]
                public Hub IP
                {
                    get
                    {
                        return (ip);
                    }
                    set
                    {
                        ip = value;
                    }
                }
                protected int port = "";
                [XmlIgnoreAttribute]
                public Hub Port
                {
                    get
                    {
                        return (port);
                    }
                    set
                    {
                        port = value;
                    }
                }
                public bool HasAddress
                {
                    get
                    {
                        if (!string.IsNullOrEmpty(ip)) return (true);
                        else return (false);
                    }
                }
                */
                public Source()
                {
                }
                public Source(string user_name, string filename)
                {
                    this.user_name = user_name;
                    this.filename = filename;
                }
                public Source(string user_name, string filename, Hub source_hub)
                {
                    this.hub = source_hub;
                    if (this.hub != null) is_online = true;
                    this.user_name = user_name;
                    this.filename = filename;
                }
                public void Ungrab()
                {
                    SourceStatusChanged = null;
                }
            }

            public delegate void EntrySourceAddedEventHandler(QueueEntry entry, QueueEntry.Source source);
            public event EntrySourceAddedEventHandler EntrySourceAdded;
            public delegate void EntrySourceRemovedEventHandler(QueueEntry entry, QueueEntry.Source source);
            public event EntrySourceRemovedEventHandler EntrySourceRemoved;
            public delegate void EntrySourceStatusChangedEventHandler(QueueEntry entry, QueueEntry.Source source);
            public event EntrySourceStatusChangedEventHandler EntrySourceStatusChanged;
            public delegate void EntryClaimedEventHandler(QueueEntry entry, Peer claiming_peer);
            public event EntryClaimedEventHandler EntryClaimed;
            public delegate void EntryUnclaimedEventHandler(QueueEntry entry);
            public event EntryUnclaimedEventHandler EntryUnclaimed;

            protected Object sources_lock = new Object();
            /*[XmlIgnoreAttribute]
            public Object SourcesLock
            {
                get
                {
                    return (sources_lock);
                }
                set
                {
                    sources_lock = value;
                }
            }*/
            protected Object is_in_use_lock = new Object();
            /*[XmlIgnoreAttribute]
            public Object IsInUseLock
            {
                get
                {
                    return (is_in_use_lock);
                }
                set
                {
                    is_in_use_lock = value;
                }
            }*/

            protected List<Source> sources = new List<Source>();
            public List<Source> Sources
            {
                get
                {
                    return (sources);
                }
            }
            protected long filesize = 0;
            public long Filesize
            {
                get
                {
                    return (filesize);
                }
                set
                {
                    filesize = value;
                }
            }
            //protected bool has_tth = false;
            public bool HasTTH
            {
                get
                {
                    if (!string.IsNullOrEmpty(tth)) return (true);
                    else return (false);
                    //return (has_tth);
                }
            }
            protected string tth = "";
            public string TTH
            {
                get
                {
                    return (tth);
                }
                set
                {
                    tth = value;
                }
            }


            protected byte[] tthl = new byte[0];
            public byte[] TTHL
            {
                get
                {
                    return (tthl);
                }
                set
                {
                    tthl = value;
                }
            }

            protected bool want_tthl = false;
            public bool WantTTHL
            {
                get
                {
                    return (want_tthl);
                }
                set
                {
                    want_tthl = value;
                }
            }

            protected string output_filename = "";
            public string OutputFilename
            {
                get
                {
                    return (output_filename);
                }
                set
                {
                    output_filename = value;
                }
            }
            public enum Priority
            {
                higher, normal, lesser
            };
            protected Priority download_priority = Priority.normal;
            public Priority DownloadPriority
            {
                get
                {
                    return (download_priority);
                }
                set
                {
                    download_priority = value;

                }
            }
            public enum EntryType
            {
                File, Directory, FileList
            };
            protected EntryType type = EntryType.File;
            public EntryType Type
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
            public int SourcesOnline
            {
                get
                {
                    int sources_online = 0;
                    lock (sources_lock)
                    {
                        foreach (Source source in sources)
                        {
                            if (source.IsOnline) sources_online++;
                        }
                    }
                    return (sources_online);
                }
            }
            /// <summary>
            /// get the filesize of the output file
            /// ( not very cpu efficient ;-( )
            /// </summary>
            public long FilesizeOnDisk
            {
                get
                {
                    if (File.Exists(output_filename) == false) return (0);
                    FileInfo fi = new FileInfo(output_filename);
                    return (fi.Length);
                }
            }
            protected bool is_in_use = false;
            [XmlIgnoreAttribute]
            public bool IsInUse
            {
                get
                {
                    bool ret = false;
                    lock (is_in_use_lock)
                    {
                        ret = is_in_use;
                    }
                    return (ret);
                }
                /*    set
                    {
                        lock (sources_lock)
                        {
                            is_in_use = value;
                        }
                    }*/
            }


            public bool TryToClaimEntry(Peer claiming_peer)
            {
                lock (is_in_use_lock)
                {
                    if (!is_in_use)
                    {
                        is_in_use = true;
                        if (EntryClaimed != null)
                            EntryClaimed(this, claiming_peer);
                        return (true);
                    }
                    else return (false);
                }
            }
            public void UnclaimEntry()
            {
                lock (is_in_use_lock)
                {
                    is_in_use = false;
                }
                if (EntryUnclaimed != null)
                    EntryUnclaimed(this);
            }

            public QueueEntry()
            {
             
            }

            public void StartDownload()
            {
                lock (sources_lock)
                {//TODO put this in queue class
                    foreach (Source source in sources)
                    {
                        if (source.HasHub && source.IsOnline)
                        {
                            source.Hub.SendConnectToMe(source.UserName); //signal download to hub to start it
                        }
                    }
                }
                //sources strategy 
                //for selection and skipping if source offline
                //source offline detection

                //result.Hub.SendCommand("ConnectToMe", result.UserName); //signal download to hub to start it
                //Console.WriteLine("Hub was not resolved from result hub address: " + result.HubAddress);

            }
            public Source FindFirstSourceByUser(string username)
            {
                Source ret = null;
                lock (sources_lock)
                {
                    foreach (Source source in sources)
                    {
                        if (source.UserName == username) ret = source;
                    }
                }
                return (ret);
            }
            public Source FindFirstSourceByUserAndHub(string username, Hub hub)
            {
                Source ret = null;
                lock (sources_lock)
                {
                    foreach (Source source in sources)
                    {
                        if (source.UserName == username && source.HasHub && source.Hub == hub) ret = source;
                    }
                }
                return (ret);
            }
            public void UpdateSourcesByUsername(string username, Hub source_hub, bool is_online)
            {
                lock (sources_lock)
                {
                    foreach (Source source in sources)
                    {
                        if (source.UserName == username)
                        {
                            source.Hub = source_hub;
                            source.IsOnline = is_online;

                        }
                    }
                }
            }
            public bool AddSource(Source me)
            {
                if (FindFirstSourceByUser(me.UserName) == null)
                {
                    lock (sources_lock)
                    {
                        sources.Add(me);
                    }
                    me.SourceStatusChanged += delegate(Source status_changed)
                     {
                         if (EntrySourceStatusChanged != null)
                             EntrySourceStatusChanged(this, status_changed);
                     };
                    if (EntrySourceAdded != null)
                        EntrySourceAdded(this, me);
                    return (true);
                }
                else
                {
                    return (false);
                }
            }

            public void DeleteOutputFile()
            {
                if (File.Exists(output_filename))
                {
                    File.Delete(output_filename);
                }
            }

            
            public void RemoveSource(Source me)
            {
                lock (sources_lock)
                {
                    sources.Remove(me);
                }
                me.Ungrab();
                if (EntrySourceRemoved != null)
                    EntrySourceRemoved(this, me);
            }

            public void ClearSources()
            {
                lock (sources_lock)
                {
                    foreach (Source source in sources)
                    {
                        sources.Remove(source);
                        source.Ungrab();
                        if (EntrySourceRemoved != null)
                            EntrySourceRemoved(this, source);
                    }
                }
            }


            public void GrabSources()
            {
                lock (sources_lock)
                {
                    foreach (Source source in sources)
                    {
                        source.SourceStatusChanged += delegate(Source status_changed)
                        {
                            if (EntrySourceStatusChanged != null)
                                EntrySourceStatusChanged(this, status_changed);
                        };

                    }
                }
            }
            //TODO check if sources get ungrabbed too at the end
            public void Ungrab()
            {
                EntrySourceAdded = null;
                EntrySourceRemoved = null;
                EntryUnclaimed = null;
                EntryClaimed = null;
                EntrySourceStatusChanged = null;
            }

        }
        
        public delegate void EntryAddedEventHandler(QueueEntry entry);
        public event EntryAddedEventHandler EntryAdded;
        public delegate void EntryCompletedEventHandler(QueueEntry entry);
        public event EntryCompletedEventHandler EntryCompleted;
        public delegate void EntryRemovedEventHandler(QueueEntry entry);
        public event EntryRemovedEventHandler EntryRemoved;
        public delegate void EntriesChangedEventHandler();
        public event EntriesChangedEventHandler EntriesChanged;
        public delegate void EntriesClearedEventHandler();
        public event EntriesClearedEventHandler EntriesCleared;

        public event QueueEntry.EntrySourceAddedEventHandler EntrySourceAdded;
        public event QueueEntry.EntrySourceRemovedEventHandler EntrySourceRemoved;
        public event QueueEntry.EntrySourceStatusChangedEventHandler EntrySourceStatusChanged;
        public event QueueEntry.EntryClaimedEventHandler EntryClaimed;
        public event QueueEntry.EntryUnclaimedEventHandler EntryUnclaimed;


        protected List<QueueEntry> items = new List<QueueEntry>();
        //[XmlArrayAttribute("Queue")]
        public List<QueueEntry> Items
        {
            get
            {
                return (items);
            }
            set
            {
                items = value;
            }
        }
        public int Count
        {
            get
            {
                return (items.Count);
            }
        }
        protected string download_directory = ".\\downloads";
        public string DownloadDirectory
        {
            get
            {
                return (download_directory);
            }
            set
            {
                download_directory = value;
                if (download_directory.EndsWith("\\"))
                    download_directory.TrimEnd('\\');
            }
        }
        protected string filelists_directory = ".\\filelists";
        public string FileListsDirectory
        {
            get
            {
                return (filelists_directory);
            }
            set
            {
                filelists_directory = value;
                if (filelists_directory.EndsWith("\\"))
                    filelists_directory.TrimEnd('\\');
            }
        }
        protected Object queue_lock = new Object();
        /*public Object QueueLock
        {
            get
            {
                return (queue_lock);
            }
            set
            {
                queue_lock = value;
            }
        }*/

        //deprecated , because automatic adding of sources should make this a never to be called again function
        public QueueEntry FindExistingEntryForSearchResult(SearchResults.SearchResult result)
        {
            QueueEntry ret = null;
            lock (queue_lock)
            {
                foreach (QueueEntry entry in items)
                {
                    //if ((entry.OutputFilename == download_directory + "\\" + System.IO.Path.GetFileName(result.Filename) && entry.Filesize == result.Filesize)
                    //    || (result.HasTTH && entry.HasTTH && entry.TTH == result.TTH))
                    if (result.HasTTH && entry.HasTTH && entry.TTH == result.TTH)
                    {
                        ret = (entry);
                        break;
                    }
                }
            }
            return (ret);
        }
        public QueueEntry FindExistingEntryForFileList(Hub hub,string username)
        {
            QueueEntry ret = null;
            lock (queue_lock)
            {
                foreach (QueueEntry entry in items)
                {
                    if (entry.FindFirstSourceByUserAndHub(username, hub) != null)
                    {
                        ret = entry;
                        break;
                    }
                }
            }
            return (ret);
        }
        private void GrabEntry(QueueEntry entry)
        {
            entry.EntrySourceAdded += delegate(QueueEntry add_entry, QueueEntry.Source added_source)
            {
                if (EntrySourceAdded != null)
                    EntrySourceAdded(add_entry, added_source);
            };
            entry.EntrySourceRemoved += delegate(QueueEntry remove_entry, QueueEntry.Source removed_source)
            {
                if (EntrySourceRemoved != null)
                    EntrySourceRemoved(remove_entry, removed_source);
            };
            entry.EntrySourceStatusChanged += delegate(QueueEntry change_entry, QueueEntry.Source changed_source)
            {
                if (EntrySourceStatusChanged != null)
                    EntrySourceStatusChanged(change_entry, changed_source);
            };
            entry.EntryClaimed += delegate(QueueEntry claimed_entry, Peer claiming_peer)
            {
                if (EntryClaimed != null)
                    EntryClaimed(claimed_entry, claiming_peer);
            };
            entry.EntryUnclaimed += delegate(QueueEntry unclaimed_entry)
            {
                if (EntryUnclaimed != null)
                    EntryUnclaimed(unclaimed_entry);
            };
        }

        public void CheckDownloadedData(QueueEntry queueEntry)
        {
            throw new Exception("The method or operation is not implemented.");
        }
        /*
        public void AddTTHL(QueueEntry me)
        {
            QueueEntry existing = FindExistingEntryForFileList(hub, username);
            if (existing == null)
            {
                QueueEntry entry = new QueueEntry();
                entry.Type = QueueEntry.EntryType.FileList;
                string temp_hub_address = hub.Address.Replace(":", "_");
                entry.OutputFilename = filelists_directory + "\\" + temp_hub_address + "-" + Base32.ToBase32String(Encoding.Default.GetBytes(username)) + ".xml.bz2";//TODO .. maybe changes needed here to incorporate other filelist formats
                entry.AddSource(new Queue.QueueEntry.Source(username, "", hub));
                lock (queue_lock)
                {
                    items.Add(entry);
                }
                GrabEntry(entry);
                try
                {
                    if (EntryAdded != null)
                        EntryAdded(entry);
                    if (EntriesChanged != null)
                        EntriesChanged();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured in added event callback: " + ex.Message);
                }

            }
        }*/

        public void AddFileList(Hub hub,string username)
        {
            QueueEntry existing = FindExistingEntryForFileList(hub, username);
            if (existing == null)
            {
                QueueEntry entry = new QueueEntry();
                entry.Type = QueueEntry.EntryType.FileList;
                string temp_hub_address = hub.Address.Replace(":", "_");
                entry.OutputFilename = filelists_directory + "\\" + temp_hub_address + "-" + Base32.ToBase32String(Encoding.Default.GetBytes(username)) + ".xml.bz2";//TODO .. maybe changes needed here to incorporate other filelist formats
                entry.AddSource(new Queue.QueueEntry.Source(username, "", hub));
                lock (queue_lock)
                {
                    items.Add(entry);
                }
                GrabEntry(entry);
                try
                {
                    if (EntryAdded != null)
                        EntryAdded(entry);
                    if (EntriesChanged != null)
                        EntriesChanged();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured in added event callback: " + ex.Message);
                }

            }
        }
        public void AddSearchResult(SearchResults.SearchResult result)
        {
            if (result.IsFile)
            {
                QueueEntry existing = FindExistingEntryForSearchResult(result);
                if (existing != null)
                {//This should be a deprecated case.. never ever be called again :-)
                    //lock (queue_lock)
                    //{
                        existing.AddSource(new QueueEntry.Source(result.UserName, result.Filename, result.Hub));
                        //TODO source add event
                    //}
                    return;
                }
                QueueEntry entry = new QueueEntry();
                entry.Type = QueueEntry.EntryType.File;
                entry.Filesize = result.Filesize;
                entry.OutputFilename = download_directory + "\\" + System.IO.Path.GetFileName(result.Filename); //TODO add directory support somehow
                if (File.Exists(entry.OutputFilename))
                {//already some file existing with this name ... try a (i) at the end increment until file is not there
                    int i = 1;
                    string new_extension = Path.GetExtension(entry.OutputFilename);

                    string new_filename = Path.GetDirectoryName(entry.OutputFilename) + "\\" + Path.GetFileNameWithoutExtension(entry.OutputFilename) + "(" + i + ")" + new_extension;
                    while (File.Exists(new_filename))
                    {
                        i++;
                        new_filename = Path.GetDirectoryName(entry.OutputFilename) + "\\" + Path.GetFileNameWithoutExtension(entry.OutputFilename) + "(" + i + ")" + new_extension;
                    }
                    entry.OutputFilename = new_filename;
                }
                if (result.HasTTH) entry.TTH = result.TTH;
                //if(result.IsHubResolved)
                entry.AddSource(new Queue.QueueEntry.Source(result.UserName, result.Filename, result.Hub));
                lock (queue_lock)
                {
                    items.Add(entry);
                }
                GrabEntry(entry);
                //Console.WriteLine("Queue Entry added: '"+entry.OutputFilename+"'");
                try
                {
                    if (EntryAdded != null)
                        EntryAdded(entry);
                    if (EntriesChanged != null)
                        EntriesChanged();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured in added event callback: " + ex.Message);
                }

            }
            //download_directory + "\\" + 

        }

        public void RestartDownload(QueueEntry entry)
        {
            if (entry.FilesizeOnDisk != 0 && File.Exists(entry.OutputFilename) && !entry.IsInUse)
            {
                entry.DeleteOutputFile();
            }
        }

        public Queue.QueueEntry FindFirstQueueEntryBySourceUser(string username)
        {
            QueueEntry ret = null;
            lock (queue_lock)
            {

                foreach (Queue.QueueEntry entry in items)
                {
                    foreach (QueueEntry.Source source in entry.Sources)
                    {
                        if (source.UserName == username)
                        {
                            ret = entry;
                            break;
                        }
                    }
                    if (ret != null)
                        break;
                }
            }
            return (ret);
        }
        public Queue.QueueEntry FindFirstUnusedQueueEntryBySourceUser(string username)
        {
            QueueEntry ret = null;
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    foreach (QueueEntry.Source source in entry.Sources)
                    {
                        if (source.UserName == username && !entry.IsInUse)
                        {
                            ret = entry;
                            break;
                        }
                    }
                    if (ret != null)
                        break;
                }
            }
            return (ret);
        }
        public Queue.QueueEntry FindQueueEntryByOutputFilename(string output_filename)
        {
            QueueEntry ret = null;
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    if (entry.OutputFilename == output_filename)
                    {
                        ret = entry;
                        break;
                    }
                }
            }
            return (ret);
        }
        public Queue.QueueEntry FindQueueEntryByTTH(string tth)
        {
            QueueEntry ret = null;
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    if (entry.HasTTH && entry.TTH == tth)
                    {
                        ret =  entry;
                        break;
                    }
                }
            }
            return (ret);
        }
        public void UpdateSourcesByUsername(string username, Hub source_hub, bool is_online)
        {
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    entry.UpdateSourcesByUsername(username, source_hub, is_online);
                }
            }
        }
        public void Remove(string output_filename)
        {
                foreach (QueueEntry entry in items)
                {
                    if (entry.OutputFilename == output_filename)
                    {
                        lock (queue_lock)
                        {
                            items.Remove(entry);
                        }
                        entry.Ungrab();
                        try
                        {
                            if (EntryRemoved != null)
                                EntryRemoved(entry);
                            if (EntriesChanged != null)
                                EntriesChanged();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception occured in remove event callback: " + ex.Message);
                        }
                        return;
                    }
            }
        }
        public void Add(Queue.QueueEntry item)
        {
            lock (queue_lock)
            {
                items.Add(item);
            }
            try
            {
                if (EntryAdded != null)
                    EntryAdded(item);
                if (EntriesChanged != null)
                    EntriesChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured in added event callback: " + ex.Message);
            }
        }


        public bool Remove(Queue.QueueEntry item)
        {
            bool ret = false;
            lock (queue_lock)
            {
                ret = items.Remove(item);
            }
            try
            {
                if (EntryRemoved != null)
                    EntryRemoved(item);
                if (EntriesChanged != null)
                    EntriesChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured in remove event callback: " + ex.Message);
            }
            return (ret);
        }
        public void Clear()
        {
            lock (queue_lock)
            {
                items.Clear();
            }
            try
            {

                if (EntriesCleared != null)
                    EntriesCleared();
                if (EntriesChanged != null)
                    EntriesChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured in clear event callback: " + ex.Message);
            }
        }
        public void LoadQueueFromXml(string xml)
        {
            lock (queue_lock)
            {
                items.Clear();
            }
            if (EntriesCleared != null)
                EntriesCleared();

            Queue q = new Queue();
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Queue));
                MemoryStream ms = new MemoryStream(System.Text.Encoding.Default.GetBytes(xml));
                q = (Queue)serializer.Deserialize(ms);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error deserializing queue: " + ex.Message);
            }
            lock (queue_lock)
            {
                items = q.Items;
            }
            download_directory = q.download_directory;
            if (EntryAdded != null)
            {
                foreach (QueueEntry entry in items)
                {
                    GrabEntry(entry);
                    entry.GrabSources();
                    EntryAdded(entry);
                }
            }
            if (EntriesChanged != null)
                EntriesChanged();
        }
        public string SaveQueueToXml()
        {
            //nice way but seems to not work with list<> members
            string ret = "";
            lock (queue_lock)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Queue));
                    MemoryStream ms = new MemoryStream();
                    serializer.Serialize(ms, this);
                    ms.Flush();
                    ret = System.Text.Encoding.Default.GetString(ms.GetBuffer(), 0, (int)ms.Length);//TODO ... 4gb crash border
                    //ret = ret.TrimEnd((char)0);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error serializing queue: " + ex.Message);
                    ret = null;
                }
            }
            return (ret);
        }
        public void LoadQueueFromXmlFile(string filename)
        {
            //try
            //{
                if (!File.Exists(filename)) return;
                LoadQueueFromXml(System.IO.File.ReadAllText(filename));
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("Error loading queue from: " + filename + " : " + ex.Message);
            //}
        }
        public void SaveQueueToXmlFile(string filename)
        {
            try
            {
                if (File.Exists(filename + ".backup") && File.Exists(filename))
                    File.Delete(filename + ".backup");
                if (File.Exists(filename))
                {
                    File.Move(filename, filename + ".backup");
                    Console.WriteLine("Created Backup of queue: "+filename+".backup");
                }

                System.IO.File.WriteAllText(filename, SaveQueueToXml());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving queue to: " + filename + " : " + ex.Message);
            }
        }

        #region ICollection<QueueEntry> Members
        public bool Contains(Queue.QueueEntry item)
        {
            bool ret = false;
            lock (queue_lock)
            {
                ret =items.Contains(item);
            }
            return (ret);
        }
        public void CopyTo(Queue.QueueEntry[] array, int arrayIndex)
        {
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    array.SetValue(entry, arrayIndex);
                    arrayIndex = arrayIndex + 1;
                }
            }
        }
        public bool IsReadOnly
        {
            get
            {
                return (false);
            }
        }
        #endregion
        //TODO check for thread safety
        #region IEnumerable<QueueEntry> Members

        public class QueueEnumerator2 : IEnumerator<QueueEntry>
        {
            private Queue.QueueEntry[] queue_array;
            private int Cursor;
            public QueueEnumerator2(Queue.QueueEntry[] my_array)
            {
                queue_array = my_array;
                Cursor = -1;
            }

            #region IEnumerator<QueueEntry> Members

            public object Current
            {
                get
                {
                    if ((Cursor < 0) || (Cursor == queue_array.Length))
                        throw new InvalidOperationException();
                    return ((object)queue_array[Cursor]);
                }
            }


            QueueEntry IEnumerator<QueueEntry>.Current
            {
                get 
                {
                    if ((Cursor < 0) || (Cursor == queue_array.Length))
                        throw new InvalidOperationException();
                    return (queue_array[Cursor]);
                }
            }

            public bool MoveNext()
            {
                if (Cursor < queue_array.Length)
                    Cursor++;
                return (!(Cursor == queue_array.Length));
            }

            public void Reset()
            {
                Cursor = -1;
            }



            #endregion

            #region IDisposable Members

            public void Dispose()
            {
                //Dispose();
                //base.Dispose(disposing);
            }

            #endregion
        }

        public IEnumerator<Queue.QueueEntry> GetEnumerator()
        {
            return (new QueueEnumerator2(items.ToArray()));
        }

        #endregion
        #region IEnumerable Members

        public class QueueEnumerator : IEnumerator
        {
            private Queue.QueueEntry[] queue_array;
            private int Cursor;
            public QueueEnumerator(Queue.QueueEntry[] my_array)
            {
                queue_array = my_array;
                Cursor = -1;
            }


#region IEnumerator Members

            public object Current
            {
                get
                {
                    if ((Cursor < 0) || (Cursor == queue_array.Length))
                        throw new InvalidOperationException();
                    return ((object)queue_array[Cursor]);
                }
            }

            public bool MoveNext()
            {
                if (Cursor < queue_array.Length)
                    Cursor++;
                return (!(Cursor == queue_array.Length));
            }

            public void Reset()
            {
                Cursor = -1;
            }

#endregion
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (new QueueEnumerator(items.ToArray()));
        }

        #endregion


    }
}
