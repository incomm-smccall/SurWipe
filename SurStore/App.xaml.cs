using SHDocVw;
using Shell32;
using SurStore.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace SurStore
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private List<string> _result = new List<string>();
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        private static IntPtr lastHandle = IntPtr.Zero;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            try
            {
                if (Initialize())
                {
                    Logging.LogMessage(LoggingLevel.Info, $"arguments {string.Join(", ", _result)}");
                    var viewModel = new MainWindow(_result.ToArray());
                    viewModel.Show();
                }
                else
                {
                    MutexHelper.CloseMutex();
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                Logging.LogError(LoggingLevel.Error, $"Error: {ex.Message}", ex);
                throw;
            }
        }

        private bool Initialize()
        {
            if (!MutexHelper.CreateMutex("123AKs82kA,ylAo2kAlUS2kYkala!")) return false;

            Thread staThread = new Thread(() =>
            {
                _result = GetExplorerSelectedFiles();
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
            return true;
        }

        private IntPtr GetLastActive()
        {
            IntPtr curHandle = GetForegroundWindow();

            do
            {
                IntPtr retHandle = lastHandle;
                lastHandle = curHandle;
                if (retHandle != IntPtr.Zero)
                    return retHandle;
            } while (curHandle == lastHandle);
            return IntPtr.Zero;
        }

        private List<string> GetExplorerSelectedFiles()
        {
            var lastActive = GetLastActive();
            List<string> selectFiles = new List<string>();
            Shell shell = new Shell();
            foreach (ShellBrowserWindow win in shell.Windows())
            {
                if (win.HWND == (int)lastActive)
                {
                    if (win.Document != null)
                    {
                        foreach (FolderItem fi in win.Document.SelectedItems)
                        {
                            selectFiles.Add(fi.Path);
                        }
                    }
                }
            }
            return selectFiles;
        }
    }
}
