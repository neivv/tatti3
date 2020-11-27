using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace Tatti3
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
#if DEBUG
            AllocConsole();
            this.Exit += (o, e) => {
                if (this.fileTrace != null)
                {
                    this.fileTrace.Flush();
                }
            };
            PresentationTraceSources.DataBindingSource.Switch.Level =
                System.Diagnostics.SourceLevels.Warning;
            PresentationTraceSources.DataBindingSource.Listeners.Add(new ConsoleTraceListener());
            fileTrace = new TextWriterTraceListener("wpf_trace.txt");
            PresentationTraceSources.DataBindingSource.Listeners.Add(fileTrace);
#endif
            // Magic thing that prevents short freezes when browsing through the entry list.
            // WPF seems to cause a new RCW to be allocated for each text entry that gets changed,
            // and each RCW allocation causes old ones to be cleaned up. Looking at dotnet
            // runtime source, the cleanup may cause 1ms wait to yield to finalizer
            // thread, but for me the the waits were 15ms sometimes, which, when done
            // for 20 `TextBox`es, caused a notable freeze.
            //
            // Documentation says that when using this
            // System.Runtime.InteropServices.Marshal.CleanupUnusedObjectsInCurrentContext
            // must be manually invoked instead. I didn't see any memory leaks in taskmgr when it
            // wasn't invoked, but for now it is being done whenever entry is changed in
            // MainWindow.
            System.Threading.Thread.CurrentThread.DisableComObjectEagerCleanup();
        }

#if DEBUG
        TextWriterTraceListener fileTrace;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
#endif

        void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
#if DEBUG
            fileTrace.Flush();
#endif
            using var file = new StreamWriter(File.Create("exception.txt"));
            var exception = e.Exception;
            while (true)
            {
                file.Write($"Exception: {exception.Message}\n\nStack:\n{exception.StackTrace}\n");
                exception = exception.InnerException;
                if (exception == null)
                {
                    break;
                }
                file.Write("------------\nCaused by:\n");
            }
        }
    }
}
