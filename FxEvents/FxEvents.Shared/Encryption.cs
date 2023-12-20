using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FxEvents.Shared
{
    public static class Encryption
    {
        public static byte[] EncryptBytes(byte[] data, string strEncrKey)
        {
            byte[] rgbIV = new byte[16];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(rgbIV);
            }

            byte[] bytes;
            using (SHA256Managed sha256 = new SHA256Managed())
            {
                bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(strEncrKey));
            }

            using AesManaged aesAlg = new AesManaged();
            aesAlg.Key = bytes;
            aesAlg.IV = rgbIV;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msEncrypt = new MemoryStream();
            // prepend the IV to the encrypted data
            msEncrypt.Write(rgbIV, 0, rgbIV.Length);
            using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            csEncrypt.Write(data, 0, data.Length);
            csEncrypt.FlushFinalBlock();
            return msEncrypt.ToArray();
        }

        public static byte[] DecryptBytes(byte[] data, string sDecrKey)
        {
            byte[] rgbIV = new byte[16];
            Array.Copy(data, 0, rgbIV, 0, rgbIV.Length);

            byte[] bytes;
            using (SHA256Managed sha256 = new SHA256Managed())
            {
                bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sDecrKey));
            }

            using AesManaged aesAlg = new AesManaged();
            aesAlg.Key = bytes;
            aesAlg.IV = rgbIV;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msDecrypt = new MemoryStream(data, rgbIV.Length, data.Length - rgbIV.Length);
            using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
            using MemoryStream msDecrypted = new MemoryStream();
            csDecrypt.CopyTo(msDecrypted);
            return msDecrypted.ToArray();
        }
    }
}
