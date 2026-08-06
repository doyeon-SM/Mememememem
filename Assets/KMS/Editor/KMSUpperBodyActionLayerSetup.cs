using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KMS.EditorTools
{
    internal static class KMSUpperBodyActionLayerSetup
    {
        internal const string LayerName = "UpperBodyActions";
        internal const string NoneStateName = "UpperBody_None";

        private const string MaskFolder =
            "Assets/KMS/4.Animation/Dodo/Clips/UpperBodyActions";
        private const string MaskPath = MaskFolder + "/UpperBodyFromSpine.mask";

        internal static AnimatorControllerLayer Configure(AnimatorController controller)
        {
            EnsureFolder("Assets/KMS/4.Animation/Dodo/Clips", "UpperBodyActions");
            AvatarMask mask = CreateOrUpdateMask();

            int layerIndex = FindLayerIndex(controller, LayerName);
            AnimatorControllerLayer layer;
            if (layerIndex < 0)
            {
                var stateMachine = new AnimatorStateMachine
                {
                    name = LayerName
                };
                AssetDatabase.AddObjectToAsset(stateMachine, controller);
                layer = new AnimatorControllerLayer
                {
                    name = LayerName,
                    defaultWeight = 1f,
                    avatarMask = mask,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    stateMachine = stateMachine
                };
                controller.AddLayer(layer);
                layerIndex = controller.layers.Length - 1;
            }

            AnimatorControllerLayer[] layers = controller.layers;
            layer = layers[layerIndex];
            layer.defaultWeight = 1f;
            layer.avatarMask = mask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorState noneState = FindState(layer.stateMachine, NoneStateName)
                ?? layer.stateMachine.AddState(
                    NoneStateName,
                    new Vector3(220f, 120f, 0f));
            noneState.motion = null;
            layer.stateMachine.defaultState = noneState;
            EditorUtility.SetDirty(layer.stateMachine);

            MoveLayerToTop(controller, LayerName);
            return controller.layers[controller.layers.Length - 1];
        }

        internal static AnimatorState FindState(
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

        internal static void RemoveLegacyState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            AnimatorState state = FindState(stateMachine, stateName);
            if (state == null) return;

            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state == null) continue;

                var transitionsToRemove = new List<AnimatorStateTransition>();
                foreach (AnimatorStateTransition transition in child.state.transitions)
                {
                    if (transition.destinationState == state)
                    {
                        transitionsToRemove.Add(transition);
                    }
                }

                foreach (AnimatorStateTransition transition in transitionsToRemove)
                {
                    child.state.RemoveTransition(transition);
                }
            }

            var anyTransitionsToRemove = new List<AnimatorStateTransition>();
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == state)
                {
                    anyTransitionsToRemove.Add(transition);
                }
            }

            foreach (AnimatorStateTransition transition in anyTransitionsToRemove)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }

            stateMachine.RemoveState(state);
            EditorUtility.SetDirty(stateMachine);
        }

        private static AvatarMask CreateOrUpdateMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask
                {
                    name = "UpperBodyFromSpine"
                };
                AssetDatabase.CreateAsset(mask, MaskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static void MoveLayerToTop(
            AnimatorController controller,
            string layerName)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            int layerIndex = FindLayerIndex(controller, layerName);
            if (layerIndex < 0 || layerIndex == layers.Length - 1) return;

            var reordered = new List<AnimatorControllerLayer>(layers.Length);
            for (int i = 0; i < layers.Length; i++)
            {
                if (i != layerIndex) reordered.Add(layers[i]);
            }
            reordered.Add(layers[layerIndex]);
            controller.layers = reordered.ToArray();
        }

        private static int FindLayerIndex(
            AnimatorController controller,
            string layerName)
        {
            AnimatorControllerLayer[] layers = controller.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (string.Equals(layers[i].name, layerName, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            string path = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }
    }
}
