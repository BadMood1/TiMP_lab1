using System.Text;

namespace Lab1_4Sem.Models;

public struct SpecRecord
{
    public sbyte IsDeleted;    // бит удаления (1 байт) - 0 активна, -1 удалена
    public int ProductPtr;      // указатель на запись в файле изделий (4 байта)
    public short Quantity;      // кратность вхождения (2 байта)
    public int NextPtr;         // указатель на следующую запись в спецификации (4 байта)

    public const int IsDeletedSize = sizeof(sbyte);
    public const int ProductPtrSize = sizeof(int);
    public const int QuantitySize = sizeof(short);
    public const int NextPtrSize = sizeof(int);
    public const int RecordSize = IsDeletedSize + ProductPtrSize + QuantitySize + NextPtrSize;

    public byte[] ToBytes()
    {
        var bytes = new List<byte>();

        // Бит удаления
        bytes.Add((byte)IsDeleted);

        // Указатель на изделие
        bytes.AddRange(BitConverter.GetBytes(ProductPtr));

        // Кратность
        bytes.AddRange(BitConverter.GetBytes(Quantity));

        // Указатель на следующую запись
        bytes.AddRange(BitConverter.GetBytes(NextPtr));

        return bytes.ToArray();
    }

    public static SpecRecord FromBytes(byte[] bytes, int offset = 0)
    {
        var record = new SpecRecord();

        record.IsDeleted = (sbyte)bytes[offset];
        offset += IsDeletedSize;

        record.ProductPtr = BitConverter.ToInt32(bytes, offset);
        offset += ProductPtrSize;

        record.Quantity = BitConverter.ToInt16(bytes, offset);
        offset += QuantitySize;

        record.NextPtr = BitConverter.ToInt32(bytes, offset);

        return record;
    }
}