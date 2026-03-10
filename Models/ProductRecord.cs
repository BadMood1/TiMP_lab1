using System.Text;

namespace Lab1_4Sem.Models;

public struct ProductRecord
{
    public sbyte IsDeleted;   // бит удаления (1 байт) - 0 активна, -1 удалена
    public int SpecPtr;       // указатель на запись в файле спецификаций (4 байта)
    public int NextPtr;       // указатель на следующую запись в списке (4 байта)
    public string Name;       // имя компонента (фиксированная длина)

    public const int IsDeletedSize = sizeof(sbyte);
    public const int SpecPtrSize = sizeof(int);
    public const int NextPtrSize = sizeof(int);

    public static int GetRecordSize(int nameMaxLength)
    {
        return IsDeletedSize + SpecPtrSize + NextPtrSize + nameMaxLength;
    }

    public byte[] ToBytes(int nameMaxLength)
    {
        var bytes = new List<byte>();

        // Бит удаления
        bytes.Add((byte)IsDeleted);

        // Указатель на спецификацию
        bytes.AddRange(BitConverter.GetBytes(SpecPtr));

        // Указатель на следующую запись
        bytes.AddRange(BitConverter.GetBytes(NextPtr));

        // Имя (фиксированная длина, UTF-8, с нулевым добивом)
        var nameBytes = Encoding.UTF8.GetBytes(Name ?? string.Empty);
        if (nameBytes.Length > nameMaxLength)
            throw new ArgumentException($"Имя превышает допустимый размер в байтах ({nameMaxLength}).");

        bytes.AddRange(nameBytes);
        if (nameBytes.Length < nameMaxLength)
            bytes.AddRange(new byte[nameMaxLength - nameBytes.Length]);

        return bytes.ToArray();
    }

    public static ProductRecord FromBytes(byte[] bytes, int nameMaxLength, int offset = 0)
    {
        var record = new ProductRecord();

        // Бит удаления
        record.IsDeleted = (sbyte)bytes[offset];
        offset += IsDeletedSize;

        // Указатель на спецификацию
        record.SpecPtr = BitConverter.ToInt32(bytes, offset);
        offset += SpecPtrSize;

        // Указатель на следующую запись
        record.NextPtr = BitConverter.ToInt32(bytes, offset);
        offset += NextPtrSize;

        // Имя: читаем фиксированный блок, отрезаем нулевой добив и пробелы.
        var nameRaw = new byte[nameMaxLength];
        Buffer.BlockCopy(bytes, offset, nameRaw, 0, nameMaxLength);
        var end = nameRaw.Length;
        while (end > 0 && (nameRaw[end - 1] == 0 || nameRaw[end - 1] == (byte)' '))
            end--;
        record.Name = end == 0 ? string.Empty : Encoding.UTF8.GetString(nameRaw, 0, end);

        return record;
    }
}
