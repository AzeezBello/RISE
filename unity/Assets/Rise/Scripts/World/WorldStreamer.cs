using System.Collections.Generic;
using UnityEngine;
using Rise.Data;

namespace Rise.World
{
    // Spatial 500m x 500m chunk indexer, active-radius streamer (3x3 full-detail radius), state/biome tracker,
    // and floating-origin coordinate rebasing guard (PLAN §5.2).
    public class WorldStreamer : MonoBehaviour
    {
        public const float ChunkSize = 500f;
        public const int ActiveRadius = 3;
        public const float RebaseThreshold = 4000f;

        public class WorldChunk
        {
            public Vector2Int Coord;
            public Bounds Bounds;
            public bool Active = true;
            public readonly List<Renderer> DetailRenderers = new List<Renderer>();
        }

        WorldData data;
        Transform worldRoot;
        Transform follow;
        float checkTimer;

        public readonly Dictionary<Vector2Int, WorldChunk> Chunks = new Dictionary<Vector2Int, WorldChunk>();
        public Vector2Int CurrentChunk { get; private set; }
        public string CurrentStateId { get; private set; } = "lagos";
        public string CurrentBiome { get; private set; } = "coastal";
        public Vector2 GlobalOffsetXZ { get; private set; }
        public int ActiveChunkCount { get; private set; }
        public int TotalChunkCount => Chunks.Count;
        public bool EnableDistanceCulling = true;

        [System.NonSerialized] public System.Action<string, string> OnStateChanged;

        public static Vector2Int ChunkCoord(Vector3 worldPos) =>
            new Vector2Int(Mathf.FloorToInt(worldPos.x / ChunkSize), Mathf.FloorToInt(worldPos.z / ChunkSize));

        public void Init(WorldData worldData, Transform root, Transform playerTransform)
        {
            data = worldData;
            worldRoot = root;
            follow = playerTransform;
            IndexWorldChunks();
            if (follow != null)
            {
                CurrentChunk = ChunkCoord(follow.position);
                CurrentStateId = data != null ? data.StateAt(follow.position) : "lagos";
                CurrentBiome = data != null ? data.BiomeAt(follow.position) : "coastal";
                RefreshActiveChunks(CurrentChunk, true);
            }
        }

        void IndexWorldChunks()
        {
            Chunks.Clear();
            if (worldRoot == null) return;
            // Index Housing, Street Furniture, Billboards, Vegetation, and GLB world models into 500m x 500m spatial cells.
            // Roads, Bridges, Landmarks, BusStops, and Properties remain globally active for navigation & physics.
            string[] streamGroups = { "Housing", "Vegetation", "Street Furniture", "Billboards", "LagosGlbModels" };
            foreach (var groupName in streamGroups)
            {
                var grp = worldRoot.Find(groupName);
                if (grp == null) continue;
                var renderers = grp.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var coord = ChunkCoord(r.transform.position);
                    if (!Chunks.TryGetValue(coord, out var chunk))
                    {
                        var center = new Vector3((coord.x + 0.5f) * ChunkSize, 20f, (coord.y + 0.5f) * ChunkSize);
                        chunk = new WorldChunk
                        {
                            Coord = coord,
                            Bounds = new Bounds(center, new Vector3(ChunkSize, 120f, ChunkSize)),
                            Active = true
                        };
                        Chunks[coord] = chunk;
                    }
                    chunk.DetailRenderers.Add(r);
                }
            }
            ActiveChunkCount = Chunks.Count;
        }

        public void RefreshActiveChunks()
        {
            if (follow == null) return;
            var pos = follow.position;
            RefreshActiveChunks(ChunkCoord(pos), true);
            if (data != null)
            {
                string st = data.StateAt(pos);
                string bm = data.BiomeAt(pos);
                if (st != CurrentStateId || bm != CurrentBiome)
                {
                    CurrentStateId = st;
                    CurrentBiome = bm;
                    OnStateChanged?.Invoke(CurrentStateId, CurrentBiome);
                }
            }
        }

        public void RefreshActiveChunks(Vector2Int centerChunk, bool force = false)
        {
            if (!force && centerChunk == CurrentChunk) return;
            CurrentChunk = centerChunk;
            int active = 0;
            foreach (var kv in Chunks)
            {
                var c = kv.Value;
                int dx = Mathf.Abs(c.Coord.x - centerChunk.x);
                int dz = Mathf.Abs(c.Coord.y - centerChunk.y);
                bool shouldBeActive = !EnableDistanceCulling || (dx <= ActiveRadius && dz <= ActiveRadius);
                if (shouldBeActive) active++;
                if (c.Active != shouldBeActive)
                {
                    c.Active = shouldBeActive;
                    for (int i = 0; i < c.DetailRenderers.Count; i++)
                    {
                        var r = c.DetailRenderers[i];
                        if (r != null) r.enabled = shouldBeActive;
                    }
                }
            }
            ActiveChunkCount = active;
        }

        void Update()
        {
            if (follow == null || data == null) return;
            checkTimer -= Time.deltaTime;
            if (checkTimer > 0f) return;
            checkTimer = 0.5f;

            var pos = follow.position;
            var nextChunk = ChunkCoord(pos);
            if (nextChunk != CurrentChunk)
                RefreshActiveChunks(nextChunk);

            string st = data.StateAt(pos);
            string bm = data.BiomeAt(pos);
            if (st != CurrentStateId || bm != CurrentBiome)
            {
                CurrentStateId = st;
                CurrentBiome = bm;
                OnStateChanged?.Invoke(CurrentStateId, CurrentBiome);
            }
        }
    }
}
