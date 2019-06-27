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

            return new TDScene(camera, spheres);
        }
    }

}