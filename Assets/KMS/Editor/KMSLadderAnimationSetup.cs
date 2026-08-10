using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KMS.EditorTools
{
    public static class KMSLadderAnimationSetup
    {
        private const string ControllerPath =
            "Assets/KMS/4.Animation/Dodo/Controllers/KMS_DodoAnimator.controller";
        private const string SourceClipPath =
            "Assets/KMS/4.Animation/Dodo/Clips/Climbing_Ladder.anim";
        private const string ClipPath =
            "Assets/KMS/4.Animation/Dodo/Clips/Climbing_Ladder_Humanoid.anim";
        private const string PlayerPrefabPath =
            "Assets/KMS/2.Prefabs/0720_Player_KMS.prefab";
        private const string ClimbingParameter = "IsClimbing";
        private const string ClimbCycleParameter = "ClimbCycle";
        private const string LegacyClimbSpeedParameter = "ClimbSpeed";
        private const string ClimbingStateName = "Climbing_Ladder";
        private const string LocomotionStateName = "Locomotion";

        [MenuItem("KMS/Setup/Apply Ladder Animation Structure")]
        public static void Apply()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimationClip sourceClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(SourceClipPath);

            if (controller == null)
            {
                throw new InvalidOperationException($"Animator Controller not found: {ControllerPath}");
            }

            if (sourceClip == null)
            {
                throw new InvalidOperationException(
                    $"Ladder source animation clip not found: {SourceClipPath}");
            }

            ValidateClipBindings(controller, sourceClip);
            AnimationClip clip = ConvertToHumanoidClip(controller, sourceClip);
            ConfigureLooping(clip);
            ConfigureController(controller, clip);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[KMS Ladder Animation] Configured '{ClimbingStateName}' " +
                $"({clip.length:0.###} sec, {AnimationUtility.GetCurveBindings(clip).Length} bindings).");
        }

        public static void Run()
        {
            Apply();
        }

        public static void RunDiagnostic()
        {
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (controller == null || clip == null || prefab == null)
            {
                throw new InvalidOperationException("KMS ladder diagnostic assets are missing.");
            }

            Scene previewScene = EditorSceneManager.NewPreviewScene();
            GameObject instance = null;
            try
            {
                instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject;
                Animator targetAnimator = null;
                foreach (Animator candidate in instance.GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.runtimeAnimatorController == controller)
                    {
                        targetAnimator = candidate;
                        break;
                    }
                }

                if (targetAnimator == null)
                {
                    throw new InvalidOperationException("KMS Dodo Animator was not found.");
                }

                const string leftArmPath =
                    "mixamorig:Hips/mixamorig:Spine/mixamorig:Spine1/" +
                    "mixamorig:Spine2/mixamorig:LeftShoulder/mixamorig:LeftArm";
                Transform leftArm = targetAnimator.transform.Find(leftArmPath);
                if (leftArm == null)
                {
                    throw new InvalidOperationException("KMS Dodo left arm was not found.");
                }

                clip.SampleAnimation(targetAnimator.gameObject, 0f);
                Quaternion clipStart = leftArm.localRotation;
                clip.SampleAnimation(targetAnimator.gameObject, clip.length * 0.5f);
                Quaternion clipMiddle = leftArm.localRotation;
                float directClipAngle = Quaternion.Angle(clipStart, clipMiddle);

                targetAnimator.Rebind();
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                targetAnimator.SetBool(ClimbingParameter, true);
                targetAnimator.SetFloat(ClimbCycleParameter, 0f);
                targetAnimator.Update(0f);
                targetAnimator.Update(0.2f);
                targetAnimator.SetFloat(ClimbCycleParameter, 0f);
                targetAnimator.Update(0f);
                Quaternion controllerStart = leftArm.localRotation;
                targetAnimator.SetFloat(ClimbCycleParameter, 0.5f);
                targetAnimator.Update(0f);
                Quaternion controllerMiddle = leftArm.localRotation;
                float controllerAngle = Quaternion.Angle(controllerStart, controllerMiddle);

                AnimatorStateInfo state = targetAnimator.GetCurrentAnimatorStateInfo(0);
                string layerWeights = string.Empty;
                for (int i = 0; i < targetAnimator.layerCount; i++)
                {
                    if (i > 0) layerWeights += ", ";
                    layerWeights += $"{targetAnimator.GetLayerName(i)}={targetAnimator.GetLayerWeight(i):0.###}";
                }

                Debug.Log(
                    $"[KMS Ladder Diagnostic] clip arm delta={directClipAngle:0.###}deg, " +
                    $"controller arm delta={controllerAngle:0.###}deg, " +
                    $"baseState={state.shortNameHash}, normalizedTime={state.normalizedTime:0.###}, " +
                    $"layers=[{layerWeights}].");

                if (directClipAngle < 1f)
                {
                    throw new InvalidOperationException(
                        "Climbing_Ladder has no meaningful left-arm motion.");
                }
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static AnimationClip ConvertToHumanoidClip(
            RuntimeAnimatorController controller,
            AnimationClip sourceClip)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator targetAnimator = null;
                foreach (Animator candidate in prefabRoot.GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.runtimeAnimatorController == controller)
                    {
                        targetAnimator = candidate;
                        break;
                    }
                }

                if (targetAnimator == null || targetAnimator.avatar == null
                    || !targetAnimator.avatar.isHuman)
                {
                    throw new InvalidOperationException(
                        "KMS Dodo requires a valid Humanoid Avatar for ladder conversion.");
                }

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(sourceClip);
                var samples = new Dictionary<Transform, TransformSample>();
                foreach (EditorCurveBinding binding in bindings)
                {
                    if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path))
                    {
                        continue;
                    }

                    Transform target = targetAnimator.transform.Find(binding.path);
                    if (target != null && !samples.ContainsKey(target))
                    {
                        samples.Add(target, new TransformSample(target));
                    }
                }

                AnimationClip converted =
                    AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                if (converted == null)
                {
                    converted = new AnimationClip { name = "Climbing_Ladder_Humanoid" };
                    AssetDatabase.CreateAsset(converted, ClipPath);
                }
                else
                {
                    converted.ClearCurves();
                }

                converted.frameRate = sourceClip.frameRate;
                int frameCount = Mathf.Max(
                    2,
                    Mathf.CeilToInt(sourceClip.length * sourceClip.frameRate) + 1);
                var muscleKeys = new List<Keyframe>[HumanTrait.MuscleCount];
                for (int i = 0; i < muscleKeys.Length; i++)
                {
                    muscleKeys[i] = new List<Keyframe>(frameCount);
                }

                targetAnimator.Rebind();
                targetAnimator.enabled = false;
                var poseHandler = new HumanPoseHandler(
                    targetAnimator.avatar,
                    targetAnimator.transform);
                var humanPose = new HumanPose();

                for (int frame = 0; frame < frameCount; frame++)
                {
                    float time = Mathf.Min(
                        sourceClip.length,
                        frame / sourceClip.frameRate);
                    foreach (TransformSample sample in samples.Values)
                    {
                        sample.Reset();
                    }

                    foreach (EditorCurveBinding binding in bindings)
                    {
                        if (binding.type != typeof(Transform)
                            || string.IsNullOrEmpty(binding.path))
                        {
                            continue;
                        }

                        Transform target = targetAnimator.transform.Find(binding.path);
                        if (target == null || !samples.TryGetValue(target, out TransformSample sample))
                        {
                            continue;
                        }

                        AnimationCurve curve = AnimationUtility.GetEditorCurve(sourceClip, binding);
                        if (curve != null)
                        {
                            sample.Set(binding.propertyName, curve.Evaluate(time));
                        }
                    }

                    foreach (TransformSample sample in samples.Values)
                    {
                        sample.Apply();
                    }

                    poseHandler.GetHumanPose(ref humanPose);
                    for (int muscle = 0; muscle < HumanTrait.MuscleCount; muscle++)
                    {
                        muscleKeys[muscle].Add(new Keyframe(time, humanPose.muscles[muscle]));
                    }
                }

                poseHandler.Dispose();
                for (int muscle = 0; muscle < HumanTrait.MuscleCount; muscle++)
                {
                    var binding = EditorCurveBinding.FloatCurve(
                        string.Empty,
                        typeof(Animator),
                        HumanTrait.MuscleName[muscle]);
                    AnimationUtility.SetEditorCurve(
                        converted,
                        binding,
                        new AnimationCurve(muscleKeys[muscle].ToArray()));
                }

                EditorUtility.SetDirty(converted);
                AssetDatabase.SaveAssets();
                return converted;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void ConfigureLooping(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlend = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        private static void ConfigureController(
            AnimatorController controller,
            AnimationClip clip)
        {
            EnsureParameter(controller, ClimbingParameter, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, ClimbCycleParameter, AnimatorControllerParameterType.Float);
            RemoveParameter(controller, LegacyClimbSpeedParameter);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState locomotion = FindState(stateMachine, LocomotionStateName);
            if (locomotion == null)
            {
                throw new InvalidOperationException(
                    $"Base Layer state '{LocomotionStateName}' was not found.");
            }

            AnimatorState climbing = FindState(stateMachine, ClimbingStateName);
            if (climbing == null)
            {
                climbing = stateMachine.AddState(
                    ClimbingStateName,
                    new Vector3(450f, 230f, 0f));
            }

            climbing.motion = clip;
            climbing.speed = 1f;
            climbing.speedParameterActive = false;
            climbing.speedParameter = string.Empty;
            climbing.timeParameterActive = true;
            climbing.timeParameter = ClimbCycleParameter;
            climbing.writeDefaultValues = true;

            RemoveTransitionsTo(stateMachine, climbing);
            RemoveAllTransitions(climbing);

            AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(climbing);
            enter.hasExitTime = false;
            enter.hasFixedDuration = true;
            enter.duration = 0.1f;
            enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, ClimbingParameter);

            AnimatorStateTransition exit = climbing.AddTransition(locomotion);
            exit.hasExitTime = false;
            exit.hasFixedDuration = true;
            exit.duration = 0.1f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, ClimbingParameter);

            EditorUtility.SetDirty(climbing);
            EditorUtility.SetDirty(stateMachine);
            EditorUtility.SetDirty(controller);
        }

        private static void ValidateClipBindings(
            RuntimeAnimatorController controller,
            AnimationClip clip)
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                Animator targetAnimator = null;
                foreach (Animator candidate in prefabRoot.GetComponentsInChildren<Animator>(true))
                {
                    if (candidate.runtimeAnimatorController == controller)
                    {
                        targetAnimator = candidate;
                        break;
                    }
                }

                if (targetAnimator == null)
                {
                    throw new InvalidOperationException(
                        $"No Animator using '{controller.name}' was found in {PlayerPrefabPath}.");
                }

                var missingPaths = new HashSet<string>();
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (!string.IsNullOrEmpty(binding.path)
                        && targetAnimator.transform.Find(binding.path) == null)
                    {
                        missingPaths.Add(binding.path);
                    }
                }

                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    if (!string.IsNullOrEmpty(binding.path)
                        && targetAnimator.transform.Find(binding.path) == null)
                    {
                        missingPaths.Add(binding.path);
                    }
                }

                if (missingPaths.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"'{clip.name}' has {missingPaths.Count} binding paths missing from " +
                        $"the KMS player skeleton. First missing path: {First(missingPaths)}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static string First(HashSet<string> values)
        {
            foreach (string value in values) return value;
            return string.Empty;
        }

        private static void RemoveTransitionsTo(
            AnimatorStateMachine stateMachine,
            AnimatorState destination)
        {
            var transitions = new List<AnimatorStateTransition>();
            foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
            {
                if (transition.destinationState == destination)
                {
                    transitions.Add(transition);
                }
            }

            foreach (AnimatorStateTransition transition in transitions)
            {
                stateMachine.RemoveAnyStateTransition(transition);
            }
        }

        private static void RemoveAllTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition transition in state.transitions)
            {
                state.RemoveTransition(transition);
            }
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

            var created = new AnimatorControllerParameter
            {
                name = parameterName,
                type = parameterType
            };
            controller.AddParameter(created);
            return created;
        }

        private static void RemoveParameter(
            AnimatorController controller,
            string parameterName)
        {
            AnimatorControllerParameter[] parameters = controller.parameters;
            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                if (parameters[i].name == parameterName)
                {
                    controller.RemoveParameter(i);
                }
            }
        }

        private static AnimatorState FindState(
            AnimatorStateMachine stateMachine,
            string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state != null && child.state.name == stateName)
                {
                    return child.state;
                }
            }

            return null;
        }

        private sealed class TransformSample
        {
            private readonly Transform target;
            private readonly Vector3 initialPosition;
            private readonly Quaternion initialRotation;
            private readonly Vector3 initialScale;
            private Vector3 position;
            private Quaternion rotation;
            private Vector3 scale;

            public TransformSample(Transform target)
            {
                this.target = target;
                initialPosition = target.localPosition;
                initialRotation = target.localRotation;
                initialScale = target.localScale;
                Reset();
            }

            public void Reset()
            {
                position = initialPosition;
                rotation = initialRotation;
                scale = initialScale;
            }

            public void Set(string propertyName, float value)
            {
                switch (propertyName)
                {
                    case "m_LocalPosition.x": position.x = value; break;
                    case "m_LocalPosition.y": position.y = value; break;
                    case "m_LocalPosition.z": position.z = value; break;
                    case "m_LocalRotation.x": rotation.x = value; break;
                    case "m_LocalRotation.y": rotation.y = value; break;
                    case "m_LocalRotation.z": rotation.z = value; break;
                    case "m_LocalRotation.w": rotation.w = value; break;
                    case "m_LocalScale.x": scale.x = value; break;
                    case "m_LocalScale.y": scale.y = value; break;
                    case "m_LocalScale.z": scale.z = value; break;
                }
            }

            public void Apply()
            {
                target.localPosition = position;
                target.localRotation = Normalize(rotation);
                target.localScale = scale;
            }

            private static Quaternion Normalize(Quaternion value)
            {
                float magnitude = Mathf.Sqrt(
                    value.x * value.x + value.y * value.y
                    + value.z * value.z + value.w * value.w);
                if (magnitude < 0.0001f) return Quaternion.identity;
                float inverse = 1f / magnitude;
                return new Quaternion(
                    value.x * inverse,
                    value.y * inverse,
                    value.z * inverse,
                    value.w * inverse);
            }
        }
    }
}
