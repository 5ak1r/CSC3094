using UnityEngine;

public class CORG : MonoBehaviour
{
    public bool gpu;

    public ParticleManagerCPU pmCPU;

    void Awake()
    {
        if (gpu) 
        {
            pmCPU.enabled = false;
        }
    }
}
