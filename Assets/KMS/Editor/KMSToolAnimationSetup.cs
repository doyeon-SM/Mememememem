using System;
using System.Collections.Generic;
using KMS.Harvesting;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class KMSToolAnimationSetup
    {
        private const string ControllerPath =
            "Assets/KMS/4.Animation/Dodo/Controllers/KMS_DodoAnimator.controller";
        private const string ToolActionPlaybackRateParameter = "ToolActionPlaybackRate";

        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/KMS/2.Prefabs/0714_Player_KMS.prefab",
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
        };

        private static readonly ToolStateDefinition[] ToolStates =
        {
            new ToolStateDefinition(
                "Tool_Axe",
                ToolMotionType.Axe,
                "Assets/KMS/4.Animation/Dodo/Clips/Tool_Animation/axe.anim"),
            new ToolStateDefinition("Tool_Club", ToolMotionType.Club, null),
            new ToolStateDefinition(
                "Tool_Hoe",
                ToolMotionType.Hoe,
                "Assets/KMS/4.Animation/Dodo/Clips/Tool_Animation/hoe.anim"),
            new ToolStateDefinition(
                "Tool_Pickaxe",
                ToolMotionType.Pickaxe,
                "Assets/KMS/4.Animation/Dodo/Clips/Tool_Animation/pickax.anim")
        };

        [MenuItem("KMS/Setup/Apply Tool Animation Structure")]
        public static void Apply()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                throw new InvalidOperationException($"Animator Controller not found: {ControllerPath}");
            }

            ConfigureAnimator(controller);
            KMSConsumableAnimationSetup.ConfigureAnimator(controller);

            foreach (string prefabPath in PlayerPrefabPaths)
            {
                ConfigurePlayerPrefab(prefabPath);
                KMSConsumableAnimationSetup.ConfigurePlayerPrefab(prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[KMS Tool Animation] Animator and player prefabs configured.");
        }

        private static void ConfigureAnimator(AnimatorController controller)
        {
            EnsureParameter(controller, "ToolAction", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "ToolMotionType", AnimatorControllerParameterType.Int);
            EnsureParameter(
                controller,
                ToolActionPlaybackRateParameter,
                AnimatorControllerParameterType.Float);
            SetFloatParameterDefault(controller, ToolActionPlaybackRateParameter, 1f);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(stateMachine, "Locomotion");
            AnimatorState slash = FindState(stateMachine, "Slash");
            if (locomotion == null || slash == null || slash.motion == null)
            {
                throw new InvalidOperationException(
                    "KMS_DodoAnimator requires Locomotion and Slash states with a temporary motion.");
            }

            for (int i = 0; i < ToolStates.Length; i++)
            {
                ToolStateDefinition definition = ToolStates[i];
                AnimatorState toolState = FindState(stateMachine, definition.StateName);
                bool created = toolState == null;
                if (toolState == null)
                {
                    toolState = stateMachine.AddState(definition.StateName);
                }

                SetStatePosition(
                    stateMachine,
                    toolState,
                    new Vector3(690f, 360f + i * 100f, 0f));

                AnimationClip toolClip = string.IsNullOrEmpty(definition.ClipPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<AnimationClip>(definition.ClipPath);
                if (!string.IsNullOrEmpty(definition.ClipPath) && toolClip == null)
                {
                    throw new InvalidOperationException(
                        $"Tool animation clip not found: {definition.ClipPath}");
                }

                if (toolClip != null)
                {
                    toolState.motion = toolClip;
                }
                else if (created || toolState.motion == null)
                {
                    // Club has no dedicated motion yet, so it keeps the existing Slash motion.
                    toolState.motion = slash.motion;
                }

                AnimationClip stateClip = toolState.motion as AnimationClip;
                if (stateClip == null)
                {
                    throw new InvalidOperationException(
                        $"Tool state '{definition.StateName}' requires an AnimationClip motion.");
                }

                toolState.speed = Mathf.Max(0.01f, stateClip.length);
                toolState.speedParameterActive = true;
                toolState.speedParameter = ToolActionPlaybackRateParameter;
                toolState.tag = "ToolAction";
                ConfigureBehaviour(toolState, definition.MotionType);
                ConfigureReturnTransition(toolState, locomotion);
                ConfigureEntryTransition(locomotion, toolState, definition.MotionType);
            }

            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureBehaviour(AnimatorState state, ToolMotionType motionType)
        {
            ToolActionStateBehaviour behaviour = null;
            foreach (StateMachineBehaviour candidate in state.behaviours)
            {
                if (candidate is ToolActionStateBehaviour typed)
                {
                    behaviour = typed;
                    break;
                }
            }

            if (behaviour == null)
            {
                behaviour = state.AddStateMachineBehaviour<ToolActionStateBehaviour>();
            }

            behaviour.SetMotionType(motionType);
            EditorUtility.SetDirty(behaviour);
        }

        private static void ConfigureReturnTransition(AnimatorState state, AnimatorState locomotion)
        {
            foreach (AnimatorStateTransition transition in state.transitions)
            {
                state.RemoveTransition(transition);
            }

            AnimatorStateTransition returnTransition = state.AddTransition(locomotion);
            returnTransition.hasExitTime = true;
            returnTransition.exitTime = 0.9f;
            returnTransition.hasFixedDuration = false;
            returnTransition.duration = 0.1f;
        }

        private static void ConfigureEntryTransition(
            AnimatorState locomotion,
            AnimatorState destination,
            ToolMotionType motionType)
        {
            List<AnimatorStateTransition> matchingTransitions = new List<AnimatorStateTransition>();
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
            entryTransition.AddCondition(AnimatorConditionMode.If, 0f, "ToolAction");
            entryTransition.AddCondition(
                AnimatorConditionMode.Equals,
                (float)motionType,
                "ToolMotionType");
        }

        private static AnimatorControllerParameter EnsureParameter(
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

                return parameter;
            }

            var createdParameter = new AnimatorControllerParameter
            {
                name = parameterName,
                type = parameterType
            };
            controller.AddParameter(createdParameter);
            return createdParameter;
        }

        private static void SetFloatParameterDefault(
            AnimatorController controller,
            string parameterName,
            float defaultValue)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name != parameterName) continue;

                parameters[i].defaultFloat = defaultValue;
                controller.parameters = parameters;
                return;
            }

            throw new InvalidOperationException(
                $"Animator parameter '{parameterName}' was not found.");
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
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

        private static void ConfigurePlayerPrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null) return;

            try
            {
                PlayerHarvestController harvest = root.GetComponent<PlayerHarvestController>();
                PlayerMovement movement = root.GetComponent<PlayerMovement>();
                if (harvest == null || movement == null) return;

                PlayerToolAnimationController toolAnimation =
                    root.GetComponent<PlayerToolAnimationController>();
                if (toolAnimation == null)
                {
                    toolAnimation = root.AddComponent<PlayerToolAnimationController>();
                }

                SerializedObject toolAnimationObject = new SerializedObject(toolAnimation);
                toolAnimationObject.FindProperty("movement").objectReferenceValue = movement;
                toolAnimationObject.FindProperty("animator").objectReferenceValue = movement.Animator;
                toolAnimationObject.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject harvestObject = new SerializedObject(harvest);
                harvestObject.FindProperty("toolAnimationController").objectReferenceValue =
                    toolAnimation;
                harvestObject.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private readonly struct ToolStateDefinition
        {
            public ToolStateDefinition(
                string stateName,
                ToolMotionType motionType,
                string clipPath)
            {
                StateName = stateName;
                MotionType = motionType;
                ClipPath = clipPath;
            }

            public string StateName { get; }
            public ToolMotionType MotionType { get; }
            public string ClipPath { get; }
        }
    }
}
