namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using TorchDragon.CPU;
    using TorchDragon.Scene;

    public static class TDLoader
    {
        public static TDScene LoadScene()
        {
            TDCamera camera = new TDCamera(Camera.main);

            List<TDSphere> spheres = new List<TDSphere>();

            TDSceneSphere[] sceneSpheres = MonoBehaviour.FindObjectsOfType<TDSceneSphere>();

            foreach(TDSceneSphere sceneSphere in sceneSpheres)
            {
                spheres.Add(new TDSphere(sceneSphere.transform.position, sceneSphere.transform.lossyScale.x * 0.5f));
            }

            return new TDScene(camera, spheres);
        }
    }

}