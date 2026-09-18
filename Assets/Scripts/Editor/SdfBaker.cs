using DefaultNamespace;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace DefaultNamespace.Editor
{
    public static class SdfBaker
    {
        // Bounds i rozdzielczość bake'a zostają ręcznie dobranymi stałymi - to świadomy
        // wybór (kompromis rozmiar/dokładność), nie coś, co da się "wyczytać" ze sceny.
        // Kształt i pozycja płaszczyzny/walca - te owszem, są teraz brane z transformów
        // obiektów w scenie (SdfPlaneMarker / SdfCylinderMarker), patrz niżej.
        private const float CellSize = 0.1f;
        private static float3 _boundMin = new float3(-5.3f, -0.3f, -5.3f);
        private static float3 _boundMax = new float3(5.3f, 2.3f, 5.3f);

        private const string TexturePath = "Assets/SDF/SceneField.asset";
        private const string VolumePath = "Assets/SDF/SceneFieldVolume.asset";

        [MenuItem("Tools/SDF/BakeScene")]
        public static void Bake()
        {
            var planeMarker = Object.FindFirstObjectByType<SdfPlaneMarker>();
            var cylinderMarker = Object.FindFirstObjectByType<SdfCylinderMarker>();
            if (planeMarker == null || cylinderMarker == null)
            {
                Debug.LogError("SdfBaker: brakuje SdfPlaneMarker i/lub SdfCylinderMarker w scenie - " +
                                "dodaj te komponenty do obiektów płaszczyzny i walca. Bake przerwany.");
                return;
            }

            // Domyślny Plane Unity leży w lokalnej płaszczyźnie XZ - traktujemy go jako
            // nieskończoną półprzestrzeń na wysokości jego transformu (bez rotacji).
            var planeHeight = planeMarker.transform.position.y;

            // Domyślny Cylinder Unity ma radius=0.5 i height=2 przy scale=(1,1,1) -
            // stąd mnożniki. Rotacja walca nie jest uwzględniana.
            var cylinderTransform = cylinderMarker.transform;
            var cylinderRadius = cylinderTransform.localScale.x * 0.5f;
            var cylinderHeight = cylinderTransform.localScale.y * 2f;

            // DistanceToCylinder liczy "center" jako PODSTAWĘ walca (punkt na ziemi),
            // nie jego geometryczny środek - stąd wewnątrz formuły jest "- height*0.5f".
            // Pivot obiektu w Unity siedzi natomiast w środku mesha, więc trzeba go
            // przesunąć w dół o połowę (aktualnej, przeskalowanej!) wysokości.
            var cylinderCenter = (float3)cylinderTransform.position - new float3(0, cylinderHeight * 0.5f, 0);

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
                        var worldPosition = SdfUtils.GridToWorld(x, y, z, _boundMin, CellSize);
                        var distance = Mathf.Min(
                            DistanceToPlane(worldPosition, planeHeight),
                            DistanceToCylinder(worldPosition, cylinderCenter, cylinderRadius, cylinderHeight)
                        );
                        data[index] = distance;
                        index++;
                    }
                }
            }

            var testIndex = new int3(60, 15, 60);
            var testWorldPos = SdfUtils.GridToWorld(testIndex.x, testIndex.y, testIndex.z, _boundMin, CellSize);
            var expected = math.min(DistanceToPlane(testWorldPos, planeHeight),
                DistanceToCylinder(testWorldPos, cylinderCenter, cylinderRadius, cylinderHeight));
            var flatIndex = SdfUtils.Flatten(testIndex.x, testIndex.y, testIndex.z, resX, resY);
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

        private static float DistanceToPlane(float3 worldPos, float planeHeight) => worldPos.y - planeHeight;

        private static float DistanceToCylinder(float3 p, float3 center, float radius, float height)
        {
            var d = math.abs(new float2(math.length(p.xz - center.xz), p.y - center.y - height * 0.5f))
                    - new float2(radius, height * 0.5f);
            return math.min(math.max(d.x, d.y), 0f) + math.length(math.max(d, 0f));
        }
    }
}
