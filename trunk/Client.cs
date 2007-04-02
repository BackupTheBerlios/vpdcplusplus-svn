using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

namespace DCPlusPlus
{
    /// <summary>
    /// the main class of our library
    /// were all spider threads lead together
    /// handles most of the tidious work a client 
    /// has to do
    /// </summary>
    public class Client
    {
        //TODO peer_lock
        //search results lock
        protected Object peers_lock = new Object();
        /*public Object PeersLock
        {
            get
            {
                return (peers_lock);
            }
            set
            {
                peers_lock = value;
            }
        }*/
        protected List<Peer> peers = new List<Peer>();
        /// <summary>
        /// List of connected peers 
        /// </summary>
        public List<Peer> Peers
        {
            get
            {
                return (peers);
            }
        }
        //private Object download_queue_lock = "";
        protected Queue download_queue = new Queue();
        /// <summary>
        /// our download queue 
        /// </summary>
        public Queue DownloadQueue
        {
            get
            {
                return (download_queue);
            }
        }
        protected SearchResults search_results = new SearchResults();
        /// <summary>
        /// a storage for received search results
        /// </summary>
        public SearchResults SearchResults
        {
            get
            {
                return (search_results);
            }
        }
        protected ListeningSockets local_peer = new ListeningSockets();
        /// <summary>
        /// our local peers handling instance
        /// </summary>
        public ListeningSockets LocalPeer
        {
            get
            {
                return (local_peer);
            }
        }
        protected Sharing shares = new Sharing();
        /// <summary>
        /// our shares to the world
        /// </summary>
        public Sharing Shares
        {
            get
            {
                return (shares);
            }
        }
        protected Object connected_hubs_lock = new Object(); //TODO rename connected hubs
        /// <summary>
        /// lock for connected hubs operations thread safety
        /// (deprecated ,can cause huge problems)
        /// </summary>
        public Object ConnectedHubsLock
        {
            get
            {
                return (connected_hubs_lock);
            }
            set
            {
                connected_hubs_lock = value;
            }
        }
        protected List<Hub> connected_hubs = new List<Hub>();
        /// <summary>
        /// a list of connected hubs
        /// (deprecated, will soon be only private)
        /// </summary>
        public List<Hub> ConnectedHubs
        {
            get
            {
                return (connected_hubs);
            }
        }
        protected string nick = "unknown";
        /// <summary>
        /// the nickname we want to use
        /// </summary>
        public string Nick
        {
            get
            {
                return (nick);
            }
            set
            {//set nick on all connected hubs if differed from last nick
                nick = value;
            }
        }
        protected string connection_speed = "unknown";
        /// <summary>
        /// the connection speed of the link which connects this client to the internet
        /// </summary>
        public string ConnectionSpeed
        {
            get
            {
                return (connection_speed);
            }
            set
            {
                connection_speed = value;
            }

        }
        protected Hub.ConnectionMode connection_mode = Hub.ConnectionMode.Passive;
        /// <summary>
        /// the connection mode we want to use 
        /// active or passive
        /// passive should only be used if
        /// not port forwarding is possible at all
        /// </summary>
        public Hub.ConnectionMode ConnectionMode
        {
            get
            {
                return (connection_mode);
            }
            set
            {
                connection_mode = value;
            }

        }
        protected string version = "1,0091";
        /// <summary>
        /// the version of the client
        /// </summary>
        public string Version
        {
            get
            {
                return (version);
            }
            set
            {
                version = value;
            }
        }
        protected string tag_version = "0.698";
        /// <summary>
        /// the version of the client that is used in the myinfo tag
        /// </summary>
        public string TagVersion
        {
            get
            {
                return (tag_version);
            }
            set
            {
                tag_version = value;
            }
        }
        protected string name = "c#++";
        /// <summary>
        /// the name of the client
        /// </summary>
        public string Name
        {
            get
            {
                return (name);
            }
            set
            {
                name = value;
            }
        }
        protected string email = "unknown@unknown.net";
        /// <summary>
        /// the email address of the user
        /// </summary>
        public string Email
        {
            get
            {
                return (email);
            }
            set
            {
                email = value;
            }
        }
        protected string description = "";
        /// <summary>
        /// the user description
        /// </summary>
        public string Description
        {
            get
            {
                return (description);
            }
            set
            {
                description = value;
            }
        }
        protected long share_size = 0;
        /// <summary>
        /// the total number of shared bytes by the client
        /// (TODO change this to return shares.TotalBytesShared)
        /// </summary>
        public long ShareSize
        {
            get
            {
                return (share_size);
            }
            set
            {
                share_size = value;
            }
        }

        //TODO add last search timestamp and time to wait period ended event
        //add timer that starts on a search and fires CanSearchAgain event
        protected int search_interval = 30;
        public int SearchInterval
        {
            get
            {
                return (search_interval);
            }
            set
            {
                search_interval = value;
            }
        }
        public bool HasSearchInterval
        {
            get
            {
                if (search_interval != 0)
                    return (true);
                return (false);
            }

        }
        protected int source_interval = 30;
        public int SourceInterval
        {
            get
            {
                return (source_interval);
            }
            set
            {
                source_interval = value;
            }
        }
        public bool HasSourceInterval
        {
            get
            {
                if (source_interval != 0)
                    return (true);
                return (false);
            }

        }

        private DateTime client_start= DateTime.Now;
        /// <summary>
        /// The instant in time at which the client was started 
        /// </summary>
        public DateTime ClientStart 
        {
            get { return client_start; }
        }
        /// <summary>
        /// The Uptime of the client represented in seconds
        /// </summary>
        public long Uptime
        {
            get
            {
                TimeSpan temp = DateTime.Now - client_start;
                return ((long)temp.TotalSeconds);
            }
        }

        private DateTime last_search_time_stamp; //TODO initialize to DateTime.Now - search_interval
        private bool CanSearch()
        {
            return (true);
        }

        protected bool auto_find_alternates = false;
        public bool AutoFindAlternates
        {
            get
            {
                return (auto_find_alternates);
            }
            set
            {
                auto_find_alternates = value;
            }

        }

        protected bool auto_start_downloads = false;
        public bool AutoStartDownloads
        {
            get
            {
                return (auto_start_downloads);
            }
            set
            {
                auto_start_downloads = value;
            }
        }


        private bool auto_remove_downloads=true;
        /// <summary>
        /// if TRUE ,client will automatically remove finished downloads from queue
        /// </summary>
        public bool AutoRemoveDownloads
        {
            get { return auto_remove_downloads; }
            set { auto_remove_downloads = value; }
        }
	

        /// <summary>
        /// Search for something on all hubs
        /// this will send a search request to every
        /// connected hub
        /// </summary>
        /// <param name="search_string">the term you want to search for</param>
        public void Search(string search_string)
        {
            search_results.SearchTerm = search_string;
            lock (connected_hubs_lock)
            {
                foreach (Hub hub in connected_hubs)
                {//search on all connected hubs 
                    // add filter hubs possibilities
                    hub.Search(search_string);
                }
            }
        }
        /// <summary>
        /// Search for something on all hubs
        /// this will send a search request to every
        /// connected hub
        /// </summary>
        /// <param name="search_string">the term you want to search for</param>
        /// <param name="size_restricted">TRUE if you want to restrict your search to a specific size range</param>
        /// <param name="is_max_size">TRUE if you want to size restrict your search to a max size</param>
        /// <param name="size">the size you want to use in your size resstriction,only used if size_restricted is set to TRUE</param>
        /// <param name="file_type">the specific filetype to search for ,default will be ANY</param>
        public void Search(string search_string, bool size_restricted, bool is_max_size, int size, Hub.SearchFileType file_type)
        {
            search_results.SearchTerm = search_string;
            lock (connected_hubs_lock)
            {
                foreach (Hub hub in connected_hubs)
                {//search on all connected hubs 
                    // add filter hubs possibilities
                    hub.Search(search_string, size_restricted, is_max_size, size, file_type);
                }
            }
        }
        /// <summary>
        /// Search for something on all hubs
        /// this will send a search request to every
        /// connected hub
        /// </summary>
        /// <param name="sp">all search parameters in one single parameter</param>
        public void Search(Hub.SearchParameters sp)
        {
            search_results.SearchTerm = sp.search_string;
            lock (connected_hubs_lock)
            {
                foreach (Hub hub in connected_hubs)
                {//search on all connected hubs 
                    // add filter hubs possibilities
                    hub.Search(sp);
                }
            }
        }
        /// <summary>
        /// Search for a tth on all hubs
        /// this will send a search request to every
        /// connected hub
        /// </summary>
        /// <param name="search_tth">the tth to search for</param>
        /// <param name="is_tth">Must be TRUE,or else a normal search would be started</param>
        public void Search(string search_tth, bool is_tth)
        {
            if (!is_tth) //better to catch this case ... in case someone is using it 
                Search(search_tth);
            else
            {
                lock (connected_hubs_lock)
                {
                    foreach (Hub hub in connected_hubs)
                    {//search on all connected hubs 
                        // add filter hubs possibilities
                        hub.Search(search_tth, true);
                    }
                }
            }
        }
        /// <summary>
        /// Find more sources for a queue entry
        /// this will send a tth search to all connected hubs
        /// </summary>
        /// <param name="me">the entry for which to search alternates for</param>
        public void FindAlternateSources(Queue.QueueEntry me)
        {
            //search all hubs for tth string
            if (me != null && me.HasTTH)
                Search(me.TTH, true);
        }
        /// <summary>
        /// Interpret a received search result 
        /// (active and passive results can be handled with this method)
        /// this will automatically add sources to already existing download
        /// queue entries, these will not be shown in the search results
        /// TODO make this an option
        /// </summary>
        /// <param name="result">the result to be interpreted</param>
        private void InterpretReceivedSearchResult(SearchResults.SearchResult result)
        {
            //Console.WriteLine("Adding Result to SearchResults");
            result.Hub = ResolveHub(result.HubAddress);
            if (result.Hub != null) //only add results for hubs still connected
            {
                if (result.HasTTH)
                {//only if a result has a tth it is considered a source for some queue entry
                    Queue.QueueEntry entry = download_queue.FindQueueEntryByTTH(result.TTH);
                    if (entry != null)
                    {//this searchresult is also a source for a queue entry 
                        //,instead using of giving it back as result we add it to the source pool of the queue entry
                        entry.AddSource(new Queue.QueueEntry.Source(result.UserName, result.Filename, result.Hub));

                    } //no queue entry found for this one just hand it over to SearchResults
                    else search_results.AddResult(result);
                }
                else search_results.AddResult(result);
            }

            /* 
                    //Console.WriteLine("Adding Result to SearchResults");
                    result.Hub = ResolveHub(result.HubAddress);
                    search_results.AddResult(result);
            */
        }
        /// <summary>
        /// Start getting a file list from a user
        /// </summary>
        /// <param name="hub">the hub which the user is connected to</param>
        /// <param name="username">the user from which the file list should be downloaded from</param>
        public void GetFileList(Hub hub, string username)
        {
            download_queue.AddFileList(hub, username);
            hub.SendConnectToMe(username); //signal download to hub to start it
        }
        /// <summary>
        /// TODO Work in progress
        /// </summary>
        /// <param name="me"></param>
        public void GetTTHL(Queue.QueueEntry me)
        {
            //if (me == null) return;
            me.WantTTHL = true;
            me.StartDownload();
        }
        /// <summary>
        /// Stop downloading a queue entry
        /// this will look for a peer using this 
        /// entry and disconnect it
        /// (TODO add a PauseDownload, because this doesnt stop new
        /// connections from downloading data for this entry again)
        /// </summary>
        /// <param name="me">the queue entry to be stopped</param>
        public void StopDownload(Queue.QueueEntry me)
        {
            lock (peers_lock)
            {
                foreach (Peer peer in peers)
                {
                    if (peer.QueueEntry == me && peer.IsDownloading )
                    {
                        peers.Remove(peer);
                        peer.Disconnect();
                        break;
                    }
                }
            }
        }
        /// <summary>
        /// Start downloading a search result
        /// ,also adds a queue entry
        /// </summary>
        /// <param name="result">the search result you want to download</param>
        public void StartDownload(SearchResults.SearchResult result)
        {
            if (result.IsHubResolved)
            {//TODO put this into queue class
                download_queue.AddSearchResult(result);
                result.Hub.SendConnectToMe(result.UserName); //signal download to hub to start it
            }
            else Console.WriteLine("Hub was not resolved from result hub address: " + result.HubAddress);
        }
        /// <summary>
        /// Start downloading from specific queue entry source
        /// this will send a passive or active connection request
        /// to a specific user
        /// </summary>
        /// <param name="source">the source to connect to</param>
        public void StartDownload(Queue.QueueEntry.Source source)
        {
            if (source == null) return;

            if (source.HasHub && source.IsOnline && !CheckForUserInPeers(source.UserName))
            {
                source.Hub.SendConnectToMe(source.UserName); //signal download to hub to start it
            }
        }
        /// <summary>
        /// Start downloading a queue entry
        /// this will send connection requests to every source of
        /// the entry
        /// </summary>
        /// <param name="me">the queue entry to start downloading to</param>
        public void StartDownload(Queue.QueueEntry me)
        {

            //TODO change this back to thread safe enumerator in the queue class
            if (me == null) return;
            me.StartDownload();


            /*            lock (me.SourcesLock)
            {//TODO put this in queue class
                foreach (Queue.QueueEntry.Source source in me.Sources)
                {
                    if (source.HasHub && source.IsOnline && !CheckForUserInPeers(source.UserName))
                    {
                        source.Hub.SendConnectToMe(source.UserName); //signal download to hub to start it
                    }
                }
            }
            //sources strategy 
            //for selection and skipping if source offline
            //source offline detection

            //result.Hub.SendCommand("ConnectToMe", result.UserName); //signal download to hub to start it
            //Console.WriteLine("Hub was not resolved from result hub address: " + result.HubAddress);
        */
        }
        /// <summary>
        /// Update local port bindings
        /// needs to be called after the user changed the ports
        /// or his external ip
        /// </summary>
        public void UpdateConnectionSettings()
        {
            local_peer.UpdateConnectionSettings();
            lock (connected_hubs_lock)
            {
                foreach (Hub hub in connected_hubs)
                {//tell already connected hubs the new ports/ip/etc
                    if (!string.IsNullOrEmpty(local_peer.ExternalIP)) hub.MyIP = local_peer.ExternalIP;
                    else hub.MyIP = local_peer.IP;
                    hub.MyTcpPort = local_peer.TcpPort;
                    hub.MyUdpPort = local_peer.UdpPort;
                    hub.MyConnectionSpeed = connection_speed;
                    hub.MyConnectionMode = connection_mode;
                }
            }
        }
        //TODO add UpdateUserInfo() method 
        /// <summary>
        /// Locate a connected hub with help of his address
        /// </summary>
        /// <param name="hub_address">
        /// the hub address to look for.In the form of hub.location.com:4133, 
        /// if the hub port is the default port (411) it can be omitted
        /// </param>
        /// <returns>the found hub or NULL if none exists</returns>
        private Hub ResolveHub(string hub_address)
        {
            int port = 411;
            string address = "";
            try
            {
                int port_start = hub_address.IndexOf(":");
                if (port_start != -1)
                {
                    address = hub_address.Substring(0, port_start);
                    port = int.Parse(hub_address.Substring(port_start + 1));
                }
                else address = hub_address;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception occured during resolving of hub for address: " + hub_address + " - " + ex.Message);
                return (null);
            }

            //Console.WriteLine("Searching hubs for ip: "+address + " ,port: "+port);
            lock (connected_hubs_lock)
            {
                foreach (Hub hub in connected_hubs)
                {
                    if (hub.IP == address && hub.Port == port)
                    {
                        return (hub);
                    }
                }
            }
            return (null);
        }
        /// <summary>
        /// Find the hub to which a user is connected to
        /// TODO not implemented
        /// </summary>
        /// <param name="username">the user to look for</param>
        /// <returns>the hub the user is connected to or NULL if the user is offline</returns>
        public Hub FindUserHub(string username)
        {
            return (null);
        }
        //TODO if a user is connected to more than one hub we run into inconsistencies when the user disconnects the 
        //actually used hub.. we could still send him messages over another hub but for the client the source is
        //just offline
        //solution would be maybe a list of hubs instead of just one , one feature for later
        /// <summary>
        /// Update Source Online Status for a specific User on a certain Hub
        /// this will update the online status of the user 
        /// and try to find the find the affected queue entry source ,if there is any
        /// </summary>
        /// <param name="username">the user whos status changed</param>
        /// <param name="source_hub">the hub the user's status changed on</param>
        /// <param name="is_online">TRUE if the user went ONLINE</param>
        private void UpdateSourcesByUsername(string username, Hub source_hub, bool is_online)
        {
            download_queue.UpdateSourcesByUsername(username, source_hub, is_online);
        }
        /// <summary>
        /// Update Source Online Status for a specific Hub
        /// this will update the online status of the whole user list of the specified hub
        /// and try to find the find the affected queue entry sources ,if any
        /// </summary>
        /// <param name="me">the hub which status has changed</param>
        /// <param name="is_online">TRUE if you want to set the sources to ONLINE</param>
        private void UpdateSourcesByHub(Hub me, bool is_online)
        {
            foreach (string username in me.UserList)
                UpdateSourcesByUsername(username, me, is_online);
        }
        /// <summary>
        /// Check if user is already in connected to us
        /// </summary>
        /// <param name="username">the user to check</param>
        /// <returns>TRUE if the user is connected to us already</returns>
        private bool CheckForUserInPeers(string username)
        { //TODO save originating hub in peer and check for hub/username combination
            bool ret = false;
            lock (peers_lock)
            {
                foreach (Peer peer in peers)
                {
                    if (peer.PeerNick == username)
                    {
                        ret = true;
                        break;
                    }
                }
            }
            return (ret);
        }
        /// <summary>
        /// Remove all finished downloads from queue
        /// </summary>
        public void RemoveFinishedDownloadsFromQueue()
        {
            foreach (Queue.QueueEntry entry in download_queue)
            {
                if (entry.FilesizeOnDisk == entry.Filesize)
                    download_queue.Remove(entry);
            }
        }
        /// <summary>
        /// Start the next download in line for a specific user
        /// this will search for another queue entry for this user
        /// and if found start it 
        /// (will keep the connection open if we download a whole bunch of files from this guy)
        /// </summary>
        /// <param name="username">the user to search for in our download queue entries sources</param>
        private void ContinueWithQueueForUser(string username)
        {
            //check for existing connection in peers for this user

            if (string.IsNullOrEmpty(username)) return;
            Queue.QueueEntry entry = download_queue.FindFirstUnusedQueueEntryBySourceUser(username);
            if (entry != null)
            {
                StartDownload(entry.FindFirstSourceByUser(username));
            }
        }
        /// <summary>
        /// Get the directory the client was installed in
        /// </summary>
        /// <returns>the path of the client</returns>
        public string GetClientDirectory()
        {
            return (Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName));
        }
        /// <summary>
        /// Event handler that gets called
        /// when a peer connected to our client
        /// </summary>
        public event Peer.ConnectedEventHandler PeerConnected;
        /// <summary>
        /// Event handler that gets called
        /// when a peer disconnected from our client
        /// </summary>
        public event Peer.DisconnectedEventHandler PeerDisconnected;
        /// <summary>
        /// Event handler that gets called
        /// when a peer finished his initial handshake 
        /// (TODO this needs some rewriting)
        /// </summary>
        public event Peer.HandShakeCompletedEventHandler PeerHandShakeCompleted;
        /// <summary>
        /// Event handler that gets called
        /// when a file download from a peer was completed
        /// </summary>
        public event Peer.CompletedEventHandler PeerCompleted;
        /// <summary>
        /// Event handler that gets called
        /// when some data was received from a peer
        /// </summary>
        public event Peer.DataReceivedEventHandler PeerDataReceived;
        /// <summary>
        /// Event handler that gets called
        /// when a user disconnected from a hub
        /// </summary>
        public event Hub.UserQuitEventHandler HubUserQuit;
        /// <summary>
        /// Event handler that gets called
        /// when a user joined a hub
        /// </summary>
        public event Hub.UserJoinedEventHandler HubUserJoined;
        /// <summary>
        /// Event handler that gets called
        /// when our client was unable to connect to a hub
        /// (this includes errors till we logged in the hub,
        /// so unable to connect is not very precise here)
        /// </summary>
        public event Hub.UnableToConnectEventHandler HubUnableToConnect;
        /// <summary>
        /// Event handler that gets called
        /// when our client logged into a hub
        /// </summary>
        public event Hub.LoggedInEventHandler HubLoggedIn;
        /// <summary>
        /// Event handler that gets called
        /// when our client was disconnected from a hub
        /// </summary>
        public event Hub.DisconnectedEventHandler HubDisconnected;
        /// <summary>
        /// Event handler that gets called
        /// when our client connected to a hub 
        /// </summary>
        public event Hub.ConnectedEventHandler HubConnected;
        /// <summary>
        /// Event handler that gets called
        /// when our client received a wish of a hub to move us to another hub
        /// </summary>
        public event Hub.MoveForcedEventHandler HubMoveForced;
        /// <summary>
        /// Event handler that gets called
        /// when our client received some chat from a hub
        /// (main chat)
        /// </summary>
        public event Hub.MainChatLineReceivedEventHandler HubMainChatReceived;
        /// <summary>
        /// Event handler that gets called
        /// when our client received a private message
        /// from a user or bot
        /// </summary>
        public event Hub.PrivateChatLineReceivedEventHandler HubPrivateChatReceived;
        /// <summary>
        /// Event handler that gets called
        /// when a hub requested a password from our client
        /// </summary>
        public event Hub.PasswordRequestedEventHandler HubPasswordRequested;
        /// <summary>
        /// Setup the event handlers for a fresh connected peer
        /// </summary>
        /// <param name="client">an ungrabbed peer</param>
        private void SetupPeerEventHandler(Peer client)
        {
            client.Nick = nick;
            client.DataReceived += delegate(Peer data_received_client)
            {/*
                    Queue.QueueEntry entry = download_queue.FindFirstUnusedQueueEntryBySourceUser(data_received_client.PeerNick);
                    if (entry != null)
                    {
                        Queue.QueueEntry.Source source = entry.FindFirstSourceByUser(data_received_client.PeerNick);
                        if (source != null)
                        {
                            entry.IsInUse = true;
                        }
                        else
                        {
                            Console.WriteLine("no correct source found in queue entry for user: " + data_received_client.PeerNick);
                            data_received_client.Disconnect();
                        }
                    }
                    else
                    {
                        Console.WriteLine("nothing found in queue for user: " + data_received_client.PeerNick);
                        data_received_client.Disconnect();
                    }
                    */

                if (PeerDataReceived != null)
                    PeerDataReceived(data_received_client);
            };

            client.FileListRequestReceived += delegate(Peer file_list_request_client)
            {
                if (file_list_request_client.UploadRequestFilename == "files.xml.bz2")
                    file_list_request_client.UploadFilename = file_list_request_client.UploadRequestFilename;
                file_list_request_client.UploadFileListData = shares.GetFileListXmlBZ2();
                return (Peer.FileRequestAnswer.LetsGo);
            };

            client.FileRequestReceived += delegate(Peer file_request_client)
            {
                Sharing.SharingEntry entry = shares.GetShareByFileRequest(file_request_client.UploadRequestFilename);
                if(entry!=null) 
                {
                   file_request_client.UploadFilename = entry.Filename;
                   return (Peer.FileRequestAnswer.LetsGo);
                }
                else return (Peer.FileRequestAnswer.FileNotAvailable);
            };

            client.HandShakeCompleted += delegate(Peer handshake_client)
            {
                if (PeerHandShakeCompleted != null)
                    PeerHandShakeCompleted(handshake_client);
                Queue.QueueEntry entry = download_queue.FindFirstUnusedQueueEntryBySourceUser(handshake_client.PeerNick);
                if (entry != null)
                {
                    Queue.QueueEntry.Source source = entry.FindFirstSourceByUser(handshake_client.PeerNick);
                    if (source != null)
                    {
                        //handshake_client.StartDownload(source.Filename, entry.OutputFilename, entry.Filesize);
                        if (entry.Type == Queue.QueueEntry.EntryType.File && entry.WantTTHL)
                            handshake_client.GetTTHL(entry);
                        else if (entry.Type == Queue.QueueEntry.EntryType.File )
                            handshake_client.StartDownload(source, entry);
                        else if (entry.Type == Queue.QueueEntry.EntryType.FileList)
                            handshake_client.GetFileList(entry);
                    }
                    else
                    {
                        Console.WriteLine("no correct source found in queue entry for user: " + handshake_client.PeerNick);
                    }
                }
                else
                {
                    if (handshake_client.Direction == Peer.ConnectionDirection.Download)
                    {
                        Console.WriteLine("nothing found in queue for user: " + handshake_client.PeerNick);
                        handshake_client.Disconnect();
                    }
                }
            };


            client.Completed += delegate(Peer completed_client)
            {
                //download_queue.Remove(download_queue.FindQueueEntryByOutputFilename(completed_client.OutputFilename));
                if(auto_remove_downloads)
                    download_queue.Remove(completed_client.QueueEntry);
                ContinueWithQueueForUser(completed_client.PeerNick);
                if (PeerCompleted != null)
                    PeerCompleted(completed_client);
            };

            client.Disconnected += delegate(Peer disconnected_client)
            {
                lock (peers_lock)
                {
                    if(peers.Contains(disconnected_client))
                        peers.Remove(disconnected_client);
                }
                //Queue.QueueEntry entry = download_queue.FindQueueEntryByOutputFilename(disconnected_client.OutputFilename);
                //Queue.QueueEntry entry = disconnected_client.QueueEntry;
                //if (entry != null) //TODO this will cause trouble -> fix with disconnect cause change in callback
                //    entry.IsInUse = false;
                //ContinueWithQueueForUser(disconnected_client.PeerNick);//TODO prevent hammering on strange source with a seconds counter
                if (PeerDisconnected != null)
                    PeerDisconnected(disconnected_client);
            };
        }
        /// <summary>
        /// Client Constructor
        /// this will setup some event handlers
        /// and default options
        /// </summary>
        public Client()
        {

            search_results.DiscardOldResults = true;

            download_queue.EntrySourceStatusChanged += delegate(Queue.QueueEntry entry_changed,Queue.QueueEntry.Source source)
            {
                StartDownload(source);
            };
            
            local_peer.SearchResultReceived += delegate(SearchResults.SearchResult result)
            {
                InterpretReceivedSearchResult(result);
            };

            local_peer.PeerConnected += delegate(Peer client)
            {
                SetupPeerEventHandler(client);
                if (PeerConnected != null)
                    PeerConnected(client);
                client.StartHandShake();
                lock (peers_lock)
                {
                    peers.Add(client);
                }
            };

            download_queue.FileListsDirectory = GetClientDirectory() + "\\filelists";
            download_queue.DownloadDirectory = GetClientDirectory() + "\\downloads";
            share_size = 901 * 1024 * 1024;
            share_size = share_size * 1024+523; // until we support sharing .this is just fake to get in to the nicer hubs
        }
        /// <summary>
        /// Client Deconstructor
        /// TODO check if deconstructors are really not supported by c#
        /// </summary>
        ~Client()
        {
            //local_peer.Close();

        }
        /// <summary>
        /// Connect to a hub
        /// this will initialize a hub with 
        /// some client values like our nickname etc
        /// add some event handlers
        /// and start connecting to it
        /// </summary>
        /// <param name="me">the hub you want to connect to</param>
        public void ConnectHub(Hub me)
        {
            me.Nick = nick;
            if (!string.IsNullOrEmpty(local_peer.ExternalIP)) me.MyIP = local_peer.ExternalIP;
            else me.MyIP = local_peer.IP;
            me.MyTcpPort = local_peer.TcpPort;
            me.MyUdpPort = local_peer.UdpPort;
            me.MyEmail = email;
            me.MyDescription = description;
            me.MyVersion = version;
            me.MyTagVersion = tag_version;
            me.MyShareSize = share_size;
            me.MyConnectionMode = connection_mode;
            me.MyConnectionSpeed = connection_speed;
            me.MyName = name;

            if (!me.IsGrabbedByClient)
            {
                me.SearchReceived += delegate(Hub search_hub, Hub.SearchParameters search)
                {
                    if (search.HasTTH)
                    {
                        Sharing.SharingEntry entry = shares.GetShareByTTH(search.tth);
                        if (entry != null)
                        {
                            if (search.mode == Hub.ConnectionMode.Passive)
                                search_hub.SearchReply(entry.Filename,entry.Filesize, search);
                            else local_peer.SearchReply(entry.Filename,entry.Filesize,search_hub, search);
                        }
                    }
                    else
                    {
                        //TODO add old fashioned search here
                    }

                };
                me.SearchResultReceived += delegate(Hub search_result_hub, SearchResults.SearchResult result)
                    {
                        InterpretReceivedSearchResult(result);
                    };
                me.PasswordRequested += delegate(Hub password_requested)
                {
                    //TODO add a password for hubs db
                    // and first check that db before and send a found password
                    //automagically and silent
                    if (HubPasswordRequested != null)
                        return(HubPasswordRequested(password_requested));
                    return (null);
                };
                me.MainChatLineReceived += delegate(Hub main_chat_hub, Hub.ChatLine main_chat_line)
                {
                    if (HubMainChatReceived != null)
                        HubMainChatReceived(main_chat_hub, main_chat_line);
                };
                me.PrivateChatLineReceived += delegate(Hub private_chat_hub, Hub.ChatLine private_chat_line)
                {
                    if (HubPrivateChatReceived != null)
                        HubPrivateChatReceived(private_chat_hub, private_chat_line);
                };
                me.MoveForced += delegate(Hub src_hub, Hub dst_hub)
                {
                    if (HubMoveForced != null)
                        HubMoveForced(src_hub, dst_hub);
                };
                me.ConnectToMeReceived += delegate(Hub hub, Peer connect_to_me_client)
                {
                    //free slots check maybe needed
                    SetupPeerEventHandler(connect_to_me_client);
                    connect_to_me_client.Connected += delegate(Peer connect_to_me_connected_client)
                    {
                        if (PeerConnected != null)
                            PeerConnected(connect_to_me_connected_client);
                        connect_to_me_connected_client.StartHandShake();
                        lock (peers_lock)
                        {
                            peers.Add(connect_to_me_connected_client);
                        }
                    };
                    connect_to_me_client.Connect();

                };
                me.Disconnected += delegate(Hub hub)
                {
                    UpdateSourcesByHub(hub, false);
                    lock (connected_hubs_lock)
                    {
                        if (connected_hubs.Contains(hub))
                            connected_hubs.Remove(hub);
                    }
                    if (HubDisconnected != null)
                        HubDisconnected(hub);
                };
                me.Connected += delegate(Hub hub)
                {
                    lock (connected_hubs_lock)
                    {
                        if (!connected_hubs.Contains(hub))
                            connected_hubs.Add(hub);
                    }
                    if (HubConnected != null)
                        HubConnected(hub);
                };
                me.UnableToConnect += delegate(Hub hub)
                {
                    UpdateSourcesByHub(hub, false);
                    /*lock (connected_hubs_lock) TODO check if commenting this out hurts our code---> :-)
                    {
                        if (connected_hubs.Contains(hub))
                            connected_hubs.Remove(hub);
                    }*/
                    if (HubUnableToConnect != null)
                        HubUnableToConnect(hub);
                };
                me.LoggedIn += delegate(Hub hub)
                {
                    if (HubLoggedIn != null)
                        HubLoggedIn(hub);
                };
                me.UserJoined += delegate(Hub hub, string username)
                {
                    UpdateSourcesByUsername(username, hub, true);
                    if (HubUserJoined != null)
                        HubUserJoined(hub, username);
                };
                me.UserQuit += delegate(Hub hub, string username)
                {
                    UpdateSourcesByUsername(username, hub, false);
                    if (HubUserQuit != null)
                        HubUserQuit(hub, username);
                };

                me.IsGrabbedByClient = true;
            }
            me.Connect();
        }
        /// <summary>
        /// Disconnect a hub
        /// this will also remove all event handlers from that hub
        /// (TODO this behaviour will cause problems in multi client scenarios,find a workaround)
        /// </summary>
        /// <param name="me">the hub you want to disconnect from</param>
        public void DisconnectHub(Hub me)
        {
            lock (connected_hubs_lock)
            {
                connected_hubs.Remove(me);
            }
            me.Disconnect();
            me.Ungrab(); //hub event handlers should be ungrabbed 
        }
    }
}
