using UnityEngine;

namespace KMS.Effects.DamageNumbers
{
    [CreateAssetMenu(
        fileName = "KMSDamagePopupSettings",
        menuName = "KMS/Effects/Damage Popup Settings")]
    public sealed class KMSDamagePopupSettings : ScriptableObject
    {
        public const string ResourcesPath = "KMS/DamagePopupSettings";

        [Header("Backend")]
        [Tooltip("로컬에 Damage Numbers Pro와 전용 프리팹이 있으면 우선 사용합니다.")]
        public bool preferDamageNumbersPro = true;

        [Tooltip("Damage Numbers Pro 전용 로컬 프리팹의 Resources 경로입니다.")]
        public string damageNumbersProResourcesPath = "KMSLocal/MemDamageNumber";

        [Header("Placement")]
        [Min(0f)] public float minimumHeightOffset = 1.55f;
        [Min(0f)] public float rendererTopPadding = 0.2f;
        [Min(0f)] public float spawnJitter = 0.08f;

        [Header("Fallback Animation")]
        [Min(0.1f)] public float lifetime = 1.05f;
        [Min(0f)] public float riseDistance = 0.75f;
        [Min(0f)] public float sideDrift = 0.16f;
        [Range(0f, 0.95f)] public float fadeStart = 0.55f;
        [Min(0.01f)] public float baseScale = 1f;
        [Min(0.01f)] public float spawnScale = 0.7f;
        [Min(0.01f)] public float overshootScale = 1.18f;
        [Min(0.01f)] public float referenceOrthographicSize = 5f;

        [Header("Fallback Style")]
        [Min(0.1f)] public float fontSize = 5f;
        public Color normalColor = new Color(1f, 0.72f, 0.22f, 1f);
        public Color largeDamageColor = new Color(1f, 0.2f, 0.12f, 1f);
        public Color outlineColor = new Color(0.08f, 0.025f, 0.015f, 1f);
        [Range(0f, 1f)] public float outlineWidth = 0.22f;
        [Min(1)] public int largeDamageThreshold = 10;
        [Range(1f, 2f)] public float maximumDamageScale = 1.45f;

        [Header("Fallback Pool")]
        [Min(0)] public int prewarmCount = 12;
        [Min(1)] public int retainedPoolSize = 48;

        public static KMSDamagePopupSettings LoadOrCreateRuntimeDefaults()
        {
            KMSDamagePopupSettings settings = Resources.Load<KMSDamagePopupSettings>(ResourcesPath);
            if (settings != null)
            {
                return settings;
            }

            settings = CreateInstance<KMSDamagePopupSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            return settings;
        }
    }
}
