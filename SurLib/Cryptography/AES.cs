using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SurLib.Cryptography
{
    public static class AES
    {
        private const int IvLength = 16;
        private const int HmacSha256Length = 32;
        private static byte[] _defaultKey;
        private static byte[] _defaultAuthKey;

        public static readonly byte[] Salt =
        {
            0xBF, 0xEB, 0x1E, 0x56, 0xFB, 0xCD, 0x97, 0x3B, 0xB2, 0x19, 0x2, 0x24, 0x30, 0xA5, 0x78, 0x43, 0x0, 0x3D, 0x56,
            0x44, 0xD2, 0x1E, 0x62, 0xB9, 0xD4, 0xF1, 0x80, 0xE7, 0xE6, 0xC3, 0x39, 0x41
        };

        public static void SetDefaultKey(string usbkey, string password)
        {
            var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && ni.Name == "Wi-Fi");
            string sysMac = nic.Id;
            string usbSerial = usbkey;

            if (!string.IsNullOrEmpty(sysMac) && !string.IsNullOrEmpty(usbSerial))
            {
                DeriveKeys(sysMac + usbSerial + password);
            }
        }

        public static void SetDefaultKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            DeriveKeys(key);
        }

        public static string GetDriveKeys(string passphrase)
        {
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(passphrase, Salt, 50000))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Convert.ToBase64String(derive.GetBytes(16)));
                sb.Append(Convert.ToBase64String(derive.GetBytes(32)));
                return sb.ToString();
            }
        }

        public static void UnsetDefaultKey()
        {
            _defaultKey = new byte[0];
            _defaultAuthKey = new byte[0];
        }

        private static void DeriveKeys(string passphrase)
        {
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(passphrase, Salt, 50000))
            {
                _defaultKey = derive.GetBytes(16);
                _defaultAuthKey = derive.GetBytes(32);
            }
        }

        public static byte[] Encrypt(byte[] input)
        {
            if (_defaultKey == null || _defaultKey.Length == 0) throw new Exception("Key cannot be empty");
            if (input == null || input.Length == 0) return null;

            byte[] data = input, encdata = new byte[0];

            try
            {
                using (var ms = new MemoryStream())
                {
                    ms.Position = HmacSha256Length;
                    using (var aesProvider = new AesCryptoServiceProvider())
                    {
                        aesProvider.KeySize = 128;
                        aesProvider.BlockSize = 128;
                        aesProvider.Mode = CipherMode.CBC;
                        aesProvider.Padding = PaddingMode.PKCS7;
                        aesProvider.Key = _defaultKey;
                        aesProvider.GenerateIV();

                        using (var cs = new CryptoStream(ms, aesProvider.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            ms.Write(aesProvider.IV, 0, aesProvider.IV.Length);
                            cs.Write(data, 0, data.Length);
                            cs.FlushFinalBlock();

                            using (var hmac = new HMACSHA256(_defaultAuthKey))
                            {
                                byte[] hash = hmac.ComputeHash(ms.ToArray(), HmacSha256Length, ms.ToArray().Length - HmacSha256Length);
                                ms.Position = 0;
                                ms.Write(hash, 0, hash.Length);
                            }
                        }
                    }
                    encdata = ms.ToArray();
                }
            }
            catch (Exception)
            {
                return null;
            }
            return encdata;
        }

        public static byte[] Decrypt(byte[] input)
        {
            if (_defaultKey == null || _defaultKey.Length == 0) throw new Exception("Key cannot be empty");
            if (input == null || input.Length == 0) throw new Exception("Input cannot be empty");

            byte[] data = new byte[0];

            try
            {
                using (var ms = new MemoryStream(input))
                {
                    using (var aesProvider = new AesCryptoServiceProvider())
                    {
                        aesProvider.KeySize = 128;
                        aesProvider.BlockSize = 128;
                        aesProvider.Mode = CipherMode.CBC;
                        aesProvider.Padding = PaddingMode.PKCS7;
                        aesProvider.Key = _defaultKey;

                        // read first 32 bytes for HMAC
                        using (var hmac = new HMACSHA256(_defaultAuthKey))
                        {
                            var hash = hmac.ComputeHash(ms.ToArray(), HmacSha256Length, ms.ToArray().Length - HmacSha256Length);
                            byte[] receivedHash = new byte[HmacSha256Length];
                            ms.Read(receivedHash, 0, receivedHash.Length);

                            if (!CryptographyHelper.AreEqual(hash, receivedHash))
                                return data;
                        }

                        byte[] iv = new byte[IvLength];
                        ms.Read(iv, 0, IvLength); // read next 16 bytes for IV, followed by ciphertext
                        aesProvider.IV = iv;

                        using (var cs = new CryptoStream(ms, aesProvider.CreateDecryptor(), CryptoStreamMode.Read))
                        {
                            byte[] temp = new byte[ms.Length - IvLength + 1];
                            data = new byte[cs.Read(temp, 0, temp.Length)];
                            Buffer.BlockCopy(temp, 0, data, 0, data.Length);
                        }
                    }
                }
            }
            catch (Exception)
            {

            }
            return data;
        }
    }
}
