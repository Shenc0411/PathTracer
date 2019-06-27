namespace TorchDragon.CPU
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine;

    public struct TDRay
    {
        public float3 origin;
        public float3 direction;

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

    public struct TDSphere
    {
        public float3 position;
        public float radius;

        public TDSphere(float3 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    public struct TDCamera
    {
        public float3 forward;
        public float3 up;
        public float3 right;
        public float3 position;
        public int2 pixelResolution;
        public float aspectRatio; // width / height
        public float verticalFOV;

        public TDCamera(Camera camera)
        {
            this.position = camera.transform.position;
            this.forward = camera.transform.forward;
            this.up = camera.transform.up;
            this.right = camera.transform.right;
            this.pixelResolution.x = camera.pixelWidth;
            this.pixelResolution.y = camera.pixelHeight;
            this.aspectRatio = camera.aspect;
            this.verticalFOV = camera.fieldOfView;
        }
    }

    public class TDScene
    {
        public TDCamera camera;
        public TDSphere[] spheres;

        public TDScene(TDCamera camera, List<TDSphere> spheres)
        {
            this.camera = camera;
            this.spheres = spheres.ToArray();
        }
    }
}