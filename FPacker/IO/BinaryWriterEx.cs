using System.Text;

namespace FPacker.IO; 

public static class BinaryWriterEx {
    public static void WriteAsciiZ(this BinaryWriter writer, string str) {
        var content = Encoding.UTF8.GetBytes(str + "\0");
        writer.Write(content, 0, content.Length);
    }
}