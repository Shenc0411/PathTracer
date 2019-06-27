namespace TorchDragon.Scene
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    
    public class TDSceneSphere : MonoBehaviour
    {
        public ObjectMaterial material = ObjectMaterial.Lambertian;
        public Color color = Color.red;
        public float fuzz = 0.5f;
        public float refractiveIndex = 1.5f;
    }
}