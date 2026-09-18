using DefaultNamespace;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace DefaultNamespace.Editor
{
    public static class SdfBaker
    {
        //dla walca 5height, radius 0.5, 
        private const float CellSize = 0.1f;
        private static float3 _boundMin = new float3(-5.3f, -0.3f, -5.3f);
        private static float3 _boundMax = new float3(5.3f, 2.3f, 5.3f);

        private const string TexturePath = "Assets/SDF/SceneField.asset";
        private const string VolumePath = "Assets/SDF/SceneFieldVolume.asset";

        [MenuItem("Tools/SDF/BakeScene")]
        public static void Bake()
        {
            var resX = Mathf.RoundToInt((_boundMax.x - _boundMin.x) / CellSize);
            var resY = Mathf.RoundToInt((_boundMax.y - _boundMin.y) / CellSize);
            var resZ = Mathf.RoundToInt((_boundMax.z - _boundMin.z) / CellSize);

            var texture = new Texture3D(resX, resY, resZ, TextureFormat.RFloat, false);

            var index = 0;
            var data = new float[resX * resY * resZ];
            for (var z = 0; z < resZ; z++)
            {
                for (var y = 0; y < resY; y++)
                {
                    for (var x = 0; x < resX; x++)
                    {
                        var worldPosition = IndexToWorldPosition(x, y, z);
                        var distance = Mathf.Min(
                            DistanceToPlane(worldPosition),
                            DistanceToCylinder(worldPosition, float3.zero, 0.5f, 2f)
                        );
                        data[index] = distance;
                        index++;
                    }
                }
            }

            var testIndex = new int3(60, 15, 60);
            var testWorldPos = IndexToWorldPosition(testIndex.x, testIndex.y, testIndex.z);
            var expected = math.min(DistanceToPlane(testWorldPos), DistanceToCylinder(testWorldPos, float3.zero, 0.5f, 2f));
            var flatIndex = WorldPositionToIndex(testIndex.x, testIndex.y, testIndex.z, resX, resY);
            Assert.AreApproximatelyEqual(expected, data[flatIndex]);

            texture.SetPixelData(data, 0);
            texture.Apply(false, false);

            if (!AssetDatabase.IsValidFolder("Assets/SDF"))
                AssetDatabase.CreateFolder("Assets", "SDF");

            // Bake bywa uruchamiany wielokrotnie (tuning bounds/rozdzielczości) - usuń stare
            // assety zamiast tworzyć obok nich zdublowane "SceneField 1.asset" itd.
            DeleteAssetIfExists(TexturePath);
            DeleteAssetIfExists(VolumePath);

            AssetDatabase.CreateAsset(texture, TexturePath);

            // Metadane pola: bez tego runtime musiałby na nowo zgadywać bounds/rozdzielczość,
            // z ryzykiem rozjazdu z tym, czego faktycznie użył bake. Wszystko idzie w jeden asset.
            var volume = ScriptableObject.CreateInstance<SdfVolume>();
            volume.field = texture;
            volume.boundsMin = _boundMin;
            volume.boundsMax = _boundMax;
            volume.resolutionX = resX;
            volume.resolutionY = resY;
            volume.resolutionZ = resZ;
            AssetDatabase.CreateAsset(volume, VolumePath);

            AssetDatabase.SaveAssets();
        }

        private static void DeleteAssetIfExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
        }

        private static float3 IndexToWorldPosition(int x, int y, int z)
        {
            var index = new float3(x, y, z);
            return _boundMin + (index + 0.5f) * CellSize;
        }

        private static int WorldPositionToIndex(int x, int y, int z, int resX, int resY)
        {
            return x + y * resX + z * resX * resY;
        }

        private static int3 WorldPositionToGridIndex(float3 worldPosition)
        {
            var local = (worldPosition - _boundMin) / CellSize - 0.5f;
            return new int3(math.round(local));
        }

        private static float DistanceToPlane(float3 plane) => plane.y;

        private static float DistanceToCylinder(float3 p, float3 center, float radius, float height)
        {
            var d = math.abs(new float2(math.length(p.xz - center.xz), p.y - center.y - height * 0.5f))
                    - new float2(radius, height * 0.5f);
            return math.min(math.max(d.x, d.y), 0f) + math.length(math.max(d, 0f));
        }
    }
}
