namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine;

    public struct TDRay
    {
        float3 origin;
        float3 direction;

        public TDRay(float3 origin, float direction)
        {
            this.origin = origin;
            this.direction = direction;
        }

        public float3 PointAtParameter(float t)
        {
            return origin + direction * t;
        }
    }

}