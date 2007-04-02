using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;

namespace DCPlusPlus
{
    [TestFixture]
    public class Peer : Connection
    {
        //TODO 
        //rewrite notes : download,upload,reuse connection,check of data during download
        //                resume,download block in middle of the file (also after file_end and add zeros before)
        //                simplify byte counters , tthl download , download to file or queue entry field
        //                filelists
        // DataReceivedEventResolution in bytes after which a data received event is fired to reduce load on the gui
        //
        // some connections won't disconnect and stack up in our listview after a while

        public delegate void ConnectedEventHandler(Peer peer);
        public event ConnectedEventHandler Connected;
        public delegate void DisconnectedEventHandler(Peer peer);
        public event DisconnectedEventHandler Disconnected;
        public delegate void UnableToConnectEventHandler(Peer peer);
        public event UnableToConnectEventHandler UnableToConnect;
        public delegate void HandShakeCompletedEventHandler(Peer peer);
        public event HandShakeCompletedEventHandler HandShakeCompleted;
        public delegate void CompletedEventHandler(Peer peer);
        public event CompletedEventHandler Completed;
        public delegate void DataReceivedEventHandler(Peer peer);
        public event DataReceivedEventHandler DataReceived;
        public enum FileRequestAnswer
        {
            FileNotAvailable, NoFreeSlots, LetsGo
        }
        public delegate FileRequestAnswer FileRequestReceivedEventHandler(Peer peer);
        public event FileRequestReceivedEventHandler FileRequestReceived;
        public delegate FileRequestAnswer FileListRequestReceivedEventHandler(Peer peer);
        public event FileListRequestReceivedEventHandler FileListRequestReceived;
        protected string peer_nick = "unknown";
        public string PeerNick
        {
            get
            {
                return (peer_nick);
            }
            set
            {
                peer_nick = value;
            }
        }
        private int start_tick = 0;
        protected long tthl_size = 0;
        public long TTHLSize
        {
            get
            {
                return (tthl_size);
            }
        }
        private Stream stream = null;
        protected long bytes_already_downloaded = 0;
        public long BytesAlreadyDownloaded
        {
            get
            {
                return (bytes_already_downloaded);
            }
        }
        protected long bytes_downloaded = 0;
        public long BytesDownloaded
        {
            get
            {
                return (bytes_downloaded);
            }
        }
        protected long bytes_already_uploaded = 0;
        public long BytesAlreadyUploaded
        {
            get
            {
                return (bytes_already_uploaded);
            }
        }
        protected long bytes_uploaded = 0;
        public long BytesUploaded
        {
            get
            {
                return (bytes_uploaded);
            }
        }
        protected float speed = 0;
        public float Speed
        {
            get
            {
                return (speed);
            }
        }
        protected ConnectionDirection direction = ConnectionDirection.Unknown;
        public ConnectionDirection Direction
        {
            get
            {
                return (direction);
            }
        }
        protected bool is_downloading = false;
        public bool IsDownloading
        {
            get
            {
                return (is_downloading);
            }
        }
        protected bool is_uploading = false;
        public bool IsUploading
        {
            get
            {
                return (is_uploading);
            }
        }
        protected Queue.QueueEntry queue_entry = new Queue.QueueEntry();
        public Queue.QueueEntry QueueEntry
        {
            get
            {
                return (queue_entry);
            }
        }
        protected Queue.QueueEntry.Source source = new Queue.QueueEntry.Source();
        public Queue.QueueEntry.Source Source
        {
            get
            {
                return (source);
            }
        }
        protected long upload_offset = 0;
        public long UploadOffset
        {
            get
            {
                return (upload_offset);
            }
        }
        protected long upload_length = 0;
        public long UploadLength
        {
            get
            {
                return (upload_length);
            }
        }
        protected string upload_request_filename = "";
        public string UploadRequestFilename
        {
            get
            {
                return (upload_request_filename);
            }
        }
        protected string upload_filename = "";
        public string UploadFilename
        {
            get
            {
                return (upload_filename);
            }
            set
            {
                upload_filename = value;
            }
        }
        protected byte[] upload_file_list_data = null;
        public byte[] UploadFileListData
        {
            get
            {
                return (upload_file_list_data);
            }
            set
            {
                upload_file_list_data = value;
            }
        }
        private bool first_bytes_received = false;
        //handshake randoms for priority selection
        private int handshake_my_value = 0;
        private int handshake_his_value = 0;
        private ConnectionDirection his_direction_wish = ConnectionDirection.Unknown;
        //private ConnectionDirection my_direction_wish = ConnectionDirection.Unknown;


        /*
        
        protected long filelength = 0;
        public long FileLength
        {
            get
            {
                return (filelength);
            }
        }

        protected string output_filename="";
        public string OutputFilename
        {
            get
            {
                return (output_filename);
            }

        }

        protected string filename = "";
        public string Filename
        {
            get
            {
                return (filename);
            }
        }
        */
        //protected string input_filename="";
        private void StartReceiving()
        {
            try
            {
                if (socket.Connected)
                {
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_buffer = new byte[32768];
                    socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, socket);
                }
                else
                {
                    Console.WriteLine("Connection to peer aborted.");
                    error_code = ErrorCodes.Disconnected;
                    Disconnect();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during starting of receiving data from peer: " + ex.Message);
                error_code = ErrorCodes.Disconnected;
                Disconnect();
            }

        }
        private void WriteDataToFile(int bytes_start, int bytes_end)
        {
            if (!first_bytes_received)
            {
                if (queue_entry.TryToClaimEntry(this)) //TODO -> this can be moved to client with an event handler (FirstBytesReceived(this);)
                {
                    first_bytes_received = true;
                }
                else
                {
                    Console.WriteLine("Queue Entry already in use , disconnecting.");
                    error_code = ErrorCodes.QueueEntryInUse;
                    Disconnect();
                }
            }
            bytes_downloaded += bytes_end - bytes_start;
            //TODO quite buggy divide by zero prob
            speed = (float)(((float)bytes_downloaded / 1024.0f) / ((float)(System.Environment.TickCount - start_tick) / 1000.0f));
            //Console.WriteLine("Received " + received_bytes + " bytes of data. with a speed of: " + (((System.Environment.TickCount - start_tick) / 1000) / (bytes_downloaded / 1024)) + " KB/s");
            if (stream == null)
            {//if we do not append we still have to create the output file
                if(queue_entry.WantTTHL)
                    stream = new FileStream(queue_entry.OutputFilename+".tthl", FileMode.Create, FileAccess.Write, System.IO.FileShare.ReadWrite);
                else
                    stream = new FileStream(queue_entry.OutputFilename, FileMode.Create, FileAccess.Write, System.IO.FileShare.ReadWrite);
            }
            stream.Write(receive_buffer, bytes_start, bytes_end);
            stream.Flush();
            if (DataReceived != null)
                DataReceived(this);
            if (queue_entry.WantTTHL)
            {
                if (tthl_size > 0 && bytes_downloaded + bytes_already_downloaded == tthl_size)
                {
                    queue_entry.WantTTHL = false;
                    queue_entry.UnclaimEntry();
                    /*if (Completed != null)
                        Completed(this);*/
                    if (!CheckForExtension("ADCGet"))
                        SendCommand("Send");
                    error_code = ErrorCodes.NoErrorYet;
                    Disconnect();
                }
            }
            else
            {
                if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                {
                    if (Completed != null)
                        Completed(this);
                    if (!CheckForExtension("ADCGet"))
                        SendCommand("Send");
                    error_code = ErrorCodes.NoErrorYet;
                    Disconnect();
                }
            }

        }
        private void OnReceive(IAsyncResult result)
        {
            try
            {
                Socket receive_socket = (Socket)result.AsyncState;
                //Console.WriteLine("Connection socket.");
                if (!receive_socket.Connected)
                {
                    //Console.WriteLine("Connection connected.");
                    if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                    {
                        if (Completed != null)
                            Completed(this);
                        error_code = ErrorCodes.Disconnected;
                        Disconnect();
                        return;
                    }
                    if (first_bytes_received)//we need to unclaim our used entry too
                        queue_entry.UnclaimEntry();
                    Console.WriteLine("Connection to peer dropped.");
                    /*if (Disconnected != null)
                        Disconnected(this);*/
                    error_code = ErrorCodes.Disconnected;
                    Disconnect();
                    return;
                }
                int received_bytes = receive_socket.EndReceive(result);
                if (received_bytes > 0)
                {
                    if (is_downloading)
                    {
                        WriteDataToFile(0, received_bytes);
                    }
                    else
                    {
                        string received_string = System.Text.Encoding.Default.GetString(receive_buffer, 0, received_bytes);
                        //Console.WriteLine("Received a string: "+received_string);
                        //interpret string and act accordingly
                        int bytes_used = InterpretReceivedString(received_string);
                        if (bytes_used != -1 && bytes_used < received_bytes)
                        {//we already received some data from the file ... 
                            WriteDataToFile(bytes_used, received_bytes);
                        }
                    }
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                }
                else
                {
                    if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                    {
                        if (Completed != null)
                            Completed(this);
                        error_code = ErrorCodes.Disconnected;
                        Disconnect();
                        return;
                    }
                    if (first_bytes_received) //we need to unclaim our used entry too
                        queue_entry.UnclaimEntry();
                    Console.WriteLine("Connection to peer dropped.");
                    error_code = ErrorCodes.Disconnected;
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine("Exception during receive of data: " + ex.Message);
                if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                {
                    if (Completed != null)
                        Completed(this);
                    if (stream == null)
                        stream.Close();
                    return;
                }
                if (first_bytes_received)
                    queue_entry.UnclaimEntry();
                Console.WriteLine("Error receiving data from Peer: " + ex.Message);
                error_code = ErrorCodes.Exception;
                Disconnect();
            }

        }
        /*
        private void OnReceive(IAsyncResult result)
        {
            Socket receive_socket = (Socket)result.AsyncState;
            try
            {
                if (!receive_socket.Connected)
                {
                    try
                    {
                        if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                        {
                            try
                            {
                                if (Completed != null)
                                    Completed(this);
                                return;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in Completed Event: " + ex.Message);
                            }
                        }
                        if (first_bytes_received)
                            queue_entry.IsInUse = false;
                        Console.WriteLine("Connection to peer dropped.");

                        if (Disconnected != null)
                            Disconnected(this);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception in Disconnected Event: " + ex.Message);
                    }


                }
                int received_bytes = receive_socket.EndReceive(result);
                if (received_bytes > 0)
                {
                    if (is_downloading)
                    {
                        if (!first_bytes_received)
                        {
                            if (queue_entry.TryToClaimEntry())
                            {
                                first_bytes_received = true;
                            }
                            else Disconnect();

                        }

                        bytes_downloaded += received_bytes;
                        //TODO quite buggy divide by zero prob
                        speed = (float)(((float)bytes_downloaded / 1024.0f) / ((float)(System.Environment.TickCount - start_tick) / 1000.0f));
                        //Console.WriteLine("Received " + received_bytes + " bytes of data. with a speed of: " + (((System.Environment.TickCount - start_tick) / 1000) / (bytes_downloaded / 1024)) + " KB/s");

                        if (stream == null) //if we do not append we still have to create the output file
                            stream = new FileStream(queue_entry.OutputFilename, FileMode.Create, FileAccess.Write, System.IO.FileShare.ReadWrite);

                        stream.Write(receive_buffer, 0, received_bytes);
                        stream.Flush();

                        try
                        {
                            if (DataReceived != null)
                                DataReceived(this);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception in DataReceived Event: " + ex.Message);
                        }

                        if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                        {
                            try
                            {
                                if (Completed != null)
                                    Completed(this);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in Completed Event: " + ex.Message);
                            }
                            if (!CheckForExtension("ADCGet"))
                                SendCommand("Send");
                            Disconnect();
                        }
                    }
                    else
                    {
                        //string received_string = Encoding.ASCII.GetString(receive_buffer, 0, received_bytes);
                        string received_string = System.Text.Encoding.Default.GetString(receive_buffer, 0, received_bytes);
                        //Console.WriteLine("Received a string: "+received_string);
                        //interpret string and act accordingly
                        InterpretReceivedString(received_string);

                    }
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, receive_socket);
                }
                else
                {
                    try
                    {
                        if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                        {
                            try
                            {
                                if (Completed != null)
                                    Completed(this);
                                return;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Exception in Completed Event: " + ex.Message);
                            }
                        }
                        if (first_bytes_received)
                            queue_entry.IsInUse = false;
                        Console.WriteLine("Connection to peer dropped.");

                        if (Disconnected != null)
                            Disconnected(this);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Exception in Disconnected Event: " + ex.Message);
                    }

                }

            }
            catch (Exception ex)
            {
                try
                {
                    if (queue_entry.Filesize > 0 && bytes_downloaded + bytes_already_downloaded == queue_entry.Filesize)
                    {
                        try
                        {
                            if (Completed != null)
                                Completed(this);
                            return;
                        }
                        catch (Exception ex2)
                        {
                            Console.WriteLine("Exception in Completed Event: " + ex2.Message);
                        }
                    }
                    if (first_bytes_received)
                        queue_entry.IsInUse = false;
                    Console.WriteLine("Error receiving data from Peer: " + ex.Message);
                    if (Disconnected != null)
                        Disconnected(this);

                }
                catch (Exception ex2)
                {
                    Console.WriteLine("Exception in Disconnected Event: " + ex2.Message);
                }

            }

        }
*/
        public enum ConnectionDirection
        {
            //Incoming,Outgoing
            Upload, Download, Unknown
        }
        public void GetFileList(Queue.QueueEntry entry)
        {//try to download a filelist that the peer offers by his supports
            //it would be fine to have these converted on the spot to our standard filelist format (files.xml.bz2)
            this.queue_entry = entry;
            this.source = entry.Sources[0];//filelist queue entry have exactly one source , no more no less
            direction = ConnectionDirection.Download;
            long start_pos = 1;
            if (File.Exists(queue_entry.OutputFilename))
            {
                File.Delete(queue_entry.OutputFilename);//delete old filelist if happen to be there
            }

            string filename = "MyList.DcLst";
            if (CheckForExtension("XmlBZList"))
                filename = "files.xml.bz2";
            else if (CheckForExtension("BZList"))
                filename = "MyList.bz2";

            source.Filename = filename;

            if (CheckForExtension("ADCGet"))
            {
                start_pos = start_pos - 1;
                SendCommand("ADCGET", "file " + source.Filename + " " + start_pos + " -1");
                Console.WriteLine("Trying to adc-fetch filelist(" + filename + ") from: '" + entry.Sources[0].UserName + "'");
            }
            else
            {
                SendCommand("Get", filename + "$" + start_pos);
                Console.WriteLine("Trying to fetch filelist(" + filename + ") from: '" + entry.Sources[0].UserName + "'");
            }

        }
        public void GetTTHL(Queue.QueueEntry entry)
        {//try to download a tthl block if peer offers this via supports
            this.queue_entry = entry;
            //this.source = entry.Sources[0];
            //this.source = source;
            direction = ConnectionDirection.Download;
            long start_pos = 0;
            if (File.Exists(queue_entry.OutputFilename + ".tthl"))
            {
                File.Delete(queue_entry.OutputFilename + ".tthl");//delete old tthl file if happen to be there
            }

            bytes_already_downloaded = 0;
            /*if (File.Exists(queue_entry.OutputFilename))
            {
                FileInfo fi = new FileInfo(queue_entry.OutputFilename);
                if (fi.Length >= queue_entry.Filesize)
                {//abort , file is complete or something else may happened here
                    Disconnect();
                    return;
                }
                start_pos = fi.Length + 1;
                bytes_already_downloaded = fi.Length;
                stream = new FileStream(queue_entry.OutputFilename, FileMode.Append, FileAccess.Write, System.IO.FileShare.ReadWrite);
            }
            else start_pos = 1;
            */

            if (CheckForExtension("ADCGet") && CheckForExtension("TTHL") && CheckForExtension("TTHF") && queue_entry.HasTTH)
            {
                SendCommand("ADCGET", "tthl TTH/" + queue_entry.TTH + " " + start_pos + " -1");
                //SendCommand("ADCGET", "tthl " + source.Filename + " " + start_pos + " -1");
                //Console.WriteLine("Trying to adc-fetch tthl file(" + entry.OutputFilename + ".tthl) from: '" + source.UserName + "'");
                Console.WriteLine("Trying to adc-fetch tthl for file(" + entry.OutputFilename + ")");
            }
            else
            {
                Console.WriteLine("Trying to fetch tthl for file(" + entry.OutputFilename+") from: '" + source.UserName + "' that doesn't support it");
            }

        }
        public void SendMaxedOut()
        {
            SendCommand("MaxedOut");
            error_code = ErrorCodes.NoErrorYet;
            Disconnect();
        }
        public void SendFileNotAvailableError()
        {
            SendError("File Not Available");
            error_code = ErrorCodes.NoErrorYet;
            Disconnect();
        }
        public void SendError(string message)
        {
            SendCommand("Error", message);
        }
        public void SendFailed(string message)
        {
            SendCommand("Failed", message);
        }
        public bool StartUpload()
        {
            direction = ConnectionDirection.Upload; //TODO enhance Direction handling -> esp uploading if we have nothing to to download from the user
            if (!string.IsNullOrEmpty(upload_filename))
            {
                if (upload_file_list_data != null)
                {
                    upload_length = upload_file_list_data.Length;
                    stream = new MemoryStream(upload_file_list_data);
                    stream.Seek(upload_offset, SeekOrigin.Begin);
                }
                else
                {
                    if (File.Exists(upload_filename))
                    {
                        FileInfo fi = new FileInfo(upload_filename);
                        if (fi.Length < upload_offset)
                        {//abort , file is complete or something else may happened here
                            Console.WriteLine("error requesting data at offset: " + fi.Length + " after file end: " + upload_offset);
                            Disconnect();
                            return (false);
                        }
                        if (fi.Length < upload_offset + upload_length || upload_length == -1)
                        {//abort , file is complete or something else may happened here
                            upload_length = fi.Length - upload_offset;
                        }
                        //Console.WriteLine("Trying to open file: "+f);
                        try
                        {
                            stream = new FileStream(upload_filename, FileMode.Open, FileAccess.Read, System.IO.FileShare.ReadWrite);
                            stream.Seek(upload_offset, SeekOrigin.Begin);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("exception opening file: " + ex.Message);
                            SendFileNotAvailableError();
                            return (false);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Requested file not found: " + upload_filename);
                        SendFileNotAvailableError();
                        return (false);
                    }
                }
                if (CheckForExtension("ADCGet"))
                {

                    string send_parameters = "file " + upload_request_filename + " " + upload_offset + " " + upload_length;
                    Console.WriteLine("adc send parameters : " + send_parameters);
                    SendCommand("ADCSND", send_parameters);
                    Console.WriteLine("Trying to adc-upload file: '" + upload_filename + "' starting from pos:" + upload_offset + " length: " + upload_length);
                    StartUploadTransfer();
                }
                else
                {
                    SendCommand("FileLength", upload_length.ToString());
                    Console.WriteLine("Trying to upload file: '" + upload_filename + "' starting from pos:" + upload_offset + " length: " + upload_length);
                }
                return (true);
            }
            return (false);
        }
        private long upload_block_size = 1024;//TODO make this static and public
        public void StartUploadTransfer()
        {
            is_uploading = true;
            start_tick = System.Environment.TickCount;
            bytes_uploaded = 0;
            if (socket != null)
            {
                if (!socket.Connected) return;
                try
                {
                    if (upload_length < upload_block_size) upload_block_size = upload_length;//TODO maybe better to change back after upload finished .. if we reuse peers
                    byte[] send_bytes = new byte[upload_block_size];
                    int bytes_read = stream.Read(send_bytes, (int)0, (int)upload_block_size);
                    Console.WriteLine("Sending the first " + bytes_read + " byte(s) of file: " + upload_request_filename);
                    socket.BeginSend(send_bytes, 0, bytes_read, SocketFlags.None, new AsyncCallback(UploadTransferCallback), socket);
                    //socket.BeginSend(upload_file_list_data, 0, upload_file_list_data.Length, SocketFlags.None, new AsyncCallback(UploadTransferCallback), socket);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error sending command to peer: " + e.Message);
                    error_code = ErrorCodes.Exception;
                    Disconnect();
                }
            }

        }
        protected void UploadTransferCallback(IAsyncResult ar)
        {
            Socket upload_data_socket = (Socket)ar.AsyncState;
            try
            {
                int bytes_sent = upload_data_socket.EndSend(ar);
                bytes_uploaded += bytes_sent;
                if (socket != null)
                {
                    if (bytes_uploaded == upload_length)
                    {
                        error_code = ErrorCodes.NoErrorYet; //TODO add some download error_code ..and rename the whole thing to something more intuitive
                        Disconnect();
                        return;
                    }
                    if (!socket.Connected) return;
                    if (upload_length - bytes_uploaded < upload_block_size) upload_block_size = upload_length - bytes_uploaded;//TODO maybe better to change back after upload finished .. if we reuse peers
                    byte[] send_bytes = new byte[upload_block_size];
                    //int bytes_read = stream.Read(send_bytes, (int)upload_offset + (int)bytes_uploaded, (int)upload_block_size);
                    int bytes_read = stream.Read(send_bytes, 0, (int)upload_block_size);
                    //Console.WriteLine("Already sent " + bytes_uploaded + " byte(s) of file: " + upload_request_filename);
                    //bytes_uploaded += bytes_read;
                    socket.BeginSend(send_bytes, 0, bytes_read, SocketFlags.None, new AsyncCallback(UploadTransferCallback), socket);
                    //socket.BeginSend(upload_file_list_data, 0, upload_file_list_data.Length, SocketFlags.None, new AsyncCallback(UploadTransferCallback), socket);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during sending of data: " + ex.Message);
                error_code = ErrorCodes.Exception;
                Disconnect();
            }
        }
        public void StartDownloadTransfer()
        {
            is_downloading = true;
            start_tick = System.Environment.TickCount;
            bytes_downloaded = 0;
            if (!CheckForExtension("ADCGet"))
            {
                SendCommand("Send");
            }
        }
        public void StartDownload()
        {
            direction = ConnectionDirection.Download;
            long start_pos = 1;
            if (File.Exists(queue_entry.OutputFilename))
            {
                FileInfo fi = new FileInfo(queue_entry.OutputFilename);
                if (fi.Length >= queue_entry.Filesize)
                {//abort , file is complete or something else may happened here
                    Disconnect();
                    return;
                }
                start_pos = fi.Length + 1;
                bytes_already_downloaded = fi.Length;
                stream = new FileStream(queue_entry.OutputFilename, FileMode.Append, FileAccess.Write, System.IO.FileShare.ReadWrite);
            }
            else start_pos = 1;

            if (CheckForExtension("ADCGet"))
            {
                start_pos = start_pos - 1;
                if (CheckForExtension("TTHF") && queue_entry.HasTTH)
                    SendCommand("ADCGET", "file TTH/" + queue_entry.TTH + " " + start_pos + " " + (queue_entry.Filesize - start_pos));
                else SendCommand("ADCGET", "file " + source.Filename + " " + start_pos + " " + (queue_entry.Filesize - start_pos));
                Console.WriteLine("Trying to adc-fetch file: '" + source.Filename + "' starting from pos:" + start_pos);
            }
            else
            {
                SendCommand("Get", source.Filename + "$" + start_pos);
                Console.WriteLine("Trying to fetch file: '" + source.Filename + "' starting from pos:" + start_pos);
            }
        }
        public void StartDownload(Queue.QueueEntry.Source source, Queue.QueueEntry entry)
        {
            this.queue_entry = entry;
            this.source = source;
            StartDownload();
        }
        public void StartDownload(string filename, string output_filename, long output_file_length)
        {
            //this.output_filename = output_filename;
            //this.filename = filename;
            queue_entry = new Queue.QueueEntry();
            queue_entry.OutputFilename = output_filename;
            queue_entry.Filesize = output_file_length;
            source = new Queue.QueueEntry.Source();
            source.Filename = filename;
            StartDownload();
        }
        /// <summary>
        /// internal function to interpret the received bytes of a socket
        /// </summary>
        /// <param name="received_string">data read from socket</param>
        /// <returns>
        /// returns the number of chars interpreted ,
        /// in case of a transfer the rest needs to be written
        /// into the output file
        /// </returns>
        private int InterpretReceivedString(string received_string)
        {
            //TODO counter possible packet splits in messages
            string[] received_strings = received_string.Split("|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            int interpreter_pos = 0;
            for (int i = 0; i < received_strings.Length; i++)
            {
                interpreter_pos += received_strings[i].Length + 1;
                if (received_strings[i].StartsWith("$"))
                {
                    if (InterpretCommand(received_strings[i]))
                        return (interpreter_pos);
                }
                else Console.WriteLine("Received a non command line: " + received_strings[i]);
            }
            return (-1);//tell the receive handler that we dont want to use any rest bytes from the bytes read in the actual call

        }
        /// <summary>
        /// internal function to interpret a command received from the remote peer
        /// </summary>
        /// <param name="received_command"></param>
        /// <returns>
        /// only returns true if a download transfer was started
        /// by $ADCSND
        /// </returns>
        private bool InterpretCommand(string received_command)
        {
            int command_end = received_command.IndexOf(" ");
            if (command_end == -1) command_end = received_command.Length;

            if (command_end != -1)
            {
                string parameter = "";
                string[] parameters ={ };
                string command = received_command.Substring(1);
                if (command_end != received_command.Length)
                {
                    command = received_command.Substring(1, command_end - 1);
                    parameter = received_command.Substring(command_end + 1);
                    parameters = parameter.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                }
                //Console.WriteLine("Command: '" + command + "' ,Parameter(" + parameters.Length + "): '" + parameter + "'");

                switch (command)
                {
                    case "Direction":
                        //Console.WriteLine("Direction command received: " + parameter);
                        handshake_his_value = int.Parse(parameters[1]);
                        if (parameters[0] == "Download") //turns arround in terms of our perception of the direction.
                            his_direction_wish = ConnectionDirection.Upload;
                        else his_direction_wish = ConnectionDirection.Download;
                        DecideDirection();
                        break;

                    case "MyNick":
                        peer_nick = parameters[0];
                        //Console.WriteLine("peer nick: "+peer_nick);
                        //handshake complete
                        break;

                    case "Supports":
                        //Console.WriteLine("Supports command received: " + parameter);
                        supports = (string[])parameters.Clone();
                        break;

                    case "MaxedOut":
                        error_code = ErrorCodes.NoFreeSlots;
                        Disconnect();
                        break;
                    case "Error":
                        error_code = ErrorCodes.NoErrorYet;
                        if (parameter == "File not found" || parameter == "File Not Available")
                            error_code = ErrorCodes.FileNotAvailable;
                        Disconnect();
                        break;

                    case "GetListLen":
                        //Console.WriteLine("GetListLen command received: " + parameter);
                        break;
                    //TODO check for correct direction ..else skip ,same for startdownload
                    case "ADCGET":
                    case "Get":
                        if (command == "ADCGET")
                        {
                            Console.WriteLine("ADCGET command received: " + parameter);
                            upload_request_filename = parameters[1];
                            try
                            {
                                upload_offset = long.Parse(parameters[2]);
                                upload_length = long.Parse(parameters[3]);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error parsing offsets: " + ex.Message);
                            }
                        }
                        else if (command == "Get")
                        {
                            Console.WriteLine("Get command received: " + parameter);
                            int offset_start = parameter.LastIndexOf("$");
                            if (offset_start == -1)
                                break; //no offset given skip this request
                            long offset = 1;
                            string filename = parameter.Substring(0, offset_start);
                            try
                            {
                                offset = long.Parse(parameter.Substring(offset_start + 1));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("error parsing offset: " + ex.Message);
                            }
                            upload_request_filename = filename;
                            upload_offset = offset - 1; //we want to send the first byte too , right ?
                            upload_length = -1;
                        }
                        FileRequestAnswer answer = FileRequestAnswer.FileNotAvailable;
                        if (upload_request_filename == "MyList.DcLst" || upload_request_filename == "MyList.bz2")
                        {//not supported right now,maybe never
                            SendFileNotAvailableError();
                            error_code = ErrorCodes.FileNotAvailable;
                            Disconnect();
                        }
                        else if (upload_request_filename == "files.xml.bz2")
                        {
                            if (FileListRequestReceived != null)
                                answer = FileListRequestReceived(this);
                        }
                        else
                        {
                            if (FileRequestReceived != null)
                                answer = FileRequestReceived(this);
                        }
                        //give back answer and maybe disconnect if needed or start upload

                        if (answer == FileRequestAnswer.LetsGo)
                        {
                            Console.WriteLine("Ok lets go , upload something.");
                            if (!StartUpload())
                            {
                                Console.WriteLine("Upload starting failed.Disconnecting...");
                                Disconnect();
                            }
                        }
                        else if (answer == FileRequestAnswer.FileNotAvailable)
                        {
                            Console.WriteLine("Sorry file not found replied.");
                            SendFileNotAvailableError();
                            Disconnect();
                        }
                        else if (answer == FileRequestAnswer.NoFreeSlots)
                        {
                            Console.WriteLine("Sorry no free slot for you.");
                            SendMaxedOut();
                            Disconnect();
                        }
                        break;

                    case "Send":
                        Console.WriteLine("Send command received: " + parameter);
                        StartUploadTransfer();
                        break;

                    case "ADCSND":
                        Console.WriteLine("ADCSEND command received: " + parameter);
                        try
                        {
                            long temp_upload_offset = long.Parse(parameters[2]);
                            long temp_upload_length = long.Parse(parameters[3]);
                            if (queue_entry.Type == Queue.QueueEntry.EntryType.File && !queue_entry.WantTTHL && (temp_upload_length + temp_upload_offset) != queue_entry.Filesize) Disconnect();//fail safe to secure downloads a bit 
                            if (queue_entry.Type == Queue.QueueEntry.EntryType.FileList)
                                queue_entry.Filesize = temp_upload_offset + temp_upload_length;
                            if (queue_entry.Type == Queue.QueueEntry.EntryType.File && queue_entry.WantTTHL)
                                tthl_size = temp_upload_length;
                        }
                        catch (Exception ex) { Console.WriteLine("Error parsing file length: " + ex.Message); }
                        StartDownloadTransfer();
                        return (true);
                    //break;


                    case "FileLength":
                        Console.WriteLine("FileLength command received: " + parameter);
                        try
                        {
                            long filelength = long.Parse(parameters[0]);
                            if (queue_entry.Type == Queue.QueueEntry.EntryType.File && filelength != queue_entry.Filesize) Disconnect();//fail safe to secure downloads a bit 
                            if (queue_entry.Type == Queue.QueueEntry.EntryType.FileList)
                                queue_entry.Filesize = filelength;
                        }
                        catch (Exception ex) { Console.WriteLine("Error parsing file length: " + ex.Message); }
                        StartDownloadTransfer();
                        break;

                    case "Key":
                        //Console.WriteLine("Key command received: " + parameter);
                        //Random rnd = new Random();
                        //SendCommand("Direction","Download "+rnd.Next(49999).ToString());
                        //SendCommand("GetListLen");

                        /*
                        try
                        {
                            if (HandShakeCompleted != null)
                                HandShakeCompleted(this);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Exception in Handshake Event: " + ex.Message);
                        }
                        */


                        //SendCommand("Get", "extras series\\Extras - S01E05 [digitaldistractions].avi$1");
                        //SendCommand("Get", "MyList.DcLst$1");
                        //SendCommand("Get", "files.xml.bz2$1");
                        //out_stream = System.IO.File.Create("temp.avi");
                        //out_stream = new FileStream("temp.avi", FileMode.Create, FileAccess.Write, System.IO.FileShare.ReadWrite);
                        //SendCommand("Send");
                        break;

                    case "Lock":
                        //Console.WriteLine("Lock Command received: " + parameter);
                        if (parameters.Length > 1)
                        {
                            string key = parameters[0];
                            //Console.WriteLine("Key: " + key);
                            if (key.StartsWith("EXTENDEDPROTOCOL"))
                            {
                                is_extended_protocol = true;
                                //Console.WriteLine("Peer is using the dc++ protocol enhancements.");
                                SendCommand("Supports", "MiniSlots XmlBZList TTHL TTHF ADCGet");
                                //SendCommand("Supports", "MiniSlots XmlBZList ADCGet TTHL TTHF GetZBlock ZLIG ");
                                //SendCommand("Supports", "MiniSlots XmlBZList TTHL TTHF GetZBlock ZLIG ");
                                //SendCommand("Supports", "BZList TTHL TTHF GetZBlock´ZLIG ");
                            }

                            string decoded_key = L2K(key);
                            Random rnd = new Random();
                            handshake_my_value = rnd.Next(32767);
                            //StartHandShake();
                            if (direction == ConnectionDirection.Upload)
                                SendCommand("Direction", "Upload " + handshake_my_value.ToString());
                            else SendCommand("Direction", "Download " + handshake_my_value.ToString());

                            DecideDirection();

                            //Console.WriteLine("SendingDecoded key: " + decoded_key);
                            SendCommand("Key", decoded_key);


                        }
                        break;

                    default:
                        Console.WriteLine("Unknown Command received: " + command + ", Parameter: " + parameter);
                        break;
                }
            }
            else Console.WriteLine("Error interpreting command: " + received_command);
            return (false);
        }
        private void DecideDirection()
        {
            if (direction == ConnectionDirection.Unknown && his_direction_wish != ConnectionDirection.Unknown)
            {//only decide once
                if (handshake_my_value < handshake_his_value)
                    direction = his_direction_wish;
                else direction = ConnectionDirection.Download;

                try
                {
                    if (HandShakeCompleted != null)
                        HandShakeCompleted(this);

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception in Handshake Event: " + ex.Message);
                }

            }
        }
        public Peer(Socket client, string nick)
        {
            this.nick = nick;
            this.socket = client;
            is_connected = true;
            StartReceiving();
        }
        public Peer(Socket client)
        {
            is_connected = true;
            this.socket = client;
            StartReceiving();
        }
        public Peer(string dst_ip, int dst_port)
        {
            ip = dst_ip;
            port = dst_port;
        }
        public Peer(string address)
        {
            string tmp = address;
            int port_start = tmp.IndexOf(":");
            if (port_start != -1)
            {
                int tmp_port = 411;
                string tmp_port_string = tmp.Substring(port_start + 1);
                try
                {
                    tmp_port = int.Parse(tmp_port_string);
                }
                catch (Exception)
                {
                    Console.WriteLine("error parsing port : " + tmp_port_string);
                }

                tmp = tmp.Substring(0, port_start);
                port = tmp_port;
            }
            ip = tmp; //ip or adress property?
        }
        public Peer()
        {
            ip = "";
            port = 0;
            socket = null;
        }
        public void StartHandShake()
        {
            SendCommand("MyNick", nick);
            string key = CreateKey(false);
            //Console.WriteLine("sending lock: '"+key+"'");
            SendCommand("Lock", key);
        }
        public override void Disconnect()
        {
            if (stream != null)
            {
                try
                {
                    stream.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error closing stream: "+ex.Message);
                }
            }
            if (is_connected || is_connecting)
            {
                if (socket != null)
                {
                    //if (socket.Connected)
                    //{
                    this.socket.Close();
                } else Console.WriteLine("This socket is unused -> no disconnect needed.");
                    //}
                    if (is_connected)
                    {
                        if (Disconnected != null)
                            Disconnected(this);
                    }
                    else if (is_connecting)
                    {
                        if (UnableToConnect != null)
                            UnableToConnect(this);
                    }
                    is_connected = false;
                    is_connecting = false;
                    is_downloading = false;
                    is_uploading = false;
                    //else Console.WriteLine("This peer is already disconnected");
            }
        }
        public void Connect()
        {
            if (is_connecting)
            {//better handling of fast user retries
                error_code = ErrorCodes.UserDisconnect;
                Disconnect();
            }
            if (!is_connected)
            {
                try
                {
                    is_connecting = true;
                    //Disconnect();//if still connected , disconnect first
                    socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    socket.Blocking = false;
                    IPEndPoint endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
                    AsyncCallback event_connect = new AsyncCallback(OnConnect);
                    socket.BeginConnect(endpoint, event_connect, socket);
                    socket.ReceiveTimeout = 500;
                    socket.SendTimeout = 500;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error connecting to Peer: " + ip + ":" + port + "(exception:" + ex.Message + ")");
                    error_code = ErrorCodes.UnableToConnect;
                    Disconnect();
                }
            }

        }
        public void ConnectTo(string dst_ip, int dst_port)
        {
            ip = dst_ip;
            port = dst_port;
            Connect();
        }
        private void OnConnect(IAsyncResult result)
        {
            Socket connect_socket = (Socket)result.AsyncState;
            try
            {
                if (connect_socket.Connected)
                {
                    AsyncCallback event_receive = new AsyncCallback(OnReceive);
                    receive_buffer = new byte[32768];
                    connect_socket.BeginReceive(receive_buffer, 0, receive_buffer.Length, SocketFlags.None, event_receive, connect_socket);
                    //Console.WriteLine("Successfully connected to peer: " + ip+":"+port);
                    is_connected = true;
                    is_connecting = false;
                    if (Connected != null)
                        Connected(this);
                }
                else
                {
                    //Console.WriteLine("Unable to connect to peer");
                    error_code = ErrorCodes.UnableToConnect;
                    Disconnect();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error during connect to peer(exception:" + ex.Message + ").");
                error_code = ErrorCodes.UnableToConnect;
                Disconnect();
            }
        }
        public void Ungrab()
        {
            //TODO remove all event handlers
            
        }
    }
}
