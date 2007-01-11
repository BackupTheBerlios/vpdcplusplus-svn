using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;
using System.IO;
using System.Collections;
using System.Threading;
using NUnit.Framework;
//using Jcs.Tiger;
using ThexCS;


namespace DCPlusPlus
{
    [Serializable ,TestFixture]
    public class Sharing: ICollection<Sharing.SharingEntry>
    {
        public class SharingEntry
        {
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
            [XmlIgnoreAttribute]
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

        [XmlIgnoreAttribute]
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

        public delegate void DirectoryFinishedEventHandler(string directory);
        public event DirectoryFinishedEventHandler DirectoryFinished;

        public delegate void EntryAddedEventHandler(SharingEntry entry);
        public event EntryAddedEventHandler EntryAdded;

        public delegate void EntryRemovedEventHandler(SharingEntry entry);
        public event EntryRemovedEventHandler EntryRemoved;

        public delegate void EntriesChangedEventHandler();
        public event EntriesChangedEventHandler EntriesChanged;

        public delegate void EntriesClearedEventHandler();
        public event EntriesClearedEventHandler EntriesCleared;

        private delegate SharingEntry ShareFileHandler(string filename);
        private SharingEntry ShareFileAsync(string filename)
        {//share file
            if (!System.IO.File.Exists(filename)) return (null);
            SharingEntry entry = new SharingEntry();
            entry.Filename = filename;
            try
            {
                System.IO.FileInfo fi = new FileInfo(filename);
            entry.Filesize = fi.Length;
            //now try to hash the file also
            /*
            Tiger192 tiger = new Tiger192();
            byte[] file_contents = System.IO.File.ReadAllBytes(filename);
            tiger.ComputeHash(file_contents, 0, file_contents.Length-1);
            Console.WriteLine("hash:" + Base32.GetString(tiger.Hash));
            entry.TTH = Base32.GetString(tiger.Hash);
            */
            ThexThreaded TTH = new ThexThreaded();
            entry.TTH = Base32.ToBase32String(TTH.GetTTH_Value(filename));

            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during hashing:" + ex.Message);
            }
            return (entry);
        }
        private delegate string ShareDirectoryHandler(string directory);
        private string ShareDirectoryAsync(string directory)
        {//recurse into directories
            RecurseShareDirectoryAsync(directory);
            return (directory);
        }
        private void RecurseShareDirectoryAsync(string directory)
        {
            string[] files = Directory.GetFiles(directory);
            string[] dirs = Directory.GetDirectories(directory);
            foreach (string dir in dirs)
            {
                if (System.IO.Directory.Exists(dir))
                {
                    RecurseShareDirectoryAsync(dir);
                }
            }
            foreach (string file in files)
            {
                if (System.IO.File.Exists(file))
                {
                    SharingEntry entry = ShareFileAsync(file);
                    if (entry != null)
                        Add(entry);
                }
            }
        }
        private void ShareFileFinished(IAsyncResult result)
        {
            ShareFileHandler sfh = (ShareFileHandler)result.AsyncState;
            SharingEntry entry = sfh.EndInvoke(result);
            if (entry != null)
                Add(entry);
        }
        private void ShareDirectoryFinished(IAsyncResult result)
        {
            ShareDirectoryHandler sdh = (ShareDirectoryHandler)result.AsyncState;
            string directory = sdh.EndInvoke(result);
            if (DirectoryFinished != null)
                DirectoryFinished(directory);
        }

        // share files with these functions

        public void ShareFile(string filename)
        {
            ShareFileHandler sfh = new ShareFileHandler(ShareFileAsync);
            IAsyncResult result = sfh.BeginInvoke(filename, new AsyncCallback(ShareFileFinished), sfh);
        }
        public void ShareDirectory(string directory)
        {
            ShareDirectoryHandler sdh = new ShareDirectoryHandler(ShareDirectoryAsync);
            IAsyncResult result = sdh.BeginInvoke(directory, new AsyncCallback(ShareDirectoryFinished), sdh);
        }

        //TODO make these both functions async too (via handler in parameters)
        public SharingEntry GetShareByFilename(string filename)
        {
            lock (share_lock)
            {
                foreach (SharingEntry entry in items)
                {
                    if (entry.Filename == filename)
                    {
                        //Console.WriteLine("Found entry by filename: "+filename);
                        return (entry);
                    }
                }
            }
            return (null);
        }
        public SharingEntry GetShareByTTH(string tth)
        {
            lock (share_lock)
            {
                foreach (SharingEntry entry in items)
                {
                    if (entry.HasTTH && entry.TTH == tth)
                    {
                        return (entry);
                    }
                }
            }
            return (null);
        }

        public void Remove(string filename)
        {
            lock (share_lock)
            {
                foreach (SharingEntry entry in items)
                {
                    if (entry.Filename == filename)
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


        public void UpdateFileLists()
        {
        }

        public string GetFileListXml()
        {
            return ("");
        }

        public string GetFileListXmlBZ2()
        {
            return ("");
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

#region Unit Testing
        [Test]
        public void TestShareFile()
        {
            Console.WriteLine("Test to share a file.");
            bool wait = true;
            Sharing s = new Sharing();
            s.EntryAdded = delegate(SharingEntry entry)
            {
                Console.WriteLine("File Added: " + entry.Filename + ", filesize: " + entry.Filesize + ",tth: " + entry.TTH);
                Assert.IsTrue(entry.Filename == "..\\..\\..\\TestDateien\\test.mp3", "Filename not correct(test.mp3).");
                Assert.IsTrue(entry.Filesize == 6053888, "Filesize not correct(test.mp3).");
                wait = false;
            };
            s.ShareFile("..\\..\\..\\TestDateien\\test.mp3");

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
            Console.WriteLine("Share File Test successful.");
        }
        [Test]
        public void TestShareDirectory()
        {
            Console.WriteLine("Test to share a directory.");
            bool wait = true;
            Sharing s = new Sharing();
            s.EntryAdded = delegate(SharingEntry entry)
            {
                Console.WriteLine("File Added: " + entry.Filename + ", filesize: " + entry.Filesize + ",tth: " + entry.TTH);
            };
            s.DirectoryFinished = delegate(string directory)
            {
                wait = false;
            };

            s.ShareDirectory("..\\..\\..\\TestDateien");

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

            //now check if items are correct
            Assert.IsTrue(s.items[0].Filename == "..\\..\\..\\TestDateien\\2sd.avi", "Filename not correct(2sd.avi).");
            Assert.IsTrue(s.items[0].Filesize == 28495872, "Filesize not correct(2sd.avi).");
            Assert.IsTrue(s.items[1].Filename == "..\\..\\..\\TestDateien\\test.mp3", "Filename not correct(test.mp3).");
            Assert.IsTrue(s.items[1].Filesize == 6053888, "Filesize not correct(test.mp3).");
            Assert.IsTrue(s.items[2].Filename == "..\\..\\..\\TestDateien\\test2.mp3", "Filename not correct(test2.mp3).");
            Assert.IsTrue(s.items[2].Filesize == 10539254, "Filesize not correct(test2.mp3).");
            Console.WriteLine("Share Directory Test successful.");
        }
        [Test]
        public void TestShareSaveLoad()
        {
            Console.WriteLine("Test to save and load shares.");
            bool wait = true;
            Sharing s = new Sharing();
            s.EntryAdded = delegate(SharingEntry entry)
            {
                //Console.WriteLine("");
                Console.WriteLine("File Added: " + entry.Filename + ", filesize: " + entry.Filesize + ",tth: " + entry.TTH);
            };
            s.DirectoryFinished = delegate(string directory)
            {
                wait = false;
            };
            s.ShareDirectory("..\\..\\..\\TestDateien");

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
            Console.WriteLine("");
            Console.WriteLine("Sharing of files completed.");
            //now check if items are correct
            Assert.IsTrue(s.items[0].Filename == "..\\..\\..\\TestDateien\\2sd.avi", "Filename not correct(2sd.avi).");
            Assert.IsTrue(s.items[0].Filesize == 28495872, "Filesize not correct(2sd.avi).");
            Assert.IsTrue(s.items[1].Filename == "..\\..\\..\\TestDateien\\test.mp3", "Filename not correct(test.mp3).");
            Assert.IsTrue(s.items[1].Filesize == 6053888, "Filesize not correct(test.mp3).");
            Assert.IsTrue(s.items[2].Filename == "..\\..\\..\\TestDateien\\test2.mp3", "Filename not correct(test2.mp3).");
            Assert.IsTrue(s.items[2].Filesize == 10539254, "Filesize not correct(test2.mp3).");
            s.SaveSharesToXmlFile("..\\..\\..\\TestDateien\\test_shares.xml");
            Console.WriteLine("Saved Shares.");
            s.Clear();
            Console.WriteLine("Cleared Shares");
            Assert.IsTrue(s.items.Count == 0, "Clearing Shares failed.");
            s.LoadSharesFromXmlFile("..\\..\\..\\TestDateien\\test_shares.xml");
            Console.WriteLine("Shares loaded.");
            Assert.IsTrue(s.items[0].Filename == "..\\..\\..\\TestDateien\\2sd.avi", "Filename not correct(2sd.avi).");
            Assert.IsTrue(s.items[0].Filesize == 28495872, "Filesize not correct(2sd.avi).");
            Assert.IsTrue(s.items[1].Filename == "..\\..\\..\\TestDateien\\test.mp3", "Filename not correct(test.mp3).");
            Assert.IsTrue(s.items[1].Filesize == 6053888, "Filesize not correct(test.mp3).");
            Assert.IsTrue(s.items[2].Filename == "..\\..\\..\\TestDateien\\test2.mp3", "Filename not correct(test2.mp3).");
            Assert.IsTrue(s.items[2].Filesize == 10539254, "Filesize not correct(test2.mp3).");
            Console.WriteLine("Save and Load Shares Test successful.");
        }
        [Test]
        public void TestShareSearchRemove()
        {
            Console.WriteLine("Test to search and remove a share.");
            bool wait = true;
            Sharing s = new Sharing();
            s.EntryAdded = delegate(SharingEntry entry)
            {
                Console.WriteLine("File Added: " + entry.Filename + ", filesize: " + entry.Filesize + ",tth: " + entry.TTH);
            };
            s.DirectoryFinished = delegate(string directory)
            {
                wait = false;
            };
            s.ShareDirectory("..\\..\\..\\TestDateien");

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
            Console.WriteLine("Sharing of files completed.");
            //now check if items are correct
            Assert.IsTrue(s.items[0].Filename == "..\\..\\..\\TestDateien\\2sd.avi", "Filename not correct(2sd.avi).");
            Assert.IsTrue(s.items[0].Filesize == 28495872, "Filesize not correct(2sd.avi).");
            Assert.IsTrue(s.items[1].Filename == "..\\..\\..\\TestDateien\\test.mp3", "Filename not correct(test2.mp3).");
            Assert.IsTrue(s.items[1].Filesize == 6053888, "Filesize not correct(test.mp3).");
            Assert.IsTrue(s.items[2].Filename == "..\\..\\..\\TestDateien\\test2.mp3", "Filename not correct(test2.mp3).");
            Assert.IsTrue(s.items[2].Filesize == 10539254, "Filesize not correct(test2.mp3).");
            int num = s.items.Count;
            SharingEntry found = s.GetShareByFilename("..\\..\\..\\TestDateien\\test.mp3");
            Assert.IsTrue(found != null, "Searching Share failed.");
            s.Remove(found);
            Console.WriteLine("Removed Share");
            Assert.IsTrue(s.items.Count == num - 1, "Removing Share failed(test.mp3).");
            found = s.GetShareByFilename("..\\..\\..\\TestDateien\\test.mp3");
            Assert.IsTrue(found == null, "Removing Share failed(test.mp3).");
            s.Remove("..\\..\\..\\TestDateien\\test2.mp3");
            Console.WriteLine("Removed another Share");
            Assert.IsTrue(s.items.Count == num - 2, "Removing Share failed(test2.mp3).");
            found = s.GetShareByFilename("..\\..\\..\\TestDateien\\test2.mp3");
            Assert.IsTrue(found == null, "Removing Share failed(test2.mp3).");
            Console.WriteLine("Search and Remove Test successful.");
        }
        [Test]
        public void TestTTHs()
        {
            Console.WriteLine("Test to see if correct TTHs were created.");
            bool wait = true;
            Sharing s = new Sharing();
            s.EntryAdded = delegate(SharingEntry entry)
            {
                Console.WriteLine("File Added: " + entry.Filename + ", filesize: " + entry.Filesize + ",tth: " + entry.TTH);
            };
            s.DirectoryFinished = delegate(string directory)
            {
                wait = false;
            };
            s.ShareDirectory("..\\..\\..\\TestDateien");

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

            //now check if items are correct
            Assert.IsTrue(s.items[0].Filename == "..\\..\\..\\TestDateien\\2sd.avi", "Filename not correct(2sd.avi).");
            Assert.IsTrue(s.items[0].TTH == "QNGNAPOTVVZRPGSQPOH5X4RWITB3OI27KWXGCEI", "TTH not correct(2sd.avi)(\"" + s.items[0].TTH + "\"!=\"QNGNAPOTVVZRPGSQPOH5X4RWITB3OI27KWXGCEI\").");
            Assert.IsTrue(s.items[0].Filesize == 28495872, "Filesize not correct(2sd.avi).");
            Assert.IsTrue(s.items[1].Filename == "..\\..\\..\\TestDateien\\test.mp3", "Filename not correct(test.mp3).");
            Assert.IsTrue(s.items[1].TTH == "LODVHCUGIS5G534HRWG4LIPXT5TPIO4SS6D2KKI", "TTH not correct(test.mp3)(\"" + s.items[1].TTH + "\"!=\"LODVHCUGIS5G534HRWG4LIPXT5TPIO4SS6D2KKI\").");
            Assert.IsTrue(s.items[1].Filesize == 6053888, "Filesize not correct(test.mp3).");
            Assert.IsTrue(s.items[2].Filename == "..\\..\\..\\TestDateien\\test2.mp3", "Filename not correct(test2.mp3).");
            Assert.IsTrue(s.items[2].TTH == "6CFXRPW5GWT5NQGAU3DYZOCQBAYM63WST5J3HAY", "TTH not correct(test2.mp3)(\"" + s.items[2].TTH + "\"!=\"6CFXRPW5GWT5NQGAU3DYZOCQBAYM63WST5J3HAY\").");
            Assert.IsTrue(s.items[2].Filesize == 10539254, "Filesize not correct(test2.mp3).");
            Console.WriteLine("TTHs Creation Test successful.");
        }
#endregion
    }
}
