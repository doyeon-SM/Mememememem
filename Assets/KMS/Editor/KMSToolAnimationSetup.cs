using System;
using System.Collections.Generic;
using KMS.Harvesting;
using KMS.InventoryDuped;
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
        private const string CarryTypeParameter = "HeldItemCarryType";
        private const string CarryLayerName = "HeldItemCarry";
        private const string CarryFolder =
            "Assets/KMS/4.Animation/Dodo/Clips/HeldItemCarry";
        private const string CarryMaskPath = CarryFolder + "/HeldItemRightArm.mask";
        private const string CarryLongToolClipPath = CarryFolder + "/Carry_LongTool.anim";
        private const string CarryClubClipPath = CarryFolder + "/Carry_Club.anim";
        private const string CarryReferenceClipPath =
            "Assets/KMS/4.Animation/Dodo/Clips/Happy_Idle.anim";
        private const string ClubClipPath =
            "Assets/KMS/4.Animation/Dodo/Clips/Tool_Animation/club.anim";
        private const string ClubSourceClipPath =
            "Assets/KMS/4.Animation/Dodo/Clips/Slash.anim";
        private const string ClubTimingVersion = "KMSClubTimingV1";

        private static readonly string[] PlayerPrefabPaths =
        {
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab"
        };

        private static readonly ToolStateDefinition[] ToolStates =
        {
            new ToolStateDefinition(
                "Tool_Axe",
                ToolMotionType.Axe,
                "Assets/KMS/4.Animation/Dodo/Clips/Tool_Animation/axe.anim",
                0.39f),
            new ToolStateDefinition(
                "Tool_Club",
                ToolMotionType.Club,
                ClubClipPath,
                0.64f),
            new ToolStateDefinition(
                "Tool_Hoe",
                ToolMotionType.Hoe,
                "Assets/KMS/4.Animation/Dodo/Clips/Tool_Animation/axe.anim",
                0.39f),
            new ToolStateDefinition(
                "Tool_Pickaxe",
                ToolMotionType.Pickaxe,
                "Assets/KMS/4.Animation/Dodo/Clips/Tool_Animation/pickax.anim",
                0.50f)
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

            EnsureFolder("Assets/KMS/4.Animation/Dodo/Clips", "HeldItemCarry");
            CreateClubClipIfMissing();
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

        private static void CreateClubClipIfMissing()
        {
            AnimationClip clubClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(ClubClipPath);
            if (clubClip != null)
            {
                ApplyClubTimingIfNeeded(clubClip);
                return;
            }

            AnimationClip source =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(ClubSourceClipPath);
            if (source == null)
            {
                throw new InvalidOperationException(
                    $"Club source animation clip not found: {ClubSourceClipPath}");
            }

            clubClip = new AnimationClip();
            EditorUtility.CopySerialized(source, clubClip);
            clubClip.name = "club";
            AssetDatabase.CreateAsset(clubClip, ClubClipPath);
            AssetDatabase.ImportAsset(ClubClipPath, ImportAssetOptions.ForceSynchronousImport);
            ApplyClubTimingIfNeeded(clubClip);
            EditorUtility.SetDirty(clubClip);
        }

        private static void ApplyClubTimingIfNeeded(AnimationClip clubClip)
        {
            AssetImporter importer = AssetImporter.GetAtPath(ClubClipPath);
            if (importer != null && importer.userData == ClubTimingVersion) return;

            float length = Mathf.Max(0.01f, clubClip.length);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clubClip))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clubClip, binding);
                if (curve == null) continue;

                Keyframe[] keys = curve.keys;
                for (int i = 0; i < keys.Length; i++)
                {
                    Keyframe key = keys[i];
                    float normalizedTime = Mathf.Clamp01(key.time / length);
                    key.time = RemapClubNormalizedTime(normalizedTime) * length;
                    keys[i] = key;
                }

                curve.keys = keys;
                AnimationUtility.SetEditorCurve(clubClip, binding, curve);
            }

            EditorUtility.SetDirty(clubClip);
            if (importer != null)
            {
                importer.userData = ClubTimingVersion;
                importer.SaveAndReimport();
            }
        }

        private static float RemapClubNormalizedTime(float sourceTime)
        {
            const float windupSourceEnd = 0.18f;
            const float windupTargetEnd = 0.22f;
            const float strikeSourceEnd = 0.72f;
            const float strikeTargetEnd = 0.64f;

            if (sourceTime <= windupSourceEnd)
            {
                return Mathf.InverseLerp(0f, windupSourceEnd, sourceTime)
                    * windupTargetEnd;
            }

            if (sourceTime <= strikeSourceEnd)
            {
                return Mathf.Lerp(
                    windupTargetEnd,
                    strikeTargetEnd,
                    Mathf.InverseLerp(windupSourceEnd, strikeSourceEnd, sourceTime));
            }

            return Mathf.Lerp(
                strikeTargetEnd,
                1f,
                Mathf.InverseLerp(strikeSourceEnd, 1f, sourceTime));
        }

        private static void ConfigureAnimator(AnimatorController controller)
        {
            EnsureParameter(controller, "ToolAction", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "ToolMotionType", AnimatorControllerParameterType.Int);
            EnsureParameter(controller, CarryTypeParameter, AnimatorControllerParameterType.Int);
            EnsureParameter(
                controller,
                ToolActionPlaybackRateParameter,
                AnimatorControllerParameterType.Float);
            SetFloatParameterDefault(controller, ToolActionPlaybackRateParameter, 1f);

            AnimatorStateMachine baseStateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(baseStateMachine, "Locomotion");
            AnimatorState slash = FindState(baseStateMachine, "Slash");
            if (locomotion == null || slash == null || slash.motion == null)
            {
                throw new InvalidOperationException(
                    "KMS_DodoAnimator requires Locomotion and Slash states with a temporary motion.");
            }

            ConfigureCarryLayer(controller);
            AnimatorControllerLayer actionLayer =
                KMSUpperBodyActionLayerSetup.Configure(controller);
            AnimatorStateMachine actionStateMachine = actionLayer.stateMachine;

            for (int i = 0; i < ToolStates.Length; i++)
            {
                ToolStateDefinition definition = ToolStates[i];
                AnimatorState upperBodyState = FindState(
                    actionStateMachine,
                    definition.StateName);
                AnimatorState toolState = FindState(baseStateMachine, definition.StateName);
                bool created = toolState == null;
                if (toolState == null)
                {
                    toolState = baseStateMachine.AddState(definition.StateName);
                }

                SetStatePosition(
                    baseStateMachine,
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
                    toolState.motion = upperBodyState != null && upperBodyState.motion != null
                        ? upperBodyState.motion
                        : slash.motion;
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
                ConfigureImpactEvent(stateClip, definition.ImpactNormalizedTime);
                ConfigureBehaviour(toolState, definition.MotionType);
                ConfigureReturnTransition(toolState, locomotion);
                ConfigureEntryTransition(locomotion, toolState, definition.MotionType);
                KMSUpperBodyActionLayerSetup.RemoveLegacyState(
                    actionStateMachine,
                    definition.StateName);
            }

            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureCarryLayer(AnimatorController controller)
        {
            AvatarMask carryMask = CreateOrUpdateCarryMask();
            AnimationClip referenceClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(CarryReferenceClipPath);
            if (referenceClip == null)
            {
                throw new InvalidOperationException(
                    $"Carry reference clip not found: {CarryReferenceClipPath}");
            }

            AnimationClip longToolClip = CreateOrUpdateCarryClip(
                CarryLongToolClipPath,
                "Carry_LongTool",
                referenceClip,
                0f);
            AnimationClip clubClip = CreateOrUpdateCarryClip(
                CarryClubClipPath,
                "Carry_Club",
                referenceClip,
                0f);

            int layerIndex = FindLayerIndex(controller, CarryLayerName);
            AnimatorControllerLayer layer;
            if (layerIndex < 0)
            {
                var stateMachine = new AnimatorStateMachine
                {
                    name = CarryLayerName
                };
                AssetDatabase.AddObjectToAsset(stateMachine, controller);
                layer = new AnimatorControllerLayer
                {
                    name = CarryLayerName,
                    defaultWeight = 0f,
                    avatarMask = carryMask,
                    blendingMode = AnimatorLayerBlendingMode.Override,
                    stateMachine = stateMachine
                };
                controller.AddLayer(layer);
                layerIndex = controller.layers.Length - 1;
            }

            AnimatorControllerLayer[] layers = controller.layers;
            layer = layers[layerIndex];
            layer.defaultWeight = 0f;
            layer.avatarMask = carryMask;
            layer.blendingMode = AnimatorLayerBlendingMode.Override;
            layers[layerIndex] = layer;
            controller.layers = layers;

            AnimatorStateMachine carryStateMachine = layer.stateMachine;
            AnimatorState noneState = FindState(carryStateMachine, "Carry_None")
                ?? carryStateMachine.AddState("Carry_None", new Vector3(220f, 120f, 0f));
            AnimatorState longToolState = FindState(carryStateMachine, "Carry_LongTool")
                ?? carryStateMachine.AddState("Carry_LongTool", new Vector3(460f, 70f, 0f));
            AnimatorState clubState = FindState(carryStateMachine, "Carry_Club")
                ?? carryStateMachine.AddState("Carry_Club", new Vector3(460f, 180f, 0f));

            noneState.motion = null;
            longToolState.motion = longToolClip;
            clubState.motion = clubClip;
            carryStateMachine.defaultState = noneState;

            foreach (AnimatorStateTransition transition in carryStateMachine.anyStateTransitions)
            {
                carryStateMachine.RemoveAnyStateTransition(transition);
            }

            ConfigureCarryTransition(
                carryStateMachine.AddAnyStateTransition(noneState),
                HeldItemCarryType.None);
            ConfigureCarryTransition(
                carryStateMachine.AddAnyStateTransition(longToolState),
                HeldItemCarryType.LongTool);
            ConfigureCarryTransition(
                carryStateMachine.AddAnyStateTransition(clubState),
                HeldItemCarryType.Club);

            EditorUtility.SetDirty(carryStateMachine);
        }

        private static void ConfigureCarryTransition(
            AnimatorStateTransition transition,
            HeldItemCarryType carryType)
        {
            transition.hasExitTime = false;
            transition.hasFixedDuration = true;
            transition.duration = 0.12f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(
                AnimatorConditionMode.Equals,
                (float)carryType,
                CarryTypeParameter);
        }

        private static AvatarMask CreateOrUpdateCarryMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(CarryMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask
                {
                    name = "HeldItemRightArm"
                };
                AssetDatabase.CreateAsset(mask, CarryMaskPath);
            }

            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimationClip CreateOrUpdateCarryClip(
            string clipPath,
            string clipName,
            AnimationClip referenceClip,
            float sampleTime)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.name = clipName;
            clip.frameRate = referenceClip.frameRate;
            clip.legacy = false;

            foreach (EditorCurveBinding existingBinding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, existingBinding, null);
            }

            float clampedSampleTime = Mathf.Clamp(sampleTime, 0f, referenceClip.length);
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(referenceClip))
            {
                if (!IsRightArmCarryBinding(binding)) continue;

                AnimationCurve sourceCurve = AnimationUtility.GetEditorCurve(referenceClip, binding);
                if (sourceCurve == null) continue;

                float value = sourceCurve.Evaluate(clampedSampleTime);
                AnimationUtility.SetEditorCurve(
                    clip,
                    binding,
                    AnimationCurve.Constant(0f, 1f, value));
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static bool IsRightArmCarryBinding(EditorCurveBinding binding)
        {
            string propertyName = binding.propertyName;
            return binding.type == typeof(Animator)
                && (propertyName.StartsWith("Right Shoulder ", StringComparison.Ordinal)
                    || propertyName.StartsWith("Right Arm ", StringComparison.Ordinal)
                    || propertyName.StartsWith("Right Forearm ", StringComparison.Ordinal)
                    || propertyName.StartsWith("Right Hand ", StringComparison.Ordinal)
                    || propertyName.StartsWith("RightHand.", StringComparison.Ordinal));
        }

        private static int FindLayerIndex(AnimatorController controller, string layerName)
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

        private static void ConfigureImpactEvent(
            AnimationClip clip,
            float normalizedTime)
        {
            List<AnimationEvent> events =
                new List<AnimationEvent>(AnimationUtility.GetAnimationEvents(clip));
            events.RemoveAll(animationEvent => animationEvent.functionName == "OnToolImpact");
            events.Add(new AnimationEvent
            {
                functionName = "OnToolImpact",
                time = clip.length * Mathf.Clamp01(normalizedTime)
            });
            events.Sort((left, right) => left.time.CompareTo(right.time));
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);
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

                PlayerHeldItemCarryController carryController =
                    root.GetComponent<PlayerHeldItemCarryController>();
                if (carryController == null)
                {
                    carryController = root.AddComponent<PlayerHeldItemCarryController>();
                }

                SerializedObject carryObject = new SerializedObject(carryController);
                PlayerHeldItemModelController heldItemModel =
                    root.GetComponent<PlayerHeldItemModelController>();
                if (heldItemModel != null)
                {
                    SerializedObject heldItemModelObject = new SerializedObject(heldItemModel);
                    heldItemModelObject.FindProperty("longToolCarryDirection").vector3Value =
                        new Vector3(0.12f, 0.22f, 1f);
                    heldItemModelObject.FindProperty("clubCarryDirection").vector3Value =
                        new Vector3(0.16f, 0.08f, 1f);
                    heldItemModelObject.ApplyModifiedPropertiesWithoutUndo();
                }

                carryObject.FindProperty("inventory").objectReferenceValue =
                    root.GetComponent<PlayerInventory>();
                carryObject.FindProperty("movement").objectReferenceValue = movement;
                carryObject.FindProperty("animator").objectReferenceValue = movement.Animator;
                carryObject.FindProperty("toolAnimationController").objectReferenceValue =
                    toolAnimation;
                carryObject.FindProperty("heldItemModelController").objectReferenceValue =
                    heldItemModel;
                carryObject.FindProperty("carryLayerName").stringValue = CarryLayerName;
                carryObject.FindProperty("longToolIdleWeight").floatValue = 0.8f;
                carryObject.FindProperty("longToolWalkWeight").floatValue = 0.9f;
                carryObject.FindProperty("longToolRunWeight").floatValue = 1f;
                carryObject.FindProperty("clubIdleWeight").floatValue = 0.58f;
                carryObject.FindProperty("clubWalkWeight").floatValue = 0.75f;
                carryObject.FindProperty("clubRunWeight").floatValue = 0.9f;
                carryObject.FindProperty("movingSpeedThreshold").floatValue = 0.1f;
                carryObject.FindProperty("blendInSpeed").floatValue = 8f;
                carryObject.FindProperty("blendOutSpeed").floatValue = 14f;
                carryObject.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject harvestObject = new SerializedObject(harvest);
                harvestObject.FindProperty("toolAnimationController").objectReferenceValue =
                    toolAnimation;
                harvestObject.ApplyModifiedPropertiesWithoutUndo();

                Animator animator = movement.Animator;
                if (animator != null
                    && animator.GetComponent<PlayerAnimationEvents>() == null)
                {
                    animator.gameObject.AddComponent<PlayerAnimationEvents>();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolder(string parentFolder, string childFolder)
        {
            string path = $"{parentFolder}/{childFolder}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parentFolder, childFolder);
            }
        }

        private readonly struct ToolStateDefinition
        {
            public ToolStateDefinition(
                string stateName,
                ToolMotionType motionType,
                string clipPath,
                float impactNormalizedTime)
            {
                StateName = stateName;
                MotionType = motionType;
                ClipPath = clipPath;
                ImpactNormalizedTime = impactNormalizedTime;
            }

            public string StateName { get; }
            public ToolMotionType MotionType { get; }
            public string ClipPath { get; }
            public float ImpactNormalizedTime { get; }
        }
    }
}
