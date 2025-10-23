namespace Parfy
{
    public class ConsoleWrapper : IConsole
    {
        public void ClearLastLine()
        {
            if (Console.CursorTop != 0)
            {
                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.Write(new string(' ', Console.BufferWidth));
                Console.SetCursorPosition(0, Console.CursorTop);
            }
        }

        public string? ReadLine() => Console.ReadLine();

        public void WriteLine(string message, EConsoleStatus status = EConsoleStatus.None)
        {
            Console.ForegroundColor = status switch
            {
                EConsoleStatus.Success => ConsoleColor.Green,
                EConsoleStatus.Error => ConsoleColor.Red,
                _ => ConsoleColor.Magenta
            };

            Console.WriteLine(message);

            Console.ResetColor();
        }
    }
}
