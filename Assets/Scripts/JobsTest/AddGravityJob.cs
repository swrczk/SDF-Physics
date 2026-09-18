using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics; 


[BurstCompile]
public struct AddGravityJob : IJobParallelFor
{
    public NativeArray<float3> velocities;
    public float3 gravity;
    public float deltaTime;

    public void Execute(int index)
    {
        velocities[index] += gravity * deltaTime;
    }
}