namespace TorchDragon.Scene
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    
    public class TDSceneSphere : MonoBehaviour
    {
        public ObjectMaterial material = ObjectMaterial.Lambertian;
        public Color albedo = Color.red;
        public Color emission = Color.white;
        public float emissionIntensity = 1.0f;
        public float fuzz = 0.5f;
        public float refractiveIndex = 1.5f;
    }
}