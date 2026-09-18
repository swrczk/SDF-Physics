using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DefaultNamespace
{
    [BurstCompile]
    public struct SimulateBallsJob : IJobParallelFor
    {
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;

        [ReadOnly] public NativeArray<float> sdfData;
        public int resX, resY, resZ;
        public float3 boundsMin;
        public float cellSize;

        public float3 gravity;
        public float friction;
        public float deltaTime;
        public float ballRadius;
        public float restitution;
        public float sleepThreshold; // np. 0.05f - dobierzesz eksperymentalnie
 

        public void Execute(int index)
        {
            var velocity = velocities[index];
            var position = positions[index];

            velocity += gravity * deltaTime;
            position += velocity * deltaTime;

            var distance = SdfSampler.Sample(sdfData, resX, resY, resZ, boundsMin, cellSize, position);
            if (distance < ballRadius)
            {
                var normal = SdfSampler.Gradient(sdfData, resX, resY, resZ, boundsMin, cellSize, position,  cellSize * 0.5f);
                position += normal * (ballRadius - distance);
                var vn = math.dot(velocity, normal);

                if (vn < 0)
                    velocity -= (1 + restitution) * vn * normal;
                
                
                var vnAfter = math.dot(velocity, normal);
                var tangential = velocity - vnAfter * normal;
                velocity = vnAfter * normal + tangential * friction;

                if (math.lengthsq(velocity) < sleepThreshold * sleepThreshold)
                    velocity = float3.zero;
                
                if (math.lengthsq(velocity) < sleepThreshold* sleepThreshold)
                    velocity = float3.zero;
            }

            positions[index] = position;
            velocities[index] = velocity;
        }
    }
}