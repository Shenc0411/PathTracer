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
        public int2 uv;

        public TDRay(float3 origin, float3 direction, int2 uv)
        {
            this.origin = origin;
            this.direction = math.normalize(direction);
            this.uv = uv;
        }
    }

    public struct TDRayHitRecord
    {
        public float3 point;
        public float3 normal;
        public float3 albedo;
        public float3 emission;
        public float distance;
        public float material;
        public float fuzz;
        public float refractiveIndex;
    }

    public struct TDSphere
    {
        public float3 position;
        public float3 albedo;
        public float3 emission;
        public float radius;
        public float material; // 1 = Lambertian, 2 = metal, 3 = refracive
        public float fuzz; // Used in metal materials
        public float refractiveIndex;

        public TDSphere(float3 position, float3 albedo, float3 emission, float radius, float material, float fuzz, float refractiveIndex)
        {
            this.position = position;
            this.albedo = albedo;
            this.emission = emission;
            this.radius = radius;
            this.material = material;
            this.fuzz = fuzz;
            this.refractiveIndex = refractiveIndex;
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