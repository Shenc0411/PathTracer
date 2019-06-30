namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using Unity.Mathematics;
    using Unity.Collections;
    using Unity.Jobs;
    using UnityEngine;
    using TorchDragon.CPU;
    using System.Threading;

    public class TDRenderer : MonoBehaviour
    {
        public TDScene scene;
        public TDRenderConfiguration renderConfiguration;
        public GameObject renderPlane;
        public ComputeShader cs;

        private MeshRenderer meshRenderer;

        private List<TDRay> rays;
        private float3 ambientLight;
        private float worldHeightPerPixel;
        private float worldWidthPerPixel;
        private NativeArray<Color> colors;
        private readonly float upperBoundOne = 1.0f - float.Epsilon;
        private float3[] sphereicalFibSamples = new float3[4096];
        private float3[] screenPixelPositions;
        private float3[] accumulatedColor;
        private Texture2D cpuTexture;
        private RenderTexture gpuTexture;
        private int numIterations;
        private int kernelHandle;
        private int pixelResolutionX;
        private int pixelResolutionY;

        public void Render()
        {
            this.meshRenderer = this.renderPlane.GetComponent<MeshRenderer>();
            this.renderPlane.transform.localScale = new Vector3(scene.camera.aspectRatio, 0.0f, 1.0f);
            this.ambientLight = new float3(this.renderConfiguration.ambientLight.r, this.renderConfiguration.ambientLight.g, this.renderConfiguration.ambientLight.b) * this.renderConfiguration.ambientLightIntensity;

            if(this.renderConfiguration.renderMode == RenderMode.CPU)
            {
                this.meshRenderer.material.mainTexture = CPURender();
            }
            else if(this.renderConfiguration.renderMode == RenderMode.GPU)
            {
                this.meshRenderer.material.mainTexture = GPURender();
            }
            
        }

        private void GPUPrepare()
        {
            TDCamera camera = this.scene.camera;

            float worldNearPlaneHeight = camera.nearPlaneDistance * math.tan(camera.verticalFOV * Mathf.Deg2Rad * 0.5f) * 2.0f;
            float worldNearPlaneWidth = camera.aspectRatio * worldNearPlaneHeight;

            worldHeightPerPixel = worldNearPlaneHeight / camera.pixelResolution.y;
            worldWidthPerPixel = worldNearPlaneWidth / camera.pixelResolution.x;

            pixelResolutionX = (int)this.scene.camera.pixelResolution.x;
            pixelResolutionY = (int)this.scene.camera.pixelResolution.y;

            kernelHandle = cs.FindKernel("CSMain");

            gpuTexture = new RenderTexture(pixelResolutionX, pixelResolutionY, 24);
            gpuTexture.enableRandomWrite = true;
            gpuTexture.Create();
            gpuTexture.filterMode = FilterMode.Point;

            cs.SetInt("numIterations", numIterations);
            cs.SetInt("numSpheres", this.scene.spheres.Count);
            cs.SetInt("pixelResolutionX", pixelResolutionX);
            cs.SetInt("pixelResolutionY", pixelResolutionY);
            cs.SetInt("sampleRate", this.renderConfiguration.sampleRate);
            cs.SetFloat("widthPerPixel", worldWidthPerPixel);
            cs.SetFloat("heightPerPixel", worldHeightPerPixel);
            cs.SetVector("cameraPosition", new Vector3(this.scene.camera.position.x, this.scene.camera.position.y, this.scene.camera.position.z));
            cs.SetVector("ambientLight", new Vector3(this.ambientLight.x, this.ambientLight.y, this.ambientLight.z));

            ComputeBuffer screenPixelPositionBuffer = new ComputeBuffer(pixelResolutionX * pixelResolutionY, 3 * 4);
            screenPixelPositionBuffer.SetData(this.GetScreenPixelPositions());
            cs.SetBuffer(kernelHandle, "screenPixelPositions", screenPixelPositionBuffer);
            ComputeBuffer sphereBuffer = new ComputeBuffer(this.scene.spheres.Count, 13 * 4);
            sphereBuffer.SetData(this.scene.spheres);
            cs.SetBuffer(kernelHandle, "spheres", sphereBuffer);
            //this.SphericalFib(ref this.sphereicalFibSamples);

            cs.SetTexture(kernelHandle, "Texture", gpuTexture);

        }

        public Texture GPURender()
        {
            Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)(System.DateTime.Now.Millisecond * System.DateTime.Now.Second) + 1);
            cs.SetVector("seed", new Vector3(random.NextFloat(0.0f, pixelResolutionX), random.NextFloat(0.0f, pixelResolutionY), random.NextFloat(0.0f, 1000.0f)));

            cs.Dispatch(kernelHandle, pixelResolutionX / 8, pixelResolutionY / 8, 1);

            numIterations += 1;
            cs.SetInt("numIterations", numIterations);
            return gpuTexture;
        }

        public Texture CPURender()
        {
            int pixelResolutionX = (int)this.scene.camera.pixelResolution.x;
            int pixelResolutionY = (int)this.scene.camera.pixelResolution.y;

            int length = pixelResolutionX * pixelResolutionY;

            //PixelColorJob job = new PixelColorJob();
            //job.ambientLight = this.ambientLight;
            //job.colors = new NativeArray<Color>(pixelResolutionX * pixelResolutionY, Allocator.TempJob);
            //job.renderConfiguration = this.renderConfiguration;
            //job.cameraPosition = this.scene.camera.position;
            //job.spheres = new NativeArray<TDSphere>(this.scene.spheres.ToArray(), Allocator.TempJob);
            //job.screenPixelPositions = new NativeArray<float3>(this.GetScreenPixelPositions(), Allocator.TempJob);
            //job.worldHeightPerPixel = this.worldHeightPerPixel;
            //job.worldWidthPerPixel = this.worldWidthPerPixel;
            //job.upperBoundOne = this.upperBoundOne;
            //job.random.InitState((uint)System.DateTime.Now.Second);
            //JobHandle jobHandle = job.Schedule(length, 100);

            //jobHandle.Complete();

            //for (int i = 0; i < length; ++i)
            //{
            //    texture.SetPixel(i / pixelResolutionY, i % pixelResolutionY, job.colors[i]);
            //}

            //job.screenPixelPositions.Dispose();
            //job.spheres.Dispose();
            //job.colors.Dispose();

            List<Thread> threads = new List<Thread>();
            int batchSize = length / this.renderConfiguration.cpuThreads;
            int batchStart = 0;

            while(batchStart < length)
            {
                Debug.Log(batchStart + " " + math.min(length, batchStart + batchSize));
                int start = batchStart;
                int end = math.min(length, batchStart + batchSize);
                Thread thread = new Thread(() => this.PixelColoringThread(start, end));
                thread.Start();
                threads.Add(thread);
                batchStart += batchSize;
            }

            foreach(Thread thread in threads)
            {
                thread.Join();
            }

            for(int i = 0; i < length; ++i)
            {
                this.cpuTexture.SetPixel(i / pixelResolutionY, i % pixelResolutionY, new Color(accumulatedColor[i].x, accumulatedColor[i].y, accumulatedColor[i].z, 0.0f));
            }

            this.cpuTexture.filterMode = FilterMode.Point;
            this.cpuTexture.Apply();

            return this.cpuTexture;
        }

        private void PixelColoringThread(int start, int end)
        {
            Unity.Mathematics.Random random = new Unity.Mathematics.Random((uint)(100 + start * (System.DateTime.Now.Millisecond + 100)));
            TDRay ray;
            ray.origin = this.scene.camera.position;

            for (int k = start; k < end; ++k)
            {
                float3 result = float3.zero;

                for (int i = 0; i < this.renderConfiguration.sampleRate; ++i)
                {
                    float u = random.NextFloat() * this.worldWidthPerPixel;
                    float v = random.NextFloat() * this.worldHeightPerPixel;

                    ray.direction = math.normalize(this.screenPixelPositions[k] + new float3(u, v, 0) - this.scene.camera.position);

                    result += this.TraceColor(ray, random);
                }

                accumulatedColor[k] = (accumulatedColor[k] * this.renderConfiguration.sampleRate * numIterations + result)
                    / (this.renderConfiguration.sampleRate * (numIterations + 1));
            }
        }


        private void Start()
        {
            this.scene = TDLoader.LoadScene();
            int pixelResolutionX = (int)this.scene.camera.pixelResolution.x;
            int pixelResolutionY = (int)this.scene.camera.pixelResolution.y;
            this.cpuTexture = new Texture2D(pixelResolutionX, pixelResolutionY);
            this.screenPixelPositions = this.GetScreenPixelPositions();
            this.accumulatedColor = new float3[pixelResolutionX * pixelResolutionY];
            this.numIterations = 0;
            //Debug.Log(RandomPointInUnitSphere());
            System.Random m_rng = new System.Random();
            int kernelHandle = cs.FindKernel("CSMain");

            numIterations = 0;
            

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            this.GPUPrepare();
            sw.Stop();
            Debug.Log(sw.Elapsed);

            this.StartCoroutine(RenderProgressive());
        }

        private void OnDestroy()
        {
            //this.colors.Dispose();
        }

        private IEnumerator RenderProgressive()
        {
            while(true)
            {
                this.Render();
                yield return new WaitForSeconds(0.05f);
            }
        }

        private IEnumerator RenderProgressiveCPU()
        {
            this.meshRenderer = this.renderPlane.GetComponent<MeshRenderer>();
            this.renderPlane.transform.localScale = new Vector3(scene.camera.aspectRatio, 0.0f, 1.0f);
            this.ambientLight = new float3(this.renderConfiguration.ambientLight.r, this.renderConfiguration.ambientLight.g, this.renderConfiguration.ambientLight.b) * this.renderConfiguration.ambientLightIntensity;

            int pixelResolutionX = (int)this.scene.camera.pixelResolution.x;
            int pixelResolutionY = (int)this.scene.camera.pixelResolution.y;

            int length = pixelResolutionX * pixelResolutionY;
            int batchSize = length / this.renderConfiguration.cpuThreads;

            for (int i = 0; i < 1; ++i)
            {
                List<Thread> threads = new List<Thread>();
                int batchStart = 0;

                while (batchStart < length)
                {
                    int start = batchStart;
                    int end = math.min(length, batchStart + batchSize);
                    Thread thread = new Thread(() => this.PixelColoringThread(start, end));
                    threads.Add(thread);
                    batchStart = end;
                }

                foreach (Thread thread in threads)
                {
                    thread.Start();
                }

                foreach (Thread thread in threads)
                {
                    thread.Join();
                }

                numIterations += 1;

                for (int j = 0; j < length; ++j)
                {
                    this.cpuTexture.SetPixel(j / pixelResolutionY, j % pixelResolutionY, new Color(accumulatedColor[j].x, accumulatedColor[j].y, accumulatedColor[j].z, 0.0f));
                }

                this.cpuTexture.filterMode = FilterMode.Point;
                this.cpuTexture.Apply();
                this.meshRenderer.material.mainTexture = this.cpuTexture;
                System.IO.File.WriteAllBytes(this.numIterations + ".jpg", this.cpuTexture.EncodeToJPG());

                yield return null;
            }

        }

        void SphericalFib(ref float3[] output)
        {
            double n = output.Length / 2;
            double pi = Mathf.PI;
            double dphi = pi * (3 - math.sqrt(5));
            double phi = 0;
            double dz = 1 / n;
            double z = 1 - dz / 2.0f;
            int[] indices = new int[output.Length];

            for (int j = 0; j < n; j++)
            {
                double zj = z;
                double thetaj = math.acos(zj);
                double phij = phi % (2 * pi);
                z = z - dz;
                phi = phi + dphi;

                // spherical -> cartesian, with r = 1
                output[j] = new float3((float)(math.cos(phij) * math.sin(thetaj)),
                                        (float)(zj),
                                        (float)(math.sin(thetaj) * math.sin(phij)));
                indices[j] = j;
            }


            // The code above only covers a hemisphere, this mirrors it into a sphere.
            for (int i = 0; i < n; i++)
            {
                var vz = output[i];
                vz.y *= -1;
                output[output.Length - i - 1] = vz;
                indices[i + output.Length / 2] = i + output.Length / 2;
            }
        }

        private float3[] GetScreenPixelPositions()
        {
            TDCamera camera = this.scene.camera;

            float worldNearPlaneHeight = camera.nearPlaneDistance * math.tan(camera.verticalFOV * Mathf.Deg2Rad * 0.5f) * 2.0f;
            float worldNearPlaneWidth = camera.aspectRatio * worldNearPlaneHeight;

            worldHeightPerPixel = worldNearPlaneHeight / camera.pixelResolution.y;
            worldWidthPerPixel = worldNearPlaneWidth / camera.pixelResolution.x;

            float3 worldLowerLeftCorner = camera.position + camera.forward * camera.nearPlaneDistance - camera.right * 0.5f * worldNearPlaneWidth - camera.up * 0.5f * worldNearPlaneHeight;

            float3[] pixelPositions = new float3[(int)camera.pixelResolution.x * (int)camera.pixelResolution.y];

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

        private float3 TraceColor(TDRay ray, Unity.Mathematics.Random random)
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
                        float3 target = hitRecord.point + hitRecord.normal + this.RandomPointInUnitSphere(random);
                        scatteredRayDirection = target - hitRecord.point;
                        attenuation = hitRecord.albedo;
                        scatter = true;
                    }
                    else if (hitRecord.material == 2.0f)
                    {
                        //Metal
                        scatteredRayDirection = ray.direction - 2.0f * math.dot(ray.direction, hitRecord.normal) * hitRecord.normal
                                            + hitRecord.fuzz * this.RandomPointInUnitSphere(random) * hitRecord.fuzz;
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

                        if (random.NextFloat() < reflectProbablity)
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

        private float3 RandomPointInUnitSphere(Unity.Mathematics.Random random)
        {
            float3 p;
            do
            {
                p = 2.0f * new float3(random.NextFloat(),
                    random.NextFloat(),
                    random.NextFloat())
                    - new float3(1.0f, 1.0f, 1.0f);
            } while (math.lengthsq(p) >= 1.0f);
            return p;
        }

    }

}