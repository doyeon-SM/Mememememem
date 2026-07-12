#if UNITY_EDITOR
using System.Linq;
using KMS;
using KMS.InventoryDuped;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace KMS.EditorTools
{
    public static class CapsuleThrowSetup
    {
        private const string SourceClipPath = "Assets/KMS/4.Animation/Dodo/Clips/New_Throw.fbx";
        private const string SlashClipPath = "Assets/KMS/4.Animation/Dodo/Clips/Slash.anim";
        private const string ClipFolder = "Assets/KMS/4.Animation/Dodo/Clips/ThrowTemp";
        private const string ControllerPath = "Assets/KMS/4.Animation/Dodo/Controllers/KMS_DodoAnimator.controller";
        private const string PlayerPrefabPath = "Assets/KMS/2.Prefabs/0708_Player_KMS.prefab";
        private const string CapsulePrefabPath = "Assets/HDY/2.Prefabs/ItemPrefab/TestCapsule.prefab";
        private const float TemporarySlashDuration = 0.5f;

        [MenuItem("KMS/Setup Temporary Capsule Throw")]
        public static void Run()
        {
            AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(SourceClipPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));

            EnsureFolder("Assets/KMS/4.Animation/Dodo/Clips", "ThrowTemp");
            AnimationClip prepare;
            AnimationClip idle;
            AnimationClip go;
            if (source != null)
            {
                prepare = CreateOrUpdateClip(source, $"{ClipFolder}/Throw_Prepare.anim", "Throw_Prepare", false, null);
                idle = CreateOrUpdateClip(source, $"{ClipFolder}/Throw_Idle.anim", "Throw_Idle", true, null);
                go = CreateOrUpdateClip(source, $"{ClipFolder}/Throw_Go.anim", "Throw_Go", false, new[]
                {
                    new AnimationEvent { functionName = "OnCapsuleRelease", time = source.length * 0.18f },
                    new AnimationEvent { functionName = "OnCapsuleThrowFinished", time = source.length * 0.6f }
                });
            }
            else
            {
                prepare = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/Throw_Prepare.anim");
                idle = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/Throw_Idle.anim");
                go = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/Throw_Go.anim");
                if (prepare == null || idle == null || go == null)
                {
                    throw new System.InvalidOperationException("New_Throw 원본과 기존 ThrowTemp 클립을 모두 찾을 수 없습니다.");
                }
            }
            AnimationClip slash = AssetDatabase.LoadAssetAtPath<AnimationClip>(SlashClipPath);
            if (slash == null) throw new System.InvalidOperationException($"Slash AnimationClip을 찾을 수 없습니다: {SlashClipPath}");

            ConfigureAnimator(prepare, idle, go, slash);
            ConfigurePlayerPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CapsuleThrowSetup] 임시 투척 클립, Animator, 0708_Player_KMS 연결 완료");
        }

        private static AnimationClip CreateOrUpdateClip(
            AnimationClip source,
            string path,
            string clipName,
            bool loop,
            AnimationEvent[] events)
        {
            AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (target == null)
            {
                target = Object.Instantiate(source);
                AssetDatabase.CreateAsset(target, path);
            }
            else
            {
                EditorUtility.CopySerialized(source, target);
            }

            target.name = clipName;
            SerializedObject serializedClip = new SerializedObject(target);
            SerializedProperty loopTime = serializedClip.FindProperty("m_AnimationClipSettings.m_LoopTime");
            if (loopTime != null) loopTime.boolValue = loop;
            serializedClip.ApplyModifiedPropertiesWithoutUndo();
            AnimationUtility.SetAnimationEvents(target, events ?? new AnimationEvent[0]);
            EditorUtility.SetDirty(target);
            return target;
        }

        private static void ConfigureAnimator(AnimationClip prepareClip, AnimationClip idleClip, AnimationClip goClip, AnimationClip slashClip)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null) throw new System.InvalidOperationException($"AnimatorController를 찾을 수 없습니다: {ControllerPath}");

            RemoveParameter(controller, "Throw");
            EnsureParameter(controller, "ThrowPrepare", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "ThrowReady", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "ThrowGo", AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, "Slash", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            RemoveState(machine, "Throw_Prepare");
            RemoveState(machine, "Throw_Idle");
            RemoveState(machine, "Throw_Go");
            RemoveState(machine, "Slash");

            AnimatorState locomotion = machine.states.Select(child => child.state).First(state => state.name == "Locomotion");
            AnimatorState prepare = machine.AddState("Throw_Prepare", new Vector3(690f, -80f));
            AnimatorState idle = machine.AddState("Throw_Idle", new Vector3(900f, -80f));
            AnimatorState go = machine.AddState("Throw_Go", new Vector3(900f, 30f));
            AnimatorState slash = machine.AddState("Slash", new Vector3(690f, 130f));
            prepare.motion = prepareClip;
            idle.motion = idleClip;
            go.motion = goClip;
            slash.motion = slashClip;
            slash.speed = Mathf.Max(0.01f, slashClip.length / TemporarySlashDuration);

            AnimatorStateTransition toPrepare = locomotion.AddTransition(prepare);
            toPrepare.hasExitTime = false;
            toPrepare.duration = 0.1f;
            toPrepare.AddCondition(AnimatorConditionMode.If, 0f, "ThrowPrepare");

            AnimatorStateTransition toIdle = prepare.AddTransition(idle);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.9f;
            toIdle.duration = 0.1f;
            toIdle.AddCondition(AnimatorConditionMode.If, 0f, "ThrowReady");

            AnimatorStateTransition prepareToGo = prepare.AddTransition(go);
            prepareToGo.hasExitTime = false;
            prepareToGo.duration = 0.08f;
            prepareToGo.AddCondition(AnimatorConditionMode.If, 0f, "ThrowGo");

            AnimatorStateTransition idleToGo = idle.AddTransition(go);
            idleToGo.hasExitTime = false;
            idleToGo.duration = 0.08f;
            idleToGo.AddCondition(AnimatorConditionMode.If, 0f, "ThrowGo");

            AnimatorStateTransition returnToLocomotion = go.AddTransition(locomotion);
            returnToLocomotion.hasExitTime = true;
            returnToLocomotion.exitTime = 0.98f;
            returnToLocomotion.duration = 0.12f;

            AnimatorStateTransition toSlash = locomotion.AddTransition(slash);
            toSlash.hasExitTime = false;
            toSlash.duration = 0.08f;
            toSlash.AddCondition(AnimatorConditionMode.If, 0f, "Slash");

            AnimatorStateTransition slashToLocomotion = slash.AddTransition(locomotion);
            slashToLocomotion.hasExitTime = true;
            slashToLocomotion.exitTime = 0.95f;
            slashToLocomotion.duration = 0.1f;

            EditorUtility.SetDirty(controller);
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerCapsuleThrowController throwController = root.GetComponent<PlayerCapsuleThrowController>();
                if (throwController == null) throwController = root.AddComponent<PlayerCapsuleThrowController>();

                PlayerMovement movement = root.GetComponent<PlayerMovement>();
                Animator animator = movement != null ? movement.Animator : null;
                if (animator == null)
                {
                    throw new System.InvalidOperationException("PlayerMovement에 실제 플레이어 Animator가 연결되어 있지 않습니다.");
                }

                foreach (PlayerAnimationEvents existingEvents in root.GetComponentsInChildren<PlayerAnimationEvents>(true))
                {
                    if (existingEvents.gameObject != animator.gameObject) Object.DestroyImmediate(existingEvents);
                }

                PlayerAnimationEvents animationEvents = animator.GetComponent<PlayerAnimationEvents>();
                if (animationEvents == null) animator.gameObject.AddComponent<PlayerAnimationEvents>();

                foreach (Transform oldOrigin in root.GetComponentsInChildren<Transform>(true)
                             .Where(transform => transform.name == "CapsuleThrowOrigin").ToArray())
                {
                    Object.DestroyImmediate(oldOrigin.gameObject);
                }

                Transform rightHand = animator.isHuman
                    ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                    : animator.GetComponentsInChildren<Transform>(true)
                        .FirstOrDefault(transform => transform.name.EndsWith("RightHand"));
                Transform origin = rightHand != null ? rightHand : animator.transform;

                SerializedObject serialized = new SerializedObject(throwController);
                serialized.FindProperty("input").objectReferenceValue = root.GetComponent<PlayerInput>();
                serialized.FindProperty("movement").objectReferenceValue = movement;
                serialized.FindProperty("inventory").objectReferenceValue = root.GetComponent<PlayerInventory>();
                serialized.FindProperty("hud").objectReferenceValue = root.GetComponent<PlayerHUD>();
                serialized.FindProperty("animator").objectReferenceValue = animator;
                serialized.FindProperty("throwOrigin").objectReferenceValue = origin;
                serialized.FindProperty("capsulePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(CapsulePrefabPath);
                serialized.FindProperty("throwSpeed").floatValue = 12f;
                serialized.FindProperty("upwardThrowSpeed").floatValue = 2.5f;
                serialized.FindProperty("fallbackReleaseNormalizedTime").floatValue = 0.2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                KMS.Harvesting.PlayerHarvestController harvestController = root.GetComponent<KMS.Harvesting.PlayerHarvestController>();
                if (harvestController != null)
                {
                    SerializedObject serializedHarvest = new SerializedObject(harvestController);
                    serializedHarvest.FindProperty("movement").objectReferenceValue = movement;
                    serializedHarvest.FindProperty("animator").objectReferenceValue = animator;
                    serializedHarvest.FindProperty("toolUseCooldown").floatValue = TemporarySlashDuration;
                    serializedHarvest.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(parameter => parameter.name == name)) controller.AddParameter(name, type);
        }

        private static void RemoveParameter(AnimatorController controller, string name)
        {
            for (int i = controller.parameters.Length - 1; i >= 0; i--)
            {
                if (controller.parameters[i].name == name) controller.RemoveParameter(i);
            }
        }

        private static void RemoveState(AnimatorStateMachine machine, string name)
        {
            AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(candidate => candidate.name == name);
            if (state != null) machine.RemoveState(state);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
