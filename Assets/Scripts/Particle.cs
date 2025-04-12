using UnityEngine;

public class Particle : MonoBehaviour {

    [Header("Properties")]
    public int ID;
    private Vector3 _velocity;
    private Vector3 _acceleration;

    // setters
    public void SetVelocity(Vector3 velocity) {
        _velocity = velocity;
    }

    public void SetAcceleration(Vector3 acceleration) {
        _acceleration = acceleration;
    }

    // getters
    public Vector3 GetVelocity(Vector3 velocity) {
        return _velocity;
    }

    public Vector3 GetAcceleration(Vector3 acceleration) {
        return _acceleration;
    }

    public void Start() {
        _acceleration = new Vector3(Random.Range(0, 10), ParticleManager.GRAVITY, Random.Range(0, 10));
    }

    public void Update() {
        float deltaTime = Time.deltaTime;

        _velocity += _acceleration * deltaTime;
        transform.position += _velocity * deltaTime - 0.5f * Mathf.Pow(deltaTime, 2) * _acceleration;

        ResolveCollisions();

        ParticleManager.positions[ID] = transform.position;
    }

    private void ResolveCollisions() {
        int walls = ParticleManager.BOX_SIZE;
        
        if(transform.position.y <= 0.0f) {
            Vector3 pos = transform.position;
            pos.y = 0.0f;
            transform.position = pos;

            _velocity.y *= -0.9f;

            if(Mathf.Abs(_velocity.y) < 0.1f) _velocity.y = 0.0f;
        }

        if (Mathf.Abs(transform.position.x) >= walls) {
            Vector3 pos = transform.position;
            pos.x = (transform.position.x > 0.0f) ? walls : -walls;
            transform.position = pos;

            _velocity.x *= -0.9f;
        }

        if (Mathf.Abs(transform.position.z) >= walls) {
            Vector3 pos = transform.position;
            pos.z = (transform.position.z > 0.0f) ? walls : -walls;
            transform.position = pos;

            _velocity.z *= -0.9f;
        }
    }
}