using System;
using Keepfall.Core.State;
using Newtonsoft.Json;

namespace Keepfall.Core.Save
{
    /// <summary>
    /// Serializes and deserializes <see cref="PlayerState"/> via Newtonsoft, talking to an
    /// <see cref="ISaveStore"/> for persistence. Schema-version aware: on load it reads the
    /// stored <c>schemaVersion</c> and runs forward migrations up to
    /// <see cref="PlayerState.CurrentSchemaVersion"/>. Pure logic apart from the injected
    /// store, so it is fully testable in EditMode with an in-memory store.
    /// </summary>
    public sealed class SaveSystem
    {
        private static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings
            {
                // ISO-8601 with offset keeps DateTimeOffset round-trips exact, which the
                // tile-accrual "survives restart" math depends on.
                DateParseHandling = DateParseHandling.DateTimeOffset,
                DateFormatHandling = DateFormatHandling.IsoDateFormat,
                NullValueHandling = NullValueHandling.Include,
                Formatting = Formatting.None,
            };

        private readonly ISaveStore _store;

        /// <summary>Wraps a storage backend (local file, in-memory, etc.).</summary>
        public SaveSystem(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>
        /// Loads and migrates the player save. Returns a fresh default
        /// <see cref="PlayerState"/> when nothing is stored yet. Throws
        /// <see cref="SaveCorruptException"/> if stored data exists but cannot be parsed.
        /// </summary>
        public PlayerState Load()
        {
            string json = _store.Load();
            if (string.IsNullOrEmpty(json))
            {
                return new PlayerState();
            }

            PlayerState state;
            try
            {
                state = JsonConvert.DeserializeObject<PlayerState>(json, SerializerSettings);
            }
            catch (JsonException ex)
            {
                throw new SaveCorruptException("Save data could not be parsed.", ex);
            }

            if (state == null)
            {
                throw new SaveCorruptException("Save data deserialized to null.");
            }

            Migrate(state);
            return state;
        }

        /// <summary>Serializes and persists the player save at the current schema version.</summary>
        public void Save(PlayerState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            state.SchemaVersion = PlayerState.CurrentSchemaVersion;
            string json = JsonConvert.SerializeObject(state, SerializerSettings);
            _store.Save(json);
        }

        /// <summary>
        /// Applies forward migrations so an older save loads cleanly under the current schema.
        /// Each version step is its own block; today there is only v1, so this is a no-op
        /// other than stamping the version. Future steps append here.
        /// </summary>
        private static void Migrate(PlayerState state)
        {
            if (state.SchemaVersion > PlayerState.CurrentSchemaVersion)
            {
                // Save written by a NEWER build than this client. Refuse rather than silently
                // dropping unknown fields, which would corrupt the player's progress.
                throw new SaveCorruptException(
                    $"Save schemaVersion {state.SchemaVersion} is newer than this build " +
                    $"supports ({PlayerState.CurrentSchemaVersion}).");
            }

            // Example future migration:
            // if (state.SchemaVersion < 2) { /* v1 -> v2 field moves */ state.SchemaVersion = 2; }

            state.SchemaVersion = PlayerState.CurrentSchemaVersion;
        }
    }

    /// <summary>Thrown when stored save data exists but is unreadable or from a newer build.</summary>
    public sealed class SaveCorruptException : Exception
    {
        /// <summary>Creates the exception with a message.</summary>
        public SaveCorruptException(string message)
            : base(message)
        {
        }

        /// <summary>Creates the exception with a message and inner cause.</summary>
        public SaveCorruptException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
