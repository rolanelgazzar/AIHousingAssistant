using AIHousingAssistant.Models;
using Qdrant.Client.Grpc;

namespace AIHousingAssistant.Helper
{
    public static class SearchHelper
    {
        public static List<string> ExtractKeywords(string text)
        {
            return text
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .Select(w => w.ToLowerInvariant())
                .Distinct()
                .ToList();
        }

        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length == 0 || b.Length == 0)
                return 0f;

            if (a.Length != b.Length)
                return 0f;

            float dot = 0, normA = 0, normB = 0;

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            const float eps = 1e-6f;
            if (normA < eps || normB < eps)
                return 0f;

            return dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        // Change the access modifier to 'public' or 'internal' if needed

        public static float[] ExtractEmbeddingFromVectorsOutput(object? vectorsOutput)
        {
            if (vectorsOutput == null)
                return Array.Empty<float>();

            // Case 1: Try to access single vector (Vector property)
            var vectorProp = vectorsOutput.GetType().GetProperty("Vector");
            var vectorObj = vectorProp?.GetValue(vectorsOutput);
            var data = ExtractFloatData(vectorObj);
            if (data.Length > 0)
                return data;

            // Case 2: Try to access named vectors (Vectors property)
            var vectorsProp = vectorsOutput.GetType().GetProperty("Vectors");
            var vectorsObj = vectorsProp?.GetValue(vectorsOutput);
            if (vectorsObj == null)
                return Array.Empty<float>();

            // Handle if Vectors is a collection like Dictionary or List
            if (vectorsObj is System.Collections.IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    var valueProp = item?.GetType().GetProperty("Value");
                    var valueObj = valueProp?.GetValue(item);
                    var data2 = ExtractFloatData(valueObj);
                    if (data2.Length > 0)
                        return data2;
                }
            }

            // Case 3: If vectorsObj has an inner collection, try extracting its data
            var innerVectorsProp = vectorsObj.GetType().GetProperty("Vectors");
            var innerVectorsObj = innerVectorsProp?.GetValue(vectorsObj);
            if (innerVectorsObj is System.Collections.IEnumerable innerEnumerable)
            {
                foreach (var item in innerEnumerable)
                {
                    var valueProp = item?.GetType().GetProperty("Value");
                    var valueObj = valueProp?.GetValue(item);
                    var data2 = ExtractFloatData(valueObj);
                    if (data2.Length > 0)
                        return data2;
                }
            }

            return Array.Empty<float>();
        }

        private static float[] ExtractFloatData(object? vectorObj)
        {
            if (vectorObj == null)
                return Array.Empty<float>();

            // Extract the 'Data' property, which is usually the vector data
            var dataProp = vectorObj.GetType().GetProperty("Data");
            var dataObj = dataProp?.GetValue(vectorObj);

            if (dataObj is IEnumerable<float> floats)
                return floats.ToArray();

            return Array.Empty<float>();
        }



    }

}
