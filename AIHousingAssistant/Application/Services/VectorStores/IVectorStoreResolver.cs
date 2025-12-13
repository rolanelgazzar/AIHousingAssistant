using AIHousingAssistant.Application.Enum;

namespace AIHousingAssistant.Application.Services.VectorStores
{
    public interface IVectorStoreResolver
    {
        IVectorStore Resolve(VectorStoreProvider provider);
    }

}
