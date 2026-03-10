using System.Text;

namespace Lab1_4Sem.Models;

public struct FileHeader
{
    public string Signature;      // "PS" (2 байта)
    public short RecordSize;      // длина записи данных (2 байта)
    public int HeadPtr;           // указатель на первую запись (4 байта)
    public int FreePtr;           // указатель на свободную область (4 байта)
    public string SpecFileName;   // имя файла спецификаций (16 байт)

    public const int SignatureSize = 2;
    public const int SpecFileNameSize = 16;
    public const int HeaderSize = SignatureSize + sizeof(short) + sizeof(int) + sizeof(int) + SpecFileNameSize;

    public byte[] ToBytes()
    {
        var bytes = new List<byte>();

        // Сигнатура "PS" (ASCII)
        bytes.AddRange(Encoding.ASCII.GetBytes(Signature.PadRight(SignatureSize, ' ')));

        // Длина записи
        bytes.AddRange(BitConverter.GetBytes(RecordSize));

        // Указатель на первую запись
        bytes.AddRange(BitConverter.GetBytes(HeadPtr));

        // Указатель на свободную область
        bytes.AddRange(BitConverter.GetBytes(FreePtr));

        // Имя файла спецификаций (фиксированная длина)
        var specNameBytes = Encoding.ASCII.GetBytes(SpecFileName.PadRight(SpecFileNameSize, ' '));
        bytes.AddRange(specNameBytes);

        return bytes.ToArray();
    }

    public static FileHeader FromBytes(byte[] bytes, int offset = 0)
    {
        var header = new FileHeader();

        // Сигнатура
        header.Signature = Encoding.ASCII.GetString(bytes, offset, SignatureSize).Trim();
        offset += SignatureSize;

        // Длина записи
        header.RecordSize = BitConverter.ToInt16(bytes, offset);
        offset += sizeof(short);

        // Указатель на первую запись
        header.HeadPtr = BitConverter.ToInt32(bytes, offset);
        offset += sizeof(int);

        // Указатель на свободную область
        header.FreePtr = BitConverter.ToInt32(bytes, offset);
        offset += sizeof(int);

        // Имя файла спецификаций
        header.SpecFileName = Encoding.ASCII.GetString(bytes, offset, SpecFileNameSize).Trim();

        return header;
    }
}