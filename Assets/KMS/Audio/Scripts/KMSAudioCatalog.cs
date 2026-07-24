using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace KMS.Audio
{
    public enum GameSfxId
    {
        ToolSwing,
        AxeHitTree,
        PickaxeHitStone,
        HoeHitBush,
        ClubHitMem,
        FootstepWalk,
        FootstepRun,
        Jump,
        Land,
        CaptureSuccess,
        CaptureFailure,
        ItemObtained,
        QuickSlotSelected
    }

    [Serializable]
    public sealed class KMSAudioCue
    {
        public GameSfxId Id;
        public AudioClip[] Clips = Array.Empty<AudioClip>();
        [Range(0f, 1f)] public float Volume = 1f;
        [Range(0.5f, 2f)] public float PitchMin = 0.96f;
        [Range(0.5f, 2f)] public float PitchMax = 1.04f;
        [Range(0f, 1f)] public float SpatialBlend = 1f;
        [Min(0f)] public float Cooldown;
        [Min(1)] public int MaxVoices = 4;
        public AudioMixerGroup Output;

        [NonSerialized] private int lastClipIndex = -1;

        public AudioClip PickClip()
        {
            if (Clips == null || Clips.Length == 0)
            {
                return null;
            }

            if (Clips.Length == 1)
            {
                lastClipIndex = 0;
                return Clips[0];
            }

            int index = UnityEngine.Random.Range(0, Clips.Length - 1);
            if (index >= lastClipIndex)
            {
                index++;
            }

            lastClipIndex = index;
            return Clips[index];
        }
    }

    [Serializable]
    public sealed class KMSSceneMusicEntry
    {
        public string SceneName;
        public AudioClip Clip;
        [Range(0f, 1f)] public float Volume = 0.65f;
        [Min(0f)] public float FadeSeconds = 1.25f;
        public bool Loop = true;
        public AudioMixerGroup Output;
    }

    [CreateAssetMenu(
        fileName = "KMSAudioCatalog",
        menuName = "KMS/Audio/Audio Catalog",
        order = 0)]
    public sealed class KMSAudioCatalog : ScriptableObject
    {
        [SerializeField] private List<KMSAudioCue> sfx = new List<KMSAudioCue>();
        [SerializeField] private List<KMSSceneMusicEntry> sceneMusic = new List<KMSSceneMusicEntry>();

        private Dictionary<GameSfxId, KMSAudioCue> cueLookup;
        private Dictionary<string, KMSSceneMusicEntry> musicLookup;

        public IReadOnlyList<KMSAudioCue> Sfx => sfx;
        public IReadOnlyList<KMSSceneMusicEntry> SceneMusic => sceneMusic;

        private void OnEnable()
        {
            RebuildLookup();
        }

        private void OnValidate()
        {
            RebuildLookup();
        }

        public bool TryGetCue(GameSfxId id, out KMSAudioCue cue)
        {
            if (cueLookup == null)
            {
                RebuildLookup();
            }

            return cueLookup.TryGetValue(id, out cue);
        }

        public bool TryGetSceneMusic(string sceneName, out KMSSceneMusicEntry entry)
        {
            if (musicLookup == null)
            {
                RebuildLookup();
            }

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                entry = null;
                return false;
            }

            return musicLookup.TryGetValue(sceneName, out entry);
        }

        public void EnsureDefaults()
        {
            sfx ??= new List<KMSAudioCue>();
            sceneMusic ??= new List<KMSSceneMusicEntry>();

            foreach (GameSfxId id in Enum.GetValues(typeof(GameSfxId)))
            {
                if (sfx.Exists(cue => cue != null && cue.Id == id))
                {
                    continue;
                }

                sfx.Add(CreateDefaultCue(id));
            }

            EnsureSceneMusic("Territory");
            EnsureSceneMusic("Main_World");
            EnsureSceneMusic("Main_World_2");
            RebuildLookup();
        }

        public void SetCueClips(GameSfxId id, AudioClip[] clips)
        {
            EnsureDefaults();
            if (TryGetCue(id, out KMSAudioCue cue))
            {
                cue.Clips = clips ?? Array.Empty<AudioClip>();
            }
        }

        public void SetSceneMusicClip(string sceneName, AudioClip clip)
        {
            EnsureDefaults();
            if (TryGetSceneMusic(sceneName, out KMSSceneMusicEntry entry))
            {
                entry.Clip = clip;
            }
        }

        private static KMSAudioCue CreateDefaultCue(GameSfxId id)
        {
            var cue = new KMSAudioCue { Id = id };

            switch (id)
            {
                case GameSfxId.FootstepWalk:
                    cue.Volume = 0.65f;
                    cue.Cooldown = 0.08f;
                    cue.MaxVoices = 2;
                    break;
                case GameSfxId.FootstepRun:
                    cue.Volume = 0.8f;
                    cue.Cooldown = 0.06f;
                    cue.MaxVoices = 2;
                    break;
                case GameSfxId.ItemObtained:
                case GameSfxId.QuickSlotSelected:
                    cue.SpatialBlend = 0f;
                    cue.Volume = 0.8f;
                    cue.Cooldown = 0.04f;
                    cue.MaxVoices = 2;
                    break;
                case GameSfxId.ToolSwing:
                    cue.Volume = 0.75f;
                    cue.Cooldown = 0.08f;
                    cue.MaxVoices = 2;
                    break;
                default:
                    cue.Volume = 1f;
                    break;
            }

            return cue;
        }

        private void EnsureSceneMusic(string sceneName)
        {
            if (sceneMusic.Exists(entry =>
                    entry != null
                    && string.Equals(entry.SceneName, sceneName, StringComparison.Ordinal)))
            {
                return;
            }

            sceneMusic.Add(new KMSSceneMusicEntry { SceneName = sceneName });
        }

        private void RebuildLookup()
        {
            cueLookup = new Dictionary<GameSfxId, KMSAudioCue>();
            if (sfx != null)
            {
                foreach (KMSAudioCue cue in sfx)
                {
                    if (cue != null && !cueLookup.ContainsKey(cue.Id))
                    {
                        cueLookup.Add(cue.Id, cue);
                    }
                }
            }

            musicLookup = new Dictionary<string, KMSSceneMusicEntry>(StringComparer.Ordinal);
            if (sceneMusic != null)
            {
                foreach (KMSSceneMusicEntry entry in sceneMusic)
                {
                    if (entry == null
                        || string.IsNullOrWhiteSpace(entry.SceneName)
                        || musicLookup.ContainsKey(entry.SceneName))
                    {
                        continue;
                    }

                    musicLookup.Add(entry.SceneName, entry);
                }
            }
        }
    }
}
