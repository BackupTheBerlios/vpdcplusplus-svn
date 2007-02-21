using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Xml;
using System.IO;


namespace DCPlusPlus
{
    [TestFixture]
    public class ExternalIP
    {
        public event CompletedEventHandler Completed;
        public event ProgressChangedEventHandler ProgressChanged;
        public event UnableToFetchEventHandler UnableToFetch;
        public delegate void CompletedEventHandler(ExternalIP ex_ip);
        public delegate void ProgressChangedEventHandler(ExternalIP ex_ip);
        public delegate void UnableToFetchEventHandler(ExternalIP ex_ip);
        protected Connection.ErrorCodes error_code = Connection.ErrorCodes.NoErrorYet;
        public Connection.ErrorCodes ErrorCode
        {
            get
            {
                return (error_code);
            }
        }
        protected int percentage = 0;
        public int Percentage
        {
            get
            {
                return (percentage);
            }
        }
        protected string my_ip;
        public string MyIP
        {
            get
            {
                return (my_ip);
            }
            set
            {
                my_ip = value;
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
        private WebClient wc = new WebClient();
        public ExternalIP()
        {
            my_ip = "";
            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            wc.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCallback);

        }
        private string url = "http://www.lawrencegoetz.com/programs/ipinfo/";
        public void FetchIP()
        {
            if (!busy)
            {
                busy = true;
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
                }
            }

        }
        public void AbortFetch()
        {
            if (busy)
            {
                try
                {
                    wc.CancelAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception occured during abort: " + ex.Message);
                }
                System.Threading.Thread.Sleep(10);
                //wc.IsBusy
                //wc = new WebClient();
                busy = false;
            }
        }
        private void DownloadFileCallback(object sender, DownloadDataCompletedEventArgs e)
        {
            try
            {
                if (e.Cancelled) return;
                //ASCIIEncoding ascii = new ASCIIEncoding();
                //UTF8Encoding utf = new UTF8Encoding();
                //string page_string = utf.GetString(e.Result);
                if (e.Result.Length <= 0) return;

                string page_string = "";
                try
                {
                    page_string = System.Text.Encoding.Default.GetString(e.Result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception after download: " + ex.Message);
                    return;
                }

                int start = page_string.IndexOf("<h1>Your IP address is<BR>\n");
                if (start != -1)
                {
                    string temp_ip = page_string.Substring(start + "<h1>Your IP address is<BR>".Length);
                    int end = temp_ip.IndexOf("</h1>");
                    if (end != -1)
                    {
                        temp_ip = temp_ip.Substring(0, end);
                        char[] trims = { '\n', ' ' };
                        temp_ip = temp_ip.Trim(trims);

                        //Console.WriteLine("temp_ip: '"+temp_ip+"'");
                        my_ip = temp_ip;
                        busy = false;
                        try
                        {
                            if (Completed != null)
                                Completed(this);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception during callback of own external ip resolve: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception out_ex)
            {
                Console.WriteLine("Exception during download of ip page: "+out_ex.Message);
                error_code = Connection.ErrorCodes.Exception;
                if (UnableToFetch != null)
                    UnableToFetch(this);

            }
        }
        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            percentage = e.ProgressPercentage;
            if (ProgressChanged != null)
                ProgressChanged(this);
        }
#region Unit Testing
        [Test]
        public void TestResolve()
        {
            Console.WriteLine("Test to resolve own external ip.");
            bool wait = true;
            ExternalIP ex_ip = new ExternalIP();
            ex_ip.Completed += delegate(ExternalIP ex_ip_completed)
            {
                Console.WriteLine("");
                Console.WriteLine("Fetch Completed (ip found : " + ex_ip_completed.MyIP + ")");
                Assert.IsTrue(!string.IsNullOrEmpty(ex_ip_completed.MyIP), "no ip address fetched");
                wait = false;
            };
            ex_ip.FetchIP();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 5))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("External IP resolve Test successful.");
        }
        [Test]
        public void TestResolveFailServiceOffine()
        {
            Console.WriteLine("Test to fail resolve own external ip.");
            bool wait = true;
            ExternalIP ex_ip = new ExternalIP();
            ex_ip.url = "http://bogus.url";
            ex_ip.Completed += delegate(ExternalIP ex_ip_completed)
            {
                Console.WriteLine("");
                Console.WriteLine("Fetch Completed (ip found : " + ex_ip_completed.MyIP + ")");
                Assert.Fail("Failed at failing ;-(");
            };
            ex_ip.UnableToFetch += delegate(ExternalIP ex_ip_unable)
            {
                Console.WriteLine("");
                Console.WriteLine("Failed to fetch ip page.");
                wait = false;
                
            };

            ex_ip.FetchIP();
            Console.WriteLine("Waiting for data");
            DateTime start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - start > new TimeSpan(0, 0, 5))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("Failed External IP resolve Test successful.");
        }
 
#endregion
    }
}