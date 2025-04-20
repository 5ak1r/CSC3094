using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

//https://www.youtube.com/watch?v=zbBwKMRyavE
//https://www.youtube.com/watch?v=BrZ4pWwkpto

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 44)] //maintain order of data
public struct ParticleGPU
{
    public float pressure;
    public float density;

    public Vector3 forces;
    public Vector3 velocity;
    public Vector3 position;
}

public class ParticleManagerGPU : MonoBehaviour
{
    [Header("General")]
    public bool showSpheres = true;
    
    // must be a multiple of 256 for bitonic sort
    public Vector3Int rowCount = new(16, 16, 16);
    private int ParticleCount
    {
        get
        {
            return rowCount.x * rowCount.y * rowCount.z;
        }
    }

    public float epsilon = 1e-05f;
    public float deltaTime = 0.003f;
    public float particleRadius = 0.1f;

    public Vector3 boxDimensions = new(4, 10, 3);

    [Header("Spawn Settings")]
    public Vector3 spawnPoint;
    public float jitter = 0.2f;

    [Header("Particle Rendering")]
    public Mesh particleMesh;
    public Material material;

    public float particleRenderSize = 8.0f;

    [Header("Compute Shader")]
    public ComputeShader computeShader;
    public ParticleGPU[] particles;

    private ComputeBuffer _argsBuffer;
    private ComputeBuffer _particlesBuffer;

    private int CalculatePropertiesKernel;
    private int CalculateForcesKernel;
    private int MoveParticlesKernel;

    [Header("Fluid Settings")]
    public float particleMass = 1.0f;
    public float restDensity = 1.0f;
    public float gasConstant = 1.0f;
    public float viscosity = 1.0f;

    [Header("Properties")]
    private static readonly int SizeProperty = Shader.PropertyToID("_size");
    private static readonly int ParticlesBufferProperty = Shader.PropertyToID("_particlesBuffer");

    private void Awake()
    {
        if (!GetComponent<CORG>().gpu) return;

        SpawnParticles();

        uint[] args =
        {
            particleMesh.GetIndexCount(0),
            (uint)ParticleCount,
            particleMesh.GetIndexStart(0),
            particleMesh.GetBaseVertex(0),
            0
        };

        _argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        _particlesBuffer = new ComputeBuffer(ParticleCount, 44);
        _particlesBuffer.SetData(particles);


        SetUpComputeBuffers();
    }

    private void OnDrawGizmos()
    {
        if (!GetComponent<CORG>().gpu) return;

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(boxDimensions / 2, boxDimensions);
    }

    private void Update()
    {
        // render the particles
        material.SetFloat(SizeProperty, particleRenderSize);
        material.SetBuffer(ParticlesBufferProperty, _particlesBuffer);

        if (showSpheres)
        {
            Graphics.DrawMeshInstancedIndirect
            (
                particleMesh,
                0,
                material,
                new Bounds(Vector3.zero, boxDimensions),
                _argsBuffer,
                castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
            );
        }   
    }

    private void FixedUpdate()
    {
        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetFloat("particleMass", particleMass);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("viscosity", viscosity);

        computeShader.Dispatch(CalculatePropertiesKernel, ParticleCount / 256, 1, 1);
        computeShader.Dispatch(CalculateForcesKernel, ParticleCount / 256, 1, 1);
        computeShader.Dispatch(MoveParticlesKernel, ParticleCount / 256, 1, 1);
    }

    private void OnDestroy()
    {
        _argsBuffer.Release();
        _particlesBuffer.Release();
    }

    private void SpawnParticles()
    {
        List<ParticleGPU> _particles = new();

        for (int i = 0; i < rowCount.x * rowCount.y * rowCount.z; i++)
        {
            int x = i / (rowCount.x * rowCount.y);
            int y = i / rowCount.x % rowCount.y;
            int z = i % rowCount.x;

            Vector3 spawnPos = spawnPoint + 2 * particleRadius * new Vector3(x, y, z);
            spawnPos += jitter * particleRadius * Random.onUnitSphere;

            ParticleGPU particle = new()
            {
                position = spawnPos
            };

            _particles.Add(particle);
        }

        particles = _particles.ToArray();
    }

    private void SetUpComputeBuffers()
    {
        CalculatePropertiesKernel = computeShader.FindKernel("CalculateProperties");
        CalculateForcesKernel = computeShader.FindKernel("CalculateForces");
        MoveParticlesKernel = computeShader.FindKernel("MoveParticles");

        computeShader.SetInt("particleCount", ParticleCount);
        computeShader.SetVector("boxDimensions", boxDimensions);

        computeShader.SetFloat("epsilon", epsilon);
        computeShader.SetFloat("deltaTime", deltaTime);

        computeShader.SetFloat("particleMass", particleMass);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("viscosity", viscosity);

        computeShader.SetFloat("smoothingRadius", particleRadius);
        computeShader.SetFloat("smoothingRadius2", particleRadius * particleRadius);

        float radius3 = particleRadius * particleRadius * particleRadius;
        float polyMult = 315 / (64 * Mathf.PI * radius3);
        float spikyGradMult = -45 / (Mathf.PI * radius3);
        float viscLapMult = 45 / (Mathf.PI * radius3 * particleRadius * particleRadius);

        computeShader.SetFloat("polyMult", polyMult);
        computeShader.SetFloat("spikyGradMult", spikyGradMult);
        computeShader.SetFloat("viscLapMult", viscLapMult);

        computeShader.SetBuffer(CalculatePropertiesKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(CalculateForcesKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(MoveParticlesKernel, "_particles", _particlesBuffer);
    }
}