using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace PlacementSystem
{
    /// <summary>
    /// A saved project: every placed object with its exact transform, every
    /// wire (object / connector indices) and the camera view.
    /// </summary>
    [Serializable]
    public class ProjectData
    {
        public int version = 1;
        public string savedUtc;
        public List<ProjectObject> objects = new();
        public List<SchemaWire> wires = new();

        public bool hasCamera;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation = Quaternion.identity;
    }

    [Serializable]
    public class ProjectObject
    {
        /// <summary>Name of the AssetData asset — identifies the equipment type.</summary>
        public string asset;
        /// <summary>For messages when the equipment no longer exists.</summary>
        public string displayName;
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;
    }

    /// <summary>
    /// Reads and writes project files (*.substation-save). Same protection as
    /// reference files: encrypted, and any change outside the program makes the
    /// file invalid (see <see cref="SecureContainer"/>).
    /// </summary>
    public static class ProjectFile
    {
        public const string Extension = "substation-save";

        private static readonly byte[] Magic = { (byte)'P', (byte)'S', (byte)'A', (byte)'V' };

        public static void Write(string path, ProjectData data)
        {
            var plain = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
            var bytes = SecureContainer.Protect(Magic, plain);

            // Write next to the target first: a failure half-way must not destroy the previous save.
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path))
                File.Delete(path);
            File.Move(temp, path);
        }

        /// <exception cref="InvalidDataException">Not a project file, damaged or modified.</exception>
        public static ProjectData Read(string path)
        {
            var plain = SecureContainer.Unprotect(Magic, File.ReadAllBytes(path), "FILE_NOT_PROJECT");

            var data = JsonUtility.FromJson<ProjectData>(Encoding.UTF8.GetString(plain));
            if (data == null || data.objects == null || data.wires == null)
                throw new InvalidDataException(Loc.Get("FILE_PROJECT_DAMAGED"));

            return data;
        }
    }
}
