#if UNITY_EDITOR
using System.Linq;
using KMS.InventoryDuped;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class KMSPlayerDeathSetup
    {
        private const string AnimatorControllerPath = "Assets/KMS/4.Animation/Dodo/Controllers/KMS_DodoAnimator.controller";
        private const string DeathClipPath = "Assets/KMS/4.Animation/Dodo/Clips/Death2.anim";

        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/KMS/2.Prefabs/0705_Player_KMS.prefab",
            "Assets/KMS/2.Prefabs/0708_Player_KMS.prefab",
            "Assets/KMS/2.Prefabs/0712_Player_KMS.prefab",
            "Assets/KMS/2.Prefabs/0714_Player_KMS.prefab",
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
        };

        [MenuItem("Tools/KMS/Configure Player Death")]
        public static void ConfigurePlayerDeath()
        {
            ConfigureAnimator();

            foreach (string prefabPath in PlayerPrefabPaths)
            {
                ConfigurePlayerPrefab(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMSPlayerDeathSetup] 플레이어 사망 처리와 Death2 Animator 연결을 완료했습니다.");
        }

        private static void ConfigureAnimator()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            AnimationClip deathClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DeathClipPath);
            if (controller == null || deathClip == null)
            {
                Debug.LogError("[KMSPlayerDeathSetup] AnimatorController 또는 Death2 클립을 찾지 못했습니다.");
                return;
            }

            EnsureTrigger(controller, "Death");
            EnsureTrigger(controller, "Revive");

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState deathState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == "Death2");
            if (deathState == null) deathState = stateMachine.AddState("Death2", new Vector3(650f, 250f));
            deathState.motion = deathClip;
            deathState.writeDefaultValues = true;

            AnimatorState locomotionState = stateMachine.states
                .Select(child => child.state)
                .FirstOrDefault(state => state.name == "Locomotion");
            if (locomotionState == null)
            {
                Debug.LogError("[KMSPlayerDeathSetup] Locomotion 상태를 찾지 못해 부활 전환을 만들 수 없습니다.");
                return;
            }

            AnimatorStateTransition deathTransition = stateMachine.anyStateTransitions
                .FirstOrDefault(transition => transition.destinationState == deathState);
            if (deathTransition == null)
            {
                deathTransition = stateMachine.AddAnyStateTransition(deathState);
                deathTransition.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            }
            deathTransition.hasExitTime = false;
            deathTransition.duration = 0.1f;
            deathTransition.canTransitionToSelf = false;

            AnimatorStateTransition reviveTransition = deathState.transitions
                .FirstOrDefault(transition => transition.destinationState == locomotionState);
            if (reviveTransition == null)
            {
                reviveTransition = deathState.AddTransition(locomotionState);
                reviveTransition.AddCondition(AnimatorConditionMode.If, 0f, "Revive");
            }
            reviveTransition.hasExitTime = false;
            reviveTransition.duration = 0.1f;

            EditorUtility.SetDirty(controller);
        }

        private static void EnsureTrigger(AnimatorController controller, string parameterName)
        {
            if (controller.parameters.Any(parameter => parameter.name == parameterName)) return;
            controller.AddParameter(parameterName, AnimatorControllerParameterType.Trigger);
        }

        private static void ConfigurePlayerPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                PlayerDeathController deathController = root.GetComponent<PlayerDeathController>();
                if (deathController == null) deathController = root.AddComponent<PlayerDeathController>();

                PlayerStats stats = root.GetComponent<PlayerStats>();
                PlayerInput input = root.GetComponent<PlayerInput>();
                PlayerMovement movement = root.GetComponent<PlayerMovement>();
                PlayerInventory inventory = root.GetComponent<PlayerInventory>();
                PlayerHUD hud = root.GetComponent<PlayerHUD>();
                PlayerCameraController cameraController = root.GetComponent<PlayerCameraController>();
                PlayerCapsuleThrowController throwController = root.GetComponent<PlayerCapsuleThrowController>();
                Animator animator = root.GetComponentInChildren<Animator>(true);

                SerializedObject serializedController = new SerializedObject(deathController);
                SetReference(serializedController, "stats", stats);
                SetReference(serializedController, "input", input);
                SetReference(serializedController, "movement", movement);
                SetReference(serializedController, "inventory", inventory);
                SetReference(serializedController, "hud", hud);
                SetReference(serializedController, "cameraController", cameraController);
                SetReference(serializedController, "capsuleThrowController", throwController);
                SetReference(serializedController, "animator", animator);
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetReference(SerializedObject target, string propertyName, Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
#endif
