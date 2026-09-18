using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

using Unity.Jobs;
namespace DefaultNamespace
{
    public class BallSimulation : MonoBehaviour
    {
        [SerializeField] private int ballCount = 10000;
        [SerializeField] private int initialHeight = 20;
        [SerializeField] private float ballRadius = 0.1f;
        [SerializeField] private Vector2 planeSize = new Vector2(10, 10);
        [SerializeField] private SdfVolume sdfVolume;

        private NativeArray<float3> positions;
        private NativeArray<float3> velocities;

        // Widok na dane Texture3D po stronie CPU. NIE nasza alokacja -> NIE wolno wywoływać Dispose().
        // Jej właścicielem jest sdfVolume.field; żyje tak długo, jak długo trzymamy do niego referencję.
        private NativeArray<float> sdfData;

        void OnEnable()
        {
            positions = new NativeArray<float3>(ballCount, Allocator.Persistent);
            velocities = new NativeArray<float3>(ballCount, Allocator.Persistent);

            InitializeBalls();

            sdfData = sdfVolume.field.GetPixelData<float>(0);
            var expectedLength = sdfVolume.resolutionX * sdfVolume.resolutionY * sdfVolume.resolutionZ;
            Debug.Log($"SDF data length: {sdfData.Length}, expected: {expectedLength}");
            Assert.AreEqual(expectedLength, sdfData.Length);

            var cellSize = (sdfVolume.boundsMax.x - sdfVolume.boundsMin.x) / sdfVolume.resolutionX;


            TestSampler(cellSize); 
            TestGradient(cellSize);
            
            var sphereMesh = ProceduralMesh.CreateLowPolySphere(6, 8, ballRadius);
            Debug.Log($"Sphere triangles: {sphereMesh.triangles.Length / 3}, vertices: {sphereMesh.vertexCount}");
        }

        private void Update()
        {
             

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
                gravity = new float3(0, -9.8f, 0),
                deltaTime = Time.deltaTime,
                ballRadius = ballRadius,
                restitution = 0.5f, //0 - odbicie niesprezyste, 1 odbicie doskonale spręzyste
                sleepThreshold = 0.005f
            };

            var handle = job.Schedule(velocities.Length, 64);
            handle.Complete();
            //
            // for (int i = 0; i < velocities.Length; i++)
            // {
            //     Debug.Log(velocities[i]);
            // } 
        }

        private void TestGradient(float cellSize)
        {
            var gradient = SdfSampler.Gradient(sdfData, sdfVolume.resolutionX, sdfVolume.resolutionY, sdfVolume.resolutionZ,
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
        } 
        private void InitializeBalls()
        {
            var row = Mathf.CeilToInt(Mathf.Sqrt(ballCount)); 
            var initialX = -planeSize.x / 2;
            var initialZ = -planeSize.y / 2;
            var offset = ballRadius / 2;

            var ballIndex = 0;
            for (var i = 0; i < row; i++)
            {
                for (var j = 0; j < row; j++)
                {
                    positions[ballIndex] = new float3(initialX + i * ballRadius + offset, initialHeight,
                        initialZ + j * ballRadius + offset);
                    velocities[ballIndex] = 0;

                    Assert.IsTrue(positions[ballIndex].x > initialX && positions[ballIndex].x < -initialX);
                    Assert.IsTrue(positions[ballIndex].z > initialZ && positions[ballIndex].z < -initialZ);

                    ballIndex++;
                    if (ballIndex == ballCount)
                        return;
                }
            }
        }
    }
}