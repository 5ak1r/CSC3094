using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using UnityEditor.Compilation;
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
    private ComputeBuffer _particleIndicesBuffer;
    private ComputeBuffer _cellIndicesBuffer;
    private ComputeBuffer _lookupTableBuffer;

    private int CalculatePropertiesKernel;
    private int CalculateForcesKernel;
    private int MoveParticlesKernel;
    private int HashParticlesKernel;
    private int BitonicSortKernel;
    private int FillLookupTableKernel;

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

        _particleIndicesBuffer = new ComputeBuffer(ParticleCount, 4);
        _cellIndicesBuffer = new ComputeBuffer(ParticleCount, 4);
        _lookupTableBuffer = new ComputeBuffer(ParticleCount, 4);

        //initial indices setup
        uint[] particleIndices = new uint[ParticleCount];
        for (uint i = 0; i < ParticleCount; i++) particleIndices[i] = i;
        _particleIndicesBuffer.SetData(particleIndices);

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

        if (!showSpheres) return;

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

    private void FixedUpdate()
    {
        computeShader.SetVector("boxDimensions", boxDimensions);
        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetFloat("particleMass", particleMass);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("viscosity", viscosity);

        computeShader.Dispatch(HashParticlesKernel, ParticleCount / 256, 1, 1);
        SortParticles();
        computeShader.Dispatch(FillLookupTableKernel, ParticleCount / 256, 1, 1);

        computeShader.Dispatch(CalculatePropertiesKernel, ParticleCount / 256, 1, 1);
        computeShader.Dispatch(CalculateForcesKernel, ParticleCount / 256, 1, 1);
        computeShader.Dispatch(MoveParticlesKernel, ParticleCount / 256, 1, 1);
    }

    private void OnDestroy()
    {
        _argsBuffer.Release();
        _particlesBuffer.Release();
        _particleIndicesBuffer.Release();
        _cellIndicesBuffer.Release();
        _lookupTableBuffer.Release();
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

        HashParticlesKernel = computeShader.FindKernel("HashParticles");
        BitonicSortKernel = computeShader.FindKernel("BitonicSort");
        FillLookupTableKernel = computeShader.FindKernel("FillLookupTable");

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

        computeShader.SetBuffer(HashParticlesKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(BitonicSortKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(CalculatePropertiesKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(CalculateForcesKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(MoveParticlesKernel, "_particles", _particlesBuffer);

        computeShader.SetBuffer(HashParticlesKernel, "_particleIndices", _particleIndicesBuffer);
        computeShader.SetBuffer(BitonicSortKernel, "_particleIndices", _particleIndicesBuffer);
        computeShader.SetBuffer(CalculatePropertiesKernel, "_particleIndices", _particleIndicesBuffer);
        computeShader.SetBuffer(CalculateForcesKernel, "_particleIndices", _particleIndicesBuffer);
        computeShader.SetBuffer(FillLookupTableKernel, "_particleIndices", _particleIndicesBuffer);
        
        computeShader.SetBuffer(HashParticlesKernel, "_cellIndices", _cellIndicesBuffer);
        computeShader.SetBuffer(BitonicSortKernel, "_cellIndices", _cellIndicesBuffer);
        computeShader.SetBuffer(CalculatePropertiesKernel, "_cellIndices", _cellIndicesBuffer);
        computeShader.SetBuffer(CalculateForcesKernel, "_cellIndices", _cellIndicesBuffer);
        computeShader.SetBuffer(FillLookupTableKernel, "_cellIndices", _cellIndicesBuffer);

        computeShader.SetBuffer(HashParticlesKernel, "_lookupTable", _lookupTableBuffer);
        computeShader.SetBuffer(BitonicSortKernel, "_lookupTable", _lookupTableBuffer);
        computeShader.SetBuffer(CalculatePropertiesKernel, "_lookupTable", _lookupTableBuffer);
        computeShader.SetBuffer(CalculateForcesKernel, "_lookupTable", _lookupTableBuffer);
        computeShader.SetBuffer(FillLookupTableKernel, "_lookupTable", _lookupTableBuffer);
    }

    private void SortParticles()
    {
        for (var dim = 2; dim <= ParticleCount; dim <<= 1) 
        {
            computeShader.SetInt("dim", dim);
            for (var block = dim >> 1; block > 0; block >>= 1)
            {
                computeShader.SetInt("block", block);
                computeShader.Dispatch(BitonicSortKernel, ParticleCount / 256, 1, 1);
            }
        }
    }
}