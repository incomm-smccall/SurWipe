using Microsoft.Win32;
using SurLib.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SurLib.Cryptography
{
    public static class FileControls
    {
        public static void AddFilesToList(string[] argfiles, ref ObservableCollection<string> folders, ref ObservableCollection<FileModel> files)
        {
            foreach (string arg in argfiles)
            {
                FileAttributes attrib = File.GetAttributes(arg);
                if (attrib.HasFlag(FileAttributes.Directory))
                {
                    AddFolderToList(arg, ref folders, ref files);
                }
                else
                {
                    FileInfo fileInfo = new FileInfo(arg);
                    FileModel fm = new FileModel()
                    {
                        Name = fileInfo.Name,
                        Type = GetFileType(fileInfo.Extension),
                        Path = fileInfo.FullName
                    };
                    files.Add(fm);
                }
            }
            //Logging.LogMessage(LoggingLevel.Info, $"argfiles {string.Join(", ", argfiles)} files: {string.Join(", ", files)}");
        }

        public static void AddFilesToList(List<string> folderFiles, ref ObservableCollection<FileModel> files)
        {
            foreach (string file in folderFiles)
            {
                FileInfo fileInfo = new FileInfo(file);
                files.Add(new FileModel
                {
                    Name = fileInfo.Name,
                    Type = GetFileType(fileInfo.Extension),
                    Path = fileInfo.FullName
                });
            }
        }

        public static void AddFolderToList(string selectFolder, ref ObservableCollection<string> folders, ref ObservableCollection<FileModel> files)
        {
            folders.Add(selectFolder);
            List<string> subFolders = new List<string>();
            foreach (string folder in Directory.EnumerateDirectories(selectFolder))
            {
                subFolders.Add(folder);
            }

            AddFilesToList(Directory.EnumerateFiles(selectFolder, "*.*").ToList(), ref files);

            foreach (string f in subFolders)
            {
                AddFilesToList(Directory.EnumerateFiles(f).ToList(), ref files);

                //foreach (string file in Directory.EnumerateFiles(f, "*.*"))
                //{
                //    FileInfo fileInfo = new FileInfo(file);
                //    files.Add(new FileModel
                //    {
                //        Name = fileInfo.Name,
                //        Type = GetFileType(fileInfo.Extension),
                //        Path = fileInfo.FullName
                //    });
                //}
            }
        }

        public static void RemoveFolderFromList(string selectFolder, ref ObservableCollection<string> folders, ref ObservableCollection<FileModel> files)
        {
            List<FileModel> fList = files.ToList();
            fList.RemoveAll(f => f.Path.Contains(selectFolder));
            files.Clear();
            foreach (FileModel fl in fList)
            {
                files.Add(fl);
            }
            folders.Remove(selectFolder);
        }

        public static string GenerateRandomKey(int len)
        {
            const string valid = @"abcdefghijklmnopqrstuvwxyz!@#$%^&*()|\/-+=ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
            StringBuilder res = new StringBuilder();
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] uintBuffer = new byte[sizeof(uint)];
                while (len-- > 0)
                {
                    rng.GetBytes(uintBuffer);
                    uint num = BitConverter.ToUInt32(uintBuffer, 0);
                    res.Append(valid[(int)(num % (uint)valid.Length)]);
                    //res.Append(valid[GetInt(rng, valid.Length)]);
                }
            }
            return res.ToString();
        }

        public static string GetFileType(string fileExt)
        {
            RegistryKey regKey = Registry.ClassesRoot;
            RegistryKey extKey = regKey.OpenSubKey(fileExt);
            string[] valueNames = extKey?.GetValueNames();
            if (valueNames == null) return string.Empty;
            if (valueNames.Contains("PerceivedType"))
                return extKey.GetValue("PerceivedType").ToString();
            if (valueNames.Contains("ContentType"))
                return extKey.GetValue("ContentType").ToString().Split('\\')[0];

            return string.Empty;
        }

        public static void EncryptFiles(string usbSerial, string encryptPW, ObservableCollection<FileModel> fileList)
        {
            foreach (FileModel file in fileList)
            {
                string savePath = Path.GetDirectoryName(file.Path);
                EncryptFile(usbSerial, encryptPW, file, savePath);
            }
        }

        public static void EncryptFile(string usbSerial, string pw, FileModel file, string savepath)
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            IList<string> keys = new List<string> { usbSerial, pw, nic.Id };
            if (string.IsNullOrEmpty(savepath))
                savepath = Path.GetDirectoryName(file.Path);

            savepath = Path.Combine(savepath, Path.GetFileName(file.Path) + ".aes");
            foreach (string key in keys)
            {
                byte[] inFile = File.ReadAllBytes(file.Path);
                AES.SetDefaultKey(key);
                try
                {
                    using (FileStream fs = File.Open(savepath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] data = AES.Encrypt(inFile);
                        fs.Seek(0, SeekOrigin.Begin);
                        fs.Write(data, 0, data.Length);
                        fs.Close();
                    }
                    file.Result = "Encrypted";
                    file.Path = savepath;
                }
                catch (Exception ex)
                {
                    file.Result = "Failed";
                }
            }
        }

        public static void DecryptFiles(string usbSerial, string encryptPW, ObservableCollection<FileModel> fileList)
        {
            foreach (FileModel fm in fileList)
            {
                string savePath = Path.GetDirectoryName(fm.Path);
                DecryptFile(usbSerial, encryptPW, fm, savePath);
            }
        }

        public static void DecryptFile(string usbSerial, string encryptPW, FileModel file, string savepath)
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
            List<string> keys = new List<string> { nic.Id, encryptPW, usbSerial };
            if (string.IsNullOrEmpty(savepath))
                savepath = Path.GetDirectoryName(file.Path);

            savepath = Path.Combine(savepath, Path.GetFileNameWithoutExtension(file.Path));
            foreach (string key in keys)
            {
                AES.SetDefaultKey(key);
                byte[] inFile = File.ReadAllBytes(file.Path);
                try
                {
                    using (FileStream fs = File.Open(savepath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] data = AES.Decrypt(inFile);
                        fs.Seek(0, SeekOrigin.Begin);
                        fs.Write(data, 0, data.Length);
                        fs.Close();
                    }
                    file.Result = "Decrypted";
                    file.Path = savepath;
                }
                catch (Exception)
                {
                    file.Result = "Failed";
                }
            }
        }
    }
}
