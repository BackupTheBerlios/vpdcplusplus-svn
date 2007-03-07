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
    /// <summary>
    /// a simple class
    /// to check for open ports
    /// (uses a third party webpage for this)
    /// </summary>
    [TestFixture]
    public class PortCheck
    {
        /// <summary>
        /// Event handler that gets called
        /// when a open ports check was completed
        /// </summary>
        public event CompletedEventHandler Completed;
        /// <summary>
        /// Event handler that gets called
        /// when the progress of the port check changed
        /// </summary>
        public event ProgressChangedEventHandler ProgressChanged;
        /// <summary>
        /// Event handler that gets called
        /// when the port check was unable to complete
        /// </summary>
        public event UnableToFetchEventHandler UnableToFetch;
        /// <summary>
        /// Prototype for the Completed Event Handler
        /// </summary>
        /// <param name="ex_ip"></param>
        public delegate void CompletedEventHandler(PortCheck ex_ip);
        /// <summary>
        /// Prototype for the Progress Changed Event Handler
        /// </summary>
        /// <param name="ex_ip"></param>
        public delegate void ProgressChangedEventHandler(PortCheck ex_ip);
        /// <summary>
        /// Prototype for the Unable To Fetch Event Handler
        /// </summary>
        /// <param name="ex_ip"></param>
        public delegate void UnableToFetchEventHandler(PortCheck ex_ip);
        protected Connection.ErrorCodes error_code = Connection.ErrorCodes.NoErrorYet;
        /// <summary>
        /// Contains the error code if something went wrong with the open ports check
        /// </summary>
        public Connection.ErrorCodes ErrorCode
        {
            get
            {
                return (error_code);
            }
        }
        protected int percentage = 0;
        /// <summary>
        /// Get the progress percentage of the open ports check
        /// </summary>
        public int Percentage
        {
            get
            {
                return (percentage);
            }
        }
        /// <summary>
        /// enumeration of possible 
        /// open ports combinations
        /// </summary>
        public enum Ports
        {
            UdpAndTcp,Udp,Tcp,None
        };
        /// <summary>
        /// Get the open Ports that the test found
        /// </summary>
        public Ports OpenPorts = Ports.None;
        protected string my_ip;
        /// <summary>
        /// Get/Set the ip to check the ports for
        /// </summary>
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
        /// <summary>
        /// Get/Set the client name we want to send to the ports checking service
        /// </summary>
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
        /// <summary>
        /// Get/Set the udp port to check
        /// </summary>
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
        /// <summary>
        /// Get/set the tcp port to check
        /// </summary>
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
        /// <summary>
        /// TRUE if an open ports check if running
        /// </summary>
        public bool IsBusy
        {
            get
            {
                return (busy);
            }
        }
        /// <summary>
        /// our webclient we use to communicate with the p2p-ports service
        /// </summary>
        private WebClient wc = new WebClient();
        /// <summary>
        /// PortCheck Constructor
        /// </summary>
        public PortCheck()
        {
            my_ip = "";
            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgressCallback);
            wc.DownloadDataCompleted += new DownloadDataCompletedEventHandler(DownloadFileCallback);

        }
        /// <summary>
        /// our url to the port checking service we are using
        /// </summary>
        private string url = "http://connect.majestyc.net/";
        /// <summary>
        /// Start checking of open ports
        /// </summary>
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
        /// <summary>
        /// Abort checking
        /// </summary>
        public void AbortCheck()
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
        /// <summary>
        /// Async callback for webclients get file operation
        /// ,gets called if the file was retrieved successfully
        /// </summary>
        /// <param name="sender">event sending webclient instance</param>
        /// <param name="e">event arguments of the download operation</param>
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
        /// <summary>
        /// Async callback for webclients get file operation
        /// ,gets called when the progress of the download changes
        /// </summary>
        /// <param name="sender">event sending webclient instance</param>
        /// <param name="e">event arguments of the download operation</param>
        private void DownloadProgressCallback(object sender, DownloadProgressChangedEventArgs e)
        {
            percentage = e.ProgressPercentage;
            if (ProgressChanged != null)
                ProgressChanged(this);
        }
#region Unit Testing
        /// <summary>
        /// Test to see if our port checking method works
        /// </summary>
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
        /// <summary>
        /// Test to see if our local ports are available to the outside world
        /// </summary>
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
                    if(port_check_completed.OpenPorts == Ports.None || port_check_completed.OpenPorts == Ports.Udp)
                        Assert.Fail("Test failed: tcp port not open!");
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
        /// <summary>
        /// Test to see if a failed check will not crash the client
        /// or throw unexpected exceptions
        /// </summary>
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