using UnityEngine;

[System.Serializable]
public struct Particle {
    public int ID;
    public float density;
    public float pressure;

    public Vector3 currentForce;
    public Vector3 velocity;
    public Vector3 position;

    public GameObject gameObject;

    public void ResolveCollisions(int boxSize) {
        int walls = boxSize;
        
        if(position.y <= 0.0f) {
            Vector3 pos = position;
            pos.y = 0.0f;
            position = pos;

            velocity.y *= -0.9f;

            if(Mathf.Abs(velocity.y) < 0.1f) velocity.y = 0.0f; //stop if negligible bounce
        }

        if (Mathf.Abs(position.x) >= walls) {
            Vector3 pos = position;
            pos.x = (position.x > 0.0f) ? walls : -walls;
            position = pos;

            velocity.x *= -0.9f;
        }

        if (Mathf.Abs(position.z) >= walls) {
            Vector3 pos = position;
            pos.z = (position.z > 0.0f) ? walls : -walls;
            position = pos;

            velocity.z *= -0.9f;
        }
    }
}

public class ParticleManager : MonoBehaviour {
    
    [Header("General Constants")]
    public const int BOX_SIZE = 10;
    public const float HALF_BOX = BOX_SIZE / 2;

    [Header("Particle Properties")]
    public const float PARTICLE_MASS = 1.0f;
    public const float PARTICLE_MASS_SQUARED = PARTICLE_MASS * PARTICLE_MASS;
    public const float RECIPROCAL_MASS = 1 / PARTICLE_MASS;
    public const float TARGET_DENSITY = 25.0f;
    public const float GAS_CONSTANT = 1.0f;
    public const float VISCOSITY = 1.0f;

    [Header("Particle Settings")]
    public const int ROW_COUNT = 10;
    public const int HALF_ROW = ROW_COUNT / 2;
    public const int PARTICLE_COUNT = ROW_COUNT * ROW_COUNT * ROW_COUNT;
    public const float PARTICLE_RADIUS = 0.2f;
    public const float PARTICLE_EFFECT_RADIUS = 1.0f;
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
        for(int i = 0; i < PARTICLE_COUNT; i++) {
            Particle currentParticle = particles[i];

            currentParticle.velocity += RECIPROCAL_MASS * Time.deltaTime * currentParticle.currentForce; //multiplying is faster than dividing
            currentParticle.position += currentParticle.velocity * Time.deltaTime;

            currentParticle.ResolveCollisions(BOX_SIZE);
            currentParticle = CalculateDensityPressure(currentParticle);
            currentParticle = ComputeForces(currentParticle);

            currentParticle.gameObject.transform.position = currentParticle.position;
            particles[i] = currentParticle;
        }
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
                        currentForce = new(0, GRAVITY, 0),
                        gameObject = particleInit
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

    //calculations
    public Particle CalculateDensityPressure(Particle particle) {
        float sum = 0;

        for(int i = 0; i < PARTICLE_COUNT; i++) {
            if(ReferenceEquals(particle, particles[i])) continue;

            Vector3 diff = particle.position - particles[i].position;
            float diffSquared = Vector3.Dot(diff, diff);

            if(PARTICLE_EFFECT_RADIUS_SQUARED >= diffSquared) {
                sum += Poly6(diffSquared);
            }
        }

        particle.density = sum * PARTICLE_MASS + 0.00001f; //add really small value to prevent division by 0;
        particle.pressure = GAS_CONSTANT * (particle.density - TARGET_DENSITY);

        return particle;
    }

    public Particle ComputeForces(Particle particle) {
        Vector3 pos = particle.position;
        float densitySquared = particle.density * particle.density;

        Vector3 pressure = Vector3.zero;
        Vector3 viscosity = Vector3.zero;

        for(int i = 0; i < PARTICLE_COUNT; i++) {
            if(ReferenceEquals(particle, particles[i])) continue;

            float dist = Vector3.Dot(particles[i].position, pos);

            if(dist > PARTICLE_RADIUS * 2) continue;

            Vector3 pressureGradientDir = Vector3.Normalize(particles[i].position - pos);
            Vector3 pressureContribution = PARTICLE_MASS_SQUARED * SpikyKernelGradient(dist, pressureGradientDir);
            pressureContribution *= particles[i].pressure / densitySquared + particles[i].pressure / (particles[i].density * particles[i].density);

            Vector3 viscosityContribution = VISCOSITY * PARTICLE_MASS_SQUARED * (particles[i].velocity - particle.velocity) / particles[i].density;
            viscosityContribution *= SpikyKernelSecondDerivative(dist);

            pressure += pressureContribution;
            viscosity += viscosityContribution;
        }

        particle.currentForce = new Vector3(0.0f, GRAVITY * PARTICLE_MASS, 0.0f) - pressure + viscosity;

        return particle;
    }
}