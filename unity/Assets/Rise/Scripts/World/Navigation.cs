using System.Collections.Generic;
using UnityEngine;
using Rise.Data;

namespace Rise.World
{
    // GPS: a bobbing marker on the objective and a dashed-feel route line along the road
    // grid. Junctions are nodes, stretches between neighbouring junctions are edges, and the endpoints join the graph
    // along their own road; Dijkstra finds the way. Everything in data coordinates, drawn in world coordinates.
    public class Navigation : MonoBehaviour
    {
        WorldData data;
        LineRenderer line;
        Transform marker, beacon, chevronRoot;
        readonly List<Transform> chevrons = new List<Transform>();
        readonly List<Material> chevronMats = new List<Material>();
        List<Vector3> currentRoute = new List<Vector3>();
        float refreshT;
        class Node { public float x, z; public List<(Node to, float w)> edges = new List<(Node, float)>(); }
        readonly List<Node> nodes = new List<Node>();
        readonly Dictionary<string, List<Node>> onRoad = new Dictionary<string, List<Node>>();

        [System.NonSerialized] public Vector3? Target;   // world position of the objective, or null (runtime only)
        public float RouteLength { get; private set; }
        public Vector3 NextWaypoint { get; private set; }

        public void Init(WorldData world, Transform parent)
        {
            data = world;
            foreach (var j in data.junctions)
            {
                var n = new Node { x = j.x, z = j.z }; nodes.Add(n);
                Key("h", j.z).Add(n); Key("v", j.x).Add(n);
            }
            foreach (var r in data.roads)
            {
                if (!onRoad.TryGetValue(r.axis + ":" + r.k, out var list)) continue;
                list.Sort((a, b) => (r.Horizontal ? a.x : a.z).CompareTo(r.Horizontal ? b.x : b.z));
                for (int i = 1; i < list.Count; i++)
                {
                    var u = list[i - 1]; var v = list[i]; float d = Mathf.Sqrt((u.x - v.x) * (u.x - v.x) + (u.z - v.z) * (u.z - v.z));
                    u.edges.Add((v, d)); v.edges.Add((u, d));
                }
            }
            var unlit = ShaderLib.Unlit;
            var go = new GameObject("GPS route"); go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // TransformZ alignment: the strip lies flat on the road
            line = go.AddComponent<LineRenderer>();
            line.material = new Material(unlit) { color = new Color(1.0f, 0.86f, 0.16f, 0.92f) };
            line.widthMultiplier = 0.55f; line.numCornerVertices = 3; line.numCapVertices = 3; line.alignment = LineAlignment.TransformZ; line.positionCount = 0;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Glowing golden-yellow 3D ground halo ring + floating taxi/objective beacon + road approach chevrons
            marker = new GameObject("Mission marker").transform;
            marker.SetParent(parent, false);
            var haloGold = new Material(unlit) { color = new Color(1.0f, 0.88f, 0.12f, 1f) };
            var haloRim = new Material(unlit) { color = new Color(1.0f, 0.76f, 0.08f, 1f) };
            var beaconMat = new Material(unlit) { color = new Color(1.0f, 0.84f, 0.10f, 1f) };

            Transform RingPart(Transform p, string n, PrimitiveType pt, Vector3 pos, Vector3 sc, Material m, Quaternion? rot = null)
            {
                var g = GameObject.CreatePrimitive(pt); g.name = n;
                g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = sc;
                if (rot.HasValue) g.transform.localRotation = rot.Value;
                var mr = g.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Destroy(g.GetComponent<Collider>());
                return g.transform;
            }

            // Ground halo ring & vertical light rim
            RingPart(marker, "HaloBase", PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(4.2f, 0.04f, 4.2f), haloGold);
            RingPart(marker, "HaloRim", PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 0f), new Vector3(4.35f, 0.22f, 4.35f), haloRim);

            // Floating 3D Taxi / Objective Beacon above the ring
            beacon = new GameObject("Beacon").transform;
            beacon.SetParent(marker, false);
            beacon.localPosition = new Vector3(0f, 1.85f, 0f);
            RingPart(beacon, "CarBody", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.95f, 0.30f, 1.45f), beaconMat);
            RingPart(beacon, "CarCabin", PrimitiveType.Cube, new Vector3(0f, 0.24f, -0.05f), new Vector3(0.76f, 0.26f, 0.82f), beaconMat);
            RingPart(beacon, "TaxiTopper", PrimitiveType.Cube, new Vector3(0f, 0.44f, -0.05f), new Vector3(0.36f, 0.16f, 0.24f), haloRim);

            // Animated glowing white/gold ground chevrons ("> > > > >") leading into the ring
            chevronRoot = new GameObject("ApproachChevrons").transform;
            chevronRoot.SetParent(parent, false);
            for (int i = 0; i < 6; i++)
            {
                var ch = new GameObject("Chevron_" + i).transform;
                ch.SetParent(chevronRoot, false);
                var cMat = new Material(unlit) { color = new Color(1.0f, 0.96f, 0.82f, 1f) };
                RingPart(ch, "LeftWing", PrimitiveType.Cube, new Vector3(-0.46f, 0.05f, -0.22f), new Vector3(1.10f, 0.04f, 0.20f), cMat, Quaternion.Euler(0f, -32f, 0f));
                RingPart(ch, "RightWing", PrimitiveType.Cube, new Vector3(0.46f, 0.05f, -0.22f), new Vector3(1.10f, 0.04f, 0.20f), cMat, Quaternion.Euler(0f, 32f, 0f));
                ch.gameObject.SetActive(false);
                chevrons.Add(ch);
                chevronMats.Add(cMat);
            }

            marker.gameObject.SetActive(false);
        }

        List<Node> Key(string axis, float k) { var key = axis + ":" + k; if (!onRoad.TryGetValue(key, out var l)) onRoad[key] = l = new List<Node>(); return l; }

        // Nearest road to a world point (preferring the carriageway and elevation the point sits on, e.g. bridge ramps).
        bool Snap(Vector3 w, out RoadData road, out float sx, out float sz, out float d)
        {
            float x = w.x, z = WorldData.DataZ(w.z);
            road = null; sx = x; sz = z; d = float.MaxValue;
            float bestScore = float.MaxValue;
            foreach (var r in data.roads)
            {
                float along = r.Horizontal ? x : z, across = r.Horizontal ? z : x;
                if (along < r.from - 20f || along > r.to + 20f) continue;
                float a = Mathf.Clamp(along, r.from, r.to);
                float dd = Mathf.Abs(across - r.k);
                float outside = Mathf.Max(0f, dd - r.width * 0.5f);
                float dy = Mathf.Abs(w.y - data.RoadHeightAt(r, a));
                float score = outside + dd * 0.35f + dy * 3.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    d = dd;
                    road = r;
                    sx = r.Horizontal ? a : r.k;
                    sz = r.Horizontal ? r.k : a;
                }
            }
            return road != null;
        }

        // Route between two world positions as a list of world points along the roads.
        public List<Vector3> Route(Vector3 fromW, Vector3 toW)
        {
            float ax = fromW.x, az = WorldData.DataZ(fromW.z), bx = toW.x, bz = WorldData.DataZ(toW.z);
            var pts = new List<Vector2> { new Vector2(ax, az) };
            if (!Snap(fromW, out var A, out float asx, out float asz, out float ad) || !Snap(toW, out var B, out float bsx, out float bsz, out float bd)) { pts.Add(new Vector2(bx, bz)); return ToWorld(pts, null, null); }
            if (ad > 3f) pts.Add(new Vector2(asx, asz));
            if (!(A == B))
            {
                var starts = new List<(Node, float)>(); foreach (var n in Key(A.axis, A.k)) starts.Add((n, Mathf.Abs((A.Horizontal ? n.x : n.z) - (A.Horizontal ? asx : asz))));
                var goals = new Dictionary<Node, float>(); foreach (var n in Key(B.axis, B.k)) goals[n] = Mathf.Abs((B.Horizontal ? n.x : n.z) - (B.Horizontal ? bsx : bsz));
                var path = Dijkstra(starts, goals);
                if (path != null) foreach (var n in path) pts.Add(new Vector2(n.x, n.z));
            }
            if (bd > 3f) pts.Add(new Vector2(bsx, bsz));
            pts.Add(new Vector2(bx, bz));
            var clean = new List<Vector2>();
            foreach (var p in pts) if (clean.Count == 0 || (p - clean[clean.Count - 1]).magnitude > 0.5f) clean.Add(p);
            return ToWorld(clean, A, B);
        }

        float SegmentHeight(Vector2 a, Vector2 b, Vector2 p, RoadData fallbackRoad = null)
        {
            if (Mathf.Approximately(a.y, b.y) && !Mathf.Approximately(a.x, b.x))
            {
                var r = data.FindRoad("h", a.y);
                if (r != null) return data.RoadHeightAt(r, p.x);
            }
            else if (Mathf.Approximately(a.x, b.x) && !Mathf.Approximately(a.y, b.y))
            {
                var r = data.FindRoad("v", a.x);
                if (r != null) return data.RoadHeightAt(r, p.y);
            }
            if (fallbackRoad != null)
                return data.RoadHeightAt(fallbackRoad, fallbackRoad.Horizontal ? p.x : p.y);
            return data.DeckHeightAtData(p.x, p.y);
        }

        List<Vector3> ToWorld(List<Vector2> pts, RoadData startRoad, RoadData endRoad)
        {
            var w = new List<Vector3>(pts.Count * 2);
            for (int i = 0; i < pts.Count; i++)
            {
                RoadData segRoad = i <= 1 ? startRoad : (i == pts.Count - 1 ? endRoad : null);
                if (i > 0)
                {
                    var a = pts[i - 1]; var b = pts[i];
                    float dist = Vector2.Distance(a, b);
                    int steps = Mathf.FloorToInt(dist / 8f);
                    for (int s = 1; s <= steps; s++)
                    {
                        var p = Vector2.Lerp(a, b, (float)s / (steps + 1));
                        var sv = WorldData.W(p.x, 0f, p.y); sv.y = SegmentHeight(a, b, p, segRoad) + 0.16f; w.Add(sv);
                    }
                }
                float h = i > 0 ? SegmentHeight(pts[i - 1], pts[i], pts[i], segRoad) : (startRoad != null ? data.RoadHeightAt(startRoad, startRoad.Horizontal ? pts[i].x : pts[i].y) : data.DeckHeightAtData(pts[i].x, pts[i].y));
                var v = WorldData.W(pts[i].x, 0f, pts[i].y); v.y = h + 0.16f; w.Add(v);
            }
            return w;
        }

        List<Node> Dijkstra(List<(Node n, float d)> starts, Dictionary<Node, float> goals)
        {
            var dist = new Dictionary<Node, float>(); var prev = new Dictionary<Node, Node>(); var open = new List<Node>();
            foreach (var (n, d) in starts) { if (!dist.ContainsKey(n) || d < dist[n]) dist[n] = d; if (!open.Contains(n)) open.Add(n); }
            Node best = null; float bestD = float.MaxValue;
            while (open.Count > 0)
            {
                open.Sort((p, q) => dist[p].CompareTo(dist[q]));
                var u = open[0]; open.RemoveAt(0); float du = dist[u];
                if (goals.TryGetValue(u, out var g) && du + g < bestD) { bestD = du + g; best = u; }
                if (du > bestD) break;
                foreach (var (v, w) in u.edges)
                {
                    float nd = du + w;
                    if (!dist.TryGetValue(v, out var old) || nd < old) { dist[v] = nd; prev[v] = u; if (!open.Contains(v)) open.Add(v); }
                }
            }
            if (best == null) return null;
            var path = new List<Node>(); for (var n = best; n != null; n = prev.TryGetValue(n, out var p) ? p : null) path.Insert(0, n);
            return path;
        }

        public void Refresh(Vector3 from)
        {
            if (Target == null)
            {
                line.positionCount = 0;
                marker.gameObject.SetActive(false);
                foreach (var c in chevrons) if (c != null) c.gameObject.SetActive(false);
                RouteLength = 0f;
                return;
            }
            currentRoute = Route(from, Target.Value);
            line.positionCount = currentRoute.Count; line.SetPositions(currentRoute.ToArray());
            float len = 0f; for (int i = 1; i < currentRoute.Count; i++) len += Vector3.Distance(currentRoute[i], currentRoute[i - 1]);
            RouteLength = len;
            NextWaypoint = currentRoute.Count > 1 ? currentRoute[1] : Target.Value;
            marker.gameObject.SetActive(true);
            var m = Target.Value; m.y = data.DeckHeightAt(m) + 0.12f; marker.position = m;

            // Place the 6 approach chevrons along the road leading into the destination marker.
            Vector3 dest = m;
            Vector3 prev = currentRoute.Count > 1 ? currentRoute[Mathf.Max(0, currentRoute.Count - 2)] : from;
            Vector3 approachDir = (dest - prev); approachDir.y = 0f;
            if (approachDir.sqrMagnitude < 0.01f) approachDir = Vector3.forward;
            else approachDir.Normalize();
            Quaternion rot = Quaternion.LookRotation(approachDir, Vector3.up);
            for (int i = 0; i < chevrons.Count; i++)
            {
                float backDist = 3.2f + i * 2.1f;
                Vector3 cPos = dest - approachDir * backDist;
                cPos.y = data.DeckHeightAt(cPos) + 0.14f;
                chevrons[i].position = cPos;
                chevrons[i].rotation = rot;
                chevrons[i].gameObject.SetActive(true);
            }
        }

        public void Tick(Vector3 from)
        {
            refreshT -= Time.deltaTime;
            if (refreshT <= 0f) { refreshT = 0.5f; Refresh(from); }
            if (marker.gameObject.activeSelf)
            {
                if (beacon != null)
                {
                    beacon.Rotate(Vector3.up, Time.deltaTime * 55f, Space.Self);
                    beacon.localPosition = new Vector3(0f, 1.85f + Mathf.Sin(Time.time * 3.4f) * 0.18f, 0f);
                }
                for (int i = 0; i < chevronMats.Count; i++)
                {
                    float wave = Mathf.Sin(Time.time * 6.5f + i * 0.85f) * 0.5f + 0.5f;
                    var col = Color.Lerp(new Color(1.0f, 0.78f, 0.15f), new Color(1.25f, 1.20f, 0.95f), wave);
                    chevronMats[i].color = col;
                    if (chevronMats[i].HasProperty("_BaseColor")) chevronMats[i].SetColor("_BaseColor", col);
                }
            }
        }
    }
}
