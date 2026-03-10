using Lab1_4Sem.Cli;
using Lab1_4Sem.Services;
using System.Text;
using System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var consoleEncoding = Encoding.GetEncoding(Console.OutputEncoding.CodePage);
        Console.InputEncoding = consoleEncoding;
        Console.OutputEncoding = consoleEncoding;

        Console.WriteLine("Система управления спецификациями");
        Console.WriteLine("==================================");
        Console.WriteLine("Введите Help для списка команд.\n");

        if (args != null && args.Contains("gui"))
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Lab1_4Sem.UI.MainForm());
            return;
        }

        var parser = new CommandParser();
        var service = new ProductFileService();
        var executor = new CommandExecutor(service, parser);

        while (true)
        {
            Console.Write("PS> ");
            var line = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!parser.TryParse(line, out var command, out var error))
            {
                Console.WriteLine($"Ошибка парсинга: {error}");
                continue;
            }

            executor.Execute(command);
        }
    }
}
