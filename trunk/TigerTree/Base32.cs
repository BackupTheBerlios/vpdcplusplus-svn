using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace DCPlusPlus
{
    //todo make it more encoding. ... alike
    [TestFixture]
    public class Base32
    {
        [TestFixture]
        public class BitStream : System.IO.Stream
        {

            private byte[] data = new byte[0];
            private long pos = 0;

            public override bool CanRead
            {
                get { return (true); }
            }

            public override bool CanSeek
            {
                get { return (true); }
            }

            public override bool CanWrite
            {
                get { return (true); }
            }

            public override void Flush()
            {
                throw new Exception("The method or operation is not implemented.");
            }

            public override long Length
            {
                get { return(data.Length * 8); }
            }

            public override long Position
            {
                get
                {
                    return (pos);
                }
                set
                {
                    pos = value;
                }
            }

            /// <summary>
            /// this will read count bits starting from offset into buffer as bytes(0 or 1)
            /// </summary>
            /// <param name="buffer">buffer to get bits into</param>
            /// <param name="offset">starting from here</param>
            /// <param name="count">how many bits do you want</param>
            /// <returns>number of bytes returned</returns>
            public override int Read(byte[] buffer, int offset, int count)
            {
                if (pos + count > data.Length * 8) count = (int)(data.Length * 8 - pos); //check if not reading more than there actually is
                for (int i = offset; i < offset + count; i++)
                {
                    //now convert data bits beginning from pos/8 + pos%8 bits into a byte representation of 0,1
                    //buffer[i] = (byte)(((char)data[pos / 8] >> (char)(pos % 8)) & 1);
                    //buffer[i] = (byte)(((char)data[pos / 8] >> (char)(pos % 8)) & 1);
                    buffer[i] = (byte)(((char)data[pos / 8] >> (char)(7-(pos % 8))) & 1);
                    pos++;
                }

                //pos = pos + count;
                return (count);
                //if (pos > data.Length * 8) pos = data.Length * 8;
            }

            /// <summary>
            /// seek to a specific position in the bitstream
            /// </summary>
            /// <param name="offset">offset in bits</param>
            /// <param name="origin">seek origin</param>
            /// <returns>new position relative to the start of the stream</returns>
            public override long Seek(long offset, System.IO.SeekOrigin origin)
            {
                switch (origin)
                {
                    case System.IO.SeekOrigin.Begin:
                        pos = offset;
                        break;
                    case System.IO.SeekOrigin.Current:
                        pos = pos + offset;
                        break;
                    case System.IO.SeekOrigin.End:
                        pos = data.Length * 8 - offset;
                        break;
                }
                return (pos);
            }

            public override void SetLength(long value)
            {
                byte[] new_data = new byte[value];
                //new_data = data;
                //TODO save old data
                data = new_data;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                byte[] new_data = new byte[count];
                for (int i = offset; i < offset + count; i++)
                {
                    new_data[i - offset] = buffer[i];
                }
                data = new_data;
                pos = pos + count * 8;
            }

            #region Unit Testing
            [Test]
            public void TestBitRead()
            {
                Console.WriteLine("Test to read bits from a BitStream.");
                try
                {
                    BitStream b = new BitStream();
                    byte[] test = { 0, 255, 0, 0, 255, 0 ,1,32};
                    b.Write(test, 0, test.Length);
                    b.Seek(0, System.IO.SeekOrigin.Begin);
                    Console.WriteLine("Bits in stream :" + b.Length);
                    Assert.IsTrue(b.Length == test.Length * 8, "length of bitstream not correct.");
                    byte[] output = new byte[b.Length];
                    int ret = b.Read(output, 0, output.Length);
                    Assert.IsTrue(ret == output.Length, "not enought bytes received from bitstream");
                    for (int i = 0; i < b.Length; i++)
                    {
                        Console.Write(output[i].ToString());
                    }

                    Console.WriteLine("");
                    for (int i = 0; i < b.Length; i++)
                    {
                        Console.WriteLine(i + ":" + output[i].ToString());
                    }
                    Assert.IsTrue(output[48] == 1, "Bits are not correct.");//1 in test bytes
                    Assert.IsTrue(output[61] == 1, "Bits are not correct.");//32 int test bytes

                }
                catch (Exception ex)
                {
                    Console.WriteLine("exception occured: "+ex.Message);
                    Assert.Fail("exception occured.");
                }
                Console.WriteLine("Read Bits Test successful.");
            }
            #endregion
        }


        static char[] conversion_array = {
            'A','B','C','D','E',
            'F','G','H','I','J',
            'K','L','M','N','O',
            'P','Q','R','S','T',
            'U','V','W','X','Y',
            'Z','2','3','4','5',
            '6','7'};


        static public string GetString(byte[] data)
        {
            int letters = data.Length * 8 / 5;
            string base32_string = "";
            BitStream b = new BitStream();
            b.Write(data, 0, data.Length);
            b.Seek(0, System.IO.SeekOrigin.Begin);
            if (b.Length == 0) return("");
            long output_length = b.Length;
            Console.WriteLine("bits in bitstream:" + b.Length);
            if (b.Length % 5 != 0)
            {
                Console.WriteLine("rest bits:" + (b.Length % 5));
                output_length += (5 - (b.Length % 5));
            }
            Console.WriteLine("bits in output buffer(+padding):" + output_length);
            byte[] output = new byte[output_length];
            output.Initialize();
            //output.Length
            int ret = b.Read(output, 0, (int)b.Length);//will crash with buffers over 4gb ,or something else
            //now we have an array of bits in output that will be converted to base32
            //5 bit to 1 byte + zero padding conversion
            //pad to correct length with zeros
            long pos = 0;
            while (pos < output.Length)
            {
                int value = 0;
                /*
                value += output[pos + 0] * 1;
                value += output[pos + 1] * 2;
                value += output[pos + 2] * 4;
                value += output[pos + 3] * 8;
                value += output[pos + 4] * 16;
                */
                
                value += output[pos + 0] * 16;
                value += output[pos + 1] * 8;
                value += output[pos + 2] * 4;
                value += output[pos + 3] * 2;
                value += output[pos + 4] * 1;
                
                base32_string += conversion_array[value];
                pos += 5;
            }
            Console.WriteLine("pos:"+pos);
            return (base32_string);
        }


        private static Char[] Base32Chars = {
												'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 
												'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P',
												'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 
												'Y', 'Z', '2', '3', '4', '5', '6', '7'};



        public string FromBase32String(string input_string)
        {//TODO
            return ("");
        }



        /// <summary>
        /// This method converts a byte array into a Base32-encoded string. The resulting
        /// string can be used safely for Windows file or directory names.
        /// </summary>
        /// <param name="inArray">The byte array to encode.</param>
        /// <returns>A base32-encoded string representation of the byte array.</returns>
        public static String ToBase32String(Byte[] inArray)
        {
            if (inArray == null) return null;
            int len = inArray.Length;
            // divide the input into 40-bit groups, so let's see, 
            // how many groups of 5 bytes can we get out of it?
            int numberOfGroups = len / 5;
            // and how many remaining bytes are there?
            int numberOfRemainingBytes = len - 5 * numberOfGroups;

            // after this, we're gonna split it into eight 5 bit
            // values. 
            StringBuilder sb = new StringBuilder();
            //int resultLen = 4*((len + 2)/3);
            //StringBuffer result = new StringBuffer(resultLen);

            // Translate all full groups from byte array elements to Base32
            int byteIndexer = 0;
            for (int i = 0; i < numberOfGroups; i++)
            {
                byte b0 = inArray[byteIndexer++];
                byte b1 = inArray[byteIndexer++];
                byte b2 = inArray[byteIndexer++];
                byte b3 = inArray[byteIndexer++];
                byte b4 = inArray[byteIndexer++];

                // first 5 bits from byte 0
                sb.Append(Base32Chars[b0 >> 3]);
                // the remaining 3, plus 2 from the next one
                sb.Append(Base32Chars[(b0 << 2) & 0x1F | (b1 >> 6)]);
                // get bit 3, 4, 5, 6, 7 from byte 1
                sb.Append(Base32Chars[(b1 >> 1) & 0x1F]);
                // then 1 bit from byte 1, and 4 from byte 2
                sb.Append(Base32Chars[(b1 << 4) & 0x1F | (b2 >> 4)]);
                // 4 bits from byte 2, 1 from byte3
                sb.Append(Base32Chars[(b2 << 1) & 0x1F | (b3 >> 7)]);
                // get bit 2, 3, 4, 5, 6 from byte 3
                sb.Append(Base32Chars[(b3 >> 2) & 0x1F]);
                // 2 last bits from byte 3, 3 from byte 4
                sb.Append(Base32Chars[(b3 << 3) & 0x1F | (b4 >> 5)]);
                // the last 5 bits
                sb.Append(Base32Chars[b4 & 0x1F]);
            }

            // Now, is there any remaining bytes?
            if (numberOfRemainingBytes > 0)
            {
                byte b0 = inArray[byteIndexer++];
                // as usual, get the first 5 bits
                sb.Append(Base32Chars[b0 >> 3]);
                // now let's see, depending on the 
                // number of remaining bytes, we do different
                // things
                switch (numberOfRemainingBytes)
                {
                    case 1:
                        // use the remaining 3 bits, padded with five 0 bits
                        sb.Append(Base32Chars[(b0 << 2) & 0x1F]);
                        //						sb.Append("======");
                        break;
                    case 2:
                        byte b1 = inArray[byteIndexer++];
                        sb.Append(Base32Chars[(b0 << 2) & 0x1F | (b1 >> 6)]);
                        sb.Append(Base32Chars[(b1 >> 1) & 0x1F]);
                        sb.Append(Base32Chars[(b1 << 4) & 0x1F]);
                        //						sb.Append("====");
                        break;
                    case 3:
                        b1 = inArray[byteIndexer++];
                        byte b2 = inArray[byteIndexer++];
                        sb.Append(Base32Chars[(b0 << 2) & 0x1F | (b1 >> 6)]);
                        sb.Append(Base32Chars[(b1 >> 1) & 0x1F]);
                        sb.Append(Base32Chars[(b1 << 4) & 0x1F | (b2 >> 4)]);
                        sb.Append(Base32Chars[(b2 << 1) & 0x1F]);
                        //						sb.Append("===");
                        break;
                    case 4:
                        b1 = inArray[byteIndexer++];
                        b2 = inArray[byteIndexer++];
                        byte b3 = inArray[byteIndexer++];
                        sb.Append(Base32Chars[(b0 << 2) & 0x1F | (b1 >> 6)]);
                        sb.Append(Base32Chars[(b1 >> 1) & 0x1F]);
                        sb.Append(Base32Chars[(b1 << 4) & 0x1F | (b2 >> 4)]);
                        sb.Append(Base32Chars[(b2 << 1) & 0x1F | (b3 >> 7)]);
                        sb.Append(Base32Chars[(b3 >> 2) & 0x1F]);
                        sb.Append(Base32Chars[(b3 << 3) & 0x1F]);
                        //						sb.Append("=");
                        break;
                }
            }
            return sb.ToString();
        }




#region Unit Testing
        [Test]
        public void TestGetString()
        {
            Console.WriteLine("Test to convert a byte array to a base32 string.");
            //byte[] test = { 0x3a, 0x27 };
            //byte[] test = { 0x27, 0x3a };
            byte[] test = { 0x27, 0x3a,0x33,0x56,0x32,0x05,0x12,0x40 };
            //byte[] test = { 16, 16 };
            //string compare_string = "EAQ";
            string base_32_string = Base32.GetString(test);
            string base_32_string_other = Base32.ToBase32String(test);
            //Assert.IsTrue(base_32_string == compare_string, "base32 conversion failed.(\"" + base_32_string + "\"!=\"" + compare_string + "\")");
            Assert.IsTrue(base_32_string == base_32_string_other, "base32 conversion failed.(\"" + base_32_string + "\"!=\"" + base_32_string_other + "\")");
            Console.WriteLine("Get Base32 String Test successful.");
        }

#endregion
    }
}
