using Microsoft.Extensions.VectorData;

namespace Provider.Model
{
    public class InMemoryVectorStoreRecord
    {
        [VectorStoreKey]
        public string Id { get; set; } = string.Empty;

        [VectorStoreData]
        public string EmbeddingText { get; set; } = string.Empty;

        // Note that the vector property is typed as a string, and
        // its value is derived from the Text property. The string
        // value will however be converted to a vector on upsert and
        // stored in the database as a vector.
        [VectorStoreVector(1536)]
        public string Embedding => this.EmbeddingText;

        [VectorStoreData]
        public string Content { get; set; } = string.Empty;
    }
}
