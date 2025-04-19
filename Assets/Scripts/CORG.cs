using UnityEngine;

public class CORG : MonoBehaviour
{
    public bool gpu;

    public ParticleManagerCPU pmCPU;
    public ParticleManagerGPU pmGPU;

    void Awake()
    {
        pmCPU.enabled = !gpu;
        pmGPU.enabled = gpu;
    }
}
