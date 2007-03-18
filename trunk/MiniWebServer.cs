using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;

namespace DCPlusPlus
{
    /// <summary>
    /// a very simple webserver
    /// </summary>
    public class MiniWebServer
    {
        public class Request
        {

        }

        public delegate void RequestReceivedEventHandler(MiniWebServer server,Request request);
        public event RequestReceivedEventHandler RequestReceived;

        private int port;

        public int Port
        {
            get { return port; }
            set { port = value; }
        }

        private Socket socket=null;

        public void SetupListeningSocket()
        {
        }

        public MiniWebServer()
        {

        }

    }
}
