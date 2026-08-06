# Terrain Tree NavMesh Baker

Unity AI Navigation's `NavMeshSurface` does not automatically include Terrain Tree instances as bake obstacles.

Use:

1. Open the world scene that contains the Terrain and its `NavMeshSurface` components.
2. Save any placement changes.
3. Run `Tools > GH > NavMesh > Report Active Scene Terrain Trees` to inspect the tree/prototype counts.
4. Run `Tools > GH > NavMesh > Bake Active Scene With Terrain Trees`.
5. Wait for the completion message in the Console.

The baker:

- reads every active Terrain's `TerrainData.treeInstances`;
- reads enabled, non-trigger colliders from each Tree Prototype;
- converts Box, Capsule, and Sphere colliders into temporary `Not Walkable` modifier volumes;
- uses a MeshCollider's mesh bounds as a conservative box fallback;
- updates every enabled `NavMeshSurface` with an existing `NavMeshData` asset;
- saves the updated NavMesh assets;
- removes all temporary proxy objects after the bake.

No proxy GameObjects are saved into the scene or included in a player build. Run the bake command again after adding, deleting, moving, rotating, or resizing Terrain Trees.
