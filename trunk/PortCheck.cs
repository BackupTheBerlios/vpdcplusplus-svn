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
    public class PortCheck
    {
        public event CompletedEventHandler Completed;
        public event ProgressChangedEventHandler ProgressChanged;
        public event UnableToFetchEventHandler UnableToFetch;
        public delegate void CompletedEventHandler(PortCheck ex_ip);
        public delegate void ProgressChangedEventHandler(PortCheck ex_ip);
        public delegate void UnableToFetchEventHandler(PortCheck ex_ip);
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
        public enum Ports
        {
            UdpAndTcp,Udp,Tcp,None
        };
        public Ports OpenPorts = Ports.None;
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
        protected string my_client_name="c#++";
        public string MyClientName
        {
            get
            {
                return (my_client_name);
            }
            set
            {
                my_client_name = value;
            }

        }
        protected int my_udp_port;
        public int MyUdpPort
        {
            get
            {
                return (my_udp_port);
            }
            set
            {
                my_udp_port = value;
            }

        }
        protected int my_tcp_port;
        public int MyTcpPort
        {
            get
            {
                return (my_tcp_port);
            }
            set
            {
                my_tcp_port = value;
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
        public PortCheck()
        {
            my_ip = "";
            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            wc.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCallback);

        }
        private string url = "http://connect.majestyc.net/";
        public void CheckPorts()
        {
            if (!busy)
            {
                busy = true;
                percentage = 0;
                if (ProgressChanged != null)
                    ProgressChanged(this);
                try
                {
                    wc.DownloadDataAsync(new Uri(url+"?i="+my_ip+"&t="+my_tcp_port+"&u="+my_udp_port+"&c="+my_client_name));
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
                if (e.Result.Length <= 0) return;
                string page_string = "";
                page_string = System.Text.Encoding.Default.GetString(e.Result);
                //Console.WriteLine("port page: " + page_string);
                int start = page_string.IndexOf("<strong class=\"");
                if (start != -1)
                {
                    string temp = page_string.Substring(start + "<strong class=\"".Length);
                    int end = temp.IndexOf("\"");
                    if (end != -1)
                    {
                        string tcp = temp.Substring(0, end);
                        if (tcp == "green small")
                            OpenPorts = Ports.Tcp;
                        else OpenPorts = Ports.None;

                        start = temp.IndexOf("<strong class=\"");
                        if (start != -1)
                        {
                            string temp2 = temp.Substring(start + "<strong class=\"".Length);
                            end = temp2.IndexOf("\"");
                            if (end != -1)
                            {
                                string udp = temp2.Substring(0, end);
                                if (udp == "green small" && OpenPorts == Ports.None)
                                    OpenPorts = Ports.Udp;
                                if (udp == "green small" && OpenPorts == Ports.Tcp)
                                    OpenPorts = Ports.UdpAndTcp;

                                busy = false;
                                if (Completed != null)
                                    Completed(this);
                            }
                        }
                    }
                }
            }
            catch (Exception out_ex)
            {
                Console.WriteLine("Exception during download of port page: " + out_ex.Message);
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
        public void TestCheck()
        {
            Console.WriteLine("Test to check open ports.");
            bool wait = true;

            //Assert.IsTrue(!string.IsNullOrEmpty(port_check_completed.MyIP), "no ip address fetched");
            ExternalIP ex_ip = new ExternalIP();
            ex_ip.Completed += delegate(ExternalIP ex_ip_completed)
            {
                wait = true;
                Console.WriteLine("Fetch Completed (ip found : " + ex_ip_completed.MyIP + ")");
                PortCheck port_check = new PortCheck();
                port_check.MyIP = ex_ip.MyIP;
                port_check.MyTcpPort = 3412;
                port_check.MyUdpPort = 3412;

                port_check.Completed += delegate(PortCheck port_check_completed)
                {
                    Console.WriteLine("");
                    Console.WriteLine("Check Completed (open ports : " + Enum.GetName(typeof(Ports), port_check_completed.OpenPorts) + ")");
                    wait = false;
                };
                port_check.CheckPorts();

            };
            ex_ip.FetchIP();
            Console.WriteLine("Waiting for data");
            DateTime ip_start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - ip_start > new TimeSpan(0, 0, 35))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("PortCheck open ports Test successful.");

        }
        [Test]
        public void TestCheckRunningLocalPeers()
        {
            Console.WriteLine("Test to check running open ports.");
            bool wait = true;
            ListeningSockets ls = new ListeningSockets();
            ls.TcpPort = 3412;
            ls.UdpPort = 3412;
            ls.UpdateConnectionSettings();

            //Assert.IsTrue(!string.IsNullOrEmpty(port_check_completed.MyIP), "no ip address fetched");
            ExternalIP ex_ip = new ExternalIP();
            ex_ip.Completed += delegate(ExternalIP ex_ip_completed)
            {
                wait = true;
                Console.WriteLine("Fetch Completed (ip found : " + ex_ip_completed.MyIP + ")");
                PortCheck port_check = new PortCheck();
                port_check.MyIP = ex_ip.MyIP;
                port_check.MyTcpPort = 3412;
                port_check.MyUdpPort = 3412;

                port_check.Completed += delegate(PortCheck port_check_completed)
                {
                    Console.WriteLine("");
                    Console.WriteLine("Check Completed (open ports : " + Enum.GetName(typeof(Ports), port_check_completed.OpenPorts) + ")");
                    wait = false;
                };
                port_check.CheckPorts();

            };
            ex_ip.FetchIP();
            Console.WriteLine("Waiting for data");
            DateTime ip_start = DateTime.Now;
            while (wait)
            {
                if (DateTime.Now - ip_start > new TimeSpan(0, 0, 35))
                {
                    Console.WriteLine("");
                    Console.WriteLine("Operation took too long");
                    wait = false;
                    Assert.Fail("Operation took too long");
                }
                Console.Write(".");
                Thread.Sleep(250);
            }
            Console.WriteLine("PortCheck running open ports Test successful.");

        }
        [Test]
        public void TestCheckFailed()
        {
            Console.WriteLine("Test to fail checking open ports.");
            bool wait = true;
            PortCheck port_check = new PortCheck();
            port_check.url = "http://bogus.url";
            port_check.Completed += delegate(PortCheck port_check_completed)
            {
                Console.WriteLine("");
                Console.WriteLine("Check Completed.");
                Assert.Fail("Failed at failing ;-(");
            };
            port_check.UnableToFetch += delegate(PortCheck port_check_unable)
            {
                Console.WriteLine("");
                Console.WriteLine("Failed to fetch check page.");
                wait = false;
                
            };

            port_check.CheckPorts();
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
            Console.WriteLine("Failed PortCheck Test successful.");
        }
#endregion
    }
}