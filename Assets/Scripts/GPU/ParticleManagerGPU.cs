using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.InputSystem;

//https://www.youtube.com/watch?v=zbBwKMRyavE
//https://www.youtube.com/watch?v=BrZ4pWwkpto

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 44)] //maintain order of data
public struct ParticleGPU
{
    public float pressure;
    public float density;

    public Vector3 currentAcceleration;
    public Vector3 velocity;
    public Vector3 position;
}

public class ParticleManagerGPU : MonoBehaviour
{
    [Header("General")]
    public bool showSpheres = true;
    public Transform sphere;
    public Vector3Int rowCount = new(10, 10, 10);
    private int ParticleCount
    {
        get
        {
            return rowCount.x * rowCount.y * rowCount.z;
        }
    }

    public Vector3 boxSize = new(4, 10, 3);
    public Vector3 spawnPoint;
    public float particleRadius = 0.1f;
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
    private int ApplyGravityKernel;
    private int CalculateDensityKernel;
    private int CalculatePressureKernel;
    private int CalculatePressureViscosityForceKernel;
    private int IntegrateKernel;
    private int ResolveCollisionsKernel;

    [Header("Compute Shader Properties")]
    public float damping = -0.3f;
    public float viscosity = -0.003f;
    public float particleMass = 1.0f;
    public float gasConstant = 2.0f;
    public float targetDensity = 1.0f;
    public float gravity = -9.81f;
    public float deltaTime = 0.007f;

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

        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(Vector3.zero, boxSize);

        if (!Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(spawnPoint, 0.1f);
        }
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
                new Bounds(Vector3.zero, boxSize),
                _argsBuffer,
                castShadows: UnityEngine.Rendering.ShadowCastingMode.Off
            );
        }   
    }

    private void FixedUpdate()
    {
        computeShader.SetVector("boxSize", boxSize);
        computeShader.SetFloat("deltaTime", deltaTime);

        computeShader.Dispatch(ApplyGravityKernel, ParticleCount / 100, 1, 1);
        computeShader.Dispatch(CalculateDensityKernel, ParticleCount / 100, 1, 1);
        computeShader.Dispatch(CalculatePressureKernel, ParticleCount / 100, 1, 1);
        computeShader.Dispatch(CalculatePressureViscosityForceKernel, ParticleCount / 100, 1, 1);
        computeShader.Dispatch(IntegrateKernel, ParticleCount / 100, 1, 1);
        computeShader.Dispatch(ResolveCollisionsKernel, ParticleCount / 100, 1, 1);
    }

    private void SpawnParticles()
    {
        List<ParticleGPU> _particles = new();

        for (int x = 0; x < rowCount.x; x++)
        {
            for (int y = 0; y < rowCount.y; y++)
            {
                for (int z = 0; z < rowCount.z; z++)
                {
                    Vector3 spawnPos = spawnPoint + 2 * particleRadius * new Vector3(x, y, z);
                    spawnPos += jitter * particleRadius * Random.onUnitSphere;

                    ParticleGPU particle = new()
                    {
                        position = spawnPos
                    };

                    _particles.Add(particle);
                }
            }
        }

        particles = _particles.ToArray();
    }

    private void SetUpComputeBuffers()
    {
        ApplyGravityKernel = computeShader.FindKernel("ApplyGravity");
        CalculateDensityKernel = computeShader.FindKernel("CalculateDensity");
        CalculatePressureKernel = computeShader.FindKernel("CalculatePressure");
        CalculatePressureViscosityForceKernel = computeShader.FindKernel("CalculatePressureViscosityForce");
        IntegrateKernel = computeShader.FindKernel("Integrate");
        ResolveCollisionsKernel = computeShader.FindKernel("ResolveCollisions");

        computeShader.SetFloat("particleMass", particleMass);
        computeShader.SetFloat("viscosity", viscosity);
        computeShader.SetFloat("gasConstant", gasConstant);
        computeShader.SetFloat("targetDensity", targetDensity);

        computeShader.SetFloat("damping", damping);

        computeShader.SetFloat("radius", particleRadius);
        computeShader.SetFloat("radius2", particleRadius * particleRadius);
        computeShader.SetFloat("radius3", particleRadius * particleRadius * particleRadius);
        computeShader.SetFloat("radius4", particleRadius * particleRadius * particleRadius * particleRadius);
        computeShader.SetFloat("radius5", particleRadius * particleRadius * particleRadius * particleRadius * particleRadius);

        computeShader.SetInt("particleLength", ParticleCount);

        computeShader.SetFloat("pi", Mathf.PI);
        computeShader.SetFloat("epsilon", 1e-5f);
        computeShader.SetFloat("gravity", gravity);
        
        computeShader.SetFloat("deltaTime", deltaTime);
        computeShader.SetVector("boxSize", boxSize);

        computeShader.SetBuffer(ApplyGravityKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(CalculateDensityKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(CalculatePressureKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(CalculatePressureViscosityForceKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(IntegrateKernel, "_particles", _particlesBuffer);
        computeShader.SetBuffer(ResolveCollisionsKernel, "_particles", _particlesBuffer);
    }
}