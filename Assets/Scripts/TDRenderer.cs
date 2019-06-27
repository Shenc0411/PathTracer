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
            for (int i = 0; i < camera.pixelResolution.x; ++i)
            {
                for (int j = 0; j < camera.pixelResolution.y; ++j)
                {
                    float3 color = new float3(0.0f, 0.0f, 0.0f);
                    float3 currentPixelPosition = worldLowerLeftCorner + camera.right * worldWidthPerPixel * i + camera.up * worldHeightPerPixel * j;

                    for (int k = 0; k < renderConfiguration.sampleRate; ++k)
                    {
                        float u = UnityEngine.Random.Range(0.0f, 1.0f) * worldWidthPerPixel;
                        float v = UnityEngine.Random.Range(0.0f, 1.0f) * worldHeightPerPixel;
                        float3 offset = new float3(u, v, 0);

                        TDRay ray = new TDRay(camera.position, currentPixelPosition + offset - camera.position);
                        color += Color(ray);
                    }

                    color /= renderConfiguration.sampleRate;

                    texture.SetPixel(i, j, new UnityEngine.Color(color.x, color.y, color.z));
                }
            }

            texture.filterMode = FilterMode.Point;
            texture.Apply();

            return texture;
        }

        private void Start()
        {
            this.scene = TDLoader.LoadScene();
            //Debug.Log(RandomPointInUnitSphere());
            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            this.Render();
            sw.Stop();
            Debug.Log(sw.Elapsed);
        }

        private float3 Color(TDRay ray)
        {

            TDRayHitRecord hitRecord = new TDRayHitRecord();

            if (RaySphereCollision(ray, 0.0001f, float.MaxValue, ref hitRecord))
            {
                float3 target = hitRecord.hitPoint + hitRecord.hitNormal + this.RandomPointInUnitSphere();
                return 0.5f * Color(new TDRay(hitRecord.hitPoint, target - hitRecord.hitPoint));
                /*return 0.5f * (hitRecord.hitNormal + new float3(1.0f, 1.0f, 1.0f));*/
            }

            float t = 0.5f * (ray.direction.y + 1.0f);

            return new float3(1.0f - t, 1.0f - t, 1.0f - t) + t * new float3(0.5f, 0.7f, 1.0f);
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
                            hasHit = true;
                            continue;
                        }
                    }

                    hitDistance = (-b + math.sqrt(discriminant)) / (2.0f * a);
                    if (hitDistance > hitDistanceMin && hitDistance < hitDistanceMax)
                    {
                        if (!hasHit || hitDistance < hitRecord.hitDistance)
                        {
                            float3 hitPoint = ray.origin + hitDistance * ray.direction;
                            float3 hitNormal = math.normalize(hitPoint - sphere.position);
                            hitRecord.hitPoint = hitPoint;
                            hitRecord.hitNormal = hitNormal;
                            hitRecord.hitDistance = hitDistance;
                            hasHit = true;
                        }
                    }
                }
            }

            return hasHit;
        }

        private float3 RandomPointInUnitSphere()
        {
            float3 p;
            do
            {
                p = 2.0f * new float3(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f)) - new float3(1.0f, 1.0f, 1.0f);
            } while (math.lengthsq(p) >= 1.0f);
            return p;
        }

    }

}