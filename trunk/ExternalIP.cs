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
        public delegate void CompletedEventHandler(object sender, string external_ip);
        public delegate void ProgressChangedEventHandler(object sender, int percentage);
        public delegate void ErrorEventHandler(object sender, int ErrorCode, string ErrorMessage);

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

        public event CompletedEventHandler Completed;
        public event ProgressChangedEventHandler ProgressChanged;
        public event ErrorEventHandler Error;

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
                if (ProgressChanged != null)
                    ProgressChanged(this, 0);
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
                Console.WriteLine("Exception after download: "+ex.Message);
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
                    char[] trims = {'\n',' '};
                    temp_ip = temp_ip.Trim(trims);

                    //Console.WriteLine("temp_ip: '"+temp_ip+"'");
                    my_ip = temp_ip;
                    busy = false;
                    if (Completed != null)
                        Completed(this, my_ip);
                }
            }
        }

        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            if (ProgressChanged != null)
                ProgressChanged(this, e.ProgressPercentage);
        }

        #region Unit Testing
        [Test]
        public void TestResolve()
        {
            bool wait = true;
            ExternalIP ex_ip = new ExternalIP();
            ex_ip.Completed += delegate(object sender, string external_ip)
            {
                Console.WriteLine("Fetch Completed (ip found : " + external_ip + ")");
                wait = false;
            };
            ex_ip.FetchIP();
            Console.WriteLine("Waiting for data");
            while (wait)
            {
                Console.Write(".");
                Thread.Sleep(250);
            }


        }
        #endregion
    }
}