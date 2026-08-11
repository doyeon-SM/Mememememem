using System.Collections;
using GH.World;
using HDY.Territory;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.Audio
{
    /// <summary>
    /// Plays scene-specific world ambience from the persistent KMSAudioService.
    /// Wind is available in the main world and cave, while birds are limited to the main world.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSRandomAmbientSfxPlayer : MonoBehaviour
    {
        private const string MainWorldSceneName = "Main_World_3";
        private const string CaveSceneName = "Main_World_Cave";
        private const float NightBlendThreshold = 0.5f;
        private const float FallbackSunriseNormalizedTime = 0.06f;
        private const float FallbackSunsetNormalizedTime = 0.5f;

        private enum AmbientSceneMode
        {
            Disabled,
            MainWorld,
            Cave
        }

        [Min(0.1f)]
        [SerializeField] private float minimumDelay = 10f;
        [Min(0.1f)]
        [SerializeField] private float maximumDelay = 30f;

        private Coroutine playbackRoutine;
        private AmbientSceneMode activeSceneMode;
        private GHWorldDayNightSkyController dayNightSkyController;

        private void OnEnable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            RefreshPlayback(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            StopAmbientPlayback();
        }

        private void HandleActiveSceneChanged(Scene _, Scene nextScene)
        {
            RefreshPlayback(nextScene);
        }

        private void RefreshPlayback(Scene activeScene)
        {
            AmbientSceneMode nextMode = GetSceneMode(activeScene);

            // A supported-to-supported transition still needs a full cleanup so, for example,
            // a bird started in Main_World_3 cannot continue playing inside the cave.
            StopAmbientPlayback();
            activeSceneMode = nextMode;
            dayNightSkyController = null;

            if (activeSceneMode != AmbientSceneMode.Disabled)
            {
                StartAmbientPlayback();
            }
        }

        private static AmbientSceneMode GetSceneMode(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return AmbientSceneMode.Disabled;
            }

            return scene.name switch
            {
                MainWorldSceneName => AmbientSceneMode.MainWorld,
                CaveSceneName => AmbientSceneMode.Cave,
                _ => AmbientSceneMode.Disabled
            };
        }

        private void StartAmbientPlayback()
        {
            if (playbackRoutine == null)
            {
                playbackRoutine = StartCoroutine(PlayAmbientLoop());
            }
        }

        private void StopAmbientPlayback()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }

            if (!KMSAudioService.HasInstance)
            {
                return;
            }

            KMSAudioService.StopSfx(GameSfxId.Wind);
            KMSAudioService.StopSfx(GameSfxId.Bird);
            KMSAudioService.StopSfx(GameSfxId.NightBird);
        }

        private IEnumerator PlayAmbientLoop()
        {
            while (isActiveAndEnabled)
            {
                float min = Mathf.Max(0.1f, minimumDelay);
                float max = Mathf.Max(min, maximumDelay);
                yield return new WaitForSeconds(Random.Range(min, max));

                if (activeSceneMode == AmbientSceneMode.Disabled)
                {
                    break;
                }

                KMSAudioService.Play2D(SelectAmbientId());
            }

            playbackRoutine = null;
        }

        private GameSfxId SelectAmbientId()
        {
            if (activeSceneMode == AmbientSceneMode.Cave || Random.value < 0.5f)
            {
                return GameSfxId.Wind;
            }

            return IsNightTime()
                ? GameSfxId.NightBird
                : GameSfxId.Bird;
        }

        private bool IsNightTime()
        {
            if (dayNightSkyController == null)
            {
                dayNightSkyController = FindFirstObjectByType<GHWorldDayNightSkyController>();
            }

            if (dayNightSkyController != null)
            {
                return dayNightSkyController.CurrentNightBlend >= NightBlendThreshold;
            }

            // The sky controller should exist in Main_World_3. This fallback keeps the audio
            // policy deterministic if its initialization order or scene setup changes later.
            GameTimeManager gameTimeManager = GameTimeManager.Instance;
            if (gameTimeManager == null || gameTimeManager.DayLengthSeconds <= 0f)
            {
                return false;
            }

            float normalizedTime = Mathf.Repeat(
                gameTimeManager.InGameTimeOfDaySeconds / gameTimeManager.DayLengthSeconds,
                1f);
            return normalizedTime < FallbackSunriseNormalizedTime
                || normalizedTime >= FallbackSunsetNormalizedTime;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minimumDelay = Mathf.Max(0.1f, minimumDelay);
            maximumDelay = Mathf.Max(minimumDelay, maximumDelay);
        }
#endif
    }
}
