using AIHousingAssistant.Application.Enum;
using AIHousingAssistant.Application.Services.Interfaces;
using AIHousingAssistant.Application.Services.VectorDb;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public interface IVectorDB_Resolver
    {
        IVectorDB Resolve(VectorDbProvider provider);
    }
    public class VectorDB_Resolver : IVectorDB_Resolver
    {
        private readonly QdrantVectorDb_Http _qdrantHttp;
        private readonly QdrantVectorDb_Sdk _qdrantSdk;
      //  private readonly QDrantVectorStore_SK _qdrantSk;

        public VectorDB_Resolver(
            QdrantVectorDb_Http qdrantHttp,
           QdrantVectorDb_Sdk qdrantSdk)
         //   QDrantVectorStore_SK qdrantSk)
        {
            _qdrantSdk = qdrantSdk;
            _qdrantHttp= qdrantHttp;
          //  _qdrantSk = qdrantSk;s
        }

        public IVectorDB Resolve(VectorDbProvider provider)
        {
            return provider switch
            {
                //VectorStoreProvider.InMemory => _inMemory,
                VectorDbProvider.QdrantSdk => _qdrantSdk,
                VectorDbProvider.QdrantHttp => _qdrantHttp,
                _ => _qdrantHttp
            };
        }
    }

}
