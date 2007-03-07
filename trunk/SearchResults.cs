using System;
using System.Collections.Generic;
using System.Text;
//using System.Windows.Forms;
//using System.

namespace DCPlusPlus
{
    /// <summary>
    /// A list of search results class
    /// </summary>
    public class SearchResults
    {
        //TODO deprecate this ;-)
        /// <summary>
        /// Prototype for the Results Changed Event Handler
        /// (soon to be deprecated)
        /// </summary>
        /// <param name="s_results">the search results that were changed</param>
        /// <param name="num_results">number of results in the search results list</param>
        public delegate void ResultsChangedEventHandler(SearchResults s_results, int num_results);
        /// <summary>
        /// Event handler that gets called
        /// when the search results list was changed
        /// </summary>
        public event ResultsChangedEventHandler ResultsChanged;
        /// <summary>
        /// Prototype for the Results Cleared Event Handler
        /// </summary>
        /// <param name="s_results">the search results that were cleared</param>
        public delegate void ResultsClearedEventHandler(SearchResults s_results);
        /// <summary>
        /// Event handler that gets called
        /// when the search results were cleared
        /// </summary>
        public event ResultsClearedEventHandler ResultsCleared;
        /// <summary>
        /// Prototype for the Result Added Event Handler
        /// </summary>
        /// <param name="s_results">the search results to which a result was added</param>
        /// <param name="result">the added search result</param>
        public delegate void ResultAddedEventHandler(SearchResults s_results, SearchResult result);
        /// <summary>
        /// Event handler that gets called
        /// when a result was added the search results list
        /// </summary>
        public event ResultAddedEventHandler ResultAdded;
        /// <summary>
        /// array of search terms (space seperated words of the orignal search term)
        /// </summary>
        private string[] search_terms;

        protected string search_term = "";
        /// <summary>
        /// Get/Set the term to search for
        /// </summary>
        public string SearchTerm
        {
            get
            {
                return (search_term);
            }
            set
            {
                search_term = value;
                char[] sep = new char[1];
                sep[0] = ' ';
                search_terms = search_term.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        protected bool discard_old_results = false;
        /// <summary>
        /// Set this to TRUE to automatically discard old 
        /// search results received after starting a new search
        /// (TODO check if tth sources of others downloads wont be skipped too)
        /// </summary>
        public bool DiscardOldResults 
        {
            get
            {
                return (discard_old_results);
            }
            set
            {
                discard_old_results = value;
            }
        }
        /// <summary>
        /// A class to hold the values
        /// read from a search result line orignating from
        /// a hub or was received via udp
        /// </summary>
        public class SearchResult
        {
            //public delegate bool OnHubResolveHandler(object sender, string hub_address);
            //public event OnHubResolveHandler OnHubResolve;


            //TODO change this to a queue.queueentry.source
            protected string filename = "";
            /// <summary>
            /// the filename of the search result
            /// </summary>
            public string Filename
            {
                get
                {
                    return (filename);
                }
            }
            protected string user_name = "";
            /// <summary>
            /// the username of the user who sent the search result
            /// </summary>
            public string UserName
            {
                get
                {
                    return (user_name);
                }
            }
            /// <summary>
            /// read the search result values from a result line
            /// </summary>
            private void SplitResultLine()
            {
                /*
                 * $SR <source_nick> <result> <free_slots>/<total_slots><0x05><hub_name> (<hub_ip:listening_port>)[<0x05><target_nick>]|
                 * Description
                 * This command is used to return a file or directory that matches a $Search. All terms in the original query must be present in <result>, and all types and size restrictions in the $Search must be met. 

                 * <result> is one of the following: 
                 * <file_name><0x05><file_size> for file results 
                 * <directory> for directory results 
                 * The <0x05> characters used above for deliminators are the 5th character in the ASCII character set. 
                 * Sent by a client when a match to a $Search is found. 
                 * If the $Search was a passive one, the $SR is returned via the hub connection (TCP). In this case, <0x05><target_nick> must be included on the end of the $SR. The hub must strip the deliminator and <target_nick> before sending the $SR to <target_nick>. If the search was active, it is sent to the IP address and port specified in the $Search via UDP. 
                 * The port for the hub only needs to specified if its listening port is not the default (411). 
                 * On UNIX the path delimiter / must be converted to \ for compatibility. 
                 * DC++ will send a maximum of 5 search results to passive users and 10 to active users. 
                 * For files containing TTH, the <hub_name> parameter is replaced with TTH:<base32_encoded_tth_hash> (ref: TTH_Hash) 
                 * Example
                 * Passive Result
                 * $SR User1 mypathmotd.txt<0x05>437 3/4<0x05>Testhub (10.10.10.10:411)<0x05>User2|
                 * Active Result
                 * $SR User1 mypathmotd.txt<0x05>437 3/4<0x05>Testhub (10.10.10.10:411)|
                 */

                if (string.IsNullOrEmpty(result_line))
                {
                    Console.WriteLine("Error empty result line.");
                    return;
                }

                try
                {

                    char[] sep = new char[1];
                    sep[0] = (char)0x05;
                    string[] blocks = result_line.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                    if (blocks.Length == 3)
                    {
                        //Console.WriteLine("block 1: " + blocks[0] + ",block 2: " + blocks[1] + ",block 3: " + blocks[2]);
                        string[] first_block_tokens = blocks[0].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        string[] second_block_tokens = blocks[1].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        string[] third_block_tokens = blocks[2].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        string[] slots = second_block_tokens[second_block_tokens.Length - 1].Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        user_name = first_block_tokens[0];
                        if (third_block_tokens[0].StartsWith("TTH:"))
                        {
                            tth = third_block_tokens[0].Substring(4);
                        }
                        else
                        {
                            hub_name = "";
                            for (int i = 0; i < third_block_tokens.Length - 2; i++)
                                hub_name += third_block_tokens[i] + " ";
                            hub_name.TrimEnd();//kill the last whitespace added - Maybe this will not give a correct hubname... for instance hub names using double whitespaces
                        }

                        if (third_block_tokens[third_block_tokens.Length - 1].StartsWith("(") && third_block_tokens[third_block_tokens.Length - 1].EndsWith(")"))
                        {
                            hub_address = third_block_tokens[third_block_tokens.Length - 1].Substring(1, third_block_tokens[third_block_tokens.Length - 1].Length - 2);
                        }

                        filename = blocks[0].Substring(user_name.Length + 1);
                        file_extension = System.IO.Path.GetExtension(filename);
 
                        filesize = int.Parse(second_block_tokens[0]);
                        free_slots = int.Parse(slots[0]);
                        total_slots = int.Parse(slots[1]);
                        is_directory = false;
                    }
                    else if (blocks.Length == 2)
                    {
                        //Console.WriteLine("block 1: " + blocks[0] + ",block 2: " + blocks[1]);
                        string[] first_block_tokens = blocks[0].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        string[] second_block_tokens = blocks[1].Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        string[] slots = first_block_tokens[first_block_tokens.Length - 1].Split("/".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        user_name = first_block_tokens[0];
                        if (second_block_tokens[0].StartsWith("TTH:"))
                        {
                            tth = second_block_tokens[0].Substring(4);
                        }
                        else
                        {
                            hub_name = "";
                            for (int i = 0; i < second_block_tokens.Length - 2; i++)
                                hub_name += second_block_tokens[i] + " ";
                            hub_name.TrimEnd();//kill the last whitespace added
                        }
                        if (second_block_tokens[second_block_tokens.Length - 1].StartsWith("(") && second_block_tokens[second_block_tokens.Length - 1].EndsWith(")"))
                        {
                            hub_address = second_block_tokens[second_block_tokens.Length - 1].Substring(1, second_block_tokens[second_block_tokens.Length - 1].Length - 2);
                        }

                        directory = blocks[0].Substring(user_name.Length + 1, blocks[0].Length - (user_name.Length + 1) - 1 - first_block_tokens[first_block_tokens.Length - 1].Length);
                        
                        /*
                        directory = "";
                        for (int i = 1; i < first_block_tokens.Length - 2; i++)
                            directory += first_block_tokens[i];
                        */

                        free_slots = int.Parse(slots[0]);
                        total_slots = int.Parse(slots[1]);
                        is_directory = true;
                    }


                    /*
                    Console.WriteLine("username: '" + user_name + "'");
                    if (is_directory)
                    {
                        Console.WriteLine("directory: '" + directory + "'");
                    }
                    else
                    {
                        Console.WriteLine("filename: '" + filename + "'");
                        Console.WriteLine("file_extension: '" + file_extension + "'");
                        Console.WriteLine("filesize: '" + filesize + "'");
                    }
                    Console.WriteLine("free slots: '" + free_slots + "'");
                    Console.WriteLine("total slots: '" + total_slots + "'");
                    if(has_tth)
                        Console.WriteLine("tth: '" + tth + "'");
                    else Console.WriteLine("hub_name: '" + hub_name + "'");

                    Console.WriteLine("hub_address: '" + hub_address + "'");
                    */

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error parsing result: " + ex.Message);
                }




            }
            protected string result_line;
            /// <summary>
            /// Set the Result Line received via hub or udp
            /// </summary>
            public string ResultLine
            {
                set
                {
                    result_line = value;
                    SplitResultLine();

                }
            }
            protected bool is_hub_resolved = false;
            /// <summary>
            /// TRUE if a hub was resolved from the hub address
            /// </summary>
            public bool IsHubResolved
            {
                get
                {
                    return (is_hub_resolved);
                }
            }
            protected string hub_address="";
            /// <summary>
            /// Get the hub address
            /// </summary>
            public string HubAddress
            {
                get
                {
                    return(hub_address);// = value;
                    //if (OnHubResolve != null)
                        //is_hub_resolved = OnHubResolve(this, hub_address);
                }
            }
            /// <summary>
            /// TRUE if the result is a file
            /// </summary>
            public bool IsFile
            {
                get
                {
                    return (!is_directory);
                }
            }
            protected bool is_directory = false;
            /// <summary>
            /// TRUE if the result is a directory
            /// </summary>
            public bool IsDirectory
            {
                get
                {
                    return (is_directory);
                }
            }
            protected string directory = "";
            /// <summary>
            /// the directory name of the search result
            /// </summary>
            public string Directory
            {
                get
                {
                    return (directory);
                }
            }
            protected string file_extension = "";
            /// <summary>
            /// the file extension if the result is a file
            /// </summary>
            public string FileExtension
            {
                get
                {
                    return (file_extension);
                }
            }
            protected int filesize = 0;
            /// <summary>
            /// the file size if the result is a file
            /// </summary>
            public int Filesize
            {
                get
                {
                    return (filesize);
                }
            }
            /// <summary>
            /// TRUE if the search result has a tth included
            /// </summary>
            public bool HasTTH
            {
                get
                {
                    return (!string.IsNullOrEmpty(tth));
                }
            }
            protected string tth = "";
            /// <summary>
            /// the base32 encoded tth of the search result
            /// </summary>
            public string TTH
            {
                get
                {
                    return (tth);
                }
            }
            protected string hub_name="";
            /// <summary>
            /// the name of the hub the user is connected to 
            /// </summary>
            public string HubName
            {
                get
                {
                    return (hub_name);
                }
            }
            protected int free_slots = 0;
            /// <summary>
            /// the number of free slots the user has to offer
            /// </summary>
            public int FreeSlots
            {
                get
                {
                    return (free_slots);
                }
            }
            protected int total_slots = 0;
            /// <summary>
            /// the total number of slots the user has to offer
            /// </summary>
            public int TotalSlots
            {
                get
                {
                    return (total_slots);
                }
            }
            /// <summary>
            /// TRUE if the search result has a hub 
            /// (to be found by the client in his connected hubs list)
            /// (seems to be redudant with is_hub_resolved)
            /// </summary>
            public bool HasHub
            {
                get
                {
                    return (hub!=null);
                }
            }
            protected Hub hub=null;
            /// <summary>
            /// the hub on which the user that send us the search result is connected to
            /// </summary>
            public Hub Hub
            {
                get
                {
                    return (hub);
                }
                set
                {
                    if (value != null)
                        is_hub_resolved = true;
                    hub = value;
                }
            }
            /// <summary>
            /// SearchResult Constructor
            /// </summary>
            public SearchResult()
            {

            }
            /// <summary>
            /// SearchResult Constructor
            /// initializing the result parameters
            /// </summary>
            /// <param name="result_parameter_line">the search result parameters</param>
            public SearchResult(string result_parameter_line)
            {
                result_line = result_parameter_line;
                SplitResultLine();
            }
        }
        /// <summary>
        /// SearchResults Constructor
        /// </summary>
        public SearchResults()
        {
            
        }
        /// <summary>
        /// the SearchResults lock to make
        /// this class thread safe
        /// </summary>
        protected Object results_lock = new Object();
        /*public Object ResultsLock
        {
            get
            {
                return (results_lock);
            }
            set
            {
                results_lock = value;
            }
        }*/
        protected List<SearchResult> results = new List<SearchResult>();
        /// <summary>
        /// A list of search results
        /// (TODO to be deprecated and replaced with an enumerator)
        /// </summary>
        public List<SearchResult> Results
        {
            get
            {
                return (results);
            }
        }
        /// <summary>
        /// Add a search result to the results list
        /// </summary>
        /// <param name="result">the result to be added</param>
        public void AddResult(SearchResult result)
        {

            //discard old results if desired
            if (discard_old_results)
            {
                if (string.IsNullOrEmpty(search_term)) return;
                string path = "";
                if (result.IsFile)
                    path = result.Filename;
                else if (result.IsDirectory)
                    path = result.Directory;

                if (!string.IsNullOrEmpty(path))
                {
                    //check if search terms whitespace seperated match the path+filename in the result
                    //if not discard result (return)
                    bool all_terms_included = true;
                    foreach (string term in search_terms)
                    {
                        if (!path.Contains(term))
                            all_terms_included = false;
                    }
                    if (!all_terms_included)
                        return;

                }
            }
            lock (results_lock)
            {
                results.Add(result);
            }
            try
            {
                if (ResultAdded != null)
                    ResultAdded(this, result);

                if (ResultsChanged != null)
                    ResultsChanged(this, results.Count);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in event handler: " + ex.Message);
            }

        }
        /// <summary>
        /// Remove a search result from the results list
        /// </summary>
        /// <param name="result">the search result to remove</param>
        public void RemoveResult(SearchResult result)
        {
            lock (results_lock)
            {
                results.Remove(result);
            }
            try
            {
                if (ResultsChanged != null)
                    ResultsChanged(this, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in event handler: " + ex.Message);
            }

        }
        /// <summary>
        /// Clear all results from the results list
        /// </summary>
        public void ClearResults()
        {
            search_term = "";
            lock (results_lock)
            {
                results.Clear();
            }
            try
            {
                if (ResultsCleared != null)
                    ResultsCleared(this);

                if (ResultsChanged != null)
                    ResultsChanged(this, 0);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in event handler: " + ex.Message);
            }

        }
    }
}
