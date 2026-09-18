using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Jobs;
using Random = System.Random;

namespace DefaultNamespace
{
    public class BallSimulation : MonoBehaviour
    {
        [SerializeField] private int ballCount = 10000;
        [SerializeField] private int initialHeight = 20;
        [SerializeField] private float ballRadius = 0.1f;
        [SerializeField] private float zOffsetVariation = 0.5f;
        [SerializeField] private Vector2 planeSize = new Vector2(10, 10);
        [SerializeField] private SdfVolume sdfVolume;
        [SerializeField] private Material ballMaterial;

        private Mesh sphereMesh;
        private GraphicsBuffer positionsBuffer;
        private GraphicsBuffer argsBuffer;
        private Bounds renderBounds;


        private NativeArray<float3> positions;
        private NativeArray<float3> velocities;

        // Widok na dane Texture3D po stronie CPU. NIE nasza alokacja -> NIE wolno wywoływać Dispose().
        // Jej właścicielem jest sdfVolume.field; żyje tak długo, jak długo trzymamy do niego referencję.
        private NativeArray<float> sdfData;
        private bool _ballsInstantiated;

        // Wygładzony (exponential moving average) czas klatki - surowe Time.deltaTime
        // skacze klatka do klatki i licznik byłby nieczytelny.
        private float smoothedDeltaTime;

        void OnEnable()
        {
            positions = new NativeArray<float3>(ballCount, Allocator.Persistent);
            velocities = new NativeArray<float3>(ballCount, Allocator.Persistent);
            sdfData = sdfVolume.field.GetPixelData<float>(0);

            InitializeBalls();

            var expectedLength = sdfVolume.resolutionX * sdfVolume.resolutionY * sdfVolume.resolutionZ;
            Debug.Log($"SDF data length: {sdfData.Length}, expected: {expectedLength}");
            Assert.AreEqual(expectedLength, sdfData.Length);

            var cellSize = (sdfVolume.boundsMax.x - sdfVolume.boundsMin.x) / sdfVolume.resolutionX;


            TestSampler(cellSize);
            TestGradient(cellSize);


            sphereMesh = ProceduralMesh.CreateLowPolySphere(6, 8, ballRadius);

            positionsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, ballCount, 3 * sizeof(float));
            ballMaterial.SetBuffer("_Positions", positionsBuffer);
            ballMaterial.enableInstancing = true;

            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
            argsBuffer.SetData(new uint[]
            {
                sphereMesh.GetIndexCount(0),
                (uint)ballCount,
                sphereMesh.GetIndexStart(0),
                sphereMesh.GetBaseVertex(0),
                0
            });

// Hojne granice, żeby Unity nie "obcięło" całej partii kulek przy testowaniu widoczności -
// przy DrawMeshInstancedIndirect silnik nie liczy bounds per-instancja, tylko bierze to, co dasz.
// Margines w X/Z jest duży celowo: kulka odbita od krawędzi walca potrafi zanim tarcie ją
// wyhamuje przez chwilę odjechać daleko poza widoczną płytę.
            renderBounds = new Bounds(new Vector3(0, initialHeight * 0.5f, 0),
                new Vector3(planeSize.x + 60, initialHeight + 4, planeSize.y + 60));
        }

        private void Update()
        {
            if (!_ballsInstantiated) return;
            var job = new SimulateBallsJob
            {
                positions = positions,
                velocities = velocities,
                sdfData = sdfData,
                resX = sdfVolume.resolutionX,
                resY = sdfVolume.resolutionY,
                resZ = sdfVolume.resolutionZ,
                boundsMin = sdfVolume.boundsMin,
                cellSize = sdfVolume.cellSize,
                friction = 0.9f,
                gravity = new float3(0, -9.8f, 0),
                deltaTime = Time.deltaTime,
                ballRadius = ballRadius,
                restitution = 0.5f, //0 - odbicie niesprezyste, 1 odbicie doskonale spręzyste
                sleepThreshold = 0.005f
            };

            var handle = job.Schedule(velocities.Length, 64);
            handle.Complete();
            
            
            // if (Time.frameCount % 30 == 0) // co pół sekundy przy 60fps, nie co klatkę
            // {
            //     for (var i = 0; i < velocities.Length; i++)
            //     {
            //         if (math.lengthsq(velocities[i]) > 25f) // szybciej niż 5 m/s
            //         {
            //             Debug.Log($"[{i}] pos={positions[i]:F2} vel={velocities[i]:F2}");
            //         }
            //     }
            // }

            positionsBuffer.SetData(positions);
            Graphics.DrawMeshInstancedIndirect(sphereMesh, 0, ballMaterial, renderBounds, argsBuffer);

            // EMA: nowa wartość dostaje wagę 0.1, reszta to "pamięć" poprzednich klatek.
            // Wygładza szum, a jednocześnie reaguje na trwałą zmianę w kilka klatek.
            smoothedDeltaTime += (Time.deltaTime - smoothedDeltaTime) * 0.1f;
        }

        private void OnGUI()
        {
            const int padding = 10;
            const int width = 220;
            const int height = 84; // 3 linie tekstu + padding stylu - za mało i GUI.Box tnie ostatnią linię

            var frameTimeMs = smoothedDeltaTime * 1000f;
            var fps = smoothedDeltaTime > 0f ? 1f / smoothedDeltaTime : 0f;

            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 16,
                padding = new RectOffset(10, 10, 8, 8)
            };

            var text = $"Balls: {ballCount:N0}\nFrame time: {frameTimeMs:F2} ms\nFPS: {fps:F0}";
            GUI.Box(new Rect(padding, padding, width, height), text, style);
        }

        private void TestGradient(float cellSize)
        {
            var gradient = SdfSampler.Gradient(sdfData, sdfVolume.resolutionX, sdfVolume.resolutionY,
                sdfVolume.resolutionZ,
                sdfVolume.boundsMin, cellSize, new float3(0.6f, 1f, 0f), cellSize * 0.5f);
            Debug.Log($"Gradient at (0.6,1,0): {gradient} <- should be approximately (1,0,0)"); // bez assert
        }

        private void TestSampler(float cellSize)
        {
            var testGridIndex = new int3(60, 15, 60);
            var testWorldPos = sdfVolume.boundsMin + (new float3(testGridIndex) + 0.5f) * cellSize;

            var flatIndex = testGridIndex.x
                            + testGridIndex.y * sdfVolume.resolutionX
                            + testGridIndex.z * sdfVolume.resolutionX * sdfVolume.resolutionY;
            var expected = sdfData[flatIndex];

            var sampled = SdfSampler.Sample(sdfData, sdfVolume.resolutionX, sdfVolume.resolutionY,
                sdfVolume.resolutionZ,
                sdfVolume.boundsMin, cellSize, testWorldPos);
            Assert.AreApproximatelyEqual(expected, sampled);
        }

        void OnDisable()
        {
            if (positions.IsCreated) positions.Dispose();
            if (velocities.IsCreated) velocities.Dispose();
            // sdfData celowo bez Dispose() - patrz komentarz przy deklaracji pola.
            positionsBuffer?.Dispose();
            argsBuffer?.Dispose();
        }

        private void InitializeBalls()
        {
            var row = Mathf.CeilToInt(Mathf.Sqrt(ballCount));
            var spacing = planeSize.x / row; // odstęp rozciąga siatkę na całą płytę, niezależnie od ballCount
            var initialX = -planeSize.x / 2;
            var initialZ = -planeSize.y / 2;
            var offset = spacing / 2;

            var ballIndex = 0;
            for (var i = 0; i < row; i++)
            {
                for (var j = 0; j < row; j++)
                {
                    var zJitter = UnityEngine.Random.Range(-zOffsetVariation, zOffsetVariation);
                    positions[ballIndex] = new float3(initialX + i * spacing + offset, initialHeight + zJitter,
                        initialZ + j * spacing + offset);
                    velocities[ballIndex] = 0;

                    Assert.IsTrue(positions[ballIndex].x > initialX && positions[ballIndex].x < -initialX);
                    Assert.IsTrue(positions[ballIndex].z > initialZ && positions[ballIndex].z < -initialZ);

                    ballIndex++;
                    if (ballIndex == ballCount)
                    {
                        break;
                    }
                }
            }

            _ballsInstantiated = true;
        }
    }
}