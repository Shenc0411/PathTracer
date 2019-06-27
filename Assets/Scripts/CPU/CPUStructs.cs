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

        public TDRay(float3 origin, float3 direction)
        {
            this.origin = origin;
            this.direction = math.normalize(direction);
        }

        public float3 PointAtParameter(float t)
        {
            return origin + direction * t;
        }
    }

    public struct TDRayHitRecord
    {
        public float3 hitPoint;
        public float3 hitNormal;
        public float hitDistance;

        public TDRayHitRecord(float3 hitPoint, float3 hitNormal, float hitDistance)
        {
            this.hitPoint = hitPoint;
            this.hitNormal = hitNormal;
            this.hitDistance = hitDistance;
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
        public float2 pixelResolution;
        public float nearPlaneDistance;
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
            this.nearPlaneDistance = camera.nearClipPlane;
            this.aspectRatio = camera.aspect;
            this.verticalFOV = camera.fieldOfView;
        }
    }

    public struct TDScene
    {
        public TDCamera camera;
        public List<TDSphere> spheres;

        public TDScene(TDCamera camera, List<TDSphere> spheres)
        {
            this.camera = camera;
            this.spheres = spheres;
        }
    }
}