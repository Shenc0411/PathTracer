namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using TorchDragon.CPU;
    using TorchDragon.Scene;

    public enum ObjectMaterial
    {
        Lambertian,
        Metal,
        Refractive
    }

    public static class TDLoader
    {
        public static TDScene LoadScene()
        {
            TDCamera camera = new TDCamera(Camera.main);

            List<TDSphere> spheres = new List<TDSphere>();

            TDSceneSphere[] sceneSpheres = MonoBehaviour.FindObjectsOfType<TDSceneSphere>();

            foreach(TDSceneSphere sceneSphere in sceneSpheres)
            {
                spheres.Add(new TDSphere(sceneSphere.transform.position,
                    new Unity.Mathematics.float3(sceneSphere.albedo.r, sceneSphere.albedo.g, sceneSphere.albedo.b),
                    sceneSphere.emissionIntensity * new Unity.Mathematics.float3(sceneSphere.emission.r, sceneSphere.emission.g, sceneSphere.emission.b),
                    sceneSphere.transform.lossyScale.x * 0.5f, 
                    TDLoader.GetMaterialIndex(sceneSphere.material),
                    sceneSphere.fuzz,
                    sceneSphere.refractiveIndex));
            }

            return new TDScene(camera, spheres);
        }

        public static float GetMaterialIndex(ObjectMaterial material)
        {
            if (material == ObjectMaterial.Lambertian)
            {
                return 1.0f;
            }
            else if (material == ObjectMaterial.Metal)
            {
                return 2.0f;
            }
            else if (material == ObjectMaterial.Refractive)
            {
                return 3.0f;
            }

            return 1.0f;
        }
    }

}