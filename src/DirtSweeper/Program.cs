using System;
using System.Threading;
using System.Windows.Forms;

namespace DirtSweeper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
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
