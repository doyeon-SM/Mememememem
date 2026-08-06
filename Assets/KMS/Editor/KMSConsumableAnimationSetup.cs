using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class KMSConsumableAnimationSetup
    {
        private const string ControllerPath =
            "Assets/KMS/4.Animation/Dodo/Controllers/KMS_DodoAnimator.controller";
        private const string EatClipPath =
            "Assets/KMS/4.Animation/Dodo/Clips/Eat.anim";
        private const string DeprecatedTemporaryEatClipPath =
            "Assets/KMS/4.Animation/Dodo/Dodo/Dodo_Animation/ThrowPrepare.anim";
        private const string EatStateName = "Consume_Eat";
        private const float EatPlaybackSpeed = 1.5f;
        private const float EatActionDurationSeconds = 1f;

        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
        };

        [MenuItem("KMS/Setup/Apply Consumable Animation Structure")]
        public static void Apply()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException($"Animator Controller not found: {ControllerPath}");
            }

            ConfigureAnimator(controller);
            foreach (string prefabPath in PlayerPrefabPaths)
            {
                ConfigurePlayerPrefab(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMS Consumable Animation] Animator and player prefabs configured.");
        }

        internal static void ConfigureAnimator(AnimatorController controller)
        {
            EnsureParameter(controller, "Eat", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine baseStateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(baseStateMachine, "Locomotion");
            if (locomotion == null)
            {
                throw new InvalidOperationException(
                    "KMS_DodoAnimator requires a Locomotion state.");
            }

            AnimatorControllerLayer actionLayer =
                KMSUpperBodyActionLayerSetup.Configure(controller);
            AnimatorStateMachine actionStateMachine = actionLayer.stateMachine;
            AnimatorState actionNone = KMSUpperBodyActionLayerSetup.FindState(
                actionStateMachine,
                KMSUpperBodyActionLayerSetup.NoneStateName);

            AnimationClip eatClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(EatClipPath);
            if (eatClip == null)
            {
                throw new InvalidOperationException(
                    $"Consume animation clip not found: {EatClipPath}");
            }

            AnimationClip deprecatedTemporaryClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(DeprecatedTemporaryEatClipPath);

            AnimatorState legacyEatState = FindState(baseStateMachine, EatStateName);
            AnimatorState eatState = FindState(actionStateMachine, EatStateName);
            bool created = eatState == null;
            if (eatState == null)
            {
                eatState = actionStateMachine.AddState(EatStateName);
            }

            SetStatePosition(actionStateMachine, eatState, new Vector3(930f, 360f, 0f));
            if (created
                || eatState.motion == null
                || eatState.motion == deprecatedTemporaryClip)
            {
                eatState.motion = legacyEatState != null
                    && legacyEatState.motion != null
                    && legacyEatState.motion != deprecatedTemporaryClip
                        ? legacyEatState.motion
                        : eatClip;
            }

            eatState.speed = EatPlaybackSpeed;
            float completionNormalizedTime =
                Mathf.Clamp01(EatActionDurationSeconds * EatPlaybackSpeed / eatClip.length);
            eatState.tag = "ConsumableAction";
            ConfigureBehaviour(eatState, completionNormalizedTime);
            ConfigureReturnTransition(eatState, actionNone, completionNormalizedTime);
            ConfigureEntryTransition(actionNone, eatState);
            KMSUpperBodyActionLayerSetup.RemoveLegacyState(
                baseStateMachine,
                EatStateName);
            EditorUtility.SetDirty(controller);
        }

        internal static void ConfigurePlayerPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                PlayerConsumableController consumable =
                    root.GetComponent<PlayerConsumableController>();
                PlayerMovement movement = root.GetComponent<PlayerMovement>();
                if (consumable == null || movement == null) return;

                SerializedObject consumableObject = new SerializedObject(consumable);
                consumableObject.FindProperty("movement").objectReferenceValue = movement;
                consumableObject.FindProperty("animator").objectReferenceValue = movement.Animator;
                consumableObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureBehaviour(
            AnimatorState state,
            float completionNormalizedTime)
        {
            ConsumableActionStateBehaviour behaviour = null;
            foreach (StateMachineBehaviour candidate in state.behaviours)
            {
                if (candidate is ConsumableActionStateBehaviour typed)
                {
                    behaviour = typed;
                    break;
                }
            }

            if (behaviour == null)
            {
                behaviour = state.AddStateMachineBehaviour<ConsumableActionStateBehaviour>();
            }

            behaviour.SetCompletionNormalizedTime(completionNormalizedTime);
            EditorUtility.SetDirty(behaviour);
        }

        private static void ConfigureReturnTransition(
            AnimatorState state,
            AnimatorState locomotion,
            float completionNormalizedTime)
        {
            foreach (AnimatorStateTransition transition in state.transitions)
            {
                state.RemoveTransition(transition);
            }

            AnimatorStateTransition returnTransition = state.AddTransition(locomotion);
            returnTransition.hasExitTime = true;
            returnTransition.exitTime = completionNormalizedTime;
            returnTransition.hasFixedDuration = true;
            returnTransition.duration = 0.1f;
        }

        private static void ConfigureEntryTransition(
            AnimatorState locomotion,
            AnimatorState destination)
        {
            List<AnimatorStateTransition> matchingTransitions =
                new List<AnimatorStateTransition>();
            foreach (AnimatorStateTransition transition in locomotion.transitions)
            {
                if (transition.destinationState == destination)
                {
                    matchingTransitions.Add(transition);
                }
            }

            foreach (AnimatorStateTransition transition in matchingTransitions)
            {
                locomotion.RemoveTransition(transition);
            }

            AnimatorStateTransition entryTransition = locomotion.AddTransition(destination);
            entryTransition.hasExitTime = false;
            entryTransition.hasFixedDuration = true;
            entryTransition.duration = 0.08f;
            entryTransition.AddCondition(AnimatorConditionMode.If, 0f, "Eat");
        }

        private static void EnsureParameter(
            AnimatorController controller,
            string parameterName,
            AnimatorControllerParameterType parameterType)
        {
            foreach (AnimatorControllerParameter parameter in controller.parameters)
            {
                if (parameter.name != parameterName) continue;
                if (parameter.type != parameterType)
                {
                    throw new InvalidOperationException(
                        $"Animator parameter '{parameterName}' has an unexpected type.");
                }

                return;
            }

            controller.AddParameter(parameterName, parameterType);
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState childState in stateMachine.states)
            {
                if (childState.state != null && childState.state.name == stateName)
                {
                    return childState.state;
                }
            }

            return null;
        }

        private static void SetStatePosition(
            AnimatorStateMachine stateMachine,
            AnimatorState state,
            Vector3 position)
        {
            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != state) continue;

                states[i].position = position;
                stateMachine.states = states;
                return;
            }
        }
    }
}
