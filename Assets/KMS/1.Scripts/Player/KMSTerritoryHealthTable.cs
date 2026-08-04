using UnityEngine;

namespace KMS
{
    [CreateAssetMenu(
        fileName = "KMSTerritoryHealthTable",
        menuName = "KMS/Player/Territory Health Table")]
    public sealed class KMSTerritoryHealthTable : ScriptableObject
    {
        [Tooltip("Maximum health by territory level. Index 0 represents level 1.")]
        [SerializeField] private float[] maxHealthByLevel =
        {
            100f,
            120f,
            140f,
            160f,
            200f,
            220f,
            240f,
            260f,
            280f,
            300f,
            320f
        };

        public int LevelCount => maxHealthByLevel?.Length ?? 0;

        public float GetMaxHealth(int territoryLevel)
        {
            if (maxHealthByLevel == null || maxHealthByLevel.Length == 0)
            {
                return 100f;
            }

            int index = Mathf.Clamp(territoryLevel - 1, 0, maxHealthByLevel.Length - 1);
            return Mathf.Max(1f, maxHealthByLevel[index]);
        }

        private void OnValidate()
        {
            if (maxHealthByLevel == null) return;

            for (int i = 0; i < maxHealthByLevel.Length; i++)
            {
                maxHealthByLevel[i] = Mathf.Max(1f, maxHealthByLevel[i]);
            }
        }
    }
}
