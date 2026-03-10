using Lab1_4Sem.Models;
using Lab1_4Sem.Services;

namespace Lab1_4Sem.Cli;

public class CommandExecutor
{
    private readonly ProductFileService _service;
    private readonly CommandParser _parser;

    public CommandExecutor(ProductFileService service, CommandParser parser)
    {
        _service = service;
        _parser = parser;
    }

    public void Execute(ParsedCommand command)
    {
        try
        {
            switch (command.Name.ToLowerInvariant())
            {
                case "create":
                case "c":
                    HandleCreate(command);
                    break;

                case "open":
                case "o":
                    HandleOpen(command);
                    break;

                case "input":
                case "i":
                    HandleInput(command);
                    break;

                case "print":
                case "p":
                    HandlePrint(command);
                    break;

                case "delete":
                case "d":
                    HandleDelete(command);
                    break;

                case "restore":
                case "r":
                    HandleRestore(command);
                    break;

                case "truncate":
                case "t":
                    _service.Truncate();
                    break;

                case "help":
                case "h":
                case "?":
                    HandleHelp(command);
                    break;

                case "exit":
                case "quit":
                case "q":
                    _service.Close();
                    Environment.Exit(0);
                    break;

                default:
                    Console.WriteLine($"Неизвестная команда: {command.Name}. Введите Help для списка команд.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка выполнения команды: {ex.Message}");
        }
    }

    private void HandleCreate(ParsedCommand command)
    {
        if (_parser.TryParseCreateParams(command.RawArgs, out string fileName,
            out int nameLength, out string specFileName, out string error))
        {
            _service.Create(fileName, nameLength, specFileName);
        }
        else
        {
            Console.WriteLine($"Ошибка: {error}");
        }
    }

    private void HandleOpen(ParsedCommand command)
    {
        if (command.Args.Length > 0)
        {
            _service.Open(command.Args[0]);
        }
        else
        {
            Console.WriteLine("Ошибка: укажите имя файла.");
        }
    }

    private void HandleInput(ParsedCommand command)
    {
        if (command.RawArgs.Contains('/'))
        {
            // Добавление в спецификацию
            if (_parser.TryParseComponentPair(command.RawArgs, out string component, out string part, out string error))
            {
                _service.AddToSpecification(component, part);
            }
            else
            {
                Console.WriteLine($"Ошибка: {error}");
            }
        }
        else
        {
            // Добавление компонента
            if (_parser.TryParseComponentWithType(command.RawArgs, out string name, out string typeStr, out string error))
            {
                if (Enum.TryParse<ComponentType>(typeStr, true, out var type))
                {
                    _service.AddComponent(name, type);
                }
                else
                {
                    Console.WriteLine($"Ошибка: неверный тип '{typeStr}'. Допустимые: Изделие, Узел, Деталь");
                }
            }
            else
            {
                Console.WriteLine($"Ошибка: {error}");
            }
        }
    }

    private void HandlePrint(ParsedCommand command)
    {
        if (command.Args.Length == 0)
        {
            _service.PrintAll();
        }
        else if (command.Args[0] == "*")
        {
            _service.PrintAll();
        }
        else
        {
            // Вывод спецификации
            string name = command.Args[0].Trim('(', ')');
            _service.PrintSpecification(name);
        }
    }

    private void HandleDelete(ParsedCommand command)
    {
        if (command.RawArgs.Contains('/'))
        {
            // Удаление из спецификации
            if (_parser.TryParseComponentPair(command.RawArgs, out string component, out string part, out string error))
            {
                _service.DeleteFromSpecification(component, part);
            }
            else
            {
                Console.WriteLine($"Ошибка: {error}");
            }
        }
        else
        {
            // Удаление компонента
            if (command.Args.Length == 0)
            {
                Console.WriteLine("Ошибка: укажите имя компонента.");
                return;
            }

            string name = command.Args[0].Trim('(', ')');
            _service.DeleteComponent(name);
        }
    }

    private void HandleRestore(ParsedCommand command)
    {
        if (command.Args.Length > 0)
        {
            string name = command.Args[0].Trim('(', ')');
            _service.Restore(name);
        }
        else
        {
            Console.WriteLine("Ошибка: укажите имя компонента или *");
        }
    }

    private void HandleHelp(ParsedCommand command)
    {
        if (command.Args.Length > 0)
            _service.PrintHelp(command.Args[0]);
        else
            _service.PrintHelp();
    }
}
