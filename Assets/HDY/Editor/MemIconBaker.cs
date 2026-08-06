using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using MemSystem.Data;
using MemSystem.Visual;

namespace HDY.Mem.Editor
{
    /// <summary>
    /// [에디터 전용] 멤 3D 모델(MemAppearanceTable)을 촬영해서 아이콘 PNG로 저장하거나
    /// 미리보기 텍스처를 만드는 핵심 로직.
    ///
    /// 기존 런타임 MemIconRenderer가 갖고 있던 카메라 프레이밍/바운드 계산 로직을 그대로
    /// 가져왔다(수치/공식 동일, 결과물이 달라지지 않도록).
    ///
    /// [씬 격리] 매 호출마다 완전히 새로운 임시 씬(저장 안 함, Additive)을 열어 그 안에서만
    /// 카메라/조명/모델 인스턴스를 만들고, 끝나면 전부 DestroyImmediate로 정리한 뒤 씬을 닫는다.
    /// 그래서 현재 작업 중인 씬(Dirty 상태 포함)에는 전혀 영향이 없다.
    /// 기존 런타임 버전은 "월드 좌표상 아주 멀리 떨어진 지점"에서 촬영했지만, 에디터 도구는
    /// 애초에 별도 씬에서 작업하므로 그 안전장치가 필요 없어 더 단순해졌다.
    ///
    /// [HDY 요청 - 악세서리 반영] MemData.accessories(Pikachu 추가)가 붙은 멤은 아이콘도 악세서리를
    /// 착용한 모습으로 찍어야 한다. 모델 인스턴스화 직후, 런타임 MemVisual.EquipAccessory와 완전히
    /// 같은 규칙(MemVisual.GetDefaultBoneName + MemVisual.FindBone으로 슬롯별 뼈를 찾아 그 자식으로
    /// 붙이고 positionOffset/rotationOffset/scaleMultiplier 적용)으로 악세서리를 부착한 뒤 촬영한다.
    /// 바운드 계산(CalculateWorldBounds)은 인스턴스 하위 전체 Renderer를 훑으므로, 악세서리를 붙이기만
    /// 하면 카메라 프레이밍에도 자동으로 포함된다 - 별도 수정이 필요 없다.
    /// </summary>
    public static class MemIconBaker
    {
        /// <summary>MemIconBakerWindow의 촬영 설정값 묶음. 값의 의미는 기존 MemIconRenderer와 동일하다.</summary>
        public struct Settings
        {
            public float cameraFitPadding;
            public float verticalFocusRatio;
            public float cameraAzimuthDegrees;
            public float cameraElevationDegrees;
            public float iconLightIntensity;
            public Color iconLightColor;
        }

        private struct ResolutionTarget
        {
            public int Size;
            public string Folder;

            public ResolutionTarget(int size, string folder)
            {
                Size = size;
                Folder = folder;
            }
        }

        private static readonly ResolutionTarget[] Resolutions =
        {
            new ResolutionTarget(64, "Assets/3.SO/Mems/Icons/64"),
            new ResolutionTarget(128, "Assets/3.SO/Mems/Icons/128"),
            new ResolutionTarget(512, "Assets/3.SO/Mems/Icons/512"),
        };

        /// <summary>
        /// 지정한 해상도 1개만 렌더링해서 텍스처로 반환한다(디스크에 저장하지 않음 - 미리보기 전용).
        /// 반환된 Texture2D는 호출 쪽에서 다 쓰고 나면 UnityEngine.Object.DestroyImmediate로 정리해야 한다.
        /// accessories가 비어있거나 null이면 악세서리 없이(기존과 동일하게) 촬영한다.
        /// </summary>
        public static Texture2D CapturePreview(GameObject modelPrefab, Settings settings, int resolution,
            IReadOnlyList<MemAccessoryData> accessories = null)
        {
            Texture2D result = null;

            RunInIsolatedScene(modelPrefab, settings, accessories, (camera) =>
            {
                result = RenderToTexture(camera, resolution);
            });

            return result;
        }

        /// <summary>
        /// 64/128/512 3개 해상도를 전부 렌더링해서 PNG로 저장(기존 파일은 내용만 덮어써 GUID를 유지)하고,
        /// MemIconTable에 그대로 넣을 수 있는 Entry를 만들어 반환한다. 실제로 테이블에 반영하는 것은
        /// 호출 쪽(MemIconBakerWindow)에서 MemIconTable.EditorUpsertEntry로 처리한다.
        /// accessories가 비어있거나 null이면 악세서리 없이(기존과 동일하게) 굽는다.
        /// </summary>
        public static MemIconTable.Entry BakeAndSave(string memId, GameObject modelPrefab, Settings settings,
            IReadOnlyList<MemAccessoryData> accessories = null)
        {
            var entry = new MemIconTable.Entry { Mem_ID = memId };

            RunInIsolatedScene(modelPrefab, settings, accessories, (camera) =>
            {
                for (int i = 0; i < Resolutions.Length; i++)
                {
                    var res = Resolutions[i];
                    var texture = RenderToTexture(camera, res.Size);
                    var sprite = SaveTextureAsSprite(texture, res.Folder, memId);
                    UnityEngine.Object.DestroyImmediate(texture);

                    if (res.Size == 64) entry.Icon64 = sprite;
                    else if (res.Size == 128) entry.Icon128 = sprite;
                    else if (res.Size == 512) entry.Icon512 = sprite;
                }
            });

            return entry;
        }

        // =================================================================
        // 씬 격리 + 카메라/조명/모델/악세서리 준비
        // =================================================================

        /// <summary>임시 씬에 카메라+조명+모델(+악세서리) 인스턴스를 만들고, 프레이밍까지 끝낸 카메라를
        /// 콜백에 넘겨준다. 콜백이 끝나면 전부 정리하고 임시 씬을 닫는다.</summary>
        private static void RunInIsolatedScene(GameObject modelPrefab, Settings settings,
            IReadOnlyList<MemAccessoryData> accessories, Action<Camera> action)
        {
            if (modelPrefab == null)
            {
                Debug.LogWarning("[MemIconBaker] modelPrefab이 비어있어 촬영을 건너뜁니다.");
                return;
            }

            var tempScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            GameObject instance = null;
            GameObject cameraObject = null;

            try
            {
                instance = (GameObject)UnityEngine.Object.Instantiate(modelPrefab, Vector3.zero, Quaternion.identity);
                SceneManager.MoveGameObjectToScene(instance, tempScene);

                ApplyAccessories(instance, accessories);
                DisablePhysicsSideEffects(instance); // 악세서리 부착 이후 실행 - 악세서리 쪽 콜라이더/리지드바디도 함께 무력화된다.

                cameraObject = new GameObject("MemIconBaker_Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, tempScene);

                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 완전 투명 배경
                camera.cullingMask = ~0;
                camera.nearClipPlane = 0.01f;
                camera.enabled = false; // Render()를 직접 호출할 때만 그린다.

                var light = cameraObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = settings.iconLightColor;
                light.intensity = settings.iconLightIntensity;
                light.shadows = LightShadows.None;
                light.cullingMask = ~0;

                FrameCameraToBounds(camera, light, instance, settings);

                action(camera);
            }
            finally
            {
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                EditorSceneManager.CloseScene(tempScene, true);
            }
        }

        /// <summary>
        /// [HDY 요청 - 악세서리 반영] modelData.accessories를 런타임 MemVisual.EquipAccessory와 동일한
        /// 규칙으로 부착한다: 슬롯 -> 기본 부착 뼈 이름(MemVisual.GetDefaultBoneName) -> 그 이름을 가진
        /// 뼈 Transform(MemVisual.FindBone)을 찾아 그 자식으로 인스턴스화하고, positionOffset/
        /// rotationOffset/scaleMultiplier를 그대로 적용한다.
        /// 뼈를 못 찾거나 prefab이 비어있는 항목은 경고만 남기고 건너뛴다(전체 촬영을 막지 않는다).
        /// </summary>
        private static void ApplyAccessories(GameObject instance, IReadOnlyList<MemAccessoryData> accessories)
        {
            if (accessories == null || accessories.Count == 0) return;

            for (int i = 0; i < accessories.Count; i++)
            {
                var accessory = accessories[i];
                if (accessory == null) continue;

                if (accessory.prefab == null)
                {
                    Debug.LogWarning($"[MemIconBaker] 악세서리 '{accessory.name}'에 prefab이 지정되지 않아 건너뜁니다.");
                    continue;
                }

                string boneName = MemVisual.GetDefaultBoneName(accessory.slot);
                Transform bone = MemVisual.FindBone(instance.transform, boneName);

                if (bone == null)
                {
                    Debug.LogWarning($"[MemIconBaker] 악세서리 '{accessory.name}'의 부착 뼈를 찾을 수 없습니다. " +
                                      $"슬롯: {accessory.slot}, 기대 뼈 이름: '{boneName}', 모델: {instance.name}");
                    continue;
                }

                var accessoryInstance = (GameObject)UnityEngine.Object.Instantiate(accessory.prefab, bone);
                accessoryInstance.transform.localPosition = accessory.positionOffset;
                accessoryInstance.transform.localRotation = Quaternion.Euler(accessory.rotationOffset);
                accessoryInstance.transform.localScale =
                    Vector3.Scale(accessory.prefab.transform.localScale, accessory.scaleMultiplier);
            }
        }

        /// <summary>촬영용 임시 인스턴스가 물리적으로 부작용(낙하, 충돌 등)을 일으키지 않도록 막는다.</summary>
        private static void DisablePhysicsSideEffects(GameObject instance)
        {
            foreach (var col in instance.GetComponentsInChildren<Collider>(true))
            {
                col.enabled = false;
            }

            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
        }

        // =================================================================
        // 카메라 프레이밍 (기존 MemIconRenderer.FrameCameraToBounds와 동일한 공식)
        // =================================================================

        private static void FrameCameraToBounds(Camera camera, Light light, GameObject instance, Settings settings)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"[MemIconBaker] '{instance.name}'에서 Renderer를 찾을 수 없어 기본 프레이밍으로 촬영합니다.");
                return;
            }

            Bounds bounds = CalculateWorldBounds(renderers);

            float maxExtent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z, 0.01f);
            camera.orthographicSize = maxExtent * settings.cameraFitPadding;

            // 카메라가 바라볼 지점: 발끝(bounds.min.y)에서 전체 키(bounds.size.y)의 verticalFocusRatio만큼 올라간 높이.
            float focusHeight = bounds.min.y + bounds.size.y * settings.verticalFocusRatio;
            Vector3 focusPoint = new Vector3(bounds.center.x, focusHeight, bounds.center.z);

            float azimuthRad = settings.cameraAzimuthDegrees * Mathf.Deg2Rad;
            float elevationRad = settings.cameraElevationDegrees * Mathf.Deg2Rad;
            Vector3 offsetDirection = new Vector3(
                Mathf.Sin(azimuthRad) * Mathf.Cos(elevationRad),
                Mathf.Sin(elevationRad),
                Mathf.Cos(azimuthRad) * Mathf.Cos(elevationRad));

            float cameraDistance = maxExtent * 4f + 1f;
            Vector3 cameraOffset = offsetDirection * cameraDistance;
            camera.transform.position = focusPoint + cameraOffset;
            camera.transform.LookAt(focusPoint);

            // [씬 격리라 안전 마진 불필요] 기존 런타임 버전은 조명이 실제 게임 씬으로 새어나가지 않도록
            // shootingStagePosition 기준 안전 거리 캡을 씌웠지만, 여기는 완전히 격리된 임시 씬이라
            // 그 캡이 필요 없다.
            light.range = maxExtent * 7f + 1f;
        }

        /// <summary>
        /// Renderer.bounds(월드, 애니메이션/스키닝 상태에 따라 달라짐) 대신 Renderer.localBounds(메시 원본
        /// 기준, 항상 안정적)를 각 Renderer의 transform으로 월드 변환해서 합산한다.
        /// 악세서리 Renderer도 이 배열(instance.GetComponentsInChildren)에 자연히 포함되므로,
        /// 별도 처리 없이 그대로 프레이밍에 반영된다.
        /// </summary>
        private static Bounds CalculateWorldBounds(Renderer[] renderers)
        {
            Bounds combined = TransformLocalBoundsToWorld(renderers[0]);

            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(TransformLocalBoundsToWorld(renderers[i]));
            }

            return combined;
        }

        /// <summary>로컬 바운드의 8개 모서리를 각각 월드로 변환해서 다시 감싼다 - 회전이 있어도 정확하다.</summary>
        private static Bounds TransformLocalBoundsToWorld(Renderer renderer)
        {
            var localBounds = renderer.localBounds;
            var rendererTransform = renderer.transform;

            Bounds worldBounds = new Bounds(rendererTransform.TransformPoint(localBounds.center), Vector3.zero);

            for (int xSign = -1; xSign <= 1; xSign += 2)
            {
                for (int ySign = -1; ySign <= 1; ySign += 2)
                {
                    for (int zSign = -1; zSign <= 1; zSign += 2)
                    {
                        Vector3 corner = localBounds.center + Vector3.Scale(localBounds.extents, new Vector3(xSign, ySign, zSign));
                        worldBounds.Encapsulate(rendererTransform.TransformPoint(corner));
                    }
                }
            }

            return worldBounds;
        }

        // =================================================================
        // 렌더 + PNG 저장
        // =================================================================

        /// <summary>
        /// [빌드 노이즈 방지 - 기존 로직과 동일] 같은 프레이밍으로 두 번 렌더링한다 - 첫 번째는
        /// 셰이더/드라이버 예열용으로 버리고, 두 번째 결과만 실제로 ReadPixels로 캡처한다.
        /// </summary>
        private static Texture2D RenderToTexture(Camera camera, int resolution)
        {
            var rt = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            rt.Create();
            camera.targetTexture = rt;

            camera.Render();
            GL.Flush();
            camera.Render();
            GL.Flush();

            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var previousActive = RenderTexture.active;
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
            texture.Apply();
            RenderTexture.active = previousActive;

            camera.targetTexture = null;
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);

            return texture;
        }

        /// <summary>
        /// 텍스처를 PNG로 저장(내용만 덮어써 기존 .meta/GUID 유지)하고, Sprite로 임포트 설정한 뒤
        /// 로드해서 반환한다. 신규 생성 시에도, 재굽기로 덮어쓸 때도 항상 임포트 설정을 다시 맞춘다.
        /// </summary>
        private static Sprite SaveTextureAsSprite(Texture2D texture, string folder, string memId)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var path = folder + "/" + memId + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
