using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

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
            var radius = planeSize.x / row;
            var initialX = -planeSize.x / 2;
            var initialZ = -planeSize.y / 2;
            var offset = radius / 2;

            var ballIndex = 0;
            for (var i = 0; i < row; i++)
            {
                for (var j = 0; j < row; j++)
                {
                    positions[ballIndex] = new float3(initialX + i * radius + offset, initialHeight,
                        initialZ + j * radius + offset);
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
