using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

namespace DCPlusPlus
{
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
        public List<Peer> Peers
        {
            get
            {
                return (peers);
            }
        }
        //private Object download_queue_lock = "";
        protected Queue download_queue = new Queue();
        public Queue DownloadQueue
        {
            get
            {
                return (download_queue);
            }
        }
        protected SearchResults search_results = new SearchResults();
        public SearchResults SearchResults
        {
            get
            {
                return (search_results);
            }
        }
        protected ListeningSockets local_peer = new ListeningSockets();
        public ListeningSockets LocalPeer
        {
            get
            {
                return (local_peer);
            }
        }
        protected Sharing shares = new Sharing();
        public Sharing Shares
        {
            get
            {
                return (shares);
            }
        }
        protected Object connected_hubs_lock = new Object(); //TODO rename connected hubs
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
        public List<Hub> ConnectedHubs
        {
            get
            {
                return (connected_hubs);
            }
        }
        protected string nick = "unknown";
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
        protected long share_size = 0;
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
        public void FindAlternateSources(Queue.QueueEntry me)
        {
            //search all hubs for tth string
            if (me != null && me.HasTTH)
                Search(me.TTH, true);
        }
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
        public void GetFileList(Hub hub, string username)
        {
            download_queue.AddFileList(hub, username);
            hub.SendConnectToMe(username); //signal download to hub to start it
        }

        public void GetTTHL(Queue.QueueEntry me)
        {
            //if (me == null) return;
            me.WantTTHL = true;
            me.StartDownload();
        }
        public void StopDownload(Queue.QueueEntry me)
        {
            lock (peers_lock)
            {
                foreach (Peer peer in peers)
                {
                    if (peer.QueueEntry == me)
                    {
                        peer.Disconnect();
                        //break;
                    }
                }
            }
        }
        public void StartDownload(SearchResults.SearchResult result)
        {
            if (result.IsHubResolved)
            {//TODO put this into queue class
                download_queue.AddSearchResult(result);
                result.Hub.SendConnectToMe(result.UserName); //signal download to hub to start it
            }
            else Console.WriteLine("Hub was not resolved from result hub address: " + result.HubAddress);
        }
        public void StartDownload(Queue.QueueEntry.Source source)
        {
            if (source == null) return;

            if (source.HasHub && source.IsOnline && !CheckForUserInPeers(source.UserName))
            {
                source.Hub.SendConnectToMe(source.UserName); //signal download to hub to start it
            }
        }
        public void StartDownload(Queue.QueueEntry me)
        {
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
        public Hub FindUserHub(string username)
        {
            return (null);
        }
        private void UpdateSourcesByUsername(string username, Hub source_hub, bool is_online)
        {
            download_queue.UpdateSourcesByUsername(username, source_hub, is_online);
        }
        private void UpdateSourcesByHub(Hub me, bool is_online)
        {
            foreach (string username in me.UserList)
                UpdateSourcesByUsername(username, me, is_online);
        }
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
        public string GetClientDirectory()
        {
            return (Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetModules()[0].FullyQualifiedName));
        }
        public event Peer.ConnectedEventHandler PeerConnected;
        public event Peer.DisconnectedEventHandler PeerDisconnected;
        public event Peer.HandShakeCompletedEventHandler PeerHandShakeCompleted;
        public event Peer.CompletedEventHandler PeerCompleted;
        public event Peer.DataReceivedEventHandler PeerDataReceived;
        public event Hub.UserQuitEventHandler HubUserQuit;
        public event Hub.UserJoinedEventHandler HubUserJoined;
        public event Hub.UnableToConnectEventHandler HubUnableToConnect;
        public event Hub.LoggedInEventHandler HubLoggedIn;
        public event Hub.DisconnectedEventHandler HubDisconnected;
        public event Hub.ConnectedEventHandler HubConnected;
        public event Hub.MoveForcedEventHandler HubMoveForced;
        public event Hub.MainChatLineReceivedEventHandler HubMainChatReceived;
        public event Hub.PrivateChatLineReceivedEventHandler HubPrivateChatReceived;


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
                download_queue.Remove(completed_client.QueueEntry);
                ContinueWithQueueForUser(completed_client.PeerNick);
                if (PeerCompleted != null)
                    PeerCompleted(completed_client);
            };

            client.Disconnected += delegate(Peer disconnected_client)
            {
                lock (peers_lock)
                {
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
        ~Client()
        {
            //local_peer.Close();

        }
        public void ConnectHub(Hub me)
        {
            me.Nick = nick;
            if (!string.IsNullOrEmpty(local_peer.ExternalIP)) me.MyIP = local_peer.ExternalIP;
            else me.MyIP = local_peer.IP;
            me.MyTcpPort = local_peer.TcpPort;
            me.MyUdpPort = local_peer.UdpPort;
            me.MyEmail = email;
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

                    }

                };
                me.SearchResultReceived += delegate(Hub search_result_hub, SearchResults.SearchResult result)
                    {
                        InterpretReceivedSearchResult(result);
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
        public void DisconnectHub(Hub me)
        {
            lock (connected_hubs_lock)
            {
                connected_hubs.Remove(me);
            }
            me.Disconnect();

        }

    }
}
