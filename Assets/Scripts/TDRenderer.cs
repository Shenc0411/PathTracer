namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using UnityEngine;
    using TorchDragon.CPU;

    public class TDRenderer : MonoBehaviour
    {
        public TDScene scene;
        public TDRenderConfiguration renderConfiguration;
        public GameObject renderPlane;
        private MeshRenderer meshRenderer;
        private Unity.Mathematics.Random random = new Unity.Mathematics.Random();

        public void Render()
        {
            this.meshRenderer = this.renderPlane.GetComponent<MeshRenderer>();
            this.renderPlane.transform.localScale = new Vector3(scene.camera.aspectRatio, 0.0f, 1.0f);

            this.meshRenderer.material.mainTexture = CPURender();
        }

        public Texture CPURender()
        {
            TDCamera camera = this.scene.camera;

            float worldNearPlaneHeight = camera.nearPlaneDistance * math.tan(camera.verticalFOV * Mathf.Deg2Rad * 0.5f) * 2.0f;
            float worldNearPlaneWidth = camera.aspectRatio * worldNearPlaneHeight;

            float worldHeightPerPixel = worldNearPlaneHeight / camera.pixelResolution.y;
            float worldWidthPerPixel = worldNearPlaneWidth / camera.pixelResolution.x;

            float3 worldLowerLeftCorner = camera.position + camera.forward * camera.nearPlaneDistance - camera.right * 0.5f * worldNearPlaneWidth - camera.up * 0.5f * worldNearPlaneHeight;

            Texture2D texture = new Texture2D((int)camera.pixelResolution.x, (int)camera.pixelResolution.y);

            //Spawn Screen Rays
            for(int i = 0; i < camera.pixelResolution.x; ++i)
            {
                for(int j = 0; j < camera.pixelResolution.y; ++j)
                {
                    float3 currentPixelPosition = worldLowerLeftCorner + camera.right * worldWidthPerPixel * i + camera.up * worldHeightPerPixel * j;

                    TDRay ray = new TDRay(camera.position, currentPixelPosition - camera.position);

                    float3 result = Color(ray);

                    texture.SetPixel(i, j, new UnityEngine.Color(result.x, result.y, result.z));
                }
            }

            texture.filterMode = FilterMode.Point;
            texture.Apply();

            return texture;
        }

        private void Start()
        {
            this.scene = TDLoader.LoadScene();

            this.Render();
        }

        private float3 Color(TDRay ray)
        {

            TDRayHitRecord hitRecord = new TDRayHitRecord();

            RaySphereCollision(ray, 0.0f, float.MaxValue, ref hitRecord);

            return hitRecord.hitNormal;
        }

        private bool RaySphereCollision(TDRay ray, float hitDistanceMin, float hitDistanceMax, ref TDRayHitRecord hitRecord)
        {
            bool hasHit = false;
            hitRecord.hitDistance = float.MaxValue;

            foreach(TDSphere sphere in this.scene.spheres)
            {
                float3 oc = ray.origin - sphere.position;
                float a = math.dot(ray.direction, ray.direction);
                float b = 2.0f * math.dot(oc, ray.direction);
                float c = math.dot(oc, oc) - sphere.radius * sphere.radius;
                float discriminant = b * b - 4.0f * a * c;
                if (discriminant > 0)
                {
                    float hitDistance = (-b - math.sqrt(discriminant)) / (2.0f * a);

                    if(hitDistance > hitDistanceMin && hitDistance < hitDistanceMax)
                    {
                        if(!hasHit || hitDistance < hitRecord.hitDistance)
                        {
                            float3 hitPoint = ray.origin + hitDistance * ray.direction;
                            float3 hitNormal = math.normalize(hitPoint - sphere.position);
                            hitRecord.hitPoint = hitPoint;
                            hitRecord.hitNormal = hitNormal;
                            hitRecord.hitDistance = hitDistance;
                            continue;
                        }
                    }

                    hitDistance = (-b + math.sqrt(discriminant)) / (2.0f * a);

                    if (!hasHit || hitDistance < hitRecord.hitDistance)
                    {
                        float3 hitPoint = ray.origin + hitDistance * ray.direction;
                        float3 hitNormal = math.normalize(hitPoint - sphere.position);
                        hitRecord.hitPoint = hitPoint;
                        hitRecord.hitNormal = hitNormal;
                        hitRecord.hitDistance = hitDistance;
                    }
                }
            }

            return hasHit;
        }

    }

}