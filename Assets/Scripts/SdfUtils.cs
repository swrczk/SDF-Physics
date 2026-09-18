using Unity.Mathematics;

namespace DefaultNamespace
{
    /// <summary>
    /// Wspólna konwencja adresowania siatki SDF - używana zarówno przy bake'u
    /// (Editor, zwykły C#) jak i przy próbkowaniu w Burst-owym jobie w runtime.
    /// Trzymanie tego w jednym miejscu gwarantuje, że obie strony zawsze zgadzają
    /// się co do układu danych (rozjazd w tej konwencji - odwrócona kolejność
    /// pętli x/y/z przy zapisie - był wcześniej źródłem realnego buga).
    /// </summary>
    public static class SdfUtils
    {
        /// <summary>
        /// Spłaszcza indeks siatki (x,y,z) do indeksu liniowego w tablicy/Texture3D.
        /// X jest osią najszybciej zmienną - to musi się zgadzać z kolejnością,
        /// w jakiej SdfBaker zapisuje dane do tablicy przed SetPixelData.
        /// </summary>
        public static int Flatten(int x, int y, int z, int resX, int resY) =>
            x + y * resX + z * resX * resY;

        /// <summary>
        /// Środek komórki siatki (x,y,z) we współrzędnych świata.
        /// </summary>
        public static float3 GridToWorld(int x, int y, int z, float3 boundsMin, float cellSize) =>
            boundsMin + (new float3(x, y, z) + 0.5f) * cellSize;

        /// <summary>
        /// Ciągła (nie zaokrąglona) współrzędna siatki dla danego punktu świata -
        /// odwrotność GridToWorld. Używana do trylinearnego próbkowania w SdfSampler.
        /// </summary>
        public static float3 WorldToGrid(float3 worldPos, float3 boundsMin, float cellSize) =>
            (worldPos - boundsMin) / cellSize - 0.5f;
    }
}
