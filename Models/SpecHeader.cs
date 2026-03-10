using System.Text;

namespace Lab1_4Sem.Models;

public struct SpecHeader
{
    public int HeadPtr;    // указатель на первую запись списка спецификаций (4 байта)
    public int FreePtr;    // указатель на свободную область (4 байта)

    public const int HeaderSize = sizeof(int) + sizeof(int);

    public byte[] ToBytes()
    {
        var bytes = new List<byte>();
        bytes.AddRange(BitConverter.GetBytes(HeadPtr));
        bytes.AddRange(BitConverter.GetBytes(FreePtr));
        return bytes.ToArray();
    }

    public static SpecHeader FromBytes(byte[] bytes, int offset = 0)
    {
        var header = new SpecHeader();
        header.HeadPtr = BitConverter.ToInt32(bytes, offset);
        offset += sizeof(int);
        header.FreePtr = BitConverter.ToInt32(bytes, offset);
        return header;
    }
}