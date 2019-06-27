namespace TorchDragon.CPU
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

    public struct TDSphere
    {
        float3 position;
        float radius;

        public TDSphere(float3 position, float radius)
        {
            this.position = position;
            this.radius = radius;
        }
    }

    public struct TDCamera
    {
        float3 forward;
        float3 up;
        float3 right;
        float3 position;
        int2 pixelResolution;
        float aspectRatio; // width / height
        float verticalFOV;

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

    public struct TDScene
    {
        TDCamera camera;
        TDSphere[] spheres;

        public TDScene(TDCamera camera, List<TDSphere> spheres)
        {
            this.camera = camera;
            this.spheres = spheres.ToArray();
        }
    }
}