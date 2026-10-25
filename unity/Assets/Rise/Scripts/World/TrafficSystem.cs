using System.Collections.Generic;
using UnityEngine;
using Rise.Data;
using Rise.Vehicles;

namespace Rise.World
{
    // Ambient traffic: danfos, kekes, koropes, okadas and cars follow the road grid in lanes (right-hand traffic),
    // keep their distance from the vehicle ahead and from the player, ride the bridge decks, obey the class speed and
    // the bridge rush-hour profile. Any vehicle can be taken by the player (TakeVehicle), which removes it from here.
    public class TrafficSystem : MonoBehaviour
    {
        public int Count = 210;
        public float BaseSpeed = 13f;   // m/s for a class-1.0 road

        public class Vehicle
        {
            public Transform t;
            public VehicleClassData cls;
            public RoadData road;
            public int dir, lane;
            public float along, speed, cruise;
            public Transform[] wheels;
            public bool HasDriver => t != null && t.Find("Occupants/Driver") != null && t.Find("Occupants/Driver").gameObject.activeSelf && t.Find("Occupants/Driver").childCount > 0;
            public int PassengerCount => t != null && t.Find("Occupants/Passengers") != null && t.Find("Occupants/Passengers").gameObject.activeSelf ? t.Find("Occupants/Passengers").childCount : 0;
        }

        class EjectedNpc { public Transform t; public Vector3 dir; public float ttl; }
        readonly List<EjectedNpc> ejected = new List<EjectedNpc>();
        public int EjectedCount => ejected.Count;

        WorldData data;
        DayNightCycle clock;
        Transform root;
        readonly List<Vehicle> vehicles = new List<Vehicle>();
        public IReadOnlyList<Vehicle> Vehicles => vehicles;
        [System.NonSerialized] public Transform PlayerObstacle;   // the player (on foot or driving); traffic brakes for it
        [System.NonSerialized] public TrafficLights Lights;
        [System.NonSerialized] public Events Events;
        [System.NonSerialized] public Weather Weather;
        static readonly string[] Mix = { "danfo", "danfo", "danfo", "keke", "keke", "korope", "korope", "car", "car", "car", "luxury", "luxury", "brt", "brt", "okada", "okada", "car", "danfo", "police", "police", "lawma", "lawma", "fire", "army", "tanker", "van", "pickup" };

        public void Init(WorldData world, DayNightCycle dayNight, Transform parent)
        {
            data = world; clock = dayNight;
            root = new GameObject("Traffic").transform; root.SetParent(parent, false);
            var rng = new System.Random(2026);
            var weights = new List<float>(); float total = 0f;
            foreach (var r in data.roads) { float w = Mathf.Max(0f, r.Length) * r.density; weights.Add(w); total += w; }
            for (int i = 0; i < Count; i++)
            {
                float pick = (float)rng.NextDouble() * total; int idx = 0;
                for (; idx < weights.Count - 1; idx++) { pick -= weights[idx]; if (pick <= 0f) break; }
                var road = data.roads[idx];
                var cls = data.VehicleClass(Mix[rng.Next(Mix.Length)]) ?? data.VehicleClass("car");
                if (cls == null) break;
                var v = new Vehicle { cls = cls, road = road, dir = rng.NextDouble() < 0.5 ? -1 : 1, lane = road.width >= 30f && rng.NextDouble() < 0.5 ? 1 : 0 };
                float minClear = cls.len + 5f;
                v.along = Mathf.Lerp(road.from + 6f, road.to - 6f, (float)rng.NextDouble());
                for (int attempt = 0; attempt < 16 && !IsLaneClear(v.road, v.dir, v.lane, v.along, minClear); attempt++)
                {
                    if (attempt >= 8)
                    {
                        v.road = data.roads[rng.Next(data.roads.Length)];
                        v.dir = rng.NextDouble() < 0.5 ? -1 : 1;
                        v.lane = v.road.width >= 30f && rng.NextDouble() < 0.5 ? 1 : 0;
                    }
                    v.along = Mathf.Lerp(v.road.from + 6f, v.road.to - 6f, (float)rng.NextDouble());
                }
                v.t = VehicleBodies.Build(cls, root, rng, out v.wheels).transform;
                v.cruise = Mathf.Min(cls.max, BaseSpeed * v.road.speed) * (0.85f + 0.3f * (float)rng.NextDouble());
                v.speed = v.cruise;
                vehicles.Add(v);
                Place(v);
            }
        }

        // Eject the AI driver and passengers onto the pavement beside the vehicle so they flee when hijacked.
        public void EjectOccupants(Vehicle v)
        {
            if (v == null || v.t == null || root == null) return;
            var occ = v.t.Find("Occupants");
            if (occ == null) return;
            var sources = new List<(Transform src, float sideSign)> ();
            var drv = occ.Find("Driver");
            if (drv != null && drv.childCount > 0) sources.Add((drv.GetChild(0), -1f));
            var pax = occ.Find("Passengers");
            if (pax != null)
            {
                for (int i = 0; i < Mathf.Min(2, pax.childCount); i++)
                    sources.Add((pax.GetChild(i), i % 2 == 0 ? 1f : -1f));
            }
            float halfW = (v.cls != null ? v.cls.wid * 0.5f : 0.9f) + 0.65f;
            for (int i = 0; i < sources.Count; i++)
            {
                var (src, side) = sources[i];
                var npc = Object.Instantiate(src.gameObject, root);
                npc.name = "EjectedOccupant";
                var away = (v.t.right * side + v.t.forward * (i == 0 ? -0.35f : 0.25f)).normalized;
                var pos = v.t.position + v.t.right * (side * halfW) - v.t.forward * (i * 0.6f);
                pos.y = (data != null ? data.DeckHeightAt(pos) : v.t.position.y) + 0.28f;
                npc.transform.position = pos;
                npc.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
                ejected.Add(new EjectedNpc { t = npc.transform, dir = away, ttl = 4.5f });
            }
        }

        // The player takes this vehicle: it leaves the traffic simulation and becomes a drivable VehicleController.
        public VehicleController TakeVehicle(Vehicle v)
        {
            if (v.t != null && !v.t.gameObject.activeSelf) v.t.gameObject.SetActive(true);
            EjectOccupants(v);
            vehicles.Remove(v);
            var vc = v.t.gameObject.AddComponent<VehicleController>();
            vc.Setup(v.cls, v.wheels);
            return vc;
        }

        // Leave the simulation (police pursuit) and come back to it afterwards.
        public void Detach(Vehicle v) => vehicles.Remove(v);
        public void Rejoin(Vehicle v) { if (!vehicles.Contains(v)) { vehicles.Add(v); v.speed = 0f; Place(v); } }

        public Vehicle Nearest(Vector3 pos, float maxDist)
        {
            Vehicle best = null; float bestD = maxDist;
            foreach (var v in vehicles)
            {
                if (v.t == null || !v.t.gameObject.activeSelf) continue;
                if (Mathf.Abs(v.t.position.y - pos.y) > 3.2f) continue;
                Vector3 toPos = pos - v.t.position;
                toPos.y = 0f;
                float halfLen = v.cls != null ? Mathf.Max(0f, v.cls.len * 0.5f - 1.1f) : 0f;
                float along = Mathf.Clamp(Vector3.Dot(toPos, v.t.forward), -halfLen, halfLen);
                Vector3 closest = v.t.position + v.t.forward * along;
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(closest.x, closest.z));
                if (d < bestD) { bestD = d; best = v; }
            }
            return best;
        }

        float RushFactor(RoadData r, float along)
        {
            if (clock == null) return 1f;
            float h = clock.Clock;
            bool bridge = data.OnBridge(r.axis, r.k, along);
            bool rush = (h >= 7f && h < 10f) || (h >= 16f && h < 19.5f);
            bool lateNight = h >= 21.5f || h < 5.5f;
            if (bridge) return rush ? 0.45f : lateNight ? 1.35f : 1f;
            if (lateNight) return 1.25f;
            return rush && (r.cls == "highway" || r.cls == "main") ? 0.7f : 1f;
        }

        bool ShouldOperateAtHour(Vehicle v, int idx, float h)
        {
            // Full traffic between 06:15 and 21:00
            if (h >= 6.25f && h < 21f) return true;
            string id = v.cls != null ? v.cls.id : "car";
            // Deep night (23:00–05:00): ~25% density; mostly night cars, luxury cruisers, police patrols, and occasional night transit
            if (h >= 23f || h < 5f)
            {
                if (id == "lawma" || id == "brt" || id == "korope") return idx % 10 == 0;
                if (id == "danfo" || id == "keke") return idx % 6 == 0;
                if (id == "police" || id == "luxury") return idx % 2 == 0;
                return idx % 4 == 0;
            }
            // Transition hours (21:00–23:00 and 05:00–06:15): ~50% density
            return idx % 2 == 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float h = clock != null ? clock.Clock : 12f;
            bool lateNight = h >= 21.5f || h < 5.5f;
            bool deepNight = h >= 23f || h < 5f;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v.t == null) continue;
                bool activeNow = ShouldOperateAtHour(v, i, h);
                if (v.t.gameObject.activeSelf != activeNow)
                {
                    // Don't pop a vehicle in/out right in front of the player camera
                    bool nearPlayer = PlayerObstacle != null && (v.t.position - PlayerObstacle.position).sqrMagnitude < 55f * 55f;
                    if (!nearPlayer)
                        v.t.gameObject.SetActive(activeNow);
                }
                if (!v.t.gameObject.activeSelf) continue;

                float target = v.cruise * RushFactor(v.road, v.along);
                if (Events != null && Events.InJam(v.road, v.along)) target = Mathf.Min(target, 1.6f);
                if (Weather != null && Weather.Raining) target *= 0.7f;
                float gap = float.MaxValue, aheadSpeed = 0f;
                bool crossTrafficNear = false;
                float laneAcross = v.road.k + LaneOffsetSign(v) * LaneOffset(v);
                for (int j = 0; j < vehicles.Count; j++)
                {
                    if (i == j) continue; var o = vehicles[j];
                    if (o.t == null || !o.t.gameObject.activeSelf) continue;
                    if (o.road == v.road)
                    {
                        if (o.dir != v.dir || o.lane != v.lane) continue;
                        float rawD = (o.along - v.along) * v.dir;
                        float d = rawD - (o.cls.len + v.cls.len) * 0.5f;
                        if (rawD > 0f || (rawD > -2.5f && i > j))
                        {
                            float effGap = Mathf.Max(0.1f, d);
                            if (effGap < gap) { gap = effGap; aheadSpeed = rawD > 0f ? o.speed : 0f; }
                        }
                    }
                    else if (Mathf.Abs(o.t.position.y - v.t.position.y) < 2.5f)
                    {
                        float oAlong = v.road.Along(o.t.position), oAcross = v.road.Across(o.t.position);
                        float d = (oAlong - v.along) * v.dir;
                        if (d > 0f && d < 20f && Mathf.Abs(oAcross - laneAcross) < 14f) crossTrafficNear = true;
                        float crossHalfLen = (o.cls != null ? o.cls.len * 0.5f : 2.2f) + 0.9f;
                        // Only yield to cross-traffic that is actively inside or closer to the junction box so perpendicular lanes never deadlock
                        float myDistToJunc = Mathf.Abs(o.road.k - v.along);
                        float otherDistToJunc = Mathf.Abs(v.road.k - o.along);
                        if (d > 0f && d < 10.5f && Mathf.Abs(oAcross - laneAcross) < crossHalfLen)
                        {
                            if (otherDistToJunc + 0.5f < myDistToJunc || (Mathf.Abs(otherDistToJunc - myDistToJunc) <= 0.5f && i > j))
                            {
                                gap = Mathf.Min(gap, Mathf.Max(0.5f, d - (o.cls != null ? o.cls.wid * 0.5f : 1f)));
                                aheadSpeed = 0f;
                            }
                        }
                    }
                }
                // The player (on foot or driving) ahead or beside our lane: stop for them.
                if (PlayerObstacle != null)
                {
                    float pAlong = v.road.Along(PlayerObstacle.position), pAcross = v.road.Across(PlayerObstacle.position);
                    float d = (pAlong - v.along) * v.dir - v.cls.len * 0.5f;
                    if (d > -3.5f && d < 16f && Mathf.Abs(pAcross - laneAcross) < 3.8f && Mathf.Abs(PlayerObstacle.position.y - v.t.position.y) < 3f) { gap = Mathf.Min(gap, Mathf.Max(0.5f, d)); aheadSpeed = 0f; }
                }
                // Red or amber light at the next junction ahead: stop short of it unless already inside (or deep night with clear cross-road).
                if (Lights != null && !(deepNight && !crossTrafficNear))
                {
                    string light = Lights.LightFor(v.road.axis);
                    if (light != "green")
                        foreach (var o in data.roads)
                        {
                            if (o.axis == v.road.axis) continue;
                            if (v.road.k < o.from || v.road.k > o.to || o.k < v.road.from || o.k > v.road.to) continue;
                            bool hasLight = v.road.Horizontal ? Lights.HasLight(o.k, v.road.k) : Lights.HasLight(v.road.k, o.k);
                            if (!hasLight) continue;
                            float d = (o.k - v.along) * v.dir - o.width * 0.5f;   // distance to the junction edge
                            if (d > 0.5f && d < 9f) { gap = Mathf.Min(gap, d + v.cls.len * 0.5f + 4.5f - 1f); aheadSpeed = 0f; }
                        }
                }
                float minGap = v.cls.len * 0.5f + 4.5f;
                if (gap < minGap) target = 0f;
                else if (gap < minGap + 10f) target = Mathf.Min(target, aheadSpeed * 0.95f);
                v.speed = Mathf.MoveTowards(v.speed, target, (target < v.speed ? 12f : v.cls.accel * 0.6f) * dt);
                v.along += v.dir * v.speed * dt;
                if (v.dir > 0 && v.along > v.road.to - 4f) v.along = v.road.from + 4f;
                if (v.dir < 0 && v.along < v.road.from + 4f) v.along = v.road.to - 4f;
                // Only funnel distant vehicles toward the player during daytime hours; at night let traffic stay sparse across the city
                if (!lateNight && PlayerObstacle != null && (i + Time.frameCount) % 60 == 0)
                {
                    var pPos = PlayerObstacle.position;
                    float distSq = (v.t.position - pPos).sqrMagnitude;
                    if (distSq > 450f * 450f)
                        TryRelocateNearPlayer(v, pPos, i);
                }
                if (v.wheels != null) foreach (var w in v.wheels) VehicleBodies.Spin(w, v.speed * dt * 120f);
                Place(v);
            }
            for (int i = ejected.Count - 1; i >= 0; i--)
            {
                var e = ejected[i];
                e.ttl -= dt;
                if (e.ttl <= 0f || e.t == null)
                {
                    if (e.t != null) Object.Destroy(e.t.gameObject);
                    ejected.RemoveAt(i);
                    continue;
                }
                var p = e.t.position + e.dir * (4.2f * dt);
                p.y = (data != null ? data.DeckHeightAt(p) : 0f) + 0.28f + Mathf.Abs(Mathf.Sin(e.ttl * 12f)) * 0.06f;
                e.t.position = p;
            }
        }

        // Lane 0 sits a little inside the quarter line so kerb-parked vehicles (at half width minus 2.2 m) are clear of it.
        float LaneOffset(Vehicle v) => v.road.width * (0.22f + 0.17f * v.lane);
        // Right-hand traffic: the lane is on the right of the direction of travel. In data units the sign of
        // "right" depends on the axis and direction; derive it from the world right vector.
        float LaneOffsetSign(Vehicle v)
        {
            var right = Vector3.Cross(Vector3.up, v.road.Forward(v.dir));
            return v.road.Horizontal ? -right.z : right.x;   // data across for h roads is -world z; for v roads it is world x
        }

        void Place(Vehicle v)
        {
            var r = v.road;
            var fwd = r.Forward(v.dir);
            var right = Vector3.Cross(Vector3.up, fwd);
            var p = r.Point(v.along, r.k) + right * LaneOffset(v);
            float y0 = data.RoadHeightAt(r, v.along);
            float y1 = data.RoadHeightAt(r, v.along + v.dir * 2f);
            p.y = y0 + 0.11f;
            v.t.position = p;
            var slopeFwd = (fwd * 2f + Vector3.up * (y1 - y0)).normalized;
            v.t.rotation = Quaternion.LookRotation(slopeFwd, Vector3.up);
        }

        bool IsLaneClear(RoadData r, int dir, int lane, float along, float minClearance, Vehicle ignore = null)
        {
            for (int i = 0; i < vehicles.Count; i++)
            {
                var o = vehicles[i];
                if (o == ignore || o.road != r || o.dir != dir || o.lane != lane) continue;
                float need = Mathf.Max(minClearance, (o.cls != null ? o.cls.len : 5f) * 0.5f + minClearance * 0.5f + 3f);
                if (Mathf.Abs(o.along - along) < need) return false;
            }
            return true;
        }

        void TryRelocateNearPlayer(Vehicle v, Vector3 pPos, int seed)
        {
            if (data == null || data.roads.Length == 0) return;
            int n = data.roads.Length;
            int start = (seed * 17 + Time.frameCount) % n;
            for (int a = 0; a < 12; a++)
            {
                var r = data.roads[(start + a * 7) % n];
                if (r.Length < 28f) continue;
                float pAlong = Mathf.Clamp(r.Along(pPos), r.from + 8f, r.to - 8f);
                float candidateAlong = Mathf.Clamp(pAlong + ((seed + a) % 2 == 0 ? 110f : -110f), r.from + 8f, r.to - 8f);
                int candidateLane = r.width >= 30f && (seed % 2 == 1) ? 1 : 0;
                if (!IsLaneClear(r, v.dir, candidateLane, candidateAlong, (v.cls != null ? v.cls.len : 5f) + 6f, v)) continue;
                var pt = r.Point(candidateAlong, r.k);
                float dSq = (pt - pPos).sqrMagnitude;
                if (dSq < 65f * 65f || dSq > 280f * 280f) continue;
                v.road = r;
                v.along = candidateAlong;
                v.lane = candidateLane;
                v.cruise = Mathf.Min(v.cls.max, BaseSpeed * r.speed);
                v.speed = v.cruise * 0.8f;
                return;
            }
        }
    }
}
