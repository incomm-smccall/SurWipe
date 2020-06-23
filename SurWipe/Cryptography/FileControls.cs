using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using SurWipe.Models;
using SurWipe.Utils;

namespace SurWipe.Cryptography
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
            Logging.LogMessage(LoggingLevel.Info, $"argfiles {string.Join(", ", argfiles)} files: {string.Join(", ", files)}");
        }

        public static void AddFolderToList(string selectFolder, ref ObservableCollection<string> folders, ref ObservableCollection<FileModel> files)
        {
            folders.Add(selectFolder);
            foreach(string folder in Directory.EnumerateDirectories(selectFolder))
            {
                folders.Add(folder);
            }

            foreach (string f in folders)
            {
                foreach (string file in Directory.EnumerateFiles(f, "*.*"))
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
    }
}
