using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace DCPlusPlus
{
    //todo make it more encoding. ... alike

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
                    buffer[i] = (byte)(((char)data[pos / 8] >> (char)(pos % 8)) & 1);
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
        

        static public string GetString(byte[] data)
        {
            int letters = data.Length * 8 / 5;
            string base32_string = "";
            BitStream b = new BitStream();
            b.Write(data, 0, data.Length);
            b.Seek(0, System.IO.SeekOrigin.Begin);
            byte[] output = new byte[b.Length];
            int ret = b.Read(output, 0, output.Length);
            //now we have an array of bits in output that will be converted to base32




            return (base32_string);
        }
    }
}
