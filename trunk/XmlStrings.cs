using System;
using System.Collections.Generic;
using System.Text;

namespace DCPlusPlus
{
    public abstract class XmlStrings
    {
        /// <summary>
        /// Convert a xml string back to a normal string
        /// (unquoting of invalid characters)
        /// these functions need a real rewrite going on some time
        /// </summary>
        /// <param name="org">xml string</param>
        /// <returns>an unquoted string</returns>
        public static string FromXmlString(string org)
        {
            if (org == null) return ("");
            string tmp = new string(org.ToCharArray());
            //while (tmp.IndexOf("&amp;") != -1) tmp = tmp.Replace("&amp;", "&");
            //while (tmp.IndexOf("&lt;") != -1) tmp = tmp.Replace("&lt;", "<");
            //while (tmp.IndexOf("&gt;") != -1) tmp = tmp.Replace("&gt;", ">");
            //while (tmp.IndexOf("&quot;") != -1) tmp = tmp.Replace("&quot;", "\"");
            //while (tmp.IndexOf("&apos;") != -1) tmp = tmp.Replace("&apos;", "'");
            
            //convert &#4545; alike quotes back to 'normal' characters
            int index = tmp.IndexOf("&#");
            while ( index != -1)
            {
                int end = tmp.IndexOf(";", index);
                if (end != -1)
                {
                    string num_string = tmp.Substring(index + 2, end - (index + 2));
                    int num = 0;
                    try
                    {
                        //num = int.Parse(num_string, System.Globalization.NumberStyles.AllowHexSpecifier);
                        num = int.Parse(num_string);
                    }
                    catch (Exception ex)
                    {
                        continue;
                    }
                    tmp = tmp.Remove(index, end + 1 - index);
                    char out_char = Convert.ToChar(num);
                    tmp = tmp.Insert(index, out_char.ToString());

                }
                else
                {
                    //better handle broken tags too ;-) (else an infinite loop will occur)

                    //search for first non number and use that pos as end of the tag
                    //int end = index+2;
                    tmp = tmp.Remove(index, 2);

                }
                index = tmp.IndexOf("&#");
            }
            tmp = tmp.Replace("&lt;", "<");
            tmp = tmp.Replace("&gt;", ">");
            tmp = tmp.Replace("&quot;", "\"");
            tmp = tmp.Replace("&apos;", "'");
            tmp = tmp.Replace("&amp;", "&");


            
            for (int i = 0; i < 32; i++)
            {
                char c = Convert.ToChar(i);
                if (i != 0x09 && i != 0x0a && i != 0x0d)
                {
                    int pos = -1;
                    string s = "&#" + Convert.ToString(c, 16) + ";";
                    while ((pos = tmp.IndexOf(s)) != -1)
                    {
                        tmp = tmp.Remove(pos, s.Length);
                        tmp = tmp.Insert(pos, c.ToString());
                    }
                }
            }
            return (tmp);
        }
        /// <summary>
        /// Convert a string to a valid xml string
        /// (enquoting of invalid characters)
        /// these functions need a real rewrite going on some time
        /// </summary>
        /// <param name="org">some string to be converted to a xml string</param>
        /// <returns>a quoted xml string</returns>
        public static string ToXmlString(string org)
        {
            if (String.IsNullOrEmpty(org)) return ("");

            string tmp = new string(org.ToCharArray());
            int p = 0;
            //while ((p = tmp.IndexOf("&", p)) != -1) { tmp = tmp.Replace("&", "&amp;"); if(p<tmp.Length)p++; }
            List<int> amps = new List<int>();
            while ((p = tmp.IndexOf("&", p)) != -1)
            {
                amps.Add(p);
                if (p < tmp.Length - 1) p++;
                else break;


            }
            for (int i = 0; i < amps.Count; i++)
            {
                tmp = tmp.Remove(amps[i] + (i * 4), 1);
                tmp = tmp.Insert(amps[i] + (i * 4), "&amp;");
            }

            tmp = tmp.Replace("<", "&lt;");
            tmp = tmp.Replace(">", "&gt;");
            tmp = tmp.Replace("\"", "&quot;");
            tmp = tmp.Replace("'", "&apos;");
            
            //while (tmp.IndexOf("<") != -1) tmp = tmp.Replace("<", "&lt;");
            //while (tmp.IndexOf(">") != -1) tmp = tmp.Replace(">", "&gt;");
            //while (tmp.IndexOf("\"") != -1) tmp = tmp.Replace("\"", "&quot;");
            //while (tmp.IndexOf("'") != -1) tmp = tmp.Replace("'", "&apos;");
            for (int i = 0; i < 32; i++)
            {
                char c = Convert.ToChar(i);
                if (i != 0x09 && i != 0x0a && i != 0x0d)
                {
                    int pos = -1;
                    while ((pos = tmp.IndexOf(c)) != -1)
                    {
                        tmp = tmp.Remove(pos, 1);
                        tmp = tmp.Insert(pos, "&#" + Convert.ToString(c, 16) + ";");
                    }
                }
            }
            return (tmp);
        }
    }
}
