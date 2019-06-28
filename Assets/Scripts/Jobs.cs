namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Jobs;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Mathematics;
    using Unity.Burst;
    using TorchDragon.CPU;

    public struct PixelColorJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3> screenPixelPositions;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Color> colors;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<TDSphere> spheres;
        public TDRenderConfiguration renderConfiguration;
        public float3 cameraPosition;
        public float upperBoundOne;
        public float3 ambientLight;
        public float worldWidthPerPixel;
        public float worldHeightPerPixel;
        public Unity.Mathematics.Random random;

        [BurstCompile]
        public void Execute(int index)
        {
            float3 color = float3.zero;
            float3 pixelPosition = this.screenPixelPositions[index];

            TDRay ray;
            ray.origin = this.cameraPosition;

            for (int i = 0; i < this.renderConfiguration.sampleRate; ++i)
            {
                float u = random.NextFloat() * this.worldWidthPerPixel;
                float v = random.NextFloat() * this.worldHeightPerPixel;

                ray.direction = math.normalize(pixelPosition + new float3(u, v, 0) - this.cameraPosition);

                color += this.TraceColor(ray);
            }

            color /= this.renderConfiguration.sampleRate;
            colors[index] = new Color(color.x, color.y, color.z);
        }

        [BurstCompile]
        private float3 TraceColor(TDRay ray)
        {
            TDRayHitRecord hitRecord = new TDRayHitRecord();
            float3[] emissions = new float3[this.renderConfiguration.maxBounces];
            float3[] attenuations = new float3[this.renderConfiguration.maxBounces];
            float3 result = this.ambientLight;
            int depth = this.renderConfiguration.maxBounces - 1;

            for (int i = 0; i < this.renderConfiguration.maxBounces; ++i)
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

            for (int i = depth; i >= 0; --i)
            {
                result = emissions[i] + result * attenuations[i];
            }

            return result;
        }

        [BurstCompile]
        private bool RaySphereCollision(TDRay ray, float hitDistanceMin, float hitDistanceMax, ref TDRayHitRecord hitRecord)
        {
            bool hasHit = false;
            hitRecord.distance = float.MaxValue;

            foreach (TDSphere sphere in this.spheres)
            {
                float3 oc = ray.origin - sphere.position;
                float a = math.dot(ray.direction, ray.direction);
                float b = 2.0f * math.dot(oc, ray.direction);
                float c = math.dot(oc, oc) - sphere.radius * sphere.radius;
                float discriminant = b * b - 4.0f * a * c;
                if (discriminant > 0)
                {
                    float hitDistance = (-b - math.sqrt(discriminant)) / (2.0f * a);

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

        [BurstCompile]
        private float3 RandomPointInUnitSphere()
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