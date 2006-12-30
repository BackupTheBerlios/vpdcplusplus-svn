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
                        //}
                    }
                }


                //TODO maybe put scratch this .. or replace it with a find hub of user function
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
                        if (hub!=null) return (true);
                        else return (false);
                    }
                }

                public Source()
                {
                }

                public Source(string user_name, string filename)
                {
                    this.user_name = user_name;
                    this.filename = filename;
                }

                public Source(string user_name, string filename,Hub source_hub)
                {
                    this.hub = source_hub;
                    if (this.hub != null) is_online = true;
                    this.user_name = user_name;
                    this.filename = filename;
                }
            }

            protected Object sources_lock = "";
            [XmlIgnoreAttribute]
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
            }

            protected List<Source> sources = new List<Source>();
            public List<Source> Sources
            {
                get
                {
                    return (sources);
                }
            }
            protected int filesize = 0;
            public int Filesize
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
                higher,normal,lesser
            };
            protected Priority download_priority=Priority.normal;
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

            protected bool is_in_use = false;
            [XmlIgnoreAttribute]
            public bool IsInUse
            {
                get
                {
                    lock (sources_lock)
                    {
                        return (is_in_use);
                    }
                }
                set
                {
                    lock (sources_lock)
                    {
                        is_in_use = value;
                    }
                }
            }

            public Source FindFirstSourceByUser(string username)
            {
                lock (sources_lock)
                {
                    foreach (Source source in sources)
                    {
                        if (source.UserName == username) return (source);
                    }
                }
                return (null);
            }

            public bool AddSource(Source me)
            {
                if (FindFirstSourceByUser(me.UserName) == null)
                {
                    lock (sources_lock)
                    {
                        sources.Add(me);
                    }
                    return (true);
                }
                else
                {
                    return (false);
                }
            }

            public void RemoveSource(Source me)
            {
                lock (sources_lock)
                {
                    sources.Remove(me);
                }
            }
        }
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

        protected Object queue_lock = "";
        public Object QueueLock
        {
            get
            {
                return (queue_lock);
            }
            set
            {
                queue_lock = value;
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

        //deprecated
        public QueueEntry FindExistingEntryForSearchResult(SearchResults.SearchResult result)
        {
            lock (queue_lock)
            {
                foreach (QueueEntry entry in items)
                {
                    //if ((entry.OutputFilename == download_directory + "\\" + System.IO.Path.GetFileName(result.Filename) && entry.Filesize == result.Filesize)
                    //    || (result.HasTTH && entry.HasTTH && entry.TTH == result.TTH))
                    if (result.HasTTH && entry.HasTTH && entry.TTH == result.TTH)
                        return (entry);
                }
                return (null);
            }
        }

        public void AddSearchResult(SearchResults.SearchResult result)
        {
            if (result.IsFile)
            {
                QueueEntry existing = FindExistingEntryForSearchResult(result);
                if (existing != null)
                {//This should be a deprecated case.. never ever be called again :-)
                    lock (queue_lock)
                    {
                        existing.AddSource(new QueueEntry.Source(result.UserName, result.Filename, result.Hub));
                        //TODO source add event
                    }
                    return;
                }
                QueueEntry entry = new QueueEntry();
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
                lock (queue_lock)
                {
                    entry.AddSource(new Queue.QueueEntry.Source(result.UserName, result.Filename, result.Hub));

                    items.Add(entry);
                }
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

        public Queue.QueueEntry FindFirstQueueEntryBySourceUser(string username)
        {
            lock (queue_lock)
            {

                foreach (Queue.QueueEntry entry in items)
                {
                    foreach (QueueEntry.Source source in entry.Sources)
                    {
                        if (source.UserName == username) return (entry);
                    }
                }
                return (null);
            }
        }

        public Queue.QueueEntry FindFirstUnusedQueueEntryBySourceUser(string username)
        {
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    foreach (QueueEntry.Source source in entry.Sources)
                    {
                        if (source.UserName == username && !entry.IsInUse) return (entry);
                    }
                }
                return (null);
            }
        }

        public Queue.QueueEntry FindQueueEntryByOutputFilename(string output_filename)
        {
            lock (queue_lock)
            {

                foreach (Queue.QueueEntry entry in items)
                {
                    if (entry.OutputFilename == output_filename) return (entry);
                }
                return (null);
            }
        }

        public Queue.QueueEntry FindQueueEntryByTTH(string tth)
        {
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    if (entry.HasTTH && entry.TTH == tth) return (entry);
                }
                return (null);
            }
        }

        public void UpdateSourcesByUsername(string username, Hub source_hub, bool is_online)
        {
            lock (queue_lock)
            {
                foreach (Queue.QueueEntry entry in items)
                {
                    foreach (QueueEntry.Source source in entry.Sources)
                    {
                        if (source.UserName == username)
                        {
                            source.Hub = source_hub;
                            source.IsOnline = is_online;

                        }
                    }
                }
            }
        }

        public void Remove(string output_filename)
        {
            lock (queue_lock)
            {
                foreach (QueueEntry entry in items)
                {
                    if (entry.OutputFilename == output_filename)
                    {
                        items.Remove(entry);
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
        }

        public void LoadQueueFromXml(string xml)
        {
            lock (queue_lock)
            {
                try
                {
                    Queue q = new Queue();
                    XmlSerializer serializer = new XmlSerializer(typeof(Queue));
                    MemoryStream ms = new MemoryStream(System.Text.Encoding.Default.GetBytes(xml));
                    q = (Queue)serializer.Deserialize(ms);
                    items = q.Items;
                    download_directory = q.download_directory;
                    if (EntriesCleared != null)
                        EntriesCleared();
                    if (EntryAdded != null)
                    {

                        foreach (QueueEntry entry in items)
                        {
                            EntryAdded(entry);
                        }
                    }
                    if (EntriesChanged != null)
                    {
                        EntriesChanged();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error deserializing queue: " + ex.Message);
                }
            }
        }

        public string SaveQueueToXml()
        {
            //nice way but seems to not work with list<> members
            lock (queue_lock)
            {
                string ret = "";
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Queue));
                    MemoryStream ms = new MemoryStream();
                    serializer.Serialize(ms, this);
                    ms.Flush();
                    ret = System.Text.Encoding.Default.GetString(ms.GetBuffer());
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error serializing queue: " + ex.Message);
                    return (null);
                }
                return (ret);
            }              
        }

        public void LoadQueueFromXmlFile(string filename)
        {
            try
            {
                if (!File.Exists(filename)) return;
                LoadQueueFromXml(System.IO.File.ReadAllText(filename));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading queue from: " + filename + " : " + ex.Message);
            }
        }

        public void SaveQueueToXmlFile(string filename)
        {
            try
            {
                System.IO.File.WriteAllText(filename, SaveQueueToXml());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving queue to: " + filename + " : " + ex.Message);
            }
        }


        #region ICollection<QueueEntry> Members

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

        public void Clear()
        {
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
            lock (queue_lock)
            {
                items.Clear();
            }
        }

        public bool Contains(Queue.QueueEntry item)
        {
            lock (queue_lock)
            {
                return (items.Contains(item));
            }
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

        public bool Remove(Queue.QueueEntry item)
        {
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
            lock (queue_lock)
            {
                return (items.Remove(item));
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
