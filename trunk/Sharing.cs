using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Collections;

namespace DCPlusPlus
{
    [Serializable]
    public class Sharing: ICollection<Sharing.SharingEntry>
    {
        public class SharingEntry
        {
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

        }

        protected List<SharingEntry> items = new List<SharingEntry>();
        //[XmlArrayAttribute("Queue")]
        public List<SharingEntry> Items
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

        protected Object share_lock = "";
        public Object SharingLock
        {
            get
            {
                return (share_lock);
            }
            set
            {
                share_lock = value;
            }
        }

        public delegate void EntryAddedEventHandler(SharingEntry entry);
        public event EntryAddedEventHandler EntryAdded;

        public delegate void EntryRemovedEventHandler(SharingEntry entry);
        public event EntryRemovedEventHandler EntryRemoved;

        public delegate void EntriesChangedEventHandler();
        public event EntriesChangedEventHandler EntriesChanged;

        public delegate void EntriesClearedEventHandler();
        public event EntriesClearedEventHandler EntriesCleared;


        // share files with these functions



        public void ShareFile(string filename)
        {

        }

        public void ShareDirectory(string filename)
        {

        }

        public SharingEntry GetShareByFilename(string filename)
        {

        }

        public SharingEntry GetShareByTTH(string tth)
        {

        }





        public void LoadSharesFromXml(string xml)
        {
            lock (share_lock)
            {
                try
                {
                    Sharing s = new Sharing();
                    XmlSerializer serializer = new XmlSerializer(typeof(Sharing));
                    MemoryStream ms = new MemoryStream(System.Text.Encoding.Default.GetBytes(xml));
                    s = (Sharing)serializer.Deserialize(ms);
                    items = s.Items;
                    if (EntriesCleared != null)
                        EntriesCleared();
                    if (EntryAdded != null)
                    {

                        foreach (SharingEntry entry in items)
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
        public string SaveSharesToXml()
        {
            //nice way but seems to not work with list<> members
            lock (share_lock)
            {
                string ret = "";
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Sharing));
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
        public void LoadSharesFromXmlFile(string filename)
        {
            try
            {
                if (!File.Exists(filename)) return;
                LoadSharesFromXml(System.IO.File.ReadAllText(filename));
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading queue from: " + filename + " : " + ex.Message);
            }
        }
        public void SaveSharesToXmlFile(string filename)
        {
            try
            {
                System.IO.File.WriteAllText(filename, SaveSharesToXml());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving queue to: " + filename + " : " + ex.Message);
            }
        }

        #region ICollection<SharingEntry> Members

        public void Add(SharingEntry item)
        {
            lock (share_lock)
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
            lock (share_lock)
            {
                items.Clear();
            }
        }

        public bool Contains(SharingEntry item)
        {
            lock (share_lock)
            {
                return (items.Contains(item));
            }
        }

        public void CopyTo(SharingEntry[] array, int arrayIndex)
        {
            lock (share_lock)
            {

                foreach (SharingEntry entry in items)
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

        public bool Remove(SharingEntry item)
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
            lock (share_lock)
            {
                return (items.Remove(item));
            }
        }

        #endregion
        #region IEnumerable<SharingEntry> Members

        public class SharingEnumerator2 : IEnumerator<SharingEntry>
        {
            private SharingEntry[] sharing_array;
            private int Cursor;
            public SharingEnumerator2(SharingEntry[] my_array)
            {
                sharing_array = my_array;
                Cursor = -1;
            }

            #region IEnumerator<SharingEntry> Members

            public object Current
            {
                get
                {
                    if ((Cursor < 0) || (Cursor == sharing_array.Length))
                        throw new InvalidOperationException();
                    return ((object)sharing_array[Cursor]);
                }
            }


            SharingEntry IEnumerator<SharingEntry>.Current
            {
                get
                {
                    if ((Cursor < 0) || (Cursor == sharing_array.Length))
                        throw new InvalidOperationException();
                    return (sharing_array[Cursor]);
                }
            }

            public bool MoveNext()
            {
                if (Cursor < sharing_array.Length)
                    Cursor++;
                return (!(Cursor == sharing_array.Length));
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

        public IEnumerator<SharingEntry> GetEnumerator()
        {
            return (new SharingEnumerator2(items.ToArray()));
        }

        #endregion
        #region IEnumerable Members

        public class SharingEnumerator : IEnumerator
        {
            private SharingEntry[] sharing_array;
            private int Cursor;
            public SharingEnumerator(SharingEntry[] my_array)
            {
                sharing_array = my_array;
                Cursor = -1;
            }


            #region IEnumerator Members

            public object Current
            {
                get
                {
                    if ((Cursor < 0) || (Cursor == sharing_array.Length))
                        throw new InvalidOperationException();
                    return ((object)sharing_array[Cursor]);
                }
            }

            public bool MoveNext()
            {
                if (Cursor < sharing_array.Length)
                    Cursor++;
                return (!(Cursor == sharing_array.Length));
            }

            public void Reset()
            {
                Cursor = -1;
            }

            #endregion
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return (new SharingEnumerator(items.ToArray()));
        }

        #endregion



    }
}
