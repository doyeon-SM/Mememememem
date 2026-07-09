using UnityEngine;

namespace GH.Loading
{
    public readonly struct LoadingProgress
    {
        public LoadingProgress(string description, float normalizedProgress)
        {
            Description = description;
            NormalizedProgress = Mathf.Clamp01(normalizedProgress);
        }

        public string Description { get; }
        public float NormalizedProgress { get; }
        public int Percent => Mathf.RoundToInt(NormalizedProgress * 100f);
    }
}
