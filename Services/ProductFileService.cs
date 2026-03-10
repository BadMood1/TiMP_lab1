using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lab1_4Sem.Models;

namespace Lab1_4Sem.Services;

public class ProductFileService
{
    private string _productFilePath = "";
    private string _specFilePath = "";
    private FileHeader _productHeader;
    private SpecHeader _specHeader;
    private int _nameMaxLength;
    private bool _isOpen;
    private string _typesFilePath = "";

    private readonly Dictionary<string, int> _nameToPtr = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComponentType> _componentTypes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _componentSpecPtr = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ComponentType> _manualTypes = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions TypeJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ProductFilePath => _productFilePath;
    public string SpecFilePath => _specFilePath;
    public bool IsOpen => _isOpen;
    public bool LastOperationSucceeded { get; private set; }
    public string LastOperationMessage { get; private set; } = "";

    private void EnsureOpen()
    {
        if (!_isOpen)
            throw new InvalidOperationException("Файл не открыт. Сначала выполните Open или Create.");
    }

    private void SetResult(bool ok, string message, bool toConsole = true)
    {
        LastOperationSucceeded = ok;
        LastOperationMessage = message;
        if (toConsole && !string.IsNullOrWhiteSpace(message))
            Console.WriteLine(message);
    }

    private static string ProductPath(string fileName)
    {
        var p = fileName.EndsWith(".prd", StringComparison.OrdinalIgnoreCase) ? fileName : fileName + ".prd";
        return Path.GetFullPath(p);
    }

    private static string SpecPath(string productPath, string? specFileName)
    {
        var s = string.IsNullOrWhiteSpace(specFileName) ? Path.GetFileNameWithoutExtension(productPath) + ".prs" : specFileName!;
        if (!s.EndsWith(".prs", StringComparison.OrdinalIgnoreCase))
            s += ".prs";
        if (Path.IsPathRooted(s))
            return s;
        var dir = Path.GetDirectoryName(productPath) ?? "";
        return Path.GetFullPath(Path.Combine(dir, s));
    }

    private static string TypesPath(string productPath)
    {
        return Path.ChangeExtension(productPath, ".types.json");
    }

    private void LoadTypeMetadata()
    {
        _manualTypes.Clear();
        if (string.IsNullOrWhiteSpace(_typesFilePath) || !File.Exists(_typesFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_typesFilePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, ComponentType>>(json, TypeJsonOptions);
            if (data == null)
                return;

            foreach (var pair in data)
                _manualTypes[pair.Key] = pair.Value;
        }
        catch
        {
            // Игнорируем битые/старые метаданные типов.
        }
    }

    private void SaveTypeMetadata()
    {
        if (string.IsNullOrWhiteSpace(_typesFilePath))
            return;

        try
        {
            var json = JsonSerializer.Serialize(_manualTypes, TypeJsonOptions);
            File.WriteAllText(_typesFilePath, json);
        }
        catch
        {
            // Ошибка сохранения метаданных не должна ломать основные операции с файлами.
        }
    }

    private void PreserveCurrentTypesAsManual()
    {
        var changed = false;
        foreach (var pair in _componentTypes)
        {
            if (_nameToPtr.ContainsKey(pair.Key) && !_manualTypes.ContainsKey(pair.Key))
            {
                _manualTypes[pair.Key] = pair.Value;
                changed = true;
            }
        }

        if (changed)
            SaveTypeMetadata();
    }

    private bool ValidateName(string name, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "Имя не может быть пустым.";
            return false;
        }

        if (name.Any(char.IsControl))
        {
            error = "Имя не должно содержать управляющие символы.";
            return false;
        }

        if (name.Contains('/') || name.Contains('\\'))
        {
            error = "Имя не должно содержать символы '/' и '\\'.";
            return false;
        }

        var utf8Length = Encoding.UTF8.GetByteCount(name);
        if (utf8Length > _nameMaxLength)
        {
            error = $"Слишком длинное имя: {utf8Length} байт. Допустимо: {_nameMaxLength} байт.";
            return false;
        }
        return true;
    }

    private ProductRecord ReadProduct(FileStream fs, int pos)
    {
        fs.Seek(pos, SeekOrigin.Begin);
        var b = new byte[_productHeader.RecordSize];
        fs.Read(b, 0, b.Length);
        return ProductRecord.FromBytes(b, _nameMaxLength);
    }

    private List<(int Pos, ProductRecord Rec)> ReadAllProductRecordsLinear(FileStream fs)
    {
        var list = new List<(int Pos, ProductRecord Rec)>();
        var pos = FileHeader.HeaderSize;
        while (pos + _productHeader.RecordSize <= fs.Length)
        {
            var rec = ReadProduct(fs, pos);
            list.Add((pos, rec));
            pos += _productHeader.RecordSize;
        }
        return list;
    }

    private void WriteProduct(FileStream fs, int pos, ProductRecord r)
    {
        fs.Seek(pos, SeekOrigin.Begin);
        fs.Write(r.ToBytes(_nameMaxLength), 0, _productHeader.RecordSize);
    }

    private SpecRecord ReadSpec(FileStream fs, int pos)
    {
        fs.Seek(pos, SeekOrigin.Begin);
        var b = new byte[SpecRecord.RecordSize];
        fs.Read(b, 0, b.Length);
        return SpecRecord.FromBytes(b);
    }

    private void WriteSpec(FileStream fs, int pos, SpecRecord r)
    {
        fs.Seek(pos, SeekOrigin.Begin);
        fs.Write(r.ToBytes(), 0, SpecRecord.RecordSize);
    }

    private void SaveProductHeader(FileStream fs)
    {
        fs.Seek(0, SeekOrigin.Begin);
        fs.Write(_productHeader.ToBytes(), 0, FileHeader.HeaderSize);
    }

    private void SaveSpecHeader(FileStream fs)
    {
        fs.Seek(0, SeekOrigin.Begin);
        fs.Write(_specHeader.ToBytes(), 0, SpecHeader.HeaderSize);
    }

    private IEnumerable<(int Pos, ProductRecord Rec)> ActiveProducts(FileStream fs)
    {
        var seen = new HashSet<int>();
        var cur = _productHeader.HeadPtr;
        while (cur != -1 && seen.Add(cur))
        {
            if (cur < FileHeader.HeaderSize || cur + _productHeader.RecordSize > fs.Length)
                yield break;
            var r = ReadProduct(fs, cur);
            if (r.IsDeleted == 0)
                yield return (cur, r);
            cur = r.NextPtr;
        }
    }

    private IEnumerable<(int Pos, SpecRecord Rec)> SpecChain(FileStream fs, int head)
    {
        var seen = new HashSet<int>();
        var cur = head;
        while (cur != -1 && seen.Add(cur))
        {
            if (cur < SpecHeader.HeaderSize || cur + SpecRecord.RecordSize > fs.Length)
                yield break;
            var r = ReadSpec(fs, cur);
            yield return (cur, r);
            cur = r.NextPtr;
        }
    }

    private int AllocProduct(FileStream fs)
    {
        if (_productHeader.FreePtr == -1)
        {
            fs.Seek(0, SeekOrigin.End);
            return (int)fs.Position;
        }
        var pos = _productHeader.FreePtr;
        if (pos < FileHeader.HeaderSize || pos + _productHeader.RecordSize > fs.Length)
        {
            _productHeader.FreePtr = -1;
            fs.Seek(0, SeekOrigin.End);
            return (int)fs.Position;
        }
        var free = ReadProduct(fs, pos);
        _productHeader.FreePtr = free.NextPtr;
        return pos;
    }

    private int AllocSpec(FileStream fs)
    {
        if (_specHeader.FreePtr == -1)
        {
            fs.Seek(0, SeekOrigin.End);
            return (int)fs.Position;
        }
        var pos = _specHeader.FreePtr;
        if (pos < SpecHeader.HeaderSize || pos + SpecRecord.RecordSize > fs.Length)
        {
            _specHeader.FreePtr = -1;
            fs.Seek(0, SeekOrigin.End);
            return (int)fs.Position;
        }
        var free = ReadSpec(fs, pos);
        _specHeader.FreePtr = free.NextPtr;
        return pos;
    }

    private void InsertSorted(FileStream fs, int pos, ref ProductRecord rec)
    {
        var prev = -1;
        var cur = _productHeader.HeadPtr;
        var seen = new HashSet<int>();
        while (cur != -1 && seen.Add(cur))
        {
            var c = ReadProduct(fs, cur);
            if (string.Compare(rec.Name, c.Name, StringComparison.Ordinal) < 0)
                break;
            prev = cur;
            cur = c.NextPtr;
        }
        rec.NextPtr = cur;
        if (prev == -1)
        {
            _productHeader.HeadPtr = pos;
        }
        else
        {
            var p = ReadProduct(fs, prev);
            p.NextPtr = pos;
            WriteProduct(fs, prev, p);
        }
    }

    private void RebuildSortedActive(FileStream fs)
    {
        var list = ActiveProducts(fs).ToList();
        list.Sort((a, b) => string.Compare(a.Rec.Name, b.Rec.Name, StringComparison.Ordinal));
        for (var i = 0; i < list.Count; i++)
        {
            var r = list[i].Rec;
            r.NextPtr = i + 1 < list.Count ? list[i + 1].Pos : -1;
            WriteProduct(fs, list[i].Pos, r);
        }
        _productHeader.HeadPtr = list.Count > 0 ? list[0].Pos : -1;
    }

    private void RepairProductListsFromLinearScan()
    {
        using var fs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var records = ReadAllProductRecordsLinear(fs);
        if (records.Count == 0)
        {
            _productHeader.HeadPtr = -1;
            _productHeader.FreePtr = -1;
            SaveProductHeader(fs);
            return;
        }

        var active = records.Where(r => r.Rec.IsDeleted == 0).ToList();
        var deleted = records.Where(r => r.Rec.IsDeleted == -1).ToList();

        active.Sort((a, b) => string.Compare(a.Rec.Name, b.Rec.Name, StringComparison.Ordinal));

        for (var i = 0; i < active.Count; i++)
        {
            var rec = active[i].Rec;
            rec.NextPtr = i + 1 < active.Count ? active[i + 1].Pos : -1;
            WriteProduct(fs, active[i].Pos, rec);
        }

        for (var i = 0; i < deleted.Count; i++)
        {
            var rec = deleted[i].Rec;
            rec.NextPtr = i + 1 < deleted.Count ? deleted[i + 1].Pos : -1;
            WriteProduct(fs, deleted[i].Pos, rec);
        }

        _productHeader.HeadPtr = active.Count > 0 ? active[0].Pos : -1;
        _productHeader.FreePtr = deleted.Count > 0 ? deleted[0].Pos : -1;
        SaveProductHeader(fs);
    }

    private void RecomputeSpecHead(FileStream productFs)
    {
        _specHeader.HeadPtr = -1;
        foreach (var (_, p) in ActiveProducts(productFs))
        {
            if (p.SpecPtr != -1)
            {
                _specHeader.HeadPtr = p.SpecPtr;
                break;
            }
        }
    }

    private bool HasDeletedWithSameName(string name)
    {
        using var fs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read);
        var cur = _productHeader.FreePtr;
        var seen = new HashSet<int>();
        while (cur != -1 && seen.Add(cur))
        {
            if (cur < FileHeader.HeaderSize || cur + _productHeader.RecordSize > fs.Length)
                break;
            var r = ReadProduct(fs, cur);
            if (r.IsDeleted == -1 && string.Equals(r.Name, name, StringComparison.Ordinal))
                return true;
            cur = r.NextPtr;
        }
        return false;
    }

    private bool TryResolveProduct(string name, out int pos)
    {
        pos = -1;
        if (_nameToPtr.TryGetValue(name, out var ptr))
        {
            pos = ptr;
            return true;
        }

        LoadNameCache();
        if (_nameToPtr.TryGetValue(name, out ptr))
        {
            pos = ptr;
            return true;
        }

        RepairProductListsFromLinearScan();
        LoadNameCache();
        if (_nameToPtr.TryGetValue(name, out ptr))
        {
            pos = ptr;
            return true;
        }

        return false;
    }

    private bool WouldCreateCycle(string componentName, string partName)
    {
        if (string.Equals(componentName, partName, StringComparison.Ordinal))
            return true;

        using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.Read);
        using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read);

        var stack = new Stack<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        stack.Push(partName);

        while (stack.Count > 0)
        {
            var curName = stack.Pop();
            if (!seen.Add(curName))
                continue;
            if (!_componentSpecPtr.TryGetValue(curName, out var head) || head == -1)
                continue;

            foreach (var (_, s) in SpecChain(specFs, head))
            {
                if (s.IsDeleted != 0)
                    continue;
                if (s.ProductPtr < FileHeader.HeaderSize || s.ProductPtr + _productHeader.RecordSize > productFs.Length)
                    continue;
                var p = ReadProduct(productFs, s.ProductPtr);
                if (p.IsDeleted != 0)
                    continue;
                if (string.Equals(p.Name, componentName, StringComparison.Ordinal))
                    return true;
                stack.Push(p.Name);
            }
        }
        return false;
    }

    private void DeleteSpecChain(FileStream specFs, int head)
    {
        foreach (var (pos, s0) in SpecChain(specFs, head).ToList())
        {
            var s = s0;
            s.IsDeleted = -1;
            s.NextPtr = _specHeader.FreePtr;
            WriteSpec(specFs, pos, s);
            _specHeader.FreePtr = pos;
        }
    }

    public void Create(string fileName, int nameMaxLength, string specFileName = null)
    {
        try
        {
            if (nameMaxLength <= 0)
            {
                SetResult(false, "Ошибка: длина имени должна быть положительной.");
                return;
            }
            _productFilePath = ProductPath(fileName);
            _specFilePath = SpecPath(_productFilePath, specFileName);
            _typesFilePath = TypesPath(_productFilePath);
            _nameMaxLength = nameMaxLength;

            _productHeader = new FileHeader
            {
                Signature = "PS",
                RecordSize = (short)ProductRecord.GetRecordSize(_nameMaxLength),
                HeadPtr = -1,
                FreePtr = -1,
                SpecFileName = Path.GetFileName(_specFilePath)
            };
            _specHeader = new SpecHeader { HeadPtr = -1, FreePtr = -1 };

            using (var p = new FileStream(_productFilePath, FileMode.Create, FileAccess.Write))
                p.Write(_productHeader.ToBytes(), 0, FileHeader.HeaderSize);
            using (var s = new FileStream(_specFilePath, FileMode.Create, FileAccess.Write))
                s.Write(_specHeader.ToBytes(), 0, SpecHeader.HeaderSize);

            _isOpen = true;
            _nameToPtr.Clear();
            _componentTypes.Clear();
            _componentSpecPtr.Clear();
            _manualTypes.Clear();
            SaveTypeMetadata();
            SetResult(true, $"Файлы созданы: {_productFilePath}, {_specFilePath}");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при создании файлов: {ex.Message}");
        }
    }

    public void Open(string fileName)
    {
        try
        {
            _productFilePath = ProductPath(fileName);
            _typesFilePath = TypesPath(_productFilePath);
            if (!File.Exists(_productFilePath))
            {
                SetResult(false, "Файл не существует.");
                return;
            }

            using (var p = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read))
            {
                var b = new byte[FileHeader.HeaderSize];
                p.Read(b, 0, b.Length);
                _productHeader = FileHeader.FromBytes(b);
            }
            if (_productHeader.Signature != "PS")
            {
                SetResult(false, "Ошибка: неверная сигнатура файла.");
                return;
            }

            _nameMaxLength = _productHeader.RecordSize - (ProductRecord.IsDeletedSize + ProductRecord.SpecPtrSize + ProductRecord.NextPtrSize);
            if (_nameMaxLength <= 0)
            {
                SetResult(false, "Ошибка: некорректный размер записи.");
                return;
            }

            var specCandidate = _productHeader.SpecFileName;
            if (!Path.IsPathRooted(specCandidate))
            {
                var dir = Path.GetDirectoryName(_productFilePath) ?? "";
                specCandidate = Path.Combine(dir, specCandidate);
            }
            _specFilePath = Path.GetFullPath(specCandidate);

            if (!File.Exists(_specFilePath))
            {
                _specHeader = new SpecHeader { HeadPtr = -1, FreePtr = -1 };
                using var s = new FileStream(_specFilePath, FileMode.Create, FileAccess.Write);
                s.Write(_specHeader.ToBytes(), 0, SpecHeader.HeaderSize);
            }
            else
            {
                using var s = new FileStream(_specFilePath, FileMode.Open, FileAccess.Read);
                var b = new byte[SpecHeader.HeaderSize];
                s.Read(b, 0, b.Length);
                _specHeader = SpecHeader.FromBytes(b);
            }

            _isOpen = true;
            RepairProductListsFromLinearScan();
            LoadTypeMetadata();
            LoadNameCache();
            SetResult(true, $"Файлы открыты: {_productFilePath}, {_specFilePath}");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при открытии файлов: {ex.Message}");
        }
    }

    private void LoadNameCache()
    {
        try
        {
            var nameToPtr = new Dictionary<string, int>(StringComparer.Ordinal);
            var componentTypes = new Dictionary<string, ComponentType>(StringComparer.Ordinal);
            var componentSpecPtr = new Dictionary<string, int>(StringComparer.Ordinal);

            using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            var products = ActiveProducts(productFs).ToList();
            foreach (var (pos, p) in products)
            {
                nameToPtr[p.Name] = pos;
                componentSpecPtr[p.Name] = p.SpecPtr;
            }

            var referenced = new HashSet<int>();
            foreach (var (_, p) in products)
            {
                if (p.SpecPtr == -1)
                    continue;
                foreach (var (_, s) in SpecChain(specFs, p.SpecPtr))
                {
                    if (s.IsDeleted == 0)
                        referenced.Add(s.ProductPtr);
                }
            }

            foreach (var (pos, p) in products)
            {
                if (_manualTypes.TryGetValue(p.Name, out var manual))
                {
                    componentTypes[p.Name] = manual;
                }
                else if (p.SpecPtr == -1)
                {
                    componentTypes[p.Name] = ComponentType.Деталь;
                }
                else
                {
                    componentTypes[p.Name] = referenced.Contains(pos) ? ComponentType.Узел : ComponentType.Изделие;
                }
            }

            _nameToPtr.Clear();
            foreach (var pair in nameToPtr)
                _nameToPtr[pair.Key] = pair.Value;

            _componentTypes.Clear();
            foreach (var pair in componentTypes)
                _componentTypes[pair.Key] = pair.Value;

            _componentSpecPtr.Clear();
            foreach (var pair in componentSpecPtr)
                _componentSpecPtr[pair.Key] = pair.Value;

        }
        catch
        {
        }
    }

    public void AddComponent(string name, ComponentType type)
    {
        EnsureOpen();
        try
        {
            PreserveCurrentTypesAsManual();

            if (!ValidateName(name, out var error))
            {
                SetResult(false, $"Ошибка: {error}");
                return;
            }
            if (_nameToPtr.ContainsKey(name))
            {
                SetResult(false, $"Ошибка: компонент '{name}' уже существует.");
                return;
            }
            if (HasDeletedWithSameName(name))
            {
                SetResult(false, $"Ошибка: компонент '{name}' есть в удаленных. Используйте Restore.");
                return;
            }

            using var fs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            var pos = AllocProduct(fs);
            var rec = new ProductRecord { IsDeleted = 0, SpecPtr = -1, NextPtr = -1, Name = name };
            InsertSorted(fs, pos, ref rec);
            WriteProduct(fs, pos, rec);
            SaveProductHeader(fs);
            fs.Flush();

            _manualTypes[name] = type;
            SaveTypeMetadata();
            LoadNameCache();
            _componentTypes[name] = type;
            SetResult(true, $"Компонент '{name}' ({type}) добавлен.");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при добавлении компонента: {ex.Message}");
        }
    }

    public void AddToSpecification(string componentName, string partName, short quantity = 1)
    {
        EnsureOpen();
        try
        {
            PreserveCurrentTypesAsManual();

            if (quantity <= 0)
            {
                SetResult(false, "Ошибка: кратность должна быть положительной.");
                return;
            }
            if (!TryResolveProduct(componentName, out var componentPos))
            {
                SetResult(false, $"Ошибка: компонент '{componentName}' не найден.");
                return;
            }
            if (!TryResolveProduct(partName, out var partPos))
            {
                SetResult(false, $"Ошибка: компонент '{partName}' не найден.");
                return;
            }
            if (_componentTypes.TryGetValue(componentName, out var type) && type == ComponentType.Деталь)
            {
                SetResult(false, $"Ошибка: деталь '{componentName}' не может иметь спецификацию.");
                return;
            }
            if (WouldCreateCycle(componentName, partName))
            {
                SetResult(false, "Ошибка: операция приведет к циклической спецификации.");
                return;
            }

            using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            var componentRecord = ReadProduct(productFs, componentPos);

            var tail = -1;
            if (componentRecord.SpecPtr != -1)
            {
                foreach (var (pos, s) in SpecChain(specFs, componentRecord.SpecPtr))
                {
                    if (s.IsDeleted == 0 && s.ProductPtr == partPos)
                    {
                        SetResult(false, $"Компонент '{partName}' уже есть в спецификации '{componentName}'.");
                        return;
                    }
                    tail = pos;
                }
            }

            var newSpecPos = AllocSpec(specFs);
            var newSpec = new SpecRecord
            {
                IsDeleted = 0,
                ProductPtr = partPos,
                Quantity = quantity,
                NextPtr = -1
            };
            WriteSpec(specFs, newSpecPos, newSpec);

            if (componentRecord.SpecPtr == -1)
            {
                componentRecord.SpecPtr = newSpecPos;
                WriteProduct(productFs, componentPos, componentRecord);
            }
            else if (tail != -1)
            {
                var t = ReadSpec(specFs, tail);
                t.NextPtr = newSpecPos;
                WriteSpec(specFs, tail, t);
            }

            RecomputeSpecHead(productFs);
            SaveSpecHeader(specFs);
            productFs.Flush();
            specFs.Flush();
            LoadNameCache();
            SetResult(true, $"Компонент '{partName}' добавлен в спецификацию '{componentName}' (x{quantity}).");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при добавлении в спецификацию: {ex.Message}");
        }
    }

    public void DeleteComponent(string name)
    {
        EnsureOpen();
        try
        {
            PreserveCurrentTypesAsManual();

            if (!_nameToPtr.ContainsKey(name))
            {
                SetResult(false, $"Ошибка: компонент '{name}' не найден.");
                return;
            }
            if (HasReferences(name))
            {
                SetResult(false, $"Ошибка: на компонент '{name}' есть ссылки в спецификациях. Удаление невозможно.");
                return;
            }

            using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            var cur = _productHeader.HeadPtr;
            var prev = -1;
            ProductRecord rec = default;
            var found = false;

            while (cur != -1)
            {
                rec = ReadProduct(productFs, cur);
                if (rec.IsDeleted == 0 && string.Equals(rec.Name, name, StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
                prev = cur;
                cur = rec.NextPtr;
            }

            if (!found)
            {
                SetResult(false, $"Ошибка: компонент '{name}' не найден в активном списке.");
                return;
            }

            if (prev == -1)
            {
                _productHeader.HeadPtr = rec.NextPtr;
            }
            else
            {
                var p = ReadProduct(productFs, prev);
                p.NextPtr = rec.NextPtr;
                WriteProduct(productFs, prev, p);
            }

            if (rec.SpecPtr != -1)
                DeleteSpecChain(specFs, rec.SpecPtr);

            rec.IsDeleted = -1;
            rec.SpecPtr = -1;
            rec.NextPtr = _productHeader.FreePtr;
            WriteProduct(productFs, cur, rec);
            _productHeader.FreePtr = cur;

            RecomputeSpecHead(productFs);
            SaveProductHeader(productFs);
            SaveSpecHeader(specFs);
            productFs.Flush();
            specFs.Flush();

            LoadNameCache();
            SetResult(true, $"Компонент '{name}' удален.");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при удалении компонента: {ex.Message}");
        }
    }

    public void DeleteFromSpecification(string componentName, string partName)
    {
        EnsureOpen();
        try
        {
            PreserveCurrentTypesAsManual();

            if (!TryResolveProduct(componentName, out var componentPos))
            {
                SetResult(false, $"Ошибка: компонент '{componentName}' не найден.");
                return;
            }
            if (!TryResolveProduct(partName, out var partPos))
            {
                SetResult(false, $"Ошибка: компонент '{partName}' не найден.");
                return;
            }
            if (_componentTypes.TryGetValue(componentName, out var type) && type == ComponentType.Деталь)
            {
                SetResult(false, $"Ошибка: деталь '{componentName}' не может иметь спецификацию.");
                return;
            }

            using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

            var componentRec = ReadProduct(productFs, componentPos);
            if (componentRec.SpecPtr == -1)
            {
                SetResult(false, $"У компонента '{componentName}' нет спецификации.");
                return;
            }

            var prev = -1;
            var target = -1;
            SpecRecord targetRec = default;

            foreach (var (pos, s) in SpecChain(specFs, componentRec.SpecPtr))
            {
                if (s.IsDeleted == 0 && s.ProductPtr == partPos)
                {
                    target = pos;
                    targetRec = s;
                    break;
                }
                prev = pos;
            }

            if (target == -1)
            {
                SetResult(false, $"Компонент '{partName}' не найден в спецификации '{componentName}'.");
                return;
            }

            if (prev == -1)
            {
                componentRec.SpecPtr = targetRec.NextPtr;
                WriteProduct(productFs, componentPos, componentRec);
            }
            else
            {
                var p = ReadSpec(specFs, prev);
                p.NextPtr = targetRec.NextPtr;
                WriteSpec(specFs, prev, p);
            }

            targetRec.IsDeleted = -1;
            targetRec.NextPtr = _specHeader.FreePtr;
            WriteSpec(specFs, target, targetRec);
            _specHeader.FreePtr = target;

            RecomputeSpecHead(productFs);
            SaveSpecHeader(specFs);
            productFs.Flush();
            specFs.Flush();
            LoadNameCache();
            SetResult(true, $"Компонент '{partName}' удален из спецификации '{componentName}'.");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при удалении из спецификации: {ex.Message}");
        }
    }

    private bool HasReferences(string name)
    {
        try
        {
            if (!_nameToPtr.TryGetValue(name, out var ptr))
                return false;

            using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read);
            using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.Read);

            foreach (var (_, p) in ActiveProducts(productFs))
            {
                if (p.SpecPtr == -1)
                    continue;
                foreach (var (_, s) in SpecChain(specFs, p.SpecPtr))
                {
                    if (s.IsDeleted == 0 && s.ProductPtr == ptr)
                        return true;
                }
            }
        }
        catch
        {
        }
        return false;
    }

    public void Restore(string name)
    {
        EnsureOpen();
        try
        {
            PreserveCurrentTypesAsManual();

            using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            if (_productHeader.FreePtr == -1)
            {
                SetResult(false, "Удаленных компонентов нет.");
                return;
            }

            if (name == "*")
            {
                var free = new List<(int Pos, ProductRecord Rec)>();
                var cur = _productHeader.FreePtr;
                var seen = new HashSet<int>();
                while (cur != -1 && seen.Add(cur))
                {
                    var r = ReadProduct(productFs, cur);
                    free.Add((cur, r));
                    cur = r.NextPtr;
                }

                _productHeader.FreePtr = -1;
                var restored = 0;
                foreach (var (pos, r0) in free)
                {
                    var r = r0;
                    if (r.IsDeleted != -1)
                        continue;
                    r.IsDeleted = 0;
                    r.NextPtr = -1;
                    InsertSorted(productFs, pos, ref r);
                    WriteProduct(productFs, pos, r);
                    restored++;
                }
                SaveProductHeader(productFs);
                productFs.Flush();
                LoadNameCache();
                SetResult(true, $"Восстановлено компонентов: {restored}.");
                return;
            }

            var prevFree = -1;
            var currentFree = _productHeader.FreePtr;
            var freeSeen = new HashSet<int>();

            while (currentFree != -1 && freeSeen.Add(currentFree))
            {
                var r = ReadProduct(productFs, currentFree);
                var next = r.NextPtr;

                if (r.IsDeleted == -1 && string.Equals(r.Name, name, StringComparison.Ordinal))
                {
                    if (prevFree == -1)
                    {
                        _productHeader.FreePtr = next;
                    }
                    else
                    {
                        var p = ReadProduct(productFs, prevFree);
                        p.NextPtr = next;
                        WriteProduct(productFs, prevFree, p);
                    }

                    r.IsDeleted = 0;
                    r.NextPtr = -1;
                    InsertSorted(productFs, currentFree, ref r);
                    WriteProduct(productFs, currentFree, r);
                    SaveProductHeader(productFs);
                    productFs.Flush();
                    LoadNameCache();
                    SetResult(true, $"Компонент '{name}' восстановлен.");
                    return;
                }

                prevFree = currentFree;
                currentFree = next;
            }

            SetResult(false, $"Компонент '{name}' не найден среди удаленных.");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при восстановлении: {ex.Message}");
        }
    }

    private void RebuildListInAlphabeticalOrder()
    {
        try
        {
            using var fs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            RebuildSortedActive(fs);
            SaveProductHeader(fs);
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при перестроении списка: {ex.Message}");
        }
    }

    public void Truncate()
    {
        EnsureOpen();
        try
        {
            PreserveCurrentTypesAsManual();

            var tmpPrd = _productFilePath + ".tmp";
            var tmpPrs = _specFilePath + ".tmp";

            var products = new List<(int OldPos, int NewPos, ProductRecord Rec, List<(int OldPartPos, short Qty)> Entries)>();
            var oldToNew = new Dictionary<int, int>();

            using (var oldPrd = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read))
            using (var oldPrs = new FileStream(_specFilePath, FileMode.Open, FileAccess.Read))
            {
                var active = ActiveProducts(oldPrd).ToList();
                active.Sort((a, b) => string.Compare(a.Rec.Name, b.Rec.Name, StringComparison.Ordinal));

                for (var i = 0; i < active.Count; i++)
                {
                    var newPos = FileHeader.HeaderSize + i * _productHeader.RecordSize;
                    oldToNew[active[i].Pos] = newPos;
                }

                foreach (var (oldPos, rec) in active)
                {
                    var entries = new List<(int OldPartPos, short Qty)>();
                    if (rec.SpecPtr != -1)
                    {
                        foreach (var (_, s) in SpecChain(oldPrs, rec.SpecPtr))
                        {
                            if (s.IsDeleted == 0)
                                entries.Add((s.ProductPtr, s.Quantity));
                        }
                    }
                    products.Add((oldPos, oldToNew[oldPos], rec, entries));
                }
            }

            var newSpecHeader = new SpecHeader { HeadPtr = -1, FreePtr = -1 };
            var newSpecHeadByProduct = new Dictionary<int, int>();

            using (var newPrs = new FileStream(tmpPrs, FileMode.Create, FileAccess.Write))
            {
                newPrs.Write(newSpecHeader.ToBytes(), 0, SpecHeader.HeaderSize);
                var nextSpecPos = SpecHeader.HeaderSize;

                foreach (var p in products)
                {
                    var valid = p.Entries.Where(e => oldToNew.ContainsKey(e.OldPartPos)).ToList();
                    if (valid.Count == 0)
                    {
                        newSpecHeadByProduct[p.NewPos] = -1;
                        continue;
                    }

                    newSpecHeadByProduct[p.NewPos] = nextSpecPos;
                    if (newSpecHeader.HeadPtr == -1)
                        newSpecHeader.HeadPtr = nextSpecPos;

                    for (var i = 0; i < valid.Count; i++)
                    {
                        var pos = nextSpecPos + i * SpecRecord.RecordSize;
                        var next = i + 1 < valid.Count ? pos + SpecRecord.RecordSize : -1;
                        var specRec = new SpecRecord
                        {
                            IsDeleted = 0,
                            ProductPtr = oldToNew[valid[i].OldPartPos],
                            Quantity = valid[i].Qty,
                            NextPtr = next
                        };
                        newPrs.Seek(pos, SeekOrigin.Begin);
                        newPrs.Write(specRec.ToBytes(), 0, SpecRecord.RecordSize);
                    }

                    nextSpecPos += valid.Count * SpecRecord.RecordSize;
                }

                newPrs.Seek(0, SeekOrigin.Begin);
                newPrs.Write(newSpecHeader.ToBytes(), 0, SpecHeader.HeaderSize);
            }

            var newProductHeader = _productHeader;
            newProductHeader.HeadPtr = products.Count > 0 ? products[0].NewPos : -1;
            newProductHeader.FreePtr = -1;

            using (var newPrd = new FileStream(tmpPrd, FileMode.Create, FileAccess.Write))
            {
                newPrd.Write(newProductHeader.ToBytes(), 0, FileHeader.HeaderSize);
                for (var i = 0; i < products.Count; i++)
                {
                    var rec = products[i].Rec;
                    rec.IsDeleted = 0;
                    rec.NextPtr = i + 1 < products.Count ? products[i + 1].NewPos : -1;
                    rec.SpecPtr = newSpecHeadByProduct.TryGetValue(products[i].NewPos, out var head) ? head : -1;

                    newPrd.Seek(products[i].NewPos, SeekOrigin.Begin);
                    newPrd.Write(rec.ToBytes(_nameMaxLength), 0, _productHeader.RecordSize);
                }
                newPrd.Seek(0, SeekOrigin.Begin);
                newPrd.Write(newProductHeader.ToBytes(), 0, FileHeader.HeaderSize);
            }

            File.Copy(tmpPrd, _productFilePath, true);
            File.Copy(tmpPrs, _specFilePath, true);
            File.Delete(tmpPrd);
            File.Delete(tmpPrs);

            Open(_productFilePath);
            SetResult(true, "Файлы уплотнены.");
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при уплотнении: {ex.Message}");
        }
    }

    public void PrintAll()
    {
        EnsureOpen();
        try
        {
            using var fs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read);
            Console.WriteLine();
            Console.WriteLine("Список компонентов:");
            Console.WriteLine("===================");
            foreach (var (pos, rec) in ActiveProducts(fs))
            {
                var type = _componentTypes.TryGetValue(rec.Name, out var t) ? t.ToString() : "?";
                Console.WriteLine($"{rec.Name,-20} | {type,-8} | Указатель: {pos}");
            }
            Console.WriteLine();
            SetResult(true, "", false);
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при выводе списка: {ex.Message}");
        }
    }

    public void PrintSpecification(string componentName, int indentLevel = 0)
    {
        EnsureOpen();
        if (!_nameToPtr.ContainsKey(componentName))
        {
            SetResult(false, $"Ошибка: компонент '{componentName}' не найден.");
            return;
        }
        try
        {
            var path = new HashSet<string>(StringComparer.Ordinal);
            PrintSpecNode(componentName, indentLevel, path);
            SetResult(true, "", false);
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка при выводе спецификации: {ex.Message}");
        }
    }

    public void PrintHelp(string fileName = null)
    {
        TextWriter output = Console.Out;
        try
        {
            if (fileName != null)
                output = new StreamWriter(fileName);

            output.WriteLine();
            output.WriteLine("Доступные команды:");
            output.WriteLine("==================");
            output.WriteLine("Create имя_файла(максимальная_длина_имени[, имя_файла_спецификаций])");
            output.WriteLine("Open имя_файла");
            output.WriteLine("Input (имя_компонента, тип) - тип: Изделие, Узел, Деталь");
            output.WriteLine("Input (имя_компонента/имя_комплектующего)");
            output.WriteLine("Delete (имя_компонента)");
            output.WriteLine("Delete (имя_компонента/имя_комплектующего)");
            output.WriteLine("Restore (имя_компонента)");
            output.WriteLine("Restore (*)");
            output.WriteLine("Truncate");
            output.WriteLine("Print (*)");
            output.WriteLine("Print (имя_компонента)");
            output.WriteLine("Help [имя_файла]");
            output.WriteLine("Exit");
            output.WriteLine();

            if (fileName != null)
                Console.WriteLine($"Справка сохранена в файл {fileName}");
            SetResult(true, "", false);
        }
        catch (Exception ex)
        {
            SetResult(false, $"Ошибка вывода справки: {ex.Message}");
        }
        finally
        {
            if (fileName != null)
                output.Dispose();
        }
    }

    public void Close()
    {
        _isOpen = false;
        _nameToPtr.Clear();
        _componentTypes.Clear();
        _componentSpecPtr.Clear();
        SetResult(true, "Файлы закрыты.");
    }

    public IEnumerable<(string Name, ComponentType Type, int SpecPtr, int Position)> GetAllActiveProducts()
    {
        EnsureOpen();
        var list = new List<(string, ComponentType, int, int)>();
        try
        {
            LoadNameCache();
            using var fs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            foreach (var (pos, rec) in ActiveProducts(fs))
            {
                var type = _componentTypes.TryGetValue(rec.Name, out var t)
                    ? t
                    : _manualTypes.TryGetValue(rec.Name, out var manual) ? manual : ComponentType.Деталь;
                list.Add((rec.Name, type, rec.SpecPtr, pos));
            }
        }
        catch
        {
        }
        return list;
    }

    public IEnumerable<(string PartName, short Quantity, int SpecPosition)> GetSpecificationEntries(string componentName)
    {
        EnsureOpen();
        var result = new List<(string, short, int)>();
        LoadNameCache();
        if (!_componentSpecPtr.TryGetValue(componentName, out var head) || head == -1)
            return result;

        try
        {
            using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.Read);
            using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read);
            foreach (var (specPos, s) in SpecChain(specFs, head))
            {
                if (s.IsDeleted != 0)
                    continue;
                if (s.ProductPtr < FileHeader.HeaderSize || s.ProductPtr + _productHeader.RecordSize > productFs.Length)
                    continue;
                var part = ReadProduct(productFs, s.ProductPtr);
                if (part.IsDeleted != 0)
                    continue;
                result.Add((part.Name, s.Quantity, specPos));
            }
        }
        catch
        {
        }
        return result;
    }

    public bool RenameComponent(string oldName, string newName)
    {
        EnsureOpen();
        if (!_nameToPtr.ContainsKey(oldName) || _nameToPtr.ContainsKey(newName))
            return false;
        if (!ValidateName(newName, out _))
            return false;

        try
        {
            var pos = _nameToPtr[oldName];
            using var fs = new FileStream(_productFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            var rec = ReadProduct(fs, pos);
            rec.Name = newName;
            WriteProduct(fs, pos, rec);

            if (_manualTypes.TryGetValue(oldName, out var t))
            {
                _manualTypes.Remove(oldName);
                _manualTypes[newName] = t;
                SaveTypeMetadata();
            }

            RebuildSortedActive(fs);
            SaveProductHeader(fs);
            fs.Flush();
            LoadNameCache();
            SetResult(true, $"Компонент '{oldName}' переименован в '{newName}'.");
            return true;
        }
        catch
        {
            SetResult(false, "Ошибка при переименовании компонента.");
            return false;
        }
    }

    private void PrintSpecNode(string componentName, int indentLevel, HashSet<string> path)
    {
        var indent = new string(' ', indentLevel * 2);
        Console.WriteLine($"{indent}{componentName}");

        if (!path.Add(componentName))
        {
            Console.WriteLine($"{indent}  (циклическая ссылка)");
            return;
        }

        if (!_componentSpecPtr.TryGetValue(componentName, out var head) || head == -1)
        {
            Console.WriteLine($"{indent}  (пусто)");
            path.Remove(componentName);
            return;
        }

        using var specFs = new FileStream(_specFilePath, FileMode.Open, FileAccess.Read);
        using var productFs = new FileStream(_productFilePath, FileMode.Open, FileAccess.Read);

        foreach (var (_, s) in SpecChain(specFs, head))
        {
            if (s.IsDeleted != 0)
                continue;
            if (s.ProductPtr < FileHeader.HeaderSize || s.ProductPtr + _productHeader.RecordSize > productFs.Length)
                continue;
            var part = ReadProduct(productFs, s.ProductPtr);
            if (part.IsDeleted != 0)
                continue;

            Console.WriteLine($"{indent}  |- {part.Name} (x{s.Quantity})");
            if (_componentSpecPtr.TryGetValue(part.Name, out var nested) && nested != -1)
                PrintSpecNode(part.Name, indentLevel + 2, path);
        }

        path.Remove(componentName);
    }

    public void SetComponentType(string name, ComponentType type)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;
        _manualTypes[name] = type;
        SaveTypeMetadata();
        if (_componentTypes.ContainsKey(name))
            _componentTypes[name] = type;
    }
}

