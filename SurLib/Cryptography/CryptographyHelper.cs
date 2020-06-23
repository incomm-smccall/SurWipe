using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SurLib.Cryptography
{
    public static class CryptographyHelper
    {
        [MethodImpl(MethodImplOptions.NoOptimization)]
        public static bool AreEqual(byte[] a1, byte[] a2)
        {
            bool result = true;
            for (int i = 0; i < a1.Length; ++i)
            {
                if (a1[i] != a2[i])
                    result = false;
            }
            return result;
        }

        public static void DeriveKeys(string password, out string key, out string authKey)
        {
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(password, AES.Salt, 50000))
            {
                key = Convert.ToBase64String(derive.GetBytes(16));
                authKey = Convert.ToBase64String(derive.GetBytes(64));
            }
        }
    }
}
