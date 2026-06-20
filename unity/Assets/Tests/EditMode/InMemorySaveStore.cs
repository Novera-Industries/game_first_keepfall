using Keepfall.Core.Save;

namespace Keepfall.Tests
{
    /// <summary>Engine-free <see cref="ISaveStore"/> for EditMode tests (no filesystem).</summary>
    public sealed class InMemorySaveStore : ISaveStore
    {
        private string _json;

        /// <summary>The currently stored blob (null until first save).</summary>
        public string Stored => _json;

        /// <inheritdoc />
        public string Load() => _json;

        /// <inheritdoc />
        public void Save(string json) => _json = json;
    }
}
