// Program.cs — Entry point for RamWatchdog v1.0.0
namespace RamWatchdog;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Prevent multiple instances
        using var mutex = new Mutex(true, "Global\\RamWatchdog_SingleInstance", out bool isNew);
        if (!isNew)
        {
            MessageBox.Show("RAM Watchdog is already running.", "RAM Watchdog",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
