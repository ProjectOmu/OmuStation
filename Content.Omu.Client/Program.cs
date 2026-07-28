using Robust.Client;

namespace Content.Omu.Client;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ContentStart.Start(args);
    }
}
