using System.Collections;
using UnityEngine;

namespace KMS.Audio
{
    /// <summary>
    /// 10~30초 사이의 무작위 간격으로 Wind 또는 Bird 환경음을 재생합니다.
    /// KMSAudioService에 자동으로 추가되며 씬이 바뀌어도 유지됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KMSRandomAmbientSfxPlayer : MonoBehaviour
    {
        [Min(0.1f)]
        [SerializeField] private float minimumDelay = 10f;
        [Min(0.1f)]
        [SerializeField] private float maximumDelay = 30f;

        private Coroutine playbackRoutine;

        private void OnEnable()
        {
            playbackRoutine = StartCoroutine(PlayAmbientLoop());
        }

        private void OnDisable()
        {
            if (playbackRoutine != null)
            {
                StopCoroutine(playbackRoutine);
                playbackRoutine = null;
            }
        }

        private IEnumerator PlayAmbientLoop()
        {
            while (isActiveAndEnabled)
            {
                float min = Mathf.Max(0.1f, minimumDelay);
                float max = Mathf.Max(min, maximumDelay);
                yield return new WaitForSeconds(Random.Range(min, max));

                GameSfxId ambientId = Random.value < 0.5f
                    ? GameSfxId.Wind
                    : GameSfxId.Bird;
                KMSAudioService.Play2D(ambientId);
            }

            playbackRoutine = null;
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
