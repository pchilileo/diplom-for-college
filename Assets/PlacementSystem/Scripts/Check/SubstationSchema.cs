using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// Reference wiring of a substation: which equipment exists and which
    /// connector of which object is wired to which. Positions are stored only
    /// to help match identical equipment (e.g. three disconnectors) when checking.
    /// </summary>
    [Serializable]
    public class SubstationSchema
    {
        public int version = 1;
        public string createdUtc;
        public List<SchemaObject> objects = new();
        public List<SchemaWire> wires = new();
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

    /// <summary>
    /// Reads and writes schema files (*.substation).
    ///
    /// Layout: "PSCH" | version byte | 16-byte IV | AES-256-CBC(JSON) | HMAC-SHA256.
    /// The content can't be read in a text editor, and any change to the file
    /// breaks the HMAC so it is rejected on load. The key lives in the program,
    /// so this protects against ordinary snooping and editing, not against
    /// someone decompiling the trainer.
    /// </summary>
    public static class SchemaFile
    {
        public const string Extension = "substation";

        private static readonly byte[] Magic = { (byte)'P', (byte)'S', (byte)'C', (byte)'H' };
        private const byte FormatVersion = 1;
        private const int IvLength = 16;
        private const int MacLength = 32;

        // Keys are derived from an embedded secret. Change it to invalidate all old files.
        private const string Secret = "PlacementSystem.Substation.Check/v1/4f1c9a7e-2b8d-4e61-9c35-7a0d2e8b51f6";
        private static readonly byte[] EncryptionKey = Derive("enc");
        private static readonly byte[] MacKey = Derive("mac");

        public static void Write(string path, SubstationSchema schema)
        {
            var plain = Encoding.UTF8.GetBytes(JsonUtility.ToJson(schema));

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
            stream.Write(Magic, 0, Magic.Length);
            stream.WriteByte(FormatVersion);
            stream.Write(iv, 0, iv.Length);
            stream.Write(cipher, 0, cipher.Length);

            var body = stream.ToArray();
            var mac = ComputeMac(body, body.Length);

            var result = new byte[body.Length + mac.Length];
            Buffer.BlockCopy(body, 0, result, 0, body.Length);
            Buffer.BlockCopy(mac, 0, result, body.Length, mac.Length);

            File.WriteAllBytes(path, result);
        }

        /// <exception cref="InvalidDataException">The file is not a schema, is damaged or was modified.</exception>
        public static SubstationSchema Read(string path)
        {
            var data = File.ReadAllBytes(path);

            var headerLength = Magic.Length + 1;
            if (data.Length < headerLength + IvLength + 16 + MacLength)
                throw new InvalidDataException("Файл повреждён или не является схемой подстанции.");

            for (var i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                    throw new InvalidDataException("Файл не является схемой подстанции.");
            }

            if (data[Magic.Length] != FormatVersion)
                throw new InvalidDataException("Схема сохранена в другой версии тренажёра.");

            var bodyLength = data.Length - MacLength;
            var expected = ComputeMac(data, bodyLength);
            if (!FixedTimeEquals(expected, data, bodyLength))
                throw new InvalidDataException("Файл схемы был изменён или повреждён.");

            var iv = new byte[IvLength];
            Buffer.BlockCopy(data, headerLength, iv, 0, IvLength);
            var cipherOffset = headerLength + IvLength;
            var cipherLength = bodyLength - cipherOffset;

            byte[] plain;
            try
            {
                using var aes = CreateAes();
                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                plain = decryptor.TransformFinalBlock(data, cipherOffset, cipherLength);
            }
            catch (CryptographicException)
            {
                throw new InvalidDataException("Не удалось расшифровать файл схемы.");
            }

            var schema = JsonUtility.FromJson<SubstationSchema>(Encoding.UTF8.GetString(plain));
            if (schema == null || schema.objects == null || schema.wires == null)
                throw new InvalidDataException("Файл схемы повреждён.");

            return schema;
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

        private static bool FixedTimeEquals(byte[] expected, byte[] data, int offset)
        {
            var diff = 0;
            for (var i = 0; i < expected.Length; i++)
                diff |= expected[i] ^ data[offset + i];
            return diff == 0;
        }

        private static byte[] Derive(string purpose)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(Secret + "/" + purpose));
        }
    }
}
