using Unity.Collections;
using Unity.Mathematics;

namespace DefaultNamespace
{
    public static class SdfSampler
    {
        public static float Sample(
            [ReadOnly] in NativeArray<float> data,
            int resX, int resY, int resZ,
            float3 boundsMin, float cellSize,
            float3 worldPos)
        {

            var gridPos = (worldPos - boundsMin) / cellSize - (float3)0.5;
            var clamped = math.clamp(gridPos, float3.zero, new float3(resX - 1, resY - 1, resZ - 1));
            var baseValue = math.floor(clamped);
            
            int x0 = (int)baseValue.x;
            int y0 = (int)baseValue.y;
            int z0 = (int)baseValue.z;
            
            int x1 = math.min(x0 + 1, resX - 1);
            int y1 = math.min(y0 + 1, resY - 1);
            int z1 = math.min(z0 + 1, resZ - 1);

            float v000 = data[Flatten(x0, y0, z0, resX, resY)];
            float v100 = data[Flatten(x1, y0, z0, resX, resY)];
            float v010 = data[Flatten(x0, y1, z0, resX, resY)];
            float v110 = data[Flatten(x1, y1, z0, resX, resY)];
            float v001 = data[Flatten(x0, y0, z1, resX, resY)];
            float v101 = data[Flatten(x1, y0, z1, resX, resY)];
            float v011 = data[Flatten(x0, y1, z1, resX, resY)];
            float v111 = data[Flatten(x1, y1, z1, resX, resY)];
            
            var frac = clamped - baseValue;
            
            float c00 = math.lerp(v000, v100, frac.x); // y=0, z=0
            float c10 = math.lerp(v010, v110, frac.x); // y=1, z=0
            float c01 = math.lerp(v001, v101, frac.x); // y=0, z=1
            float c11 = math.lerp(v011, v111, frac.x); // y=1, z=1
            
            float c0 = math.lerp(c00, c10, frac.y); // z=0
            float c1 = math.lerp(c01, c11, frac.y); // z=1
            
            return math.lerp(c0, c1, frac.z);

            // 1. zamień worldPos na ciągłą (nie zaokrągloną!) współrzędną siatki,
            //    tym samym wzorem co w bake'u: (worldPos - boundsMin) / cellSize - 0.5
            // 2. sklamruj do [0, res-1] w każdej osi (na razie: to nasza odpowiedź na
            //    "co zwrócić poza wolumenem" - wracamy do tego za chwilę)
            // 3. base = floor(gridPos), frac = gridPos - base (wagi interpolacji, 0..1)
            
            // 4. weź 8 wartości z data w narożnikach (base i base+1 w każdej osi,
            //    też sklamrowane do zakresu!), użyj tej samej formuły spłaszczania co w bake'u
            // 5. math.lerp trzykrotnie: najpierw po X (4 pary -> 4 wyniki),
            //    potem po Y (2 pary -> 2 wyniki), na końcu po Z (1 wynik)
        }
        private static int Flatten(int x, int y, int z, int resX, int resY) => x + y * resX + z * resX * resY;
        public static float3 Gradient(
            [ReadOnly] in NativeArray<float> data,
            int resX, int resY, int resZ,
            float3 boundsMin, float cellSize,
            float3 worldPos, float epsilon)
        {
            float dx = Sample(data, resX, resY, resZ, boundsMin, cellSize, worldPos + new float3(epsilon, 0, 0))
                       - Sample(data, resX, resY, resZ, boundsMin, cellSize, worldPos - new float3(epsilon, 0, 0));
            float dy = Sample(data, resX, resY, resZ, boundsMin, cellSize, worldPos + new float3(0, epsilon, 0))
                       - Sample(data, resX, resY, resZ, boundsMin, cellSize, worldPos - new float3(0, epsilon, 0));
            float dz = Sample(data, resX, resY, resZ, boundsMin, cellSize, worldPos + new float3(0, 0, epsilon))
                       - Sample(data, resX, resY, resZ, boundsMin, cellSize, worldPos - new float3(0, 0, epsilon));

            return math.normalize(new float3(dx, dy, dz));
        }
    }
}