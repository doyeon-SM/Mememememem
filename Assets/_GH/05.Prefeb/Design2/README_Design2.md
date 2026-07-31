# Design2 Migration Report

Generated from Main_World_2 instances whose source prefabs were under Assets/Ignore.

- Root prefabs: 16
- Copied dependency assets: 35
- Prefabs in Design2 (including nested dependencies): 16
- Converted MeshColliders: 10
- Copied bytes: 1738442
- Remaining Assets/Ignore dependencies: 0

## Terrain Tree LOD

- Applied one root `LODGroup` to all 16 Design2 prefabs.
- The existing enabled `MeshRenderer` components are grouped into LOD0.
- The final transition height is calculated per prefab from its renderer bounds.
- Reference settings: effective culling distance 175, PC LOD Bias 2, vertical FOV 60.
- The current `Main_World_2` Tree Distance 1424 is a test value and is not used for these LOD thresholds.
- After the final LOD threshold, the prefab is culled at approximately 175 world units on the PC quality level.
- Particle renderers in `CookingFireplace` and `TorchBig_Burning` are not registered as Terrain Tree LOD renderers.

## Moved code dependencies

- `Assets/Ignore/Lowpoly Style Ultra Pack/Shared Scripts/FlickerLight.cs -> Assets/_GH/02.Script/Design2/Imported/Lowpoly Style Ultra Pack/Shared Scripts/FlickerLight.cs`

## Scene prefab replacements

- `Assets/Ignore/Lowpoly Style Ultra Pack/Alpine Woodland/Prefabs/Bench1.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bench1.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/Alpine Woodland/Prefabs/Wagon.prefab` -> `Assets/_GH/05.Prefeb/Design2/Wagon.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/Asia/Prefabs/PrayerWheel.prefab` -> `Assets/_GH/05.Prefeb/Design2/PrayerWheel.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/Asia/Prefabs/PrayerWheelMovable.prefab` -> `Assets/_GH/05.Prefeb/Design2/PrayerWheelMovable.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/Asia/Prefabs/RadishField_Low.prefab` -> `Assets/_GH/05.Prefeb/Design2/RadishField_Low.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/Desert/Prefabs/Barrel.prefab` -> `Assets/_GH/05.Prefeb/Design2/Barrel.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/Desert/Prefabs/CookingFireplace.prefab` -> `Assets/_GH/05.Prefeb/Design2/CookingFireplace.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/Desert/Prefabs/TreeTrunk2.prefab` -> `Assets/_GH/05.Prefeb/Design2/TreeTrunk2.prefab`
- `Assets/Ignore/Lowpoly Style Ultra Pack/ForestPack/Prefabs/TorchBig_Burning.prefab` -> `Assets/_GH/05.Prefeb/Design2/TorchBig_Burning.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Beach_M/Sand_Castle.prefab` -> `Assets/_GH/05.Prefeb/Design2/Sand_Castle.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Beach_M/Sunscreen.prefab` -> `Assets/_GH/05.Prefeb/Design2/Sunscreen.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Beach_M/Towel_Beach.prefab` -> `Assets/_GH/05.Prefeb/Design2/Towel_Beach.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Farm_M/Scarecrow.prefab` -> `Assets/_GH/05.Prefeb/Design2/Scarecrow.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Farm_M/Timber.prefab` -> `Assets/_GH/05.Prefeb/Design2/Timber.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Farm_M/Wheat_Flour.prefab` -> `Assets/_GH/05.Prefeb/Design2/Wheat_Flour.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Medieval_M/Windmill_Medieval.prefab` -> `Assets/_GH/05.Prefeb/Design2/Windmill_Medieval.prefab`

## Collider conversions

- Assets/_GH/05.Prefeb/Design2/Barrel.prefab :: Barrel :: MeshCollider -> CapsuleCollider
- Assets/_GH/05.Prefeb/Design2/Bench1.prefab :: Bench1 :: MeshCollider -> BoxCollider
- Assets/_GH/05.Prefeb/Design2/PrayerWheel.prefab :: PrayerWheel :: MeshCollider -> CapsuleCollider
- Assets/_GH/05.Prefeb/Design2/Sand_Castle.prefab :: Sand_Castle :: MeshCollider -> BoxCollider
- Assets/_GH/05.Prefeb/Design2/Scarecrow.prefab :: Scarecrow :: MeshCollider -> BoxCollider
- Assets/_GH/05.Prefeb/Design2/Sunscreen.prefab :: Sunscreen :: MeshCollider -> SphereCollider
- Assets/_GH/05.Prefeb/Design2/Wagon.prefab :: Wagon :: MeshCollider -> BoxCollider
- Assets/_GH/05.Prefeb/Design2/Wheat_Flour.prefab :: Wheat_Flour :: MeshCollider -> BoxCollider
- Assets/_GH/05.Prefeb/Design2/Windmill_Medieval.prefab :: Windmill_Medieval/windmill-base-medieval :: MeshCollider -> BoxCollider
- Assets/_GH/05.Prefeb/Design2/Windmill_Medieval.prefab :: Windmill_Medieval/windmill-propeller-medieval :: MeshCollider -> BoxCollider

## 2026-07-29 Additional Migration

Additional prefabs placed in `Main_World_2` were copied out of `Assets/Ignore`
with new GUIDs so the scene can be reconstructed from Git.

- Root prefabs covered: 18
- New scene prefab replacements: 17
- Existing `Bg_TreeDead00` Design2 root completed: 1
- Copied root prefab assets: 17
- Copied dependency assets: 44
- Reused existing Design2 dependency assets: 1
- Copied bytes: 1172127
- Remaining `Assets/Ignore` prefab sources in `Main_World_2`: 0
- Remaining `Assets/Ignore` dependencies from migrated assets: 0
- Unresolved dependency GUIDs: 0

### Additional scene prefab replacements

- `Assets/Ignore/Lowpoly Style Ultra Pack/Greek Island/Prefabs/Miniscenes/AmphoresWhite2.prefab` -> `Assets/_GH/05.Prefeb/Design2/AmphoresWhite2.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Barrel01.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Barrel01.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Barrel02.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Barrel02.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Minecart00.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Minecart00.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Plank04.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Plank04.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Rail00.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Rail00.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Rail00Broken.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Rail00Broken.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Rail00C.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Rail00C.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Scaffold00Broken.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Scaffold00Broken.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Scaffold01.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Scaffold01.prefab`
- `Assets/Ignore/LowPolyTerrain-Mesa/Prefabs/Props/Bg_Tarp00.prefab` -> `Assets/_GH/05.Prefeb/Design2/Bg_Tarp00.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Props_M/Props Camping_M/Campfire_Cooker.prefab` -> `Assets/_GH/05.Prefeb/Design2/Campfire_Cooker.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Props_M/Props Camping_M/Chair_Outdoor.prefab` -> `Assets/_GH/05.Prefeb/Design2/Chair_Outdoor.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Props_M/Props Camping_M/Lantern_Camp.prefab` -> `Assets/_GH/05.Prefeb/Design2/Lantern_Camp.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Props_M/Props Camping_M/Log_Bench_Long.prefab` -> `Assets/_GH/05.Prefeb/Design2/Log_Bench_Long.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Props_M/Props Camping_M/Tent_Big_Roof.prefab` -> `Assets/_GH/05.Prefeb/Design2/Tent_Big_Roof.prefab`
- `Assets/Ignore/LowPolyUltimatePack/Low Poly Ultimate Pack/_M/Prefabs_M/Props_M/Props Camping_M/Thermos.prefab` -> `Assets/_GH/05.Prefeb/Design2/Thermos.prefab`

### Terrain tree dependency completion

- `Assets/_GH/05.Prefeb/Design2/Bg_TreeDead00.prefab`
- Mesh dependency copied to `Assets/_GH/05.Prefeb/Design2/Dependencies/LowPolyTerrain-Mesa/Meshes/Bg_TreeDead00.FBX`

### Scene-wide Git dependency completion

The final Unity scene dependency scan found 11 pre-existing shared assets under
Git-ignored folders. These asset/meta pairs were moved with their GUIDs unchanged
to `Assets/_GH/05.Prefeb/Design2/Dependencies/SceneShared`, so existing scene,
prefab, ScriptableObject, and UI references continue to resolve without YAML
rewrites.

- Shared assets moved: 11
- Shared bytes moved: 273155
- GUI sprites: 3
- Pandazole FBX meshes: 8

### Unity validation

Validated with Unity `6000.3.9f1` by synchronously importing the project and
opening `Assets/0.Scene/Main_World_2.unity`.

- Scene dependencies loaded: 1329
- Design2 prefab types loaded: 27
- Dependencies remaining under Git-ignored `Assets/Ignore`: 0
- Missing prefab roots: 0
- Missing meshes: 0
- Missing materials: 0
- Missing scripts: 0
