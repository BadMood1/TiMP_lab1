using System.Text.RegularExpressions;

namespace Lab1_4Sem.Cli;

public sealed record ParsedCommand(string Name, string[] Args, string RawArgs);

public sealed class CommandParser
{
    public bool TryParse(string line, out ParsedCommand command, out string error)
    {
        command = new ParsedCommand("", Array.Empty<string>(), "");
        error = "";

        line = line.Trim();
        if (line.Length == 0)
        {
            error = "Пустая команда.";
            return false;
        }

        var firstSpace = line.IndexOf(' ');
        string name;
        string rest;

        if (firstSpace < 0)
        {
            name = line;
            rest = "";
        }
        else
        {
            name = line[..firstSpace].Trim();
            rest = line[(firstSpace + 1)..].Trim();
        }

        command = new ParsedCommand(name, ParseArguments(rest), rest);
        return true;
    }

    private string[] ParseArguments(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
            return Array.Empty<string>();

        var result = new List<string>();
        var regex = new Regex(@"\(([^)]+)\)|\S+");
        var matches = regex.Matches(args);

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                // Это аргумент в скобках, разделяем по запятой
                var inner = match.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim()).ToArray();
                result.AddRange(inner);
            }
            else
            {
                result.Add(match.Value);
            }
        }

        return result.ToArray();
    }

    public bool TryParseComponentWithType(string args, out string name, out string typeStr, out string error)
    {
        name = "";
        typeStr = "";
        error = "";

        var match = Regex.Match(args, @"\(([^,]+),\s*([^)]+)\)");
        if (!match.Success)
        {
            error = "Неверный формат. Ожидается: (имя, тип)";
            return false;
        }

        name = match.Groups[1].Value.Trim();
        typeStr = match.Groups[2].Value.Trim();
        return true;
    }

    public bool TryParseComponentPair(string args, out string componentName, out string partName, out string error)
    {
        componentName = "";
        partName = "";
        error = "";

        var match = Regex.Match(args, @"\(([^/]+)/([^)]+)\)");
        if (!match.Success)
        {
            error = "Неверный формат. Ожидается: (имя_компонента/имя_комплектующего)";
            return false;
        }

        componentName = match.Groups[1].Value.Trim();
        partName = match.Groups[2].Value.Trim();
        return true;
    }

    public bool TryParseCreateParams(string args, out string fileName, out int nameLength, out string specFileName, out string error)
    {
        fileName = "";
        nameLength = 0;
        specFileName = "";
        error = "";

        // Ищем имя файла перед скобками
        var fileMatch = Regex.Match(args, @"^([^(]+)\(([^)]+)\)");
        if (!fileMatch.Success)
        {
            error = "Неверный формат. Ожидается: Create имя_файла(длина[, спецификация])";
            return false;
        }

        fileName = fileMatch.Groups[1].Value.Trim();
        var inner = fileMatch.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).ToArray();

        if (inner.Length < 1)
        {
            error = "Не указана максимальная длина имени.";
            return false;
        }

        if (!int.TryParse(inner[0], out nameLength) || nameLength <= 0)
        {
            error = "Максимальная длина имени должна быть положительным числом.";
            return false;
        }

        if (inner.Length >= 2)
            specFileName = inner[1];

        return true;
    }
}