using System.Text;

namespace FPacker.Compression; 

public class BisCompression {
    private const int PacketFormatUncompressed = 1;
    private const byte Space = 0x20;
    
    //Compression probably breaks when applied to a file with 
    //a byte count over int.MaxValue ~ 2^(32-1)
    public static byte[] Compress(byte[] data) {
        var output = new List<byte>();
        var buffer = new CompressionBuffer();

        var dataLength = data.Length;
        var readData = 0;

        //Generate and add compressed data
        while (readData < dataLength) {
            var packet = new Packet();
            readData = packet.Pack(data, readData, buffer);

            var content = packet.GetContent();

            output.AddRange(content);
        }

        //Calculate and add checksum of compressed data
        var checksum = CalculateChecksum(data);
        output.AddRange(checksum);

        //Console.WriteLine(Encoding.UTF8.GetString(Decompress(output.ToArray(), dataLength)));
        
        return output.ToArray();
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public static byte[] Decompress(byte[] compressedData, long targetLength) {

        Stream source = new MemoryStream(compressedData);

        using var reader = new BinaryReader(source, Encoding.UTF8, true);
        using var dest = new MemoryStream();
        using var writer = new BinaryWriter(dest);

        var ctx = new ProcessContext {
            Reader = reader,
            Writer = writer,
            Dest = dest
        };

        var noOfBytes = dest.Position + targetLength;
        while (dest.Position < noOfBytes && source.CanRead) {
            var format = reader.ReadByte();
            for (byte i = 0; i < 8 && dest.Position < noOfBytes && source.Position < source.Length - 2; i++) {
                ctx.Format = format >> i & 0x01;
                ProcessBlock(ctx);
            }
        }
        
        return (Validate(ctx) ? dest.ToArray() : null) ?? throw new InvalidOperationException();
    }

    private static IEnumerable<byte> CalculateChecksum(IEnumerable<byte> data) {
        return BitConverter.GetBytes(data.Aggregate<byte, uint>(0, (current, t) => current + t));
    }

    private class Packet {
        private const int m_DataBlockCount = 8;
        private const int m_MinPackBytes = 3;
        private const int m_MaxDataBlockSize = m_MinPackBytes + 0b1111;
        private const int m_MaxOffsetForWhitespaces = 0b0000111111111111 - m_MaxDataBlockSize;

        private int m_Flagbits;
        public readonly List<byte> m_Content = new List<byte>();
        private List<byte> m_Next = new List<byte>();
        CompressionBuffer m_CompressionBuffer = new CompressionBuffer();

        public int Pack(byte[] data, int currPos, CompressionBuffer buffer) {
            m_CompressionBuffer = buffer;

            for (var i = 0; i < m_DataBlockCount && currPos < data.Length; i++) {
                var blockSize = Math.Min(m_MaxDataBlockSize, data.Length - currPos);
                if (blockSize < m_MinPackBytes) {
                    currPos += AddUncompressed(i, data, currPos);
                    continue;
                }

                currPos += AddCompressed(i, data, currPos, blockSize);
            }

            return currPos;
        }

        public byte[] GetContent() {
            var output = new byte[1 + m_Content.Count];
            output[0] = BitConverter.GetBytes(m_Flagbits)[0];

            for (var i = 1; i < output.Length; i++) {
                output[i] = m_Content[i - 1];
            }

            return output;
        }

        public int AddUncompressed(int blockIndex, byte[] data, int currPos) {
            m_CompressionBuffer.AddByte(data[currPos]);
            m_Content.Add(data[currPos]);
            m_Flagbits += 1 << blockIndex;
            return 1;
        }

        public int AddCompressed(int blockIndex, byte[] data, int currPos, int blockSize) {
            m_Next = new List<byte>();
            for (var i = 0; i < blockSize; i++) {
                m_Next.Add(data[currPos + i]);
            }

            var next = m_Next.ToArray();
            var intersection = m_CompressionBuffer.Intersect(next, blockSize);
            var whitespace = currPos < m_MaxOffsetForWhitespaces
                ? m_CompressionBuffer.CheckWhiteSpace(next, blockSize)
                : 0;
            var sequence = m_CompressionBuffer.CheckSequence(next, blockSize);

            if (intersection.Length < m_MinPackBytes && whitespace < m_MinPackBytes &&
                sequence.SourceBytes < m_MinPackBytes) {
                return AddUncompressed(blockIndex, data, currPos);
            }

            var processed = 0;
            short pointer = 0;

            if (intersection.Length >= whitespace && intersection.Length >= sequence.SourceBytes) {
                pointer = CreatePointer(m_CompressionBuffer.GetLength() - intersection.Position, intersection.Length);
                processed = intersection.Length;
            }
            else if (whitespace >= intersection.Length && whitespace >= sequence.SourceBytes) {
                pointer = CreatePointer(currPos + whitespace, whitespace);
                processed = whitespace;
            }
            else {
                pointer = CreatePointer(sequence.SequenceBytes, sequence.SourceBytes);
                processed = sequence.SourceBytes;
            }

            m_CompressionBuffer.AddBytes(data, currPos, processed);
            var tmp = BitConverter.GetBytes(pointer);
            foreach (var t in tmp) {
                m_Content.Add(t);
            }

            return processed;
        }

        short CreatePointer(int offset, int length) {
            //4 bits
            //00001111 00000000
            var lengthEntry = (short) ((length - m_MinPackBytes) << 8);
            //12 bits
            //11110000 11111111
            var offsetEntry = (short) (((offset & 0x0F00) << 4) + (offset & 0x00FF));

            return (short) (offsetEntry + lengthEntry);
        }
    }

    private class CompressionBuffer {
        public struct Intersection {
            public int Position;
            public int Length;
        }

        public struct Sequence {
            public int SourceBytes;
            public int SequenceBytes;
        }

        //4095 ---> 2^12
        long m_Size = 0b0000111111111111;
        List<byte> m_Content;

        public CompressionBuffer(long size = 0) {
            if (size != 0) {
                m_Size = size;
            }

            m_Content = new List<byte>();
        }

        public int GetLength() {
            return m_Content.Count;
        }

        public void AddBytes(byte[] data, int currPos, int length) {
            for (var i = 0; i < length; i++) {
                if (m_Size < m_Content.Count + 1) {
                    m_Content.RemoveAt(0);
                }

                m_Content.Add(data[currPos + i]);
            }
        }

        public void AddByte(byte data) {
            if (m_Size < m_Content.Count + 1) {
                m_Content.RemoveAt(0);
            }

            m_Content.Add(data);
        }

        public Intersection Intersect(byte[] buffer, int length) {
            var intersection = new Intersection {
                Position = -1,
                Length = 0
            };

            if (length == 0 || m_Content.Count == 0) {
                return intersection;
            }

            var offset = 0;
            while (true) {
                var next = IntersectAtOffset(buffer, length, offset);

                if (next.Position >= 0 && intersection.Length < next.Length) {
                    intersection = next;
                }

                if (next.Position < 0 || next.Position > m_Content.Count - 1) {
                    break;
                }

                offset = next.Position + 1;
            }

            return intersection;
        }

        Intersection IntersectAtOffset(byte[] buffer, int bLength, int offset) {
            var position = m_Content.IndexOf(buffer[0], offset);
            var length = 0;

            if (position >= 0 && position < m_Content.Count) {
                length++;
                for (int bufIndex = 1, dataIndex = position + 1;
                     bufIndex < bLength && dataIndex < m_Content.Count;
                     bufIndex++, dataIndex++) {
                    if (m_Content[dataIndex] != buffer[bufIndex]) {
                        break;
                    }

                    length++;
                }
            }

            Intersection intersection;
            intersection.Position = position;
            intersection.Length = length;
            return intersection;
        }


        public int CheckWhiteSpace(byte[] buffer, int length) {
            var count = 0;
            for (var i = 0; i < length; i++) {
                if (buffer[i] != 0x20) {
                    break;
                }

                count++;
            }

            return count;
        }

        public Sequence CheckSequence(byte[] buffer, int length) {
            Sequence result;
            result.SequenceBytes = 0;
            result.SourceBytes = 0;

            var maxSourceBytes = Math.Min(m_Content.Count, length);
            for (var i = 1; i < maxSourceBytes; i++) {
                var sequence = CheckSequenceImpl(buffer, length, i);
                if (sequence.SourceBytes > result.SourceBytes) {
                    result = sequence;
                }
            }

            return result;
        }

        Sequence CheckSequenceImpl(byte[] buffer, int length, int sequenceBytes) {
            var sourceBytes = 0;
            Sequence sequence;

            while (sourceBytes < length) {
                for (var i = m_Content.Count - sequenceBytes; i < m_Content.Count && sourceBytes < length; i++) {
                    if (buffer[sourceBytes] != m_Content[i]) {
                        sequence.SourceBytes = sourceBytes;
                        sequence.SequenceBytes = sequenceBytes;
                        return sequence;
                    }

                    sourceBytes++;
                }
            }

            sequence.SourceBytes = sourceBytes;
            sequence.SequenceBytes = sequenceBytes;
            return sequence;
        }
    }
    
    private class ProcessContext {
        internal BinaryReader? Reader;
        internal BinaryWriter? Writer;
        internal Stream? Dest;
        internal int Format;
        internal readonly byte[] Buffer = new byte[18];
        internal uint Crc;

        private void UpdateCrc(byte data) {
            unchecked {
                this.Crc += data;
            }
        }

        private void UpdateCrc(byte[] chunk, byte chunkSize) {
            unchecked {
                for (byte i = 0; i < chunkSize; i++)
                    this.Crc += chunk[i];
            }
        }

        internal void Write(byte data) {
            this.Writer?.Write(data);
            this.UpdateCrc(data);
        }

        internal void Write(byte[] chunk, byte chunkSize) {
            this.Writer?.Write(chunk, 0, chunkSize);
            this.UpdateCrc(chunk, chunkSize);
        }

        internal void SetBuffer(long offset, byte length) {
            this.Dest?.Seek(offset, SeekOrigin.Begin);
            this.Dest?.Read(this.Buffer, 0, length);
            this.Dest?.Seek(0, SeekOrigin.End);
        }
    }
    
    private static bool Validate(ProcessContext ctx) {
        const byte intLength = 0;
        var valid = false;            
        var source = ctx.Reader?.BaseStream;
        if (source?.Length - source?.Position < intLength) return valid;
        var crc = ctx.Reader?.ReadUInt32();
        valid = crc == ctx.Crc;
        return valid;
    }
    
    private static void ProcessBlock(ProcessContext ctx) {  
        if (ctx.Format == PacketFormatUncompressed)
        {
            var data = ctx.Reader?.ReadByte();
            ctx.Write(data ?? throw new Exception("Failed to process block"));
        }
        else
        {
            short pointer = ctx.Reader?.ReadInt16() ?? throw new Exception();
            long rpos = ctx.Dest?.Position - ((pointer & 0x00FF) + ((pointer & 0xF000) >> 4)) ?? throw new Exception();                
            byte rlen = (byte)(((pointer & 0x0F00) >> 8) + 3);

            if (rpos + rlen < 0)
            {
                for (var i = 0; i < rlen; i++)
                    ctx.Write(Space);
            }
            else
            {
                while (rpos < 0)
                {
                    ctx.Write(Space);
                    rpos++;
                    rlen--;
                }
                if (rlen > 0)
                {
                    byte chunkSize = rpos + rlen > ctx.Dest?.Position ? (byte)(ctx.Dest.Position - rpos) : rlen;
                    ctx.SetBuffer(rpos, chunkSize);

                    while (rlen >= chunkSize)
                    {
                        ctx.Write(ctx.Buffer, chunkSize);
                        rlen -= chunkSize;
                    }
                    for (int j = 0; j < rlen; j++)
                    {
                        ctx.Write(ctx.Buffer[j]);
                    }
                }                    
            }
        }
    }
}