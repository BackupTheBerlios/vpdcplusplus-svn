using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Collections;
using System.Threading;
using NUnit.Framework;
//using Jcs.Tiger;
using ThexCS;
using ICSharpCode.SharpZipLib;


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

        protected long total_bytes_shared = 0;
        [XmlIgnoreAttribute]
        public long TotalBytesShared
        {
            get
            {
                
                return (total_bytes_shared);
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
            if (!Directory.Exists(directory)) return;
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
                    {
                        total_bytes_shared += entry.Filesize;
                        Add(entry);
                    }
                }
            }
        }
        private void ShareFileFinished(IAsyncResult result)
        {
            ShareFileHandler sfh = (ShareFileHandler)result.AsyncState;
            SharingEntry entry = sfh.EndInvoke(result);
            if (entry != null)
            {
                total_bytes_shared += entry.Filesize;
                Add(entry);
            }
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

        public SharingEntry GetShareByFileRequest(string file_request)
        {
            if(file_request.StartsWith("TTH/"))
                return(GetShareByTTH(file_request.Substring(4)));
            lock (share_lock)
            {
                foreach (SharingEntry entry in items)
                {
                    if (entry.Filename.EndsWith(file_request)) //maybe change this to a more sophisticated approach
                    {
                        //Console.WriteLine("Found entry by filename: "+filename);
                        return (entry);
                    }
                }
            }
            return (null);

        }
        //TODO make these both functions async too (via handler in parameters)
        public SharingEntry GetShareByFilename(string filename)
        {
            lock (share_lock)
            {
                foreach (SharingEntry entry in items)
                {
                    if (entry.Filename.EndsWith(filename)) //maybe change this to a more sophisticated approach
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
                        total_bytes_shared -= entry.Filesize;
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
                            total_bytes_shared += entry.Filesize;
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
                    ret = System.Text.Encoding.Default.GetString(ms.GetBuffer(), 0, (int)ms.Length);//TODO ... 4gb crash border
                    //ret = ret.TrimEnd((char)0);

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
                if (File.Exists(filename + ".backup") && File.Exists(filename))
                    File.Delete(filename + ".backup");
                if (File.Exists(filename))
                    File.Move(filename, filename + ".backup");

                System.IO.File.WriteAllText(filename, SaveSharesToXml());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving queue to: " + filename + " : " + ex.Message);
            }
        }

        //string file_list

        public void UpdateFileLists()
        {
        }
        protected string cid = "D2QLOGUYDX3QA";
        public string CID
        {
            get
            {
                return (cid);
            }
            set
            {
                cid = value;
            }
        }
        protected string generator = "vpDcPlusPlus 0.2";
        public string Generator
        {
            get
            {
                return (generator);
            }
            set
            {
                generator = value;
            }
        }
        public class DirectoryContents
        {
            public string directory_name = "";
            public List<SharingEntry> files = new List<SharingEntry>();
            public List<DirectoryContents> directories = new List<DirectoryContents>();
        }
 
        private static string ToXmlString(string org)
        {
            if (String.IsNullOrEmpty(org)) return ("");

            string tmp = new string(org.ToCharArray());
            //System.Console.WriteLine("ToXml on string content: '" + tmp+"'");
            int p = 0;
            //while ((p = tmp.IndexOf("&", p)) != -1) { tmp = tmp.Replace("&", "&amp;"); if(p<tmp.Length)p++; }
            List<int> amps = new List<int>();
            while ((p = tmp.IndexOf("&", p)) != -1)
            {
                //System.Console.WriteLine("ToXml add amp: '" + p + "'");
                amps.Add(p);
                if (p < tmp.Length - 1) p++;
                else break;


            }
            //System.Console.WriteLine("ToXml amp count: '" + amps.Count + "'");
            for (int i = 0; i < amps.Count; i++)
            {
                //System.Console.WriteLine("ToXml amps["+i+"]: '" + amps[i] + "' length of tmp:"+tmp.Length);
                tmp = tmp.Remove(amps[i] + (i * 4), 1);
                tmp = tmp.Insert(amps[i] + (i * 4), "&amp;");
                //System.Console.WriteLine("ToXml tmp after amp conversion: '" + tmp + "'");
            }


            while (tmp.IndexOf("<") != -1) tmp = tmp.Replace("<", "&lt;");
            while (tmp.IndexOf(">") != -1) tmp = tmp.Replace(">", "&gt;");
            while (tmp.IndexOf("\"") != -1) tmp = tmp.Replace("\"", "&quot;");
            while (tmp.IndexOf("'") != -1) tmp = tmp.Replace("'", "&apos;");
            for (int i = 0; i < 32; i++)
            {
                char c = Convert.ToChar(i);
                if (i != 0x09 && i != 0x0a && i != 0x0d)
                {
                    int pos = -1;
                    while ((pos = tmp.IndexOf(c)) != -1)
                    {
                        tmp = tmp.Remove(pos, 1);
                        tmp = tmp.Insert(pos, "&#" + Convert.ToString(c, 16) + ";");
                    }
                }
            }
            //System.Console.WriteLine("after multiple newline remove check: '" + tmp + "'");
            return (tmp);
        }
        private static string FromXmlString(string org)
        {
            if (org == null) return ("");
            string tmp = new string(org.ToCharArray());
            //System.Console.WriteLine("Stripping Linefeeds on string content: '" + tmp+"'");
            while (tmp.IndexOf("&amp;") != -1) tmp = tmp.Replace("&amp;", "&");
            while (tmp.IndexOf("&lt;") != -1) tmp = tmp.Replace("&lt;", "<");
            while (tmp.IndexOf("&gt;") != -1) tmp = tmp.Replace("&gt;", ">");
            while (tmp.IndexOf("&quot;") != -1) tmp = tmp.Replace("&quot;", "\"");
            while (tmp.IndexOf("&apos;") != -1) tmp = tmp.Replace("&apos;", "'");
            for (int i = 0; i < 32; i++)
            {
                char c = Convert.ToChar(i);
                if (i != 0x09 && i != 0x0a && i != 0x0d)
                {
                    int pos = -1;
                    string s = "&#" + Convert.ToString(c, 16) + ";";
                    while ((pos = tmp.IndexOf(s)) != -1)
                    {
                        tmp = tmp.Remove(pos, s.Length);
                        tmp = tmp.Insert(pos, c.ToString());
                    }
                }
            }
            //System.Console.WriteLine("after multiple newline remove check: '" + tmp + "'");
            return (tmp);
        }
        private DirectoryContents FindExistingDirectory(DirectoryContents dc,string directory_name)
        {
            foreach (DirectoryContents dir in dc.directories)
            {
                if (dir.directory_name == directory_name) return (dir);
            }
            return (null);
        }
        private void FillDirectories(DirectoryContents root)
        {
            lock (share_lock)
            {
                foreach (SharingEntry entry in items)
                {
                    string path = Path.GetDirectoryName(entry.Filename);
                    path = path.Substring(3);//strip away the drive letter --> WIN32 specific!!!! needs to be changed if other platform shall be supported
                    char[] seps = {'\\'};
                    string[] paths = path.Split(seps);
                    DirectoryContents actual = root;
                    foreach (string path_part in paths)
                    {
                        DirectoryContents existing = FindExistingDirectory(actual,path_part);
                        if (existing == null)
                        {
                            DirectoryContents add = new DirectoryContents();
                            add.directory_name = path_part;
                            actual.directories.Add(add);
                            existing = add;
                        }
                        actual = existing;
                    }
                    if(!actual.files.Contains(entry))
                        actual.files.Add(entry);


                }
            }
        }
        private bool CleanDirectories(DirectoryContents dc)
        {//cleans directorycontents class of empty shares
            //check if this dir is empty and shall be removed
            foreach (DirectoryContents dir in dc.directories)
            {
                if (!CleanDirectories(dir))
                    dc.directories.Remove(dir); //hope this will not throw an exception
            }
            if (dc.directories.Count == 0 && dc.files.Count == 0)
            {
                return (false);
            }
            else if (dc.directories.Count == 1 && dc.files.Count == 0)
            {//only one shared dir here .. exchange this , this may lead into problems of incorrect tree... investigation needed
                dc = dc.directories[0];
            }
            return (true);
        }
        private string GetDirectoryContentsString(DirectoryContents dc)
        {//recursively get all contents in one string
            string dir_string = "";
            if (dc.directory_name != "")
                dir_string += "<Directory Name=\"" + ToXmlString(dc.directory_name) + "\">\n";
            foreach (DirectoryContents dir in dc.directories)
            {
                dir_string += GetDirectoryContentsString(dir);
            }
            if (dc.directory_name != "")
            {
                foreach (SharingEntry file in dc.files)
                {
                    dir_string += "<File Name=\"" + ToXmlString(Path.GetFileName(file.Filename)) + "\" Size=\"" + file.Filesize + "\" TTH=\"" + file.TTH + "\"/>\n";
                }
                dir_string += "</Directory>\n";
            }
            return (dir_string);
        }
        public string GetFileListXml()
        {
            string xml = "<?xml version=\"1.0\" encoding=\"utf-8\" standalone=\"yes\"?>\n";
            xml += "<FileListing Version=\"1\" CID=\""+cid+"\" Base=\"/\" Generator=\""+generator+"\">\n";
            //hierarchical tree that lists all directories that have
            // 1. a shared file in it
            // 2. at least two directories in it that have shared files in its tree
            //use DirectoryContents class to hold data structure
            //combine directoryContents to xml string
            //return xml string
            DirectoryContents root = new DirectoryContents();
            FillDirectories(root);
            if (!CleanDirectories(root)) return (""); //empty shares
            xml += GetDirectoryContentsString(root);
            xml += "</FileListing>\n";
            return (xml);
        }
        public byte[] GetFileListXmlBZ2()
        {
            try
            {
                string file_list = GetFileListXml();
                MemoryStream input = new MemoryStream(System.Text.Encoding.Default.GetBytes(file_list));
                MemoryStream output = new MemoryStream();
                ICSharpCode.SharpZipLib.BZip2.BZip2.Compress(input, output, 1024);
                input.Flush();
                byte[] out_data = output.GetBuffer();
                //string hubs_string = System.Text.Encoding.Default.GetString(out_data);
                return (out_data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error compressing file list: "+ex.Message);
                return (null);
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
        [Test]
        public void TestSharingNotExistingDirectory()
        {
            Console.WriteLine("Test to see if no files were shared.");
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
            s.ShareDirectory("..\\..\\..\\NotExistingDirectory");

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
            Assert.IsTrue(s.items.Count == 0, "Test failed : More than none files were shared.");
            Console.WriteLine("Sharing Empty Dir Test successful.");
        }

        [Test]
        public void TestEmptyGetFileListXml()
        {
            Console.WriteLine("Test to see if a correct empty filelist was created.");
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
            s.ShareDirectory("..\\..\\..\\TestFileListEmpty"); 

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
            string file_list = s.GetFileListXml();
            Console.WriteLine("\nfilelist:\n" + file_list);

            //now check if items are correct
            Assert.IsTrue(file_list == "", "Empty FileList expected.");
            Console.WriteLine("Empty GetFilesList Creation Test successful.");
        }

        [Test]
        public void TestGetFileListXml()
        {
            Console.WriteLine("Test to see if a correct filelist was created.");
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
            s.ShareDirectory("..\\..\\..\\TestFileList");

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
            string file_list = s.GetFileListXml();
            Console.WriteLine("\nfilelist:\n" + file_list);

            //now check if items are correct
            Assert.IsTrue(file_list == "", "Empty FileList expected.");
            Console.WriteLine("GetFilesList Creation Test successful.");
        }


#endregion

    }
}
