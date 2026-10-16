using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace FxEvents.Shared.Encryption
{
	public static class Encryption
	{
		private static readonly Random random = new Random();

		#region Byte encryption
		private static byte[] GenerateIV()
		{
			byte[] rgbIV = new byte[16];
			lock (random)
			{
				random.NextBytes(rgbIV);
			}
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
			using AesManaged aesAlg = new AesManaged
			{
				Key = keyBytes,
				IV = rgbIV
			};

			ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

			using MemoryStream msEncrypt = new MemoryStream();
			msEncrypt.Write(rgbIV, 0, rgbIV.Length);
			using CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
			csEncrypt.Write(data, 0, data.Length);
			csEncrypt.FlushFinalBlock();

			return msEncrypt.ToArray();
		}

		private static byte[] DecryptBytes(byte[] data, object input)
		{
			if (data == null || data.Length < 16)
				throw new ArgumentException("Encrypted data is invalid or too short.", nameof(data));

			byte[] rgbIV = new byte[16];
			Buffer.BlockCopy(data, 0, rgbIV, 0, 16);

			byte[] keyBytes = input switch
			{
				int sourceId => EventHub.Gateway.GetSecret(sourceId),
				string strKey => GenerateHash(strKey),
				_ => throw new ArgumentException("Input must be an int or a string.", nameof(input)),
			};
			using AesManaged aesAlg = new AesManaged
			{
				Key = keyBytes,
				IV = rgbIV
			};

			ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

			using MemoryStream msDecrypt = new MemoryStream(data, 16, data.Length - 16, writable: false);
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

		/// <summary>
		/// Encrypt the object.
		/// </summary>
		/// <typeparam name="T"/>
		/// <param name="obj">The object to encrypt.</param>
		/// <param name="key">The string key to encrypt it.</param>
		/// <exception cref="Exception"></exception>
		/// <returns>An encrypted array of byte</returns>
		public static byte[] EncryptObject<T>(this T obj, string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new Exception("FXEvents: Encryption key cannot be empty!");
			return EncryptBytes(obj.ToBytes(), key);
		}

		/// <summary>
		/// Decrypt the object.
		/// </summary>
		/// <typeparam name="T"/>
		/// <param name="data">The data to decrypt.</param>
		/// <param name="key">The key to decrypt it (MUST BE THE SAME AS THE ENCRYPTION KEY).</param>
		/// <exception cref="Exception"></exception>
		/// <returns>A <typeparamref name="T"/></returns>
		public static T DecryptObject<T>(this byte[] data, string key)
		{
			if (string.IsNullOrWhiteSpace(key))
				throw new Exception("FXEvents: Encryption key cannot be empty!");
			return DecryptBytes(data, key).FromBytes<T>();
		}

		/// <summary>
		/// Generate the Sha-256 hash of the given input string.
		/// </summary>
		/// <param name="input">The input string.</param>
		/// <returns>The generated hash in byte[]</returns>
		public static byte[] GenerateHash(string input)
		{
			using SHA256Managed sha256 = new SHA256Managed();
			return sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
		}

		internal static async Task<Tuple<string, string>> GenerateKey()
		{
			string[] words = ["Scalder", "Suscipient", "Sodalite", "Maharanis", "Mussier", "Abouts", "Geologized", "Antivenins", "Volcanized", "Heliskier", "Bedclothes", "Streamier", "Postulant", "Grizzle", "Folkies", "Poplars", "Stalls", "Chiefess", "Trip", "Untarred", "Cadillacs", "Fixings", "Overage", "Upbraider", "Phocas", "Galton", "Pests", "Saxifraga", "Erodes", "Bracketing", "Rugs", "Deprecate", "Monomials", "Subtracts", "Kettledrum", "Cometic", "Wrvs", "Phalangids", "Vareuse", "Pinchbecks", "Moony", "Scissoring", "Sarks", "Victresses", "Thorned", "Bowled", "Bakeries", "Printable", "Beethoven", "Sacher"];
			int i = 0;
			int length;
			lock (random)
			{
				length = random.Next(5, 10);
			}

			string passfrase = "";
			while (i <= length)
			{
				await BaseScript.Delay(5);
				string symbol = "";
				if (i > 0)
					symbol = "-";

				string word;
				lock (random)
				{
					word = words[random.Next(words.Length - 1)];
				}

				passfrase += symbol + word;
				i++;
			}

			int keyLength;
			lock (random)
			{
				keyLength = random.Next(30, 50);
			}

			return new(passfrase, passfrase.EncryptObject(GetRandomString(keyLength)).BytesToString());
		}

		private static string GetRandomString(int size, bool lowerCase = false)
		{
			StringBuilder builder = new StringBuilder(size);
			char offset = lowerCase ? 'a' : 'A';
			const int lettersOffset = 26;

			for (int i = 0; i < size; i++)
			{
				char @char;
				lock (random)
				{
					@char = (char)random.Next(offset, offset + lettersOffset);
				}
				builder.Append(@char);
			}

			return lowerCase ? builder.ToString().ToLower() : builder.ToString();
		}
	}
}