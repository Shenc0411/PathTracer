namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Jobs;
    using Unity.Jobs;
    using Unity.Collections;
    using Unity.Mathematics;
    using TorchDragon.CPU;

    public struct TraceColorJob : IJobParallelFor
    {
        public NativeArray<TDRay> rays;
        public TDRenderConfiguration renderConfiguration;
        public TDScene scene;
        public Texture2D texture;
        public float upperBoundOne;
        public float3 ambientLight;

        public void Execute(int index)
        {
            throw new System.NotImplementedException();
        }

        private float3 TraceColor(TDRay ray, int numBounces)
        {

            if (numBounces > renderConfiguration.maxBounces)
            {
                return float3.zero;
            }

            TDRayHitRecord hitRecord = new TDRayHitRecord();

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
                    TDRay scatteredRay = new TDRay(hitRecord.point, scatteredRayDirection, ray.uv);

                    return hitRecord.emission + attenuation * TraceColor(scatteredRay, numBounces + 1);
                }

                return float3.zero;
            }

            return this.ambientLight;
        }

        private bool RaySphereCollision(TDRay ray, float hitDistanceMin, float hitDistanceMax, ref TDRayHitRecord hitRecord)
        {
            bool hasHit = false;
            hitRecord.distance = float.MaxValue;

            foreach (TDSphere sphere in this.scene.spheres)
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