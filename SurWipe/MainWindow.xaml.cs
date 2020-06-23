using SurLib.Cryptography;
using SurLib.Models;
using SurWipe.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Threading;

namespace SurWipe
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private int _fileCount { get; set; }
        public ObservableCollection<FileModel> files = new ObservableCollection<FileModel>();
        public ObservableCollection<string> folders = new ObservableCollection<string>();

        public MainWindow(string[] argfiles)
        {
            InitializeComponent();
            Logging.LogMessage(LoggingLevel.Info, $"argfiles {string.Join(", ", argfiles)}");
            FileControls.AddFilesToList(argfiles, ref folders, ref files);
            Logging.LogMessage(LoggingLevel.Info, $"file count: {files.Count}");
            FileListView.ItemsSource = files;
            FolderListView.ItemsSource = folders;
            if (files.Count > 0)
                resultMsg.Text = $"Folders {folders.Count} : {files.Count} files ready for shredding";
            else
                resultMsg.Text = "Select files for shredding";
            //UpdateProgressBarDelegate updateProgressBarDelegate = new UpdateProgressBarDelegate(progressBar.SetValue);
        }

        private delegate void UpdateProgressBarDelegate(DependencyProperty dp, object value);
        private delegate void UpdateMainProgressBarDelegate(DependencyProperty dp, object value);
        //private delegate void UpdateProgressBarDelegate(int index, object value);

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    FileInfo file = new FileInfo(filename);
                    FileModel fm = new FileModel()
                    {
                        Name = file.Name,
                        Type = FileControls.GetFileType(file.Extension),
                        Path = file.FullName
                    };
                    files.Add(fm);
                }
            }
            FileListView.ItemsSource = files;
            resultMsg.Text = $"{files.Count} files ready for shredding";
        }

        private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            FileModel fm = (FileModel)FileListView.SelectedItem;
            files.Remove(fm);
            FileListView.ItemsSource = files;
            resultMsg.Text = $"{files.Count} files ready for shredding";
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog folderBrowser = new FolderBrowserDialog();
            folderBrowser.Description = "Select a folder";
            folderBrowser.ShowNewFolderButton = false;
            DialogResult result = folderBrowser.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                FileControls.AddFolderToList(folderBrowser.SelectedPath, ref folders, ref files);
                resultMsg.Text = $"Folders {folders.Count} : {files.Count} files ready for shredding";
            }
        }

        private void BtnRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            // This is going to be a bitch
            new NotImplementedException("This feature still needs to be created");
        }

        private void BtnWipe_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch sp = Stopwatch.StartNew();
            UpdateMainProgressBarDelegate updateMainPBar = new UpdateMainProgressBarDelegate(mainProgressBar.SetValue);
            UpdateProgressBarDelegate updateProgressBarDelegate = new UpdateProgressBarDelegate(progressBar.SetValue);
            _fileCount = files.Count;
            int fileIndex = 1;
            for (int i = _fileCount - 1; i >= 0; i--)
            {
                if (WipeFile(files[i]))
                {
                    //progressBar.Value = Convert.ToInt32((double)fileIndex / _fileCount * 100);
                    Dispatcher.Invoke(updateProgressBarDelegate, DispatcherPriority.Background, new object[] { RangeBase.ValueProperty, (double)fileIndex / _fileCount * 100 });
                    files.RemoveAt(i);
                    fileIndex++;
                }
                FileListView.ItemsSource = files;
                resultMsg.Text = $"{files.Count} files ready for shredding";
            }
            if (folders.Count > 0)
            {
                int folderCount = folders.Count;
                for (int i = folders.Count - 1; i >= 0; i--)
                {
                    Directory.Delete(folders[i]);
                    folders.RemoveAt(i);
                    FolderListView.ItemsSource = folders;
                }
            }

            Dispatcher.Invoke(updateProgressBarDelegate, DispatcherPriority.Background, new object[] { RangeBase.ValueProperty, 100.0 });
            Dispatcher.Invoke(updateMainPBar, DispatcherPriority.Background, new object[] { RangeBase.ValueProperty, 100.0 });
            //for (int i = (int)progressBar.Minimum; i <= (int)progressBar.Maximum; i++)
            //{
            //    Dispatcher.Invoke(updateProgressBarDelegate, DispatcherPriority.Background, new object[] { RangeBase.ValueProperty, Convert.ToDouble(i) });
            //}
            resultMsg.Text = $"Files successfully shreded";
            Logging.LogMessage(LoggingLevel.Info, resultMsg.Text);
            sp.Stop();
            completedIn.Text = $"{sp.Elapsed.TotalMilliseconds.ToString("F3")} ms";
            Logging.LogMessage(LoggingLevel.Info, completedIn.Text);
        }

        public void VerifyPropertyName(string propertyName)
        { 
            if (TypeDescriptor.GetProperties(this)[propertyName] == null)
            {
                string msg = "Invalid property name: " + propertyName;
                if (this.ThrowOnInvalidPropertyName)
                    throw new Exception(msg);

                Debug.Fail(msg);
            }
        }

        public virtual string DisplayName { get; protected set; }
        protected virtual bool ThrowOnInvalidPropertyName { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            VerifyPropertyName(propertyName);
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        private bool WipeFile(FileModel file)
        {
            UpdateMainProgressBarDelegate updateMainPBar = new UpdateMainProgressBarDelegate(mainProgressBar.SetValue);
            try
            {
                int timesToWrite = 10;
                File.SetAttributes(file.Path, FileAttributes.Normal);
                double sectors = Math.Ceiling(new FileInfo(file.Path).Length / 512.0);
                byte[] dummyBuffer = new byte[512];
                RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

                for (int i = 0; i <= 3; i++)
                {
                    byte[] infile = File.ReadAllBytes(file.Path);
                    AES.SetDefaultKey(FileControls.GenerateRandomKey(25));

                    if (infile.Length > 0)
                    {
                        FileStream inputStream = new FileStream(file.Path, FileMode.Open);
                        byte[] data = AES.Encrypt(infile);
                        inputStream.SetLength(0);
                        inputStream.Seek(0, SeekOrigin.Begin);
                        inputStream.Write(data, 0, data.Length);

                        for (int passIndex = 0; passIndex < timesToWrite; passIndex++)
                        {
                            inputStream.Position = 0;
                            for (int sectorIndex = 0; sectorIndex < sectors; sectorIndex++)
                            {
                                rng.GetBytes(dummyBuffer);
                                inputStream.Write(dummyBuffer, 0, dummyBuffer.Length);
                            }
                            Dispatcher.Invoke(updateMainPBar, DispatcherPriority.Background, new object[] { RangeBase.ValueProperty, (double)passIndex / timesToWrite * 100 });
                        }
                        //inputStream.SetLength(0);
                        inputStream.Close();
                    }
                }

                FileStream instream = new FileStream(file.Path, FileMode.Open);
                instream.SetLength(0);
                instream.Close();

                DateTime dt = new DateTime(2037, 1, 1, 0, 0, 0);
                File.SetCreationTime(file.Path, dt);
                File.SetLastAccessTime(file.Path, dt);
                File.SetLastWriteTime(file.Path, dt);

                File.SetCreationTimeUtc(file.Path, dt);
                File.SetLastAccessTimeUtc(file.Path, dt);
                File.SetLastWriteTimeUtc(file.Path, dt);

                File.Delete(file.Path);
                AES.UnsetDefaultKey();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (folders.Count > 0)
                {
                    
                }
            }
        }
    }
}
