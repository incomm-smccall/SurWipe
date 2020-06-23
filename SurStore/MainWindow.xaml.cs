using SurLib.Cryptography;
using SurLib.Models;
using SurStore.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using sysIo = System.IO;
using System.Security.Cryptography;

namespace SurStore
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private int _fileCount { get; set; }
        private bool shredOrig = false;
        private ManagementEventWatcher w = null;
        private UsbModel selectedUsb = null;
        public ObservableCollection<FileModel> files = new ObservableCollection<FileModel>();
        public ObservableCollection<string> folders = new ObservableCollection<string>();
        public ObservableCollection<UsbModel> UsbList = new ObservableCollection<UsbModel>();

        public MainWindow(string[] argfiles)
        {
            InitializeComponent();
            Logging.LogMessage(LoggingLevel.Info, $"argfiles {string.Join(", ", argfiles)}");
            FileControls.AddFilesToList(argfiles, ref folders, ref files);
            Logging.LogMessage(LoggingLevel.Info, $"file count: {files.Count}");
            FileListView.ItemsSource = files;
            FolderListView.ItemsSource = folders;

            if (files.Count > 0)
                UpdateResultMessage(folders.Count, files.Count);
            else
                resultMsg.Text = "Select files for shredding";
            BuildUsbList();
            AddRemoveUSBHandler();
            AddInsertUSBHandler();
        }

        private void BtnAddFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Multiselect = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    sysIo.FileInfo file = new sysIo.FileInfo(filename);
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
            UpdateResultMessage(folders.Count, files.Count);
        }

        private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
        {
            FileModel fm = (FileModel)FileListView.SelectedItem;
            files.Remove(fm);
            FileListView.ItemsSource = files;
            UpdateResultMessage(folders.Count, files.Count);
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
                UpdateResultMessage(folders.Count, files.Count);
            }
        }

        private void BtnRemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            string selFolder = (string)FolderListView.SelectedItem;
            FileControls.RemoveFolderFromList(selFolder, ref folders, ref files);
            FileListView.ItemsSource = files;
            FolderListView.ItemsSource = folders;
            UpdateResultMessage(folders.Count, files.Count);
        }

        private void BtnEncrypt_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string password = InputPassword.Password;
            string encryptPassword = AES.GetDriveKeys(password);
            if (selectedUsb == null)
            {
                completedIn.Text = "Please select a device";
                return;
            }
            //FileControls.EncryptFiles(selectedUsb.UsbSerialNumber, encryptPassword, files);
            int fileIndex = 0;
            foreach (FileModel fm in files)
            {
                string origPath = fm.Path;
                string savepath = sysIo.Path.GetDirectoryName(fm.Path);
                FileControls.EncryptFile(selectedUsb.UsbSerialNumber, encryptPassword, fm, savepath);
                completedIn.Text = $"{fileIndex++} encrypted";
                fm.Path = origPath;
                if (shredOrig)
                    WipeFile(fm);
            }
            sw.Stop();
            completedIn.Text = $"Completed In: {sw.Elapsed.TotalMilliseconds.ToString("F3") } ms";
        }

        private void BtnDecrypt_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch sw = Stopwatch.StartNew();
            string password = InputPassword.Password;
            string decryptPassword = AES.GetDriveKeys(password);
            if (selectedUsb == null)
            {
                completedIn.Text = "Please select a device";
                return;
            }
            //FileControls.DecryptFiles(selectedUsb.UsbSerialNumber, decryptPassword, files);
            int fileIndex = 0;
            foreach (FileModel fm in files)
            {
                string origPath = fm.Path;
                string savepath = sysIo.Path.GetDirectoryName(fm.Path);
                FileControls.DecryptFile(selectedUsb.UsbSerialNumber, decryptPassword, fm, savepath);
                completedIn.Text = $"{fileIndex++} decrypted";
                fm.Path = origPath;
                if (shredOrig)
                    WipeFile(fm);
            }
            sw.Stop();
            completedIn.Text = $"Completed In: {sw.Elapsed.TotalMilliseconds.ToString("F3")} ms";
        }

        private void Checkbox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                selectedUsb = UsbList.FirstOrDefault(x => x.UsbChecked == true);
                //UsbList.Select(x => { x.UsbChecked = false; return x; });

                //var sel = (System.Windows.Controls.CheckBox)sender;
                //selectedUsb = UsbList.FirstOrDefault(x => x.UsbCaption == sel.Content.ToString());
                //selectedUsb.UsbChecked = true;
            }
            catch (Exception ex)
            {
                string junk = ex.Message;
            }
        }

        private void CheckBoxChanged(object sender, RoutedEventArgs e)
        {
            var shredCheck = (System.Windows.Controls.CheckBox)sender;
            shredOrig = (bool)shredCheck.IsChecked;
        }

        private void UpdateResultMessage(int folders, int files)
        {
            resultMsg.Text = $"Folders {folders} : {files} files ready";
        }

        public void AddRemoveUSBHandler()
        {
            WqlEventQuery q;
            ManagementScope scope = new ManagementScope("root\\CIMV2");
            scope.Options.EnablePrivileges = true;
            try
            {
                q = new WqlEventQuery();
                q.EventClassName = "__InstanceDeletionEvent";
                q.WithinInterval = new TimeSpan(0, 0, 3);
                q.Condition = @"TargetInstance ISA 'Win32_USBHub'";
                w = new ManagementEventWatcher(scope, q);
                w.EventArrived += new EventArrivedEventHandler(USBControl);
                w.Start();
                resultMsg.Text = "USB Flash drive removed";
            }
            catch (Exception e)
            {
                resultMsg.Text = e.Message;
                if (w != null)
                    w.Stop();
            }
        }

        public void AddInsertUSBHandler()
        {
            WqlEventQuery q;
            ManagementScope scope = new ManagementScope("root\\CIMV2");
            scope.Options.EnablePrivileges = true;
            try
            {
                q = new WqlEventQuery();
                q.EventClassName = "__InstanceCreationEvent";
                q.WithinInterval = new TimeSpan(0, 0, 3);
                q.Condition = @"TargetInstance ISA 'Win32_USBHub'";
                w = new ManagementEventWatcher(scope, q);
                w.EventArrived += new EventArrivedEventHandler(USBControl);
                w.Start();
                resultMsg.Text = "USB Flash drive added";
            }
            catch (Exception e)
            {
                resultMsg.Text = e.Message;
                if (w != null)
                    w.Stop();
            }
        }

        private void USBControl(object sender, EventArgs e)
        {
            BuildUsbList();
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

        private void BuildUsbList()
        {
            ManagementObjectCollection searchObjs = new ManagementObjectSearcher("select * from Win32_DiskDrive where InterfaceType = 'USB'").Get();
            App.Current.Dispatcher.Invoke((Action)delegate
            {
                UsbList.Clear();
                if (searchObjs.Count > 0)
                {
                    foreach (ManagementObject usb in searchObjs)
                    {
                        try
                        {
                            var u = new UsbModel();
                            u.UsbCaption = usb["Caption"].ToString();
                            u.UsbSerialNumber = usb["SerialNumber"].ToString();
                            u.UsbChecked = false;
                            UsbList.Add(u);
                            UsbListView.ItemsSource = UsbList;
                        }
                        catch (Exception e)
                        {
                            string err = e.Message;
                        }
                    }
                }
                CollectionViewSource.GetDefaultView(UsbList).Refresh();
            });
        }

        private void WipeFile(FileModel file)
        {
            try
            {
                int timesToWrite = 10;
                sysIo.File.SetAttributes(file.Path, sysIo.FileAttributes.Normal);
                double sectors = Math.Ceiling(new sysIo.FileInfo(file.Path).Length / 512.0);
                byte[] dummyBuffer = new byte[512];
                RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
                for (int i = 0; i <= 3; i++)
                {
                    byte[] inFile = sysIo.File.ReadAllBytes(file.Path);
                    AES.SetDefaultKey(FileControls.GenerateRandomKey(25));
                    if (inFile.Length > 0)
                    {
                        sysIo.FileStream inputStream = new sysIo.FileStream(file.Path, sysIo.FileMode.Open);
                        byte[] data = AES.Encrypt(inFile);
                        inputStream.SetLength(0);
                        inputStream.Seek(0, sysIo.SeekOrigin.Begin);
                        inputStream.Write(data, 0, data.Length);

                        for (int passIndex = 0; passIndex < timesToWrite; passIndex++)
                        {
                            inputStream.Position = 0;
                            for (int sectorIndex = 0; sectorIndex < sectors; sectorIndex++)
                            {
                                rng.GetBytes(dummyBuffer);
                                inputStream.Write(dummyBuffer, 0, dummyBuffer.Length);
                            }
                        }
                        inputStream.Close();
                    }
                }

                sysIo.FileStream instream = new sysIo.FileStream(file.Path, sysIo.FileMode.Open);
                instream.SetLength(0);
                instream.Close();

                DateTime dt = new DateTime(2037, 1, 1, 0, 0, 0);
                sysIo.File.SetCreationTime(file.Path, dt);
                sysIo.File.SetLastAccessTime(file.Path, dt);
                sysIo.File.SetLastWriteTime(file.Path, dt);

                sysIo.File.SetCreationTimeUtc(file.Path, dt);
                sysIo.File.SetLastAccessTimeUtc(file.Path, dt);
                sysIo.File.SetLastWriteTimeUtc(file.Path, dt);

                sysIo.File.Delete(file.Path);
                AES.UnsetDefaultKey();
            }
            catch (Exception ex)
            {
                Logging.LogError(LoggingLevel.Error, "File Wipe Failed", ex);
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
    }
}
