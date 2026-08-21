using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CitizenFX.Core;

namespace FxEvents.Shared.Encryption
{
	public static class Encryption
	{
		#region Byte encryption
		private static byte[] GenerateIV()
		{
			byte[] rgbIV = new byte[16];
			RandomNumberGenerator.Fill(rgbIV);
			return rgbIV;
		}

		private static byte[] EncryptBytes(byte[] data, object input)
		{
			byte[] rgbIV = GenerateIV();
			byte[] keyBytes = input switch
			{
				int sourceId => EventHub.Gateway.GetSecret(sourceId),
				string strKey => GenerateHash(strKey),
				_ => throw new ArgumentException("Input must be an int or a string.", nameof(input)),
			};

			using Aes aesAlg = Aes.Create();
			aesAlg.Key = keyBytes;
			aesAlg.IV = rgbIV;

			using ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);
			using MemoryStream msEncrypt = new MemoryStream();

			msEncrypt.Write(rgbIV, 0, rgbIV.Length);
			using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
			{
				csEncrypt.Write(data, 0, data.Length);
				csEncrypt.FlushFinalBlock();
			}

			return msEncrypt.ToArray();
		}

		private static byte[] DecryptBytes(byte[] data, object input)
		{
			if (data == null || data.Length < 16)
				throw new ArgumentException("Encrypted data is invalid or too short.", nameof(data));

			byte[] keyBytes = input switch
			{
				int sourceId => EventHub.Gateway.GetSecret(sourceId),
				string strKey => GenerateHash(strKey),
				_ => throw new ArgumentException("Input must be an int or a string.", nameof(input)),
			};

			using Aes aesAlg = Aes.Create();
			aesAlg.Key = keyBytes;

			byte[] rgbIV = new byte[16];
			Buffer.BlockCopy(data, 0, rgbIV, 0, 16);
			aesAlg.IV = rgbIV;

			using ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);
			using MemoryStream msDecrypt = new MemoryStream(data, 16, data.Length - 16);
			using CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
			using MemoryStream msDecrypted = new MemoryStream();

			csDecrypt.CopyTo(msDecrypted);
			return msDecrypted.ToArray();
		}
		#endregion

		internal static byte[] EncryptObject<T>(this T obj, int plySource = -1)
		{
			return EncryptBytes(obj.ToBytes(), plySource);
		}

		internal static T DecryptObject<T>(this byte[] data, int plySource = -1)
		{
			return DecryptBytes(data, plySource).FromBytes<T>();
		}

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

			// FIX: Chiamata a DecryptBytes anziché EncryptBytes
			return DecryptBytes(data, key).FromBytes<T>();
		}

		public static byte[] GenerateHash(string input)
		{
			return SHA256.HashData(Encoding.UTF8.GetBytes(input));
		}

		internal static async Task<Tuple<string, string>> GenerateKey()
		{
			string[] words = ["Scalder", "Suscipient", "Sodalite", "Maharanis", "Mussier", "Abouts", "Geologized", "Antivenins", "Volcanized", "Heliskier", "Bedclothes", "Streamier", "Postulant", "Grizzle", "Folkies", "Poplars", "Stalls", "Chiefess", "Trip", "Untarred", "Cadillacs", "Fixings", "Overage", "Upbraider", "Phocas", "Galton", "Pests", "Saxifraga", "Erodes", "Bracketing", "Rugs", "Deprecate", "Monomials", "Subtracts", "Kettledrum", "Cometic", "Wrvs", "Phalangids", "Vareuse", "Pinchbecks", "Moony", "Scissoring", "Sarks", "Victresses", "Thorned", "Bowled", "Bakeries", "Printable", "Beethoven", "Sacher"];

			int length = Random.Shared.Next(5, 10);
			StringBuilder passphraseBuilder = new StringBuilder();

			for (int i = 0; i <= length; i++)
			{
				await API.Delay(5);
				if (i > 0)
					passphraseBuilder.Append('-');

				passphraseBuilder.Append(words[Random.Shared.Next(words.Length)]);
			}

			string passphrase = passphraseBuilder.ToString();
			return new(passphrase, passphrase.EncryptObject(GetRandomString(Random.Shared.Next(30, 50))).BytesToString());
		}

		private static string GetRandomString(int size, bool lowerCase = false)
		{
			StringBuilder builder = new StringBuilder(size);
			char offset = lowerCase ? 'a' : 'A';
			const int lettersOffset = 26;

			for (int i = 0; i < size; i++)
			{
				char @char = (char)Random.Shared.Next(offset, offset + lettersOffset);
				builder.Append(@char);
			}

			return lowerCase ? builder.ToString().ToLower() : builder.ToString();
		}
	}
}