namespace Keepfall.Core.Save
{
    /// <summary>
    /// Storage abstraction for the serialized save blob. Keeps the JSON layer (
    /// <see cref="SaveSystem"/>) independent of where bytes live, so EditMode tests can use
    /// an in-memory store while the device uses the filesystem, and the backend cloud-save
    /// can reuse the same JSON contract.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>Returns the stored JSON, or <c>null</c> if nothing has been saved yet.</summary>
        string Load();

        /// <summary>Writes the JSON blob, overwriting any previous save.</summary>
        void Save(string json);
    }
}
