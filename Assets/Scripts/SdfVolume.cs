using Unity.Mathematics;
using UnityEngine;

namespace DefaultNamespace
{
    public class SdfVolume : ScriptableObject
    {
        public Texture3D field;
        public float3 boundsMin;
        public float3 boundsMax;
        public int resolutionX;
        public int resolutionY;
        public int resolutionZ;
        
        public float cellSize => ( boundsMax.x -  boundsMin.x) / resolutionX;
    }
}
