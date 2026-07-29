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
