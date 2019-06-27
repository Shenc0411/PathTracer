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

        private readonly float upperBoundOne = 1.0f - float.Epsilon;

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
                    float3 color = float3.zero;
                    float3 currentPixelPosition = worldLowerLeftCorner + camera.right * worldWidthPerPixel * i + camera.up * worldHeightPerPixel * j;

                    for (int k = 0; k < renderConfiguration.sampleRate; ++k)
                    {
                        float u = UnityEngine.Random.Range(0.0f, this.upperBoundOne) * worldWidthPerPixel;
                        float v = UnityEngine.Random.Range(0.0f, this.upperBoundOne) * worldHeightPerPixel;
                        float3 offset = new float3(u, v, 0);

                        TDRay ray = new TDRay(camera.position, currentPixelPosition + offset - camera.position);
                        color += TraceColor(ray, 0);
                    }

                    color /= renderConfiguration.sampleRate;

                    texture.SetPixel(i, j, new Color(color.x, color.y, color.z));
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

        private float3 TraceColor(TDRay ray, int numBounces)
        {

            if(numBounces > renderConfiguration.maxBounces)
            {
                return float3.zero;
            }

            TDRayHitRecord hitRecord = new TDRayHitRecord();

            if (RaySphereCollision(ray, 0.0001f, float.MaxValue, ref hitRecord))
            {
                float3 attenuation = float3.zero;
                float3 scatteredRayDirection = float3.zero;

                if(hitRecord.material == 1.0f)
                {
                    //Lambertian
                    float3 target = hitRecord.point + hitRecord.normal + this.RandomPointInUnitSphere();
                    scatteredRayDirection = target - hitRecord.point;
                    attenuation = hitRecord.albedo;
                }
                else if(hitRecord.material == 2.0f)
                {
                    //Metal
                    scatteredRayDirection = ray.direction - 2.0f * math.dot(ray.direction, hitRecord.normal) * hitRecord.normal
                                        + hitRecord.fuzz * this.RandomPointInUnitSphere() * hitRecord.fuzz;
                    attenuation = hitRecord.albedo;
                }

                TDRay scatteredRay = new TDRay(hitRecord.point, scatteredRayDirection);

                return attenuation * TraceColor(scatteredRay, numBounces + 1);
            }

            float t = 0.5f * (ray.direction.y + 1.0f);

            return new float3(1.0f - t, 1.0f - t, 1.0f - t) + t * new float3(0.5f, 0.7f, 1.0f);
        } 

        private bool RaySphereCollision(TDRay ray, float hitDistanceMin, float hitDistanceMax, ref TDRayHitRecord hitRecord)
        {
            bool hasHit = false;
            hitRecord.distance = float.MaxValue;

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
                        if(!hasHit || hitDistance < hitRecord.distance)
                        {
                            float3 hitPoint = ray.origin + hitDistance * ray.direction;
                            float3 hitNormal = math.normalize(hitPoint - sphere.position);
                            hitRecord.point = hitPoint;
                            hitRecord.normal = hitNormal;
                            hitRecord.material = sphere.material;
                            hitRecord.albedo = sphere.albedo;
                            hitRecord.distance = hitDistance;
                            hitRecord.fuzz = sphere.fuzz;
                            hasHit = true;
                            continue;
                        }
                    }

                    hitDistance = (-b + math.sqrt(discriminant)) / (2.0f * a);
                    if (hitDistance > hitDistanceMin && hitDistance < hitDistanceMax)
                    {
                        if (!hasHit || hitDistance < hitRecord.distance)
                        {
                            float3 hitPoint = ray.origin + hitDistance * ray.direction;
                            float3 hitNormal = math.normalize(hitPoint - sphere.position);
                            hitRecord.point = hitPoint;
                            hitRecord.normal = hitNormal;
                            hitRecord.material = sphere.material;
                            hitRecord.albedo = sphere.albedo;
                            hitRecord.distance = hitDistance;
                            hitRecord.fuzz = sphere.fuzz;
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
                p = 2.0f * new float3(UnityEngine.Random.Range(0.0f, this.upperBoundOne),
                    UnityEngine.Random.Range(0.0f, this.upperBoundOne),
                    UnityEngine.Random.Range(0.0f, this.upperBoundOne))
                    - new float3(1.0f, 1.0f, 1.0f);
            } while (math.lengthsq(p) >= 1.0f);
            return p;
        }

    }

}