using System;
using System.IO;
using UnityEngine;

namespace Keepfall.Core.Save
{
    /// <summary>
    /// On-device <see cref="ISaveStore"/> backed by a file under
    /// <see cref="Application.persistentDataPath"/>. Writes atomically (temp file + replace)
    /// so a crash mid-write cannot leave a half-written, unparseable save. This is the only
    /// type in the save layer that touches UnityEngine; all JSON/migration logic in
    /// <see cref="SaveSystem"/> stays engine-free and unit-testable.
    /// </summary>
    public sealed class LocalSaveStore : ISaveStore
    {
        private const string DefaultFileName = "keepfall_player.json";

        private readonly string _path;

        /// <summary>Uses the default save file under persistentDataPath.</summary>
        public LocalSaveStore()
            : this(DefaultFileName)
        {
        }

        /// <summary>Uses a named save file under persistentDataPath (e.g. for slots/tests).</summary>
        public LocalSaveStore(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("Save file name is required.", nameof(fileName));
            }

            _path = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        }

        /// <summary>Absolute path of the save file.</summary>
        public string FilePath => _path;

        /// <inheritdoc />
        public string Load()
        {
            return File.Exists(_path) ? File.ReadAllText(_path) : null;
        }

        /// <inheritdoc />
        public void Save(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            string tempPath = _path + ".tmp";
            File.WriteAllText(tempPath, json);

            // Atomic replace where supported; fall back to delete+move on first write.
            if (File.Exists(_path))
            {
                File.Replace(tempPath, _path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, _path);
            }
        }
    }
}
