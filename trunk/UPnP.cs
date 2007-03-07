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
    /// A simple class to use Routers
    /// who support the UPnP Protocol
    /// takes care of discovery and
    /// can setup port mappings and
    /// tell the external ip address
    /// </summary>
    [TestFixture]
    public class UPnP
    {
     /// <summary>
     /// A class to use UPnP supporting Routers
     /// </summary>
        public class Router
        {
            //TODO include external ip and functions to add port mappings
            public Router(UPnP upnp)
            {
                //give upnp class as parameter to use the upnp protocol functions
            }
        }

        /// <summary>
        /// Prototype for the Router Discovered Event Handler
        /// </summary>
        public event RouterDiscoveredEventHandler RouterDiscovered; 
        /// <summary>
        /// Prototype for the Router State Changed Event Handler
        /// </summary>
        public event RouterStateChangedEventHandler RouterStateChanged;
        /// <summary>
        /// Event handler that gets called
        /// when a UPnP Router was discovered in the lan
        /// </summary>
        /// <param name="router">the router that was discovered</param>
        public delegate void RouterDiscoveredEventHandler(Router router);
        /// <summary>
        /// Event handler that gets called
        /// when a state of a UPnP Router has changed
        /// </summary>
        /// <param name="router">the router which state has changed</param>
        public delegate void RouterStateChangedEventHandler(Router router);

        /// <summary>
        /// Start an UPnP service discovery
        /// </summary>
        public void StartDiscovery()
        {
        }

        //TODO maybe better off if these are public
        private void SetupListeningSocket()
        {
        }
        private void CloseListeningSocket()
        {
        }



        //TODO add udp listening socket and http protocol stuff here

        #region Unit Testing
        /// <summary>
        /// Test to see if discovery works the way it should
        /// (a upnp router in your lan is need to finish this test successfully)
        /// </summary>
        [Test]
        public void TestDiscovery()
        {
        }

        #endregion
    }
}
