namespace TorchDragon
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public enum RenderMode
    {
        CPU,
        GPU
    }

    [System.Serializable]
    public struct TDRenderConfiguration
    {
        public int sampleRate;
        public int maxBounces;
        public Color ambientLight;
        public float ambientLightIntensity;
        public RenderMode renderMode;
        public int cpuThreads;
    }

}