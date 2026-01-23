using System;
using System.Collections.Generic;
using UnityEngine;

public class UtilityScorer
{
    private readonly List<float> considerations = new();

    // Add a normalized 0-1 consideration score
    public void AddConsideration(float value)
    {
        considerations.Add(Mathf.Clamp01(value)); // ensure 0-1
    }

    // Calculate the final utility score
    public float GetScore()
    {
        int n = considerations.Count;
        if (n == 0) return 0f;

        // Multiply all considerations
        float raw = 1f;
        foreach (var c in considerations)
        {
            raw *= c;
            if (raw == 0f || float.IsNaN(raw) || float.IsInfinity(raw))
                return 0f;
        }

        // Compensation factor to prevent too-small scores
        float modFactor = 1f - (1f / n);
        float makeup = (1f - raw) * modFactor;
        float finalScore = raw + (makeup * raw);

        return Mathf.Clamp01(finalScore); // keep result 0-1
    }

    // Clear considerations for next calculation
    public void Reset()
    {
        considerations.Clear();
    }
}
