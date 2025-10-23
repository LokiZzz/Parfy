namespace Parfy
{
    public interface IConsole
    {
        void WriteLine(string message, EConsoleStatus status = EConsoleStatus.None);

        string? ReadLine();

        void ClearLastLine();
    }

    public enum EConsoleStatus
    {
        None = 0,
        Success = 1,
        Error = 2
    }
}
