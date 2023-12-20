using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FxEvents.Shared
{
    public static class Encryption
    {
        static readonly Random random = new Random(DateTime.Now.Millisecond);
        #region Byte encryption
        private static byte[] EncryptBytes(byte[] data, string strEncrKey)
        {
            byte[] rgbIV = new byte[16];
            using (RNGCryptoServiceProvider rng = new())
            {
                rng.GetBytes(rgbIV);
            }

            byte[] bytes;
            using (SHA256Managed sha256 = new())
            {
                bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(strEncrKey));
            }

            using AesManaged aesAlg = new();
            aesAlg.Key = bytes;
            aesAlg.IV = rgbIV;

            ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msEncrypt = new();
            // prepend the IV to the encrypted data
            msEncrypt.Write(rgbIV, 0, rgbIV.Length);
            using CryptoStream csEncrypt = new(msEncrypt, encryptor, CryptoStreamMode.Write);
            csEncrypt.Write(data, 0, data.Length);
            csEncrypt.FlushFinalBlock();
            return msEncrypt.ToArray();
        }

        private static byte[] DecryptBytes(byte[] data, string sDecrKey)
        {
            byte[] rgbIV = new byte[16];
            Array.Copy(data, 0, rgbIV, 0, rgbIV.Length);

            byte[] bytes;
            using (SHA256Managed sha256 = new())
            {
                bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sDecrKey));
            }

            using AesManaged aesAlg = new();
            aesAlg.Key = bytes;
            aesAlg.IV = rgbIV;

            ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

            using MemoryStream msDecrypt = new(data, rgbIV.Length, data.Length - rgbIV.Length);
            using CryptoStream csDecrypt = new(msDecrypt, decryptor, CryptoStreamMode.Read);
            using MemoryStream msDecrypted = new();
            csDecrypt.CopyTo(msDecrypted);
            return msDecrypted.ToArray();
        }
        #endregion

        public static byte[] EncryptObject<T>(this T obj, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new Exception("FXEvents: Encryption key cannot be empty!");
            return EncryptBytes(obj.ToBytes(), key);
        }

        public static T DecryptObject<T>(this byte[] data, string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new Exception("FXEvents: Encryption key cannot be empty!");
            return DecryptBytes(data, key).FromBytes<T>();
        }

        internal static async Task<Tuple<string, string>> GenerateKey()
        {
            string[] words = ["Scalder", "Suscipient", "Sodalite", "Maharanis", "Mussier", "Abouts", "Geologized", "Antivenins", "Volcanized", "Heliskier", "Bedclothes", "Streamier", "Postulant", "Grizzle", "Folkies", "Poplars", "Stalls", "Chiefess", "Trip", "Untarred", "Cadillacs", "Fixings", "Overage", "Upbraider", "Phocas", "Galton", "Pests", "Saxifraga", "Erodes", "Bracketing", "Rugs", "Deprecate", "Monomials", "Subtracts", "Kettledrum", "Cometic", "Wrvs", "Phalangids", "Vareuse", "Pinchbecks", "Moony", "Scissoring", "Sarks", "Victresses", "Thorned", "Bowled", "Bakeries", "Printable", "Beethoven", "Sacher"];
            int i = 0;
            int length = random.Next(5, 10);
            string passfrase = "";
            while (i <= length)
            {
                await BaseScript.Delay(5);
                string symbol = "";
                if (i > 0)
                    symbol = "-";
                passfrase += symbol + words[random.Next(words.Length - 1)];
                i++;
            }
            return new(passfrase, passfrase.EncryptObject(GetRandomString(random.Next(30, 50))).BytesToString());
        }

        private static string GetRandomString(int size, bool lowerCase = false)
        {
            StringBuilder builder = new StringBuilder(size);
            // Unicode/ASCII Letters are divided into two blocks
            // (Letters 65�90 / 97�122):
            // The first group containing the uppercase letters and
            // the second group containing the lowercase.  

            // char is a single Unicode character  
            char offset = lowerCase ? 'a' : 'A';
            const int lettersOffset = 26; // A...Z or a..z: length=26

            for (int i = 0; i < size; i++)
            {
                char @char = (char)random.Next(offset, offset + lettersOffset);
                builder.Append(@char);
            }

            return lowerCase ? builder.ToString().ToLower() : builder.ToString();
        }
    }
}