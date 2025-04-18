using System;
using System.Collections.Generic;
using UnityEngine;

//https://www.youtube.com/watch?v=zbBwKMRyavE
//https://www.youtube.com/watch?v=9M72KrGhYuE
//https://matthias-research.github.io/pages/publications/sca03.pdf
//https://cg.informatik.uni-freiburg.de/publications/2014_EG_SPH_STAR.pdf
//https://www.slideserve.com/Mia_John/animation-of-fluids
//https://mmacklin.com/pbf_sig_preprint.pdf

public class SpatialHashCPU : MonoBehaviour
{

    public const float CELL_SIZE = ParticleManagerCPU.PARTICLE_EFFECT_RADIUS;

    // property calculations
    public static Vector3Int CalculateCell(Vector3 position)
    {
        return Vector3Int.FloorToInt(position / CELL_SIZE);
    }

    public static uint CalculateCellHash(Vector3Int cell)
    {
        unchecked
        {
            int hash = cell.x * 73856093 ^
                       cell.y * 19349663 ^
                       cell.z * 83492791;
            return (uint)hash;
        }
    }

    // sort keys for spatial lookup
    public static Particle[] SortParticles(Particle[] particles)
    {
        Array.Sort(particles, (i, j) => i.hash.CompareTo(j.hash));

        return particles;
    }

    public static Dictionary<uint, List<int>> NeighbourTable(Particle[] particles)
    {
        Dictionary<uint, List<int>> neighbourTable = new();

        for (int i = 0; i < particles.Length; i++)
        {
            uint hash = particles[i].hash;

            if (!neighbourTable.ContainsKey(hash))
            {
                neighbourTable[hash] = new List<int>();
            }

            neighbourTable[hash].Add(i);
        }

        return neighbourTable;
    }

    public static HashSet<int> GetNeighbours(Dictionary<uint, List<int>> neighbourTable, Particle particle, Particle[] particles)
    {
        HashSet<int> neighbours = new();

        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    Vector3Int neighbourCell = particle.cell + new Vector3Int(dx, dy, dz);
                    uint neighbourHash = CalculateCellHash(neighbourCell);

                    if (neighbourTable.TryGetValue(neighbourHash, out var indices))
                    {
                        foreach (int index in indices)
                        {
                            if (particles[index].ID != particle.ID)
                            {
                                neighbours.Add(particles[index].ID);
                            }
                        }
                    }
                }
            }
        }

        return neighbours;
    }
}
