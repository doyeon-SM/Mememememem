using System.Collections;
using System.Collections.Generic;
using KMS.InventoryDuped;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.Audio
{
    [DisallowMultipleComponent]
    public sealed class KMSAudioService : MonoBehaviour
    {
        private const string CatalogResourceName = "KMSAudioCatalog";
        private const string SfxVolumeKey = "KMS.Audio.SfxVolume";
        private const string MusicVolumeKey = "KMS.Audio.MusicVolume";
        private const int InitialSourceCount = 12;
        private const int MaximumSourceCount = 32;

        private static KMSAudioService instance;

        [SerializeField] private KMSAudioCatalog catalog;

        private readonly List<AudioSource> sfxSources = new List<AudioSource>();
        private readonly Dictionary<AudioSource, GameSfxId> sourceCueIds =
            new Dictionary<AudioSource, GameSfxId>();
        private readonly Dictionary<GameSfxId, float> lastPlayTimes =
            new Dictionary<GameSfxId, float>();

        private AudioSource musicSourceA;
        private AudioSource musicSourceB;
        private AudioSource activeMusicSource;
        private Coroutine musicFadeRoutine;
        private int sourceReplacementIndex;
        private float sfxVolume = 1f;
        private float musicVolume = 1f;

        public static KMSAudioService Instance => EnsureInstance();
        public static bool HasInstance => instance != null;
        public KMSAudioCatalog Catalog => catalog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static KMSAudioService EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<KMSAudioService>();
            if (instance != null)
            {
                return instance;
            }

            var root = new GameObject("KMS Audio Service");
            instance = root.AddComponent<KMSAudioService>();
            return instance;
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

            if (catalog == null)
            {
                catalog = Resources.Load<KMSAudioCatalog>(CatalogResourceName);
            }

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<KMSAudioCatalog>();
            }

            catalog.EnsureDefaults();
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));

            CreateMusicSources();
            for (int i = 0; i < InitialSourceCount; i++)
            {
                CreateSfxSource();
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        public static bool Play2D(GameSfxId id)
        {
            return EnsureInstance().Play(id, Vector3.zero, true);
        }

        public static bool PlayAt(GameSfxId id, Vector3 position)
        {
            return EnsureInstance().Play(id, position, false);
        }

        public static void SetSfxVolume(float value)
        {
            KMSAudioService service = EnsureInstance();
            service.sfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, service.sfxVolume);
        }

        public static void SetMusicVolume(float value)
        {
            KMSAudioService service = EnsureInstance();
            service.musicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, service.musicVolume);
            if (service.activeMusicSource != null
                && service.catalog.TryGetSceneMusic(
                    SceneManager.GetActiveScene().name,
                    out KMSSceneMusicEntry entry))
            {
                service.activeMusicSource.volume = entry.Volume * service.musicVolume;
            }
        }

        public void RefreshCurrentSceneMusic()
        {
            PlaySceneMusic(SceneManager.GetActiveScene().name);
        }

        private bool Play(GameSfxId id, Vector3 position, bool force2D)
        {
            if (catalog == null
                || !catalog.TryGetCue(id, out KMSAudioCue cue)
                || cue == null)
            {
                return false;
            }

            AudioClip clip = cue.PickClip();
            if (clip == null)
            {
                return false;
            }

            float now = Time.unscaledTime;
            if (cue.Cooldown > 0f
                && lastPlayTimes.TryGetValue(id, out float lastTime)
                && now - lastTime < cue.Cooldown)
            {
                return false;
            }

            if (CountPlayingVoices(id) >= Mathf.Max(1, cue.MaxVoices))
            {
                return false;
            }

            AudioSource source = GetAvailableSfxSource();
            source.Stop();
            source.clip = clip;
            source.outputAudioMixerGroup = cue.Output;
            source.volume = cue.Volume * sfxVolume;
            source.pitch = Random.Range(
                Mathf.Min(cue.PitchMin, cue.PitchMax),
                Mathf.Max(cue.PitchMin, cue.PitchMax));
            source.spatialBlend = force2D ? 0f : cue.SpatialBlend;
            source.transform.position = position;
            source.loop = false;
            sourceCueIds[source] = id;
            lastPlayTimes[id] = now;
            source.Play();
            return true;
        }

        private int CountPlayingVoices(GameSfxId id)
        {
            int count = 0;
            for (int i = 0; i < sfxSources.Count; i++)
            {
                AudioSource source = sfxSources[i];
                if (source.isPlaying
                    && sourceCueIds.TryGetValue(source, out GameSfxId playingId)
                    && playingId == id)
                {
                    count++;
                }
            }

            return count;
        }

        private AudioSource GetAvailableSfxSource()
        {
            for (int i = 0; i < sfxSources.Count; i++)
            {
                if (!sfxSources[i].isPlaying)
                {
                    return sfxSources[i];
                }
            }

            if (sfxSources.Count < MaximumSourceCount)
            {
                return CreateSfxSource();
            }

            AudioSource replacement =
                sfxSources[sourceReplacementIndex % sfxSources.Count];
            sourceReplacementIndex++;
            return replacement;
        }

        private AudioSource CreateSfxSource()
        {
            var sourceObject = new GameObject($"SFX Source {sfxSources.Count + 1:00}");
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1f;
            source.maxDistance = 25f;
            sfxSources.Add(source);
            return source;
        }

        private void CreateMusicSources()
        {
            musicSourceA = CreateMusicSource("Music Source A");
            musicSourceB = CreateMusicSource("Music Source B");
        }

        private AudioSource CreateMusicSource(string objectName)
        {
            var sourceObject = new GameObject(objectName);
            sourceObject.transform.SetParent(transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = true;
            return source;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode _)
        {
            AttachInventoryFeedback();
            PlaySceneMusic(scene.name);
        }

        private void AttachInventoryFeedback()
        {
            PlayerInventory[] inventories = FindObjectsByType<PlayerInventory>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (PlayerInventory inventory in inventories)
            {
                if (inventory.GetComponent<KMSInventoryAudioFeedback>() == null)
                {
                    inventory.gameObject.AddComponent<KMSInventoryAudioFeedback>();
                }
            }
        }

        private void PlaySceneMusic(string sceneName)
        {
            if (catalog == null
                || !catalog.TryGetSceneMusic(sceneName, out KMSSceneMusicEntry entry))
            {
                return;
            }

            if (entry.Clip == null)
            {
                FadeOutCurrentMusic(entry.FadeSeconds);
                return;
            }

            if (activeMusicSource != null
                && activeMusicSource.isPlaying
                && activeMusicSource.clip == entry.Clip)
            {
                activeMusicSource.outputAudioMixerGroup = entry.Output;
                activeMusicSource.loop = entry.Loop;
                activeMusicSource.volume = entry.Volume * musicVolume;
                return;
            }

            AudioSource incoming =
                activeMusicSource == musicSourceA ? musicSourceB : musicSourceA;
            AudioSource outgoing = activeMusicSource;

            incoming.Stop();
            incoming.clip = entry.Clip;
            incoming.outputAudioMixerGroup = entry.Output;
            incoming.loop = entry.Loop;
            incoming.volume = 0f;
            incoming.Play();
            activeMusicSource = incoming;

            StartMusicFade(
                outgoing,
                incoming,
                entry.Volume * musicVolume,
                entry.FadeSeconds);
        }

        private void FadeOutCurrentMusic(float fadeSeconds)
        {
            if (activeMusicSource == null || !activeMusicSource.isPlaying)
            {
                return;
            }

            AudioSource outgoing = activeMusicSource;
            activeMusicSource = null;
            StartMusicFade(outgoing, null, 0f, fadeSeconds);
        }

        private void StartMusicFade(
            AudioSource outgoing,
            AudioSource incoming,
            float targetVolume,
            float fadeSeconds)
        {
            if (musicFadeRoutine != null)
            {
                StopCoroutine(musicFadeRoutine);
            }

            musicFadeRoutine = StartCoroutine(
                CrossfadeRoutine(outgoing, incoming, targetVolume, fadeSeconds));
        }

        private IEnumerator CrossfadeRoutine(
            AudioSource outgoing,
            AudioSource incoming,
            float targetVolume,
            float fadeSeconds)
        {
            float duration = Mathf.Max(0f, fadeSeconds);
            float outgoingStart = outgoing != null ? outgoing.volume : 0f;
            float elapsed = 0f;

            if (duration <= 0f)
            {
                if (outgoing != null)
                {
                    outgoing.Stop();
                    outgoing.volume = 0f;
                }

                if (incoming != null)
                {
                    incoming.volume = targetVolume;
                }

                musicFadeRoutine = null;
                yield break;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                if (outgoing != null)
                {
                    outgoing.volume = Mathf.Lerp(outgoingStart, 0f, progress);
                }

                if (incoming != null)
                {
                    incoming.volume = Mathf.Lerp(0f, targetVolume, progress);
                }

                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.Stop();
                outgoing.volume = 0f;
            }

            if (incoming != null)
            {
                incoming.volume = targetVolume;
            }

            musicFadeRoutine = null;
        }
    }
}
