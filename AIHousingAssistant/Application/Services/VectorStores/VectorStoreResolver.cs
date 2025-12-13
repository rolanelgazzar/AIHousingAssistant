using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public class VectorStoreResolver : IVectorStoreResolver
    {
        private readonly InMemoryVectorStore _inMemory;
        private readonly QDrantVectorStore_Sdk _qdrantSdk;
        private readonly QDrantVectorStore_SK _qdrantSk;

        public VectorStoreResolver(
            InMemoryVectorStore inMemory,
            QDrantVectorStore_Sdk qdrantSdk,
            QDrantVectorStore_SK qdrantSk)
        {
            _inMemory = inMemory;
            _qdrantSdk = qdrantSdk;
            _qdrantSk = qdrantSk;
        }

        public IVectorStore Resolve(VectorStoreProvider provider)
        {
            return provider switch
            {
                VectorStoreProvider.InMemory => _inMemory,
                VectorStoreProvider.QdrantSdk => _qdrantSdk,
                VectorStoreProvider.QdrantSemanticKernel => _qdrantSk,
                _ => _inMemory
            };
        }
    }

}
