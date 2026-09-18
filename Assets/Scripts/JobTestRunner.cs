using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
{
    public class JobTestRunner : MonoBehaviour
    {
        private void Start()
        {
            var velocities = new NativeArray<float3>(5, Allocator.TempJob);

            var job = new AddGravityJob
            {
                velocities = velocities,
                gravity = new float3(0, -9.8f, 0),
                deltaTime = 0.02f
            };

            var handle = job.Schedule(velocities.Length, 64);
            handle.Complete();

            for (int i = 0; i < velocities.Length; i++)
            {
                Debug.Log(velocities[i]);
            }

            velocities.Dispose();
        }
    }
}