#if UNITY_EDITOR
using System;
using System.Linq;
using KMS.Audio.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class KMSPlayerHitSetup
    {
        private const string AnimatorControllerPath =
            "Assets/KMS/4.Animation/Dodo/Controllers/KMS_DodoAnimator.controller";
        private const string HitClipPath = "Assets/KMS/4.Animation/Dodo/Clips/Hit.anim";
        private const string HitClipSourcePath =
            "Assets/100.Base/Shooter/Art/Animations/Armature_Shoot_HitReaction.fbx";
        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
        };

        [MenuItem("Tools/KMS/Configure Player Hit Feedback")]
        public static void ConfigurePlayerHitFeedback()
        {
            AnimationClip hitClip = EnsureKmsHitClip();
            ConfigureAnimator(hitClip);

            foreach (string prefabPath in PlayerPrefabPaths)
            {
                ConfigurePrefab(prefabPath);
            }

            KMSAudioSetupTool.Run();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMSPlayerHitSetup] 플레이어 피격 모션/사운드 연결을 완료했습니다.");
        }

        private static AnimationClip EnsureKmsHitClip()
        {
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(HitClipPath);
            if (existing != null) return existing;

            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(HitClipSourcePath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (source == null)
                throw new InvalidOperationException($"피격 애니메이션 원본을 찾지 못했습니다: {HitClipSourcePath}");

            AnimationClip copy = UnityEngine.Object.Instantiate(source);
            copy.name = "Hit";
            copy.wrapMode = WrapMode.Once;
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
            settings.loopTime = false;
            AnimationUtility.SetAnimationClipSettings(copy, settings);
            AssetDatabase.CreateAsset(copy, HitClipPath);
            return copy;
        }

        private static void ConfigureAnimator(AnimationClip hitClip)
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            if (controller == null)
                throw new InvalidOperationException($"Animator Controller를 찾지 못했습니다: {AnimatorControllerPath}");

            EnsureTrigger(controller, "Hit");
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState locomotion = machine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == "Locomotion");
            if (locomotion == null)
                throw new InvalidOperationException("KMS_DodoAnimator에 Locomotion 상태가 없습니다.");

            AnimatorState hit = machine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == "Hit");
            if (hit == null) hit = machine.AddState("Hit", new Vector3(650f, 340f));
            hit.motion = hitClip;
            hit.writeDefaultValues = true;

            AnimatorStateTransition toHit = machine.anyStateTransitions
                .FirstOrDefault(transition => transition.destinationState == hit);
            if (toHit == null)
            {
                toHit = machine.AddAnyStateTransition(hit);
                toHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            }
            toHit.hasExitTime = false;
            toHit.duration = 0.05f;
            toHit.canTransitionToSelf = false;

            AnimatorStateTransition toLocomotion = hit.transitions
                .FirstOrDefault(transition => transition.destinationState == locomotion);
            if (toLocomotion == null) toLocomotion = hit.AddTransition(locomotion);
            toLocomotion.hasExitTime = true;
            toLocomotion.exitTime = 0.9f;
            toLocomotion.duration = 0.08f;

            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                if (root.GetComponent<PlayerHitFeedbackController>() == null)
                    root.AddComponent<PlayerHitFeedbackController>();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureTrigger(AnimatorController controller, string parameterName)
        {
            AnimatorControllerParameter parameter = controller.parameters
                .FirstOrDefault(candidate => candidate.name == parameterName);
            if (parameter == null)
            {
                controller.AddParameter(parameterName, AnimatorControllerParameterType.Trigger);
                return;
            }

            if (parameter.type != AnimatorControllerParameterType.Trigger)
                throw new InvalidOperationException($"Animator 파라미터 '{parameterName}'의 타입이 Trigger가 아닙니다.");
        }

    }
}
#endif
