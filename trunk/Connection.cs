using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace DCPlusPlus
{
    /*
     TODO
     * allow to change the supports client will send back
     * oncommandhandler etc
     */


    /// <summary>
    /// Basic Class for Peer and Hub Connections
    /// Contains properties and methods used by both
    /// to reduce redundancy
    /// </summary>
    public abstract class Connection
    {
        protected bool is_extended_protocol = false;
        /// <summary>
        /// Returns TRUE if the other side
        /// supports DC++ Extensions ($support comand)
        /// </summary>
        public bool IsExtendedProtocol
        {
            get
            {
                return (is_extended_protocol);
            }

        }
        protected string nick = "unknown";
        /// <summary>
        /// Get/Set your own Nickname (gets transfered via $Nick 
        /// during handshake of the connection)
        /// </summary>
        public string Nick
        {
            get
            {
                return (nick);
            }
            set
            {
                nick = value;
            }
        }
        //TODO change is_connected etc to a state enum 
        protected bool is_connected = false;
        /// <summary>
        ///  Returns TRUE if Peer/Hub is connected
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return (is_connected);
            }

        }
        protected bool is_connecting = false;
        /// <summary>
        /// Returns TRUE if Peer/Hub is still connecting
        /// </summary>
        public bool IsConnecting
        {
            get
            {
                return (is_connecting);
            }

        }
        protected string ip = "";
        /// <summary>
        /// Get/Set ip address of the remote end of the connection
        /// </summary>
        public string IP
        {
            get
            {
                return (ip);
            }
            set
            {
                ip = value;
            }
        }
        protected int port = 0;
        /// <summary>
        /// Get/Set port of the remote end of the connection
        /// </summary>
        public int Port
        {
            get
            {
                return (port);
            }
            set
            {
                port = value;
            }
        }
        /// <summary>
        /// The Socket used for all Peer/Hub communications
        /// </summary>
        protected Socket socket = null;
        /// <summary>
        /// The Receive buffer used by socket 
        /// </summary>
        protected byte[] receive_buffer = null;
        /// <summary>
        /// Prototype for the Disconnect method 
        /// used in the Peer/Hub classes
        /// </summary>
        public abstract void Disconnect();
        /// <summary>
        /// Send a command without parameters
        /// (for example $Send)
        /// </summary>
        /// <param name="command">the command to send</param>
        public void SendCommand(string command)
        {
            SendCommand(command, "");
        }
        /// <summary>
        /// Send a command with a parameter
        /// (for example $Nick test)
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="parameter">the parameter of the command</param>
        public void SendCommand(string command, string parameter)
        {
            if (!string.IsNullOrEmpty(parameter))
                SendCommand(command, parameter.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries));
            else
                SendCommand(command, new string[0]);
        }
        /// <summary>
        /// Send a command with parameters
        /// (for example $SR filename filesize etc)
        /// </summary>
        /// <param name="command">the command to send</param>
        /// <param name="parameters">array of parameters of the command</param>
        public void SendCommand(string command, string[] parameters)
        {
            if (socket != null)
            {
                if (!socket.Connected) return;
                string send_string = "$" + command;
                for (int i = 0; i < parameters.Length; i++)
                    send_string = send_string + " " + parameters[i];
                send_string = send_string + "|";
                try
                {
                    //socket.Send(Encoding.UTF8.GetBytes(send_string), SocketFlags.None);
                    //socket.Send(System.Text.Encoding.Default.GetBytes(send_string), SocketFlags.None);
                    byte[] send_bytes = System.Text.Encoding.Default.GetBytes(send_string);
                    socket.BeginSend(send_bytes, 0, send_bytes.Length, SocketFlags.None, new AsyncCallback(SendCommandCallback), socket);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error sending command to peer: " + e.Message);
                    error_code = ErrorCodes.Exception;
                    Disconnect();
                }
            }
        }
        /// <summary>
        /// Async Callback for SendCommand
        /// (gets called when the send is completed)
        /// </summary>
        /// <param name="ar">Async Result/State </param>
        protected void SendCommandCallback(IAsyncResult ar)
        {
            Socket send_command_socket = (Socket)ar.AsyncState;
            try
            {
                int bytes = send_command_socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                Console.WriteLine("exception during send of command: " + ex.Message);
                error_code = ErrorCodes.Exception;
                Disconnect();
            }
        }
        protected string[] supports=new string[0];
        /// <summary>
        /// Array of supported extensions by the remote side
        /// </summary>
        public string[] Supports
        {
            get
            {
                return (supports);
            }
        }
        /// <summary>
        /// Check if an extension is
        /// supported by the remote side
        /// </summary>
        /// <param name="extension">
        /// String containing the extension you 
        /// want to check for
        /// </param>
        /// <returns>Returns TRUE if the extension is supported</returns>
        public bool CheckForExtension(string extension)
        {
            foreach (string supported_extension in supports)
            {
                if (supported_extension == extension)
                    return (true);
            }
            return (false);
        }
        /// <summary>
        /// Enumeration of possible Connection ErrorCodes
        /// </summary>
        public enum ErrorCodes
        {
            UnableToConnect, Exception,UnknownException,NoFreeSlots,
            FileNotAvailable,Kicked,Banned,Disconnected,
            UnableToResolve,UrlNotFound,ProtocolError,
            ConnectionTimedOut,UserDisconnect,NoErrorYet,
            QueueEntryInUse

        }
        protected Connection.ErrorCodes error_code = Connection.ErrorCodes.NoErrorYet;
        /// <summary>
        /// Get the error code of a connection
        /// </summary>
        public Connection.ErrorCodes ErrorCode
        {
            get
            {
                return (error_code);
            }
        }

        #region LockToKey
        /*
 * This LockToKey does NOT use Microsoft.VisualBasic as a reference 
 * also strips $Lock and Pk=
 * Written by Gargol (gargol@gbot.nu)
 */
        public string L2K(string lck)
        {
            /*lck = lck.Replace("$Lock ", "");
            int iPos = lck.IndexOf(" Pk=", 1);
            if (iPos > 0) lck = lck.Substring(0, iPos);
             */
            int[] arrChar = new int[lck.Length + 1];
            int[] arrRet = new int[lck.Length + 1];
            arrChar[1] = lck[0];
            for (int i = 2; i < lck.Length + 1; i++)
            {
                arrChar[i] = lck[i - 1];
                arrRet[i] = arrChar[i] ^ arrChar[i - 1];
            }
            arrRet[1] = arrChar[1] ^ arrChar[lck.Length] ^ arrChar[lck.Length - 1] ^ 5;
            string sKey = "";
            for (int n = 1; n < lck.Length + 1; n++)
            {
                arrRet[n] = ((arrRet[n] * 16) & 240) | ((arrRet[n] / 16) & 15);
                int j = arrRet[n];
                switch (j)
                {
                    case 0:
                    case 5:
                    case 36:
                    case 96:
                    case 124:
                    case 126:
                        sKey += "/%DCN"
                             + ((string)("00" + j.ToString())).Substring(j.ToString().Length - 1)
                             + "%/";
                        break;
                    default:
                        sKey += Chr(Convert.ToByte((char)j));
                        break;
                }
            }
            return sKey;
        }

        public static char Chr(byte src)
        {
            return (Encoding.Default.GetChars(new byte[] { src })[0]);
        }
        
        public static string LockToKey2(string lockStr)
        {
            //string lockStr = GetLock(sLock);

            //			int j = Lock.IndexOf(" Pk=");
            //			string lockStr, pk;
            //			if(j > 0)
            //			{
            //				lockStr = Lock.Substring(0, j);
            //				pk = Lock.Substring(j + 4);
            //			}
            //			else
            //			{
            //				// Workaround for faulty linux hubs...
            //				j = Lock.IndexOf(" ");
            //				if(j > 0)
            //					lockStr = Lock.Substring(0, j);
            //				else
            //					lockStr = Lock;
            //			}

            //
            //			string result = "";
            //			int length = lockStr.Length;
            //			if(length > 2)
            //			{
            //				int h = lockStr[0] ^ lockStr[length - 1] ^ lockStr[length - 2] ^ 5;
            //				ConvertChar(ref h, ref result);
            //				for(int i = 1; i < lockStr.Length; i++)
            //				{
            //					h = lockStr[i] ^ lockStr[i - 1];
            //					ConvertChar(ref h, ref result);
            //				}
            //			}
            //			return result;

            if (lockStr.Length < 3)
            {
                return string.Empty;
            }

            char[] temp = new char[lockStr.Length];
            char v1;

            v1 = (char)(lockStr[0] ^ 5);
            v1 = (char)(((v1 >> 4) | (v1 << 4)) & 0xff);
            temp[0] = v1;

            int i;

            for (i = 1; i < lockStr.Length; i++)
            {
                v1 = (char)(lockStr[i] ^ lockStr[i - 1]);
                v1 = (char)(((v1 >> 4) | (v1 << 4)) & 0xff);
                temp[i] = v1;
            }

            temp[0] = (char)(temp[0] ^ temp[lockStr.Length - 1]);

            return KeySubst(temp, lockStr.Length);
        }

        static string KeySubst(char[] aKey, int len)
        {
            StringBuilder key = new StringBuilder(100);

            for (int i = 0; i < len; i++)
            {
                if (IsExtra(aKey[i]))
                {
                    key.Append("/%DCN");
                    key.Append(string.Format("{0:000}", (int)aKey[i]));
                    key.Append("%/");
                }
                else
                {
                    key.Append(aKey[i]);
                }
            }

            return key.ToString();
        }

        static bool IsExtra(char b)
        {
            return (b == 0 || b == 5 || b == 124 || b == 96 || b == 126 || b == 36);
        }



        /// <summary>
        /// Convert a Lock to a Key
        /// shamelessly grabbed from the dcpp dev wiki
        /// </summary>
        /// <param name="lck">the connections lock</param>
        /// <returns>a hopefully valid key</returns>
        protected string LockToKey(string lck)
        {
            string Key = "";
            for (int i = 0, j; lck.Length > i; i++)
            {
                if (i == 0) j = lck[0] ^ 5;
                else j = lck[i] ^ lck[i - 1];
                for (j += ((j % 17) * 15); j > 255; j -= 255) ;
                switch (j)
                {
                    case 0:
                    case 5:
                    case 36:
                    case 96:
                    case 124:
                    case 126:
                        Key += "/%DCN" + ((string)("00" + j.ToString())).Substring(j.ToString().Length - 1) + "%/";
                        break;
                    default:
                        Key += (char)j;
                        break;
                }
            }
            return (char)(Key[0] ^ Key[Key.Length - 1]) + Key.Substring(1);
        }


        private static string escapeChars(string key)
        {
            System.Text.StringBuilder builder =
                new System.Text.StringBuilder(key.Length);

            for (int index = 0; index < key.Length; index++)
            {
                int code = (int)key[index];
                if (code == 0 || code == 5 || code == 36 || code == 96
                        || code == 124 || code == 126)
                    builder.AppendFormat("/%DCN{0:000}%/", code);
                else
                    builder.Append(key[index]);
            }

            return builder.ToString();
        }

        /*
        private string LockToKey(string lck)
        {
            string Key = "";
            for (int i = 0, j; lck.Length > i; i++)
            {
                if (i == 0) j = lck[0] ^ 5;
                else j = lck[i] ^ lck[i - 1];
                for (j += ((j % 17) * 15); j > 255; j -= 255) ;
                switch (j)
                {
                    case 0:
                    case 5:
                    case 36:
                    case 96:
                    case 124:
                    case 126:
                        Key += "/%DCN" + ((string)("00" + j.ToString())).Substring(j.ToString().Length - 1) + "%/";
                        break;
                    default:
                        Key += (char)j;
                        break;
                }
            }
            return (char)(Key[0] ^ Key[Key.Length - 1]) + Key.Substring(1);
        }
        */

        /// <summary>
        /// Convert a Lock to a Key (personal version)
        /// (broken somehow)
        /// </summary>
        /// <param name="key">the connections lock</param>
        /// <returns>a hopefully valid key</returns>
        public string MyLockToKey(string key)
        {
            byte[] decoded_key_buffer = new byte[key.Length]; //so we have an exact duplicate in length
            for (int i = 1; i < key.Length; i++)
                decoded_key_buffer[i] = (byte)((char)key[i] ^ (char)key[i - 1]);
            decoded_key_buffer[0] = (byte)((char)key[0] ^ (char)key[key.Length - 1] ^ (char)key[key.Length - 2] ^ 5);
            for (int i = 0; i < key.Length; i++)
                decoded_key_buffer[i] = (byte)((((char)decoded_key_buffer[i] << 4) & 240) | (((char)decoded_key_buffer[i] >> 4) & 15));


            string decoded_key = Encoding.ASCII.GetString(decoded_key_buffer);
            for (int i = 0; i < decoded_key.Length; i++)
            {
                if (decoded_key[i] == (char)0)
                {
                    decoded_key = decoded_key.Remove(i, 1);
                    decoded_key = decoded_key.Insert(i, "/%DCN000%/");
                }
                if (decoded_key[i] == (char)5)
                {
                    decoded_key = decoded_key.Remove(i, 1);
                    decoded_key = decoded_key.Insert(i, "/%DCN005%/");
                }
                if (decoded_key[i] == (char)36)
                {
                    decoded_key = decoded_key.Remove(i, 1);
                    decoded_key = decoded_key.Insert(i, "/%DCN036%/");
                }
                if (decoded_key[i] == (char)96)
                {
                    decoded_key = decoded_key.Remove(i, 1);
                    decoded_key = decoded_key.Insert(i, "/%DCN096%/");
                }
                if (decoded_key[i] == (char)124)
                {
                    decoded_key = decoded_key.Remove(i, 1);
                    decoded_key = decoded_key.Insert(i, "/%DCN124%/");
                }
                if (decoded_key[i] == (char)126)
                {
                    decoded_key = decoded_key.Remove(i, 1);
                    decoded_key = decoded_key.Insert(i, "/%DCN126%/");
                }

            }

            return (decoded_key);
        }
        #endregion
        /// <summary>
        /// Creates a key used during handshake
        /// </summary>
        /// <param name="extended">
        /// TRUE if you want to tell the
        /// remote side that we are supporting DC++ extensions
        /// </param>
        /// <returns>String containing a valid key</returns>
        protected string CreateKey(bool extended)
        {
            string key = "";
            string pk = "";
            string id = "EXTENDEDPROTOCOL";
            Random rnd = new Random();

            int key_len = id.Length + rnd.Next(14);

            if (extended)
            {
                key_len -= id.Length;
                key = id;
            }
            for (int i = 0; i < key_len; i++)
            {
                key += (char)(rnd.Next(94) + 33);
            }

            for (int i = 0; i < 16; i++)
            {
                pk += (char)(rnd.Next(94) + 33);
            }


            key = key + " Pk=" + pk;

            for (int i = 0; i < key.Length; i++)
            {
                if (key[i] == (char)0)
                {
                    key = key.Remove(i, 1);
                    key = key.Insert(i, "/%DCN000%/");
                }
                if (key[i] == (char)5)
                {
                    key = key.Remove(i, 1);
                    key = key.Insert(i, "/%DCN005%/");
                }
                if (key[i] == (char)36)
                {
                    key = key.Remove(i, 1);
                    key = key.Insert(i, "/%DCN036%/");
                }
                if (key[i] == (char)96)
                {
                    key = key.Remove(i, 1);
                    key = key.Insert(i, "/%DCN096%/");
                }
                if (key[i] == (char)124)
                {
                    key = key.Remove(i, 1);
                    key = key.Insert(i, "/%DCN124%/");
                }
                if (key[i] == (char)126)
                {
                    key = key.Remove(i, 1);
                    key = key.Insert(i, "/%DCN126%/");
                }


            }

            key = "EXTENDEDPROTOCOLABCABCABCABCABCABC Pk=DCPLUSPLUS0.698ABCABC";
            return (key);
        }
    }
}
