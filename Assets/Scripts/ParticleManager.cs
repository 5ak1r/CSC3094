using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Particle {
    public int ID;

    public Vector3 currentForce;
    public Vector3 velocity;
    public Vector3 position;

    public float density;
    public float pressure;

    public Vector3Int cell;
    public uint hash;
    public int key;

    public Dictionary<uint, uint> neighbourTable;
    public HashSet<Particle> neighbours;

    public GameObject gameObject;

    public void ResolveCollisions(int boxSize) {
        float damping = -0.9f;

        if(position.y <= 0.0f) {
            position.y = 0.0f;
            velocity.y *= damping;

            if(Mathf.Abs(velocity.y) < 0.1f) velocity.y = 0.0f; //stop if negligible bounce
        }

        if (position.x <= 0) {
            position.x = 0;
            velocity.x *= damping;
        } else if (position.x >= boxSize) {
            position.x = position.x <= 0 ? 0: boxSize;
            velocity.x *= damping;
        }

        if (position.z <= 0) {
            position.z = 0;
            velocity.z *= damping;
        } else if (position.z >= boxSize) {
            position.z = boxSize;
            velocity.z *= damping;
        }
    }
}

public class ParticleManager : MonoBehaviour {
    
    [Header("General Constants")]
    public const int BOX_SIZE = 10;
    public const int HALF_BOX = BOX_SIZE / 2;
    public const float EPSILON = 1e-5f;

    [Header("Particle Properties")]
    public const float PARTICLE_MASS = 0.02f;
    public const float PARTICLE_MASS_SQUARED = PARTICLE_MASS * PARTICLE_MASS;
    public const float RECIPROCAL_MASS = 1 / PARTICLE_MASS;
    public const float TARGET_DENSITY = 1000.0f;
    public const float GAS_CONSTANT = 2000.0f;
    public const float VISCOSITY = 3.5f;

    [Header("Particle Settings")]
    public const int ROW_COUNT = 10;
    public const int HALF_ROW = ROW_COUNT / 2;
    public const int PARTICLE_COUNT = ROW_COUNT * ROW_COUNT * ROW_COUNT;
    public const float PARTICLE_RADIUS = 0.2f;
    public const float PARTICLE_EFFECT_RADIUS = 0.8f;
    public const float PARTICLE_EFFECT_RADIUS_SQUARED = PARTICLE_EFFECT_RADIUS * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_CUBED = PARTICLE_EFFECT_RADIUS_SQUARED * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_FOURTH = PARTICLE_EFFECT_RADIUS_CUBED * PARTICLE_EFFECT_RADIUS;
    public const float PARTICLE_EFFECT_RADIUS_FIFTH = PARTICLE_EFFECT_RADIUS_FOURTH * PARTICLE_EFFECT_RADIUS;
    public const float SPAWN_VARIANCE = 0.1f;
    public readonly Vector3 SPAWN_POINT = new(HALF_BOX, BOX_SIZE, HALF_BOX);
    
    [Header("Physics Settings")]
    public const float GRAVITY = -9.81f;

    [Header("Particles")]
    public GameObject particleObj;
    public Particle[] particles;

    private void Awake() {
        particles = new Particle[PARTICLE_COUNT];
        SpawnParticles();
    }

    private void Update() {
        Particle[] particleBuffer = new Particle[PARTICLE_COUNT];

        for(int i = 0; i < PARTICLE_COUNT; i++) {
            Particle currentParticle = particles[i];

            /*currentParticle.cell = SpatialHash.CalculateCell(currentParticle.position);
            currentParticle.hash = SpatialHash.CalculateCellHash(currentParticle.cell);
            currentParticle.key = SpatialHash.CalculateCellKey(currentParticle.hash);
            currentParticle.neighbourTable = SpatialHash.NeighbourTable(particles);
            currentParticle.neighbours = SpatialHash.GetNeighbours(currentParticle, particles);*/

            currentParticle.velocity += RECIPROCAL_MASS * Time.deltaTime * currentParticle.currentForce; //multiplying is faster than dividing
            currentParticle.position += currentParticle.velocity * Time.deltaTime;

            currentParticle.ResolveCollisions(BOX_SIZE);
            //currentParticle = CalculateDensityPressure(currentParticle);
            //currentParticle = ComputeForces(currentParticle);

            currentParticle.gameObject.transform.position = currentParticle.position;
            particleBuffer[i] = currentParticle;
        }

        particles = particleBuffer;
    }

    private void OnDrawGizmos() {
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(new(HALF_BOX, HALF_BOX, HALF_BOX), new(BOX_SIZE, BOX_SIZE, BOX_SIZE));
    }

    private void SpawnParticles() {

        for(int i = -HALF_ROW; i < HALF_ROW; i++) {
            for(int j = -HALF_ROW; j < HALF_ROW; j++) {
                for(int k = -HALF_ROW; k < HALF_ROW; k++) {
                    
                    Vector3 particlePosition = SPAWN_POINT + new Vector3(i, j, k) * PARTICLE_RADIUS + PARTICLE_RADIUS * SPAWN_VARIANCE * Random.onUnitSphere;
                    int id = (i + HALF_ROW) * ROW_COUNT * ROW_COUNT + (j + HALF_ROW) * ROW_COUNT + k + HALF_ROW;

                    GameObject particleInit = Instantiate(particleObj, particlePosition, Quaternion.identity);
                    particleInit.hideFlags = HideFlags.HideInHierarchy;

                    Particle particleInst = new()
                    {
                        ID = id,
                        position = particlePosition,
                        gameObject = particleInit,
                        currentForce = new(0.0f, PARTICLE_MASS * GRAVITY, 0.0f)
                    };

                    particles[id] = particleInst;
                }
            }
        }
    }

    //kernels
    public float Poly6(float r2) {
        //if(r2 > PARTICLE_EFFECT_RADIUS_SQUARED) return 0; don't need this check as it's done beforehand

        float x = 1.0f - r2 / PARTICLE_EFFECT_RADIUS_SQUARED;
        return 315.0f / (64.0f * Mathf.PI * PARTICLE_EFFECT_RADIUS_CUBED) * x * x * x;
    }

    public float SpikyKernelFirstDerivative(float dist) {
        float x = 1.0f - dist / PARTICLE_EFFECT_RADIUS;
        return -45.0f / (Mathf.PI * PARTICLE_EFFECT_RADIUS_FOURTH) * x * x;
    }

    public float SpikyKernelSecondDerivative(float dist) {
        float x = 1.0f - dist / PARTICLE_EFFECT_RADIUS;
        return 90.0f / (Mathf.PI * PARTICLE_EFFECT_RADIUS_FIFTH) * x;
    }

    public Vector3 SpikyKernelGradient(float dist, Vector3 dir) {
        return SpikyKernelFirstDerivative(dist) * dir;
    }

    public float ViscosityLaplacianKernel(float r) {
        if(r >= 0.0f && r <= PARTICLE_EFFECT_RADIUS) {
            return 45.0f / (Mathf.PI * Mathf.Pow(PARTICLE_EFFECT_RADIUS, 6)) * (PARTICLE_EFFECT_RADIUS - r);
        }
        
        return 0f;
    }

    //calculations
    public Particle CalculateDensityPressure(Particle particle) {
        float sum = 0;

        foreach(Particle other in particles) {
            if(ReferenceEquals(particle, other)) continue;

            Vector3 diff = particle.position - other.position;
            float diffSquared = Vector3.Dot(particle.position, other.position);

            if(diffSquared > PARTICLE_EFFECT_RADIUS_SQUARED) continue;

            sum += Poly6(diffSquared);
        }

        particle.density = sum * PARTICLE_MASS + EPSILON; //add really small value to prevent division by 0;
        particle.pressure = GAS_CONSTANT * (particle.density - TARGET_DENSITY);

        return particle;
    }

    public Particle ComputeForces(Particle particle) {
        Vector3 pos = particle.position;

        Vector3 pressureForce = Vector3.zero;
        Vector3 viscosityForce = Vector3.zero;

        foreach(Particle other in particles) {
            if(ReferenceEquals(particle, other)) continue;

            float dist = Vector3.Distance(other.position, pos);

            if(dist >= PARTICLE_RADIUS * 2 || dist <= 0.0f) continue;

            Vector3 pressureGradientDir = Vector3.Normalize(pos - other.position);
            float pressureTerm = (particle.pressure + other.pressure) / (2.0f * other.density);
            Vector3 pressureContribution = -PARTICLE_MASS * pressureTerm * SpikyKernelGradient(dist, pressureGradientDir);
            
            Vector3 velDiff = other.velocity - particle.velocity;
            Vector3 viscosityContribution = VISCOSITY * PARTICLE_MASS * velDiff / other.density;
            viscosityContribution *= ViscosityLaplacianKernel(dist);

            pressureForce += pressureContribution;
            viscosityForce += viscosityContribution;
        }

        particle.currentForce = new Vector3(0.0f, GRAVITY * PARTICLE_MASS, 0.0f) + pressureForce + viscosityForce;

        return particle;
    }
}