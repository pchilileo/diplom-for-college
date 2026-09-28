using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PlacementSystem
{
    public enum SchemaMode
    {
        /// <summary>Anyone can load the reference and check against it.</summary>
        Training = 0,
        /// <summary>Loading requires the teacher's password.</summary>
        Exam = 1
    }

    /// <summary>
    /// Reference wiring of a substation: which equipment exists and which
    /// connector of which object is wired to which. Positions are stored only
    /// to help match identical equipment (e.g. three disconnectors) when checking.
    /// </summary>
    [Serializable]
    public class SubstationSchema
    {
        public int version = 2;
        public string createdUtc;

        /// <summary>
        /// Short code (e.g. "A7K2") generated when the reference is saved. It is
        /// stored inside the encrypted file, so renaming the file does not change it.
        /// </summary>
        public string id;

        public SchemaMode mode = SchemaMode.Training;

        /// <summary>PBKDF2 hash of the exam password (Base64); empty for training.</summary>
        public string passwordHash;
        public string passwordSalt;

        public List<SchemaObject> objects = new();
        public List<SchemaWire> wires = new();

        public bool IsExam => mode == SchemaMode.Exam;
        public string DisplayId => string.IsNullOrEmpty(id) ? "—" : id;
        public string ModeName => IsExam ? "Экзамен" : "Тренировка";

        // ── ID ────────────────────────────────────────────────────────────────

        // No 0/O, 1/I/L — easy to read aloud and to copy by hand.
        private const string IdAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public static string GenerateId()
        {
            var bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            var chars = new char[4];
            for (var i = 0; i < 4; i++)
                chars[i] = IdAlphabet[bytes[i] % IdAlphabet.Length];
            return new string(chars);
        }

        // ── Exam password ─────────────────────────────────────────────────────

        private const int PasswordIterations = 20000;

        /// <summary>Stores a salted PBKDF2 hash — the password itself is never saved.</summary>
        public void SetPassword(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            passwordSalt = Convert.ToBase64String(salt);
            passwordHash = Convert.ToBase64String(HashPassword(password, salt));
        }

        public bool CheckPassword(string password)
        {
            if (!IsExam)
                return true;
            if (string.IsNullOrEmpty(passwordHash) || string.IsNullOrEmpty(passwordSalt) || password == null)
                return false;

            var expected = Convert.FromBase64String(passwordHash);
            var actual = HashPassword(password, Convert.FromBase64String(passwordSalt));

            var diff = expected.Length ^ actual.Length;
            for (var i = 0; i < Math.Min(expected.Length, actual.Length); i++)
                diff |= expected[i] ^ actual[i];
            return diff == 0;
        }

        private static byte[] HashPassword(string password, byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(password, salt, PasswordIterations);
            return kdf.GetBytes(32);
        }
    }

    [Serializable]
    public class SchemaObject
    {
        /// <summary>Name of the AssetData asset — identifies the equipment type.</summary>
        public string asset;
        /// <summary>Human-readable name for the report.</summary>
        public string displayName;
        /// <summary>Bottom-centre position relative to the centre of the whole layout.</summary>
        public Vector3 position;
    }

    [Serializable]
    public class SchemaWire
    {
        public int objectA;
        public int connectorA;
        public int objectB;
        public int connectorB;
    }

    /// <summary>Reads and writes reference files (*.substation).</summary>
    public static class SchemaFile
    {
        public const string Extension = "substation";

        private static readonly byte[] Magic = { (byte)'P', (byte)'S', (byte)'C', (byte)'H' };

        public static void Write(string path, SubstationSchema schema)
        {
            var plain = Encoding.UTF8.GetBytes(JsonUtility.ToJson(schema));
            File.WriteAllBytes(path, SecureContainer.Protect(Magic, plain));
        }

        /// <exception cref="InvalidDataException">The file is not a schema, is damaged or was modified.</exception>
        public static SubstationSchema Read(string path)
        {
            var plain = SecureContainer.Unprotect(Magic, File.ReadAllBytes(path), "схемой подстанции");

            var schema = JsonUtility.FromJson<SubstationSchema>(Encoding.UTF8.GetString(plain));
            if (schema == null || schema.objects == null || schema.wires == null)
                throw new InvalidDataException("Файл схемы повреждён.");

            return schema;
        }
    }

    /// <summary>
    /// Encrypted, tamper-proof byte container used for reference files and the
    /// check journal.
    ///
    /// Layout: magic (4 bytes) | version byte | 16-byte IV | AES-256-CBC(data) | HMAC-SHA256.
    /// The content can't be read in a text editor, and any change breaks the
    /// HMAC so the data is rejected. The key lives in the program, so this
    /// protects against ordinary snooping and editing, not against someone
    /// decompiling the trainer.
    /// </summary>
    public static class SecureContainer
    {
        private const byte FormatVersion = 1;
        private const int IvLength = 16;
        private const int MacLength = 32;

        // Keys are derived from an embedded secret. Change it to invalidate all old files.
        private const string Secret = "PlacementSystem.Substation.Check/v1/4f1c9a7e-2b8d-4e61-9c35-7a0d2e8b51f6";
        private static readonly byte[] EncryptionKey = Derive("enc");
        private static readonly byte[] MacKey = Derive("mac");

        public static byte[] Protect(byte[] magic, byte[] plain)
        {
            byte[] iv;
            byte[] cipher;
            using (var aes = CreateAes())
            {
                aes.GenerateIV();
                iv = aes.IV;
                using var encryptor = aes.CreateEncryptor();
                cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
            }

            using var stream = new MemoryStream();
            stream.Write(magic, 0, magic.Length);
            stream.WriteByte(FormatVersion);
            stream.Write(iv, 0, iv.Length);
            stream.Write(cipher, 0, cipher.Length);

            var body = stream.ToArray();
            var mac = ComputeMac(body, body.Length);

            var result = new byte[body.Length + mac.Length];
            Buffer.BlockCopy(body, 0, result, 0, body.Length);
            Buffer.BlockCopy(mac, 0, result, body.Length, mac.Length);
            return result;
        }

        /// <param name="what">For error messages: "Файл не является {what}."</param>
        /// <exception cref="InvalidDataException">Wrong type, damaged or modified data.</exception>
        public static byte[] Unprotect(byte[] magic, byte[] data, string what)
        {
            var headerLength = magic.Length + 1;
            if (data.Length < headerLength + IvLength + 16 + MacLength)
                throw new InvalidDataException($"Файл повреждён или не является {what}.");

            for (var i = 0; i < magic.Length; i++)
            {
                if (data[i] != magic[i])
                    throw new InvalidDataException($"Файл не является {what}.");
            }

            if (data[magic.Length] != FormatVersion)
                throw new InvalidDataException("Файл сохранён в другой версии тренажёра.");

            var bodyLength = data.Length - MacLength;
            var expected = ComputeMac(data, bodyLength);
            var diff = 0;
            for (var i = 0; i < MacLength; i++)
                diff |= expected[i] ^ data[bodyLength + i];
            if (diff != 0)
                throw new InvalidDataException("Файл был изменён или повреждён.");

            var iv = new byte[IvLength];
            Buffer.BlockCopy(data, headerLength, iv, 0, IvLength);
            var cipherOffset = headerLength + IvLength;

            try
            {
                using var aes = CreateAes();
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                return decryptor.TransformFinalBlock(data, cipherOffset, bodyLength - cipherOffset);
            }
            catch (CryptographicException)
            {
                throw new InvalidDataException("Не удалось расшифровать файл.");
            }
        }

        /// <summary>Short fingerprint of some data (used to detect a swapped journal).</summary>
        public static string Fingerprint(byte[] data)
        {
            using var hmac = new HMACSHA256(MacKey);
            return Convert.ToBase64String(hmac.ComputeHash(data), 0, 16);
        }

        private static Aes CreateAes()
        {
            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Key = EncryptionKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        private static byte[] ComputeMac(byte[] data, int length)
        {
            using var hmac = new HMACSHA256(MacKey);
            return hmac.ComputeHash(data, 0, length);
        }

        private static byte[] Derive(string purpose)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(Secret + "/" + purpose));
        }
    }
}
