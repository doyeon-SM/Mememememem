using System.Collections.Generic;
using MemSystem.Core;
using MemSystem.Events;
using UnityEngine;

namespace KMS.Effects.DamageNumbers
{
    [DisallowMultipleComponent]
    public sealed class KMSMemDamagePopupService : MonoBehaviour
    {
        private const string ServiceObjectName = "[KMS] Mem Damage Popup Service";

        private static KMSMemDamagePopupService instance;

        private readonly Queue<KMSFallbackDamagePopup> fallbackPool =
            new Queue<KMSFallbackDamagePopup>();

        private KMSDamagePopupSettings settings;
        private KMSDamageNumbersProBackend damageNumbersProBackend;
        private Transform poolRoot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (instance != null)
            {
                return;
            }

            KMSMemDamagePopupService existing = FindFirstObjectByType<KMSMemDamagePopupService>();
            if (existing != null)
            {
                instance = existing;
                return;
            }

            GameObject serviceObject = new GameObject(ServiceObjectName);
            instance = serviceObject.AddComponent<KMSMemDamagePopupService>();
            DontDestroyOnLoad(serviceObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            settings = KMSDamagePopupSettings.LoadOrCreateRuntimeDefaults();
            damageNumbersProBackend = new KMSDamageNumbersProBackend(settings);

            GameObject poolObject = new GameObject("Fallback Popup Pool");
            poolRoot = poolObject.transform;
            poolRoot.SetParent(transform, false);

            int prewarmCount = Mathf.Min(settings.prewarmCount, settings.retainedPoolSize);
            for (int index = 0; index < prewarmCount; index++)
            {
                fallbackPool.Enqueue(CreateFallbackPopup());
            }
        }

        private void OnEnable()
        {
            MemEvents.OnMemDamaged -= HandleMemDamaged;
            MemEvents.OnMemDamaged += HandleMemDamaged;
        }

        private void OnDisable()
        {
            MemEvents.OnMemDamaged -= HandleMemDamaged;
        }

        private void HandleMemDamaged(Mem mem, int damage)
        {
            if (mem == null || damage <= 0 || !mem.IsActive)
            {
                return;
            }

            Vector3 position = ResolvePopupPosition(mem);
            if (damageNumbersProBackend != null
                && damageNumbersProBackend.TrySpawn(position, damage))
            {
                return;
            }

            SpawnFallback(position, damage);
        }

        private Vector3 ResolvePopupPosition(Mem mem)
        {
            Vector3 rootPosition = mem.transform.position;
            float popupY = rootPosition.y + settings.minimumHeightOffset;

            Renderer[] renderers = mem.GetComponentsInChildren<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer current = renderers[index];
                if (current == null || !current.enabled || current is ParticleSystemRenderer)
                {
                    continue;
                }

                popupY = Mathf.Max(popupY, current.bounds.max.y + settings.rendererTopPadding);
            }

            Camera camera = Camera.main;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            float jitter = Random.Range(-settings.spawnJitter, settings.spawnJitter);
            return new Vector3(rootPosition.x, popupY, rootPosition.z) + right * jitter;
        }

        private void SpawnFallback(Vector3 position, int damage)
        {
            KMSFallbackDamagePopup popup = fallbackPool.Count > 0
                ? fallbackPool.Dequeue()
                : CreateFallbackPopup();

            popup.Play(position, damage, Camera.main);
        }

        private KMSFallbackDamagePopup CreateFallbackPopup()
        {
            GameObject popupObject = new GameObject(
                "KMS Fallback Damage Popup",
                typeof(KMSFallbackDamagePopup));

            popupObject.transform.SetParent(poolRoot, false);
            KMSFallbackDamagePopup popup = popupObject.GetComponent<KMSFallbackDamagePopup>();
            popup.Configure(settings, ReleaseFallbackPopup);
            return popup;
        }

        private void ReleaseFallbackPopup(KMSFallbackDamagePopup popup)
        {
            if (popup == null)
            {
                return;
            }

            popup.transform.SetParent(poolRoot, false);
            if (fallbackPool.Count < settings.retainedPoolSize)
            {
                fallbackPool.Enqueue(popup);
            }
            else
            {
                Destroy(popup.gameObject);
            }
        }
    }
}
