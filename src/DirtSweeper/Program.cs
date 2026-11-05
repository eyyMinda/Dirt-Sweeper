using System;
using System.Threading;
using System.Windows.Forms;

namespace DirtSweeper
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // dev builds: their own settings file, so testing never touches the real one
            int s = Array.IndexOf(args, "--settings");
            if (s >= 0 && s + 1 < args.Length) Settings.UseFile(args[s + 1]);

            // after an update the new exe is started by the old one: let the old one exit (and free the mutex) first
            int i = Array.IndexOf(args, "--wait-pid");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int pid))
            {
                try { using (var old = System.Diagnostics.Process.GetProcessById(pid)) old.WaitForExit(15000); }
                catch (ArgumentException) { } // already gone
            }

            using (var single = new Mutex(true, "DirtSweeper.SingleInstance", out bool first))
            {
                if (!first)
                {
                    // already running: ask that copy to open its settings window
                    if (EventWaitHandle.TryOpenExisting(App.ShowSignalName, out var show)) using (show) show.Set();
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new App());
            }
        }
    }
}
