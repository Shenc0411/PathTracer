namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine;
    using TorchDragon.CPU;

    public class TDRenderer : MonoBehaviour
    {
        public TDScene scene;
        public TDRenderConfiguration renderConfiguration;
        public GameObject renderPlane;
        private MeshRenderer meshRenderer;

        private List<TDRay> rays;
        private float3 ambientLight;
        private float worldHeightPerPixel;
        private float worldWidthPerPixel;

        private readonly float upperBoundOne = 1.0f - float.Epsilon;

        public void Render()
        {
            this.meshRenderer = this.renderPlane.GetComponent<MeshRenderer>();
            this.renderPlane.transform.localScale = new Vector3(scene.camera.aspectRatio, 0.0f, 1.0f);
            this.ambientLight = new float3(this.renderConfiguration.ambientLight.r, this.renderConfiguration.ambientLight.g, this.renderConfiguration.ambientLight.b) * this.renderConfiguration.ambientLightIntensity;

            this.meshRenderer.material.mainTexture = CPURender();
        }

        public Texture CPURender()
        {
            int pixelResolutionX = (int)this.scene.camera.pixelResolution.x;
            int pixelResolutionY = (int)this.scene.camera.pixelResolution.y;

            Texture2D texture = new Texture2D(pixelResolutionX, pixelResolutionY);
            int length = pixelResolutionX * pixelResolutionY;

            PixelColorJob job = new PixelColorJob();
            job.ambientLight = this.ambientLight;
            job.colors = new NativeArray<Color>(length, Allocator.TempJob);
            job.renderConfiguration = this.renderConfiguration;
            job.cameraPosition = this.scene.camera.position;
            job.spheres = new NativeArray<TDSphere>(this.scene.spheres.ToArray(), Allocator.TempJob);
            job.screenPixelPositions = this.GetScreenPixelPositions();
            job.worldHeightPerPixel = this.worldHeightPerPixel;
            job.worldWidthPerPixel = this.worldWidthPerPixel;
            job.upperBoundOne = this.upperBoundOne;
            job.random.InitState((uint)System.DateTime.Now.Second);
            JobHandle jobHandle = job.Schedule(length, 100);

            jobHandle.Complete();

            for(int i = 0; i < length; ++i)
            {
                texture.SetPixel(i / pixelResolutionY, i % pixelResolutionY, job.colors[i]);
            }

            job.colors.Dispose();
            job.screenPixelPositions.Dispose();
            job.spheres.Dispose();

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

        private NativeArray<float3> GetScreenPixelPositions()
        {
            TDCamera camera = this.scene.camera;

            float worldNearPlaneHeight = camera.nearPlaneDistance * math.tan(camera.verticalFOV * Mathf.Deg2Rad * 0.5f) * 2.0f;
            float worldNearPlaneWidth = camera.aspectRatio * worldNearPlaneHeight;

            worldHeightPerPixel = worldNearPlaneHeight / camera.pixelResolution.y;
            worldWidthPerPixel = worldNearPlaneWidth / camera.pixelResolution.x;

            float3 worldLowerLeftCorner = camera.position + camera.forward * camera.nearPlaneDistance - camera.right * 0.5f * worldNearPlaneWidth - camera.up * 0.5f * worldNearPlaneHeight;

            NativeArray<float3> pixelPositions = new NativeArray<float3>((int)camera.pixelResolution.x * (int)camera.pixelResolution.y, Allocator.TempJob);

            for (int i = 0; i < camera.pixelResolution.x; ++i)
            {
                for (int j = 0; j < camera.pixelResolution.y; ++j)
                {
                    float3 currentPixelPosition = worldLowerLeftCorner + camera.right * worldWidthPerPixel * i + camera.up * worldHeightPerPixel * j;

                    pixelPositions[i * (int)camera.pixelResolution.y + j] = currentPixelPosition;
                }
            }

            return pixelPositions;
        }

        private float3 TraceColor(TDRay ray)
        {
            TDRayHitRecord hitRecord = new TDRayHitRecord();
            float3[] emissions = new float3[this.renderConfiguration.maxBounces];
            float3[] attenuations = new float3[this.renderConfiguration.maxBounces];
            float3 result = this.ambientLight;
            int depth = this.renderConfiguration.maxBounces - 1;

            for(int i = 0; i < this.renderConfiguration.maxBounces; ++i)
            {
                attenuations[i] = this.ambientLight;

                if (RaySphereCollision(ray, 0.0001f, float.MaxValue, ref hitRecord))
                {

                    float3 attenuation = float3.zero;
                    float3 scatteredRayDirection = float3.zero;
                    bool scatter = false;

                    if (hitRecord.material == 1.0f)
                    {
                        //Lambertian
                        float3 target = hitRecord.point + hitRecord.normal + this.RandomPointInUnitSphere();
                        scatteredRayDirection = target - hitRecord.point;
                        attenuation = hitRecord.albedo;
                        scatter = true;
                    }
                    else if (hitRecord.material == 2.0f)
                    {
                        //Metal
                        scatteredRayDirection = ray.direction - 2.0f * math.dot(ray.direction, hitRecord.normal) * hitRecord.normal
                                            + hitRecord.fuzz * this.RandomPointInUnitSphere() * hitRecord.fuzz;
                        attenuation = hitRecord.albedo;

                        if (math.dot(scatteredRayDirection, hitRecord.normal) > 0.0f)
                        {
                            scatter = true;
                        }

                    }
                    else if (hitRecord.material == 3.0f)
                    {
                        //Refractive
                        float3 reflectedDirection = ray.direction - 2.0f * math.dot(ray.direction, hitRecord.normal) * hitRecord.normal;
                        float3 outwardNormal = float3.zero;
                        float3 refractedDirection = float3.zero;
                        float reflectProbablity = 1.0f;
                        float cosine = 0.0f;
                        float niOverNt = 0.0f;
                        attenuation = new float3(1.0f, 1.0f, 1.0f);

                        if (math.dot(ray.direction, hitRecord.normal) > 0.0f)
                        {
                            outwardNormal = -hitRecord.normal;
                            niOverNt = hitRecord.refractiveIndex;
                            cosine = hitRecord.refractiveIndex * math.dot(ray.direction, hitRecord.normal) / math.length(ray.direction);
                        }
                        else
                        {
                            outwardNormal = hitRecord.normal;
                            niOverNt = 1.0f / hitRecord.refractiveIndex;
                            cosine = -math.dot(ray.direction, hitRecord.normal) / math.length(ray.direction);
                        }

                        float dt = math.dot(ray.direction, hitRecord.normal);
                        float discriminant = 1.0f - niOverNt * niOverNt * (1.0f - dt * dt);


                        if (discriminant > 0.0f)
                        {
                            //Refract
                            refractedDirection = niOverNt * (ray.direction - outwardNormal * dt) - outwardNormal * math.sqrt(discriminant);
                            //Schlick Approximation
                            float r0 = (1.0f - hitRecord.refractiveIndex) / (1.0f + hitRecord.refractiveIndex);
                            r0 = r0 * r0;
                            reflectProbablity = r0 + (1 - r0) * math.pow((1.0f - cosine), 5.0f);
                        }

                        if (UnityEngine.Random.Range(0.0f, upperBoundOne) < reflectProbablity)
                        {
                            scatteredRayDirection = reflectedDirection;
                        }
                        else
                        {
                            scatteredRayDirection = refractedDirection;
                        }

                        scatter = true;
                    }

                    if (scatter)
                    {
                        //TDRay scatteredRay = new TDRay(hitRecord.point, scatteredRayDirection, ray.uv);

                        ray.origin = hitRecord.point;
                        ray.direction = math.normalize(scatteredRayDirection);

                        emissions[i] = hitRecord.emission;
                        attenuations[i] = attenuation;

                        //return hitRecord.emission + attenuation * TraceColor(scatteredRay, numBounces + 1);
                    }
                    else
                    {
                        depth = i;
                        break;
                    }

                    //return float3.zero;
                }
                else
                {
                    depth = i;
                    break;
                }
                //return this.ambientLight;
            }
            
            for(int i = depth; i >= 0; --i)
            {
                result = emissions[i] + result * attenuations[i];
            }

            return result;
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
                            hitRecord.emission = sphere.emission;
                            hitRecord.distance = hitDistance;
                            hitRecord.fuzz = sphere.fuzz;
                            hitRecord.refractiveIndex = sphere.refractiveIndex;

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
                            hitRecord.emission = sphere.emission;
                            hitRecord.distance = hitDistance;
                            hitRecord.fuzz = sphere.fuzz;
                            hitRecord.refractiveIndex = sphere.refractiveIndex;

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