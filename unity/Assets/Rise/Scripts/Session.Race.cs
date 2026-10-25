using System.Collections.Generic;
using UnityEngine;
using Rise.Data;
using Rise.Vehicles;
using Rise.World;

namespace Rise
{
    // Need for Speed (NFS Underground / Most Wanted / Unbound) style street racing missions:
    // Circuit, Highway Sprint, Neon Drift Touge, Radar Speedtrap Blitz, Elimination Knockout, and Blacklist Most Wanted
    // Heat Runs with NOS Nitrous Oxide flames, drift combo scoring, near-miss bonuses, and multi-rival AI racers.
    public partial class Session
    {
        public class NfsRaceEvent
        {
            public string Id, Title, Mode, Subtitle, BossName;
            public (float x, float z)[] Route;
            public int Wager, Prize, Xp, Heat;
            public float BaseRivalSpeed = 20f;
            public int TargetDriftScore;
        }

        public static readonly NfsRaceEvent[] NfsRaceEvents =
        {
            new NfsRaceEvent
            {
                Id = "nfs_circuit_funsho",
                Title = "SURULERE MIDNIGHT CIRCUIT",
                Mode = "circuit",
                Subtitle = "2-Lap Street Circuit · Funsho Williams Ave · 3 Rivals",
                BossName = "Razor Musa",
                Route = new[] { (72f, -40f), (72f, 120f), (72f, -40f), (72f, 120f) },
                Wager = 10000, Prize = 45000, Xp = 35, Heat = 0, BaseRivalSpeed = 19.5f
            },
            new NfsRaceEvent
            {
                Id = "nfs_sprint_third_mainland",
                Title = "THIRD MAINLAND SUPERSONIC SPRINT",
                Mode = "sprint",
                Subtitle = "Point-to-Point Bridge Highway Sprint · vs Blacklist #3",
                BossName = "Nitrous Dapo",
                Route = new[] { (72f, 95f), (210f, 95f), (350f, 20f), (380f, -120f), (380f, -230f) },
                Wager = 15000, Prize = 75000, Xp = 45, Heat = 1, BaseRivalSpeed = 22.0f
            },
            new NfsRaceEvent
            {
                Id = "nfs_drift_vi",
                Title = "VICTORIA ISLAND NEON DRIFT TOUGE",
                Mode = "drift",
                Subtitle = "Drift Score Challenge · Target 1,200+ Drift Pts · VI & Oniru",
                BossName = "Drift King Tobi",
                Route = new[] { (380f, 185f), (455f, 210f), (510f, 275f), (435f, 305f), (370f, 240f) },
                Wager = 12000, Prize = 65000, Xp = 40, Heat = 0, BaseRivalSpeed = 18.5f, TargetDriftScore = 1200
            },
            new NfsRaceEvent
            {
                Id = "nfs_speedtrap_lekki",
                Title = "LEKKI-EPE AUTOBAHN SPEEDTRAP BLITZ",
                Mode = "speedtrap",
                Subtitle = "4 Radar Speed Cameras · Highest Cumulative Speed Wins",
                BossName = "Apex Kazeem",
                Route = new[] { (490f, 250f), (680f, 250f), (880f, 250f), (1100f, 250f) },
                Wager = 15000, Prize = 85000, Xp = 50, Heat = 1, BaseRivalSpeed = 23.0f
            },
            new NfsRaceEvent
            {
                Id = "nfs_knockout_apapa",
                Title = "APAPA EXPRESSWAY LAP KNOCKOUT",
                Mode = "knockout",
                Subtitle = "4 Racers Start · Last Place Eliminated Every Sector",
                BossName = "Viper Segun",
                Route = new[] { (-60f, 95f), (-180f, 95f), (-310f, 95f), (-180f, 95f) },
                Wager = 18000, Prize = 95000, Xp = 55, Heat = 1, BaseRivalSpeed = 20.5f
            },
            new NfsRaceEvent
            {
                Id = "nfs_most_wanted_eko",
                Title = "BLACKLIST #1: EKO MOST WANTED HEAT RUN",
                Mode = "mostwanted",
                Subtitle = "Cross-City Boss Duel vs Baron Chidi + Level 4 Police Pursuit",
                BossName = "Baron Chidi (#1)",
                Route = new[] { (72f, -120f), (72f, 95f), (240f, 95f), (390f, 180f), (480f, 250f) },
                Wager = 25000, Prize = 150000, Xp = 75, Heat = 3, BaseRivalSpeed = 23.5f
            },
        };

        public class RivalRacer
        {
            public string Name;
            public Transform Root;
            public int CpIndex;
            public float Speed;
            public bool Eliminated;
            public TextMesh TagMesh;
        }

        public class Race
        {
            public NfsRaceEvent Event;
            public string Mode = "circuit";
            public string Title = "SURULERE STREET RACE";
            public Vector3[] Cps;
            public int I, RivalI;
            public float T;
            public Transform Rival;
            public float RivalSpeed;
            public int Wager, Prize;
            public bool Mission, Finished, Won;
            public readonly List<RivalRacer> Rivals = new List<RivalRacer>();

            // NFS Telemetry: Drift, Speedtrap, Position
            public float DriftScore;
            public float CurrentDriftCombo;
            public int DriftMultiplier = 1;
            public float DriftChainTimer;
            public float TotalSpeedtrapKmh;
            public float RivalSpeedtrapKmh;
            public float LastSpeedtrapKmh;
            public int PlayerPosition = 1;
            public string StyleBanner = "";
            public float StyleBannerTimer;
        }

        [System.NonSerialized] public Race ActiveRace;
        [System.NonSerialized] public float NosCharge = 100f;      // 0..100% Nitrous Oxide tank
        [System.NonSerialized] public bool NosActive;              // true when firing NOS
        Transform ring, ringChevron;
        Transform nosFlameRoot;
        float nearMissCooldown;

        static readonly (float x, float z)[] Lap = { (72f, -40f), (72f, 120f), (72f, -40f), (72f, 120f) };

        public bool Racing => ActiveRace != null && !ActiveRace.Finished;

        public string RaceStatus
        {
            get
            {
                if (!Racing) return null;
                var r = ActiveRace;
                string posTag = r.PlayerPosition == 1 ? "1ST" : r.PlayerPosition == 2 ? "2ND" : r.PlayerPosition == 3 ? "3RD" : "4TH";
                if (r.Mode == "drift")
                    return $"{r.Title} · {posTag} · CP {r.I + 1}/{r.Cps.Length} · DRIFT {r.DriftScore:0}/{Mathf.Max(1200, r.Event?.TargetDriftScore ?? 1200)} PTS";
                if (r.Mode == "speedtrap")
                    return $"{r.Title} · RADAR {r.I + 1}/{r.Cps.Length} · TOTAL SPEED {r.TotalSpeedtrapKmh:0} km/h (RIVAL {r.RivalSpeedtrapKmh:0} km/h)";
                return $"Race · {posTag} · checkpoint {r.I + 1}/{r.Cps.Length} · rival at {r.RivalI + 1}/{r.Cps.Length}";
            }
        }

        public static NfsRaceEvent FindNfsEvent(string id)
        {
            foreach (var e in NfsRaceEvents) if (e.Id == id) return e;
            return NfsRaceEvents[0];
        }

        public void OpenRaceMenu(ContactSystem.Contact speedy = null)
        {
            string title = speedy != null ? $"{speedy.data.name} · NFS Underground Lagos" : "NFS Street Racing Underground";
            string sub = speedy != null ? speedy.data.role : "Circuit · Sprint · Drift Touge · Speedtrap · Lap Knockout · Blacklist #1";
            string line = speedy != null ? speedy.data.line : "Fill your NOS tank by drifting and shaving past traffic, then hit [SHIFT] for Twin-Flame Nitrous boost!";
            var menu = new Menu { Title = title, Subtitle = sub, Line = line };

            if (Racing)
            {
                menu.Options.Add(new MenuOption { Label = $"Resume Active Race ({ActiveRace.Title})", Action = () => ActiveMenu = null });
                menu.Options.Add(new MenuOption { Label = "Abandon Current Race", Action = () => EndRace(false) });
            }
            else
            {
                foreach (var ev in NfsRaceEvents)
                {
                    var captured = ev;
                    string modeBadge = ev.Mode.ToUpperInvariant();
                    menu.Options.Add(new MenuOption
                    {
                        Label = $"[{modeBadge}] {ev.Title} · Wager ₦{ev.Wager:N0} → Prize ₦{ev.Prize:N0}",
                        Action = () => StartNfsRace(captured.Id)
                    });
                }
                // Classic Funsho Williams 2-lap quick race option
                menu.Options.Add(new MenuOption
                {
                    Label = $"Classic Quick Race · Funsho Williams · wager ₦{Data.raceWager:N0}",
                    Enabled = Parked.Exists(v => v.Owned) || Vehicle != null,
                    Action = () => StartRace(Data.raceWager, false)
                });
            }

            menu.Options.Add(new MenuOption
            {
                Label = "NFS Underground Tuning & Mod Garage (Performance · NOS · Aero · Paint)",
                Action = () => OpenNfsGarageMenu(Vehicle)
            });
            menu.Options.Add(new MenuOption { Label = "Not now", Action = null });
            OpenMenu(menu);
        }

        public bool StartNfsRace(string eventId, bool freeEntry = false)
        {
            var ev = FindNfsEvent(eventId);
            if (ev == null) return false;
            if (Racing) EndRace(false);
            if (InFootballMatch) EndLiveFootballMatch(false, true);
            if (InInterior) LeaveInterior();

            int wager = freeEntry ? 0 : ev.Wager;
            if (wager > 0 && !State.Pay(wager))
            {
                State.Toast($"You need ₦{wager:N0} entry wager for {ev.Title}");
                return false;
            }

            // Ensure the player is in a vehicle at the starting grid
            EnsureRaceVehicleAtStart(ev.Route[0], ev.Route.Length > 1 ? ev.Route[1] : (ev.Route[0].x, ev.Route[0].z + 50f));

            if (!StartRaceInternal(ev.Route, wager, ev.Prize, false, ev.BaseRivalSpeed, ev))
                return false;

            if (ev.Heat > 0)
                State.AddHeat(ev.Heat, $"NFS Street Race ({ev.Title})");

            ActiveMenu = null;
            PhoneOpen = false;
            State.Toast($"NFS {ev.Mode.ToUpperInvariant()} · {ev.Title} vs {ev.BossName}! Hit [SHIFT] for NOS Nitrous!");
            return true;
        }

        void EnsureRaceVehicleAtStart((float x, float z) firstCp, (float x, float z) secondCp)
        {
            Vector3 cp0 = WorldData.W(firstCp.x, 0f, firstCp.z);
            cp0.y = Data.DeckHeightAt(cp0);
            Vector3 cp1 = WorldData.W(secondCp.x, 0f, secondCp.z);
            Vector3 forward = Flat(cp1) - Flat(cp0);
            if (forward.sqrMagnitude < 1f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 startPos = cp0 - forward * 16f;
            startPos.y = Data.DeckHeightAt(startPos) + 0.12f;
            Quaternion startRot = Quaternion.LookRotation(forward, Vector3.up);

            if (Vehicle == null)
            {
                // Pick player's fastest owned vehicle or spawn a tuned loaner sports car
                VehicleController best = null;
                foreach (var v in Parked)
                {
                    if (v != null && v.Owned && (best == null || v.Class.max > best.Class.max))
                        best = v;
                }
                if (best == null && Parked.Count > 0) best = Parked[0];
                if (best == null)
                {
                    var cls = Data.VehicleClass("luxury") ?? Data.VehicleClass("car");
                    var go = VehicleBodies.Build(cls, transform, new System.Random(777), out var wheels);
                    best = go.AddComponent<VehicleController>();
                    best.Setup(cls, wheels);
                    best.NosStage = 2;
                    best.Spoiler = 2;
                    best.SkirtKit = 2;
                    best.NeonColor = 0;
                    best.ApplyLivery("#00d8ff", 2);
                    best.RebuildNfsVisualMods();
                    Parked.Add(best);
                }
                Enter(best);
            }

            if (!Vehicle.HasNos)
            {
                Vehicle.NosStage = 1;
                Vehicle.RebuildNfsVisualMods();
            }
            Vehicle.Condition = 100f;
            State.S.fuel = 100f;
            NosCharge = 100f;
            Vehicle.Place(startPos, startRot);
        }

        public bool StartRace(int wager, bool mission, float rivalSpeed = 19f)
        {
            if (Racing) return false;
            if (wager > 0 && !State.Pay(wager)) { State.Toast($"You need ₦{wager:N0} for the wager"); return false; }
            return StartRaceInternal(Lap, wager, wager * 2, mission, rivalSpeed, NfsRaceEvents[0]);
        }

        bool StartRaceInternal((float x, float z)[] coords, int wager, int prize, bool mission, float rivalSpeed, NfsRaceEvent ev)
        {
            var cps = new Vector3[coords.Length];
            for (int i = 0; i < coords.Length; i++)
            {
                cps[i] = WorldData.W(coords[i].x, 0f, coords[i].z);
                cps[i].y = Data.DeckHeightAt(cps[i]);
            }

            Vector3 startDir = cps.Length > 1 ? (Flat(cps[1]) - Flat(cps[0])).normalized : Vector3.back;
            if (startDir.sqrMagnitude < 0.01f) startDir = Vector3.back;
            Vector3 gridOrigin = cps[0] - startDir * 12f;
            gridOrigin.y = Data.DeckHeightAt(gridOrigin) + 0.11f;

            var race = new Race
            {
                Event = ev,
                Mode = ev != null ? ev.Mode : "circuit",
                Title = ev != null ? ev.Title : "SURULERE STREET CIRCUIT",
                Cps = cps,
                RivalSpeed = rivalSpeed,
                Wager = wager,
                Prize = prize > 0 ? prize : wager * 2,
                Mission = mission
            };

            // Spawn 3 NFS AI Rival Street Racers with neon liveries and floating position tags
            string[] rivalNames = { ev != null && !string.IsNullOrEmpty(ev.BossName) ? ev.BossName : "Razor Musa", "Nitrous Dapo", "Tobi Apex" };
            string[] rivalColors = { "#ff1e56", "#00e5ff", "#ffbe0b" };
            Vector3 right = Vector3.Cross(Vector3.up, startDir).normalized;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            for (int rIdx = 0; rIdx < 3; rIdx++)
            {
                var cls = Data.VehicleClass(rIdx == 0 ? "muscle" : "car") ?? Data.VehicleClass("car");
                var rivalRoot = VehicleBodies.Build(cls, transform, new System.Random(99 + rIdx * 17), out _).transform;
                rivalRoot.name = $"NfsRival_{rivalNames[rIdx]}";
                Color col = Entities.Mannequin.Hex(rivalColors[rIdx]);
                foreach (var mr in rivalRoot.GetComponentsInChildren<MeshRenderer>())
                {
                    if (mr.name == "Cube" || mr.name.StartsWith("Cube") || mr.name.Contains("body"))
                    {
                        mr.sharedMaterial = VehicleBodies.Mat(rivalColors[rIdx], 0.85f);
                    }
                }

                // Neon Underglow strip beneath each rival tuner car
                var glow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                glow.name = "NeonUnderglow";
                glow.transform.SetParent(rivalRoot, false);
                glow.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                glow.transform.localScale = new Vector3(1.75f, 0.04f, 3.6f);
                glow.GetComponent<MeshRenderer>().sharedMaterial = VehicleBodies.Mat(rivalColors[rIdx], 0.92f, 0.5f);
                Destroy(glow.GetComponent<Collider>());

                // Floating NFS Racer Nameplate
                var tagGo = new GameObject("RivalTag");
                tagGo.transform.SetParent(rivalRoot, false);
                tagGo.transform.localPosition = new Vector3(0f, 2.35f, 0f);
                var tm = tagGo.AddComponent<TextMesh>();
                tm.font = font;
                tm.fontSize = 36;
                tm.characterSize = 0.06f;
                tm.fontStyle = FontStyle.Bold;
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.color = col;
                tm.text = $"▼ {rivalNames[rIdx]}";
                if (font != null) tagGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

                Vector3 rPos = gridOrigin + right * ((rIdx - 1) * 3.2f) - startDir * (rIdx * 3.5f);
                rPos.y = Data.DeckHeightAt(rPos) + 0.11f;
                rivalRoot.position = rPos;
                rivalRoot.rotation = Quaternion.LookRotation(startDir, Vector3.up);

                var rr = new RivalRacer
                {
                    Name = rivalNames[rIdx],
                    Root = rivalRoot,
                    CpIndex = 0,
                    Speed = rivalSpeed * (1f - rIdx * 0.04f),
                    TagMesh = tm
                };
                race.Rivals.Add(rr);
                if (rIdx == 0) race.Rival = rivalRoot;
            }

            // Neon Checkpoint Gate & Directional Chevron
            if (ring == null)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                g.name = "Race ring";
                g.transform.SetParent(transform, false);
                g.transform.localScale = new Vector3(7.2f, 0.16f, 7.2f);
                var m = new Material(ShaderLib.Unlit) { color = new Color(0.0f, 0.92f, 1.0f, 0.9f) };
                g.GetComponent<MeshRenderer>().sharedMaterial = m;
                Destroy(g.GetComponent<Collider>());
                ring = g.transform;

                var chev = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chev.name = "GatePillarL";
                chev.transform.SetParent(ring, false);
                chev.transform.localPosition = new Vector3(-0.48f, 12f, 0f);
                chev.transform.localScale = new Vector3(0.06f, 24f, 0.06f);
                chev.GetComponent<MeshRenderer>().sharedMaterial = new Material(ShaderLib.Unlit) { color = new Color(1f, 0.25f, 0.65f, 0.95f) };
                Destroy(chev.GetComponent<Collider>());

                var chevR = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chevR.name = "GatePillarR";
                chevR.transform.SetParent(ring, false);
                chevR.transform.localPosition = new Vector3(0.48f, 12f, 0f);
                chevR.transform.localScale = new Vector3(0.06f, 24f, 0.06f);
                chevR.GetComponent<MeshRenderer>().sharedMaterial = chev.GetComponent<MeshRenderer>().sharedMaterial;
                Destroy(chevR.GetComponent<Collider>());
            }

            ring.gameObject.SetActive(true);
            ring.position = cps[0] + Vector3.up * 0.4f;
            NosCharge = 100f;
            ActiveRace = race;
            State.Toast($"NFS {race.Mode.ToUpperInvariant()} · {race.Title} — Hit the neon gates!");
            RefreshNav();
            return true;
        }

        void EndRace(bool won)
        {
            var r = ActiveRace;
            if (r == null) return;
            r.Finished = true;
            r.Won = won;
            if (r.Rival != null) Destroy(r.Rival.gameObject);
            foreach (var rv in r.Rivals)
                if (rv.Root != null) Destroy(rv.Root.gameObject);
            r.Rivals.Clear();

            if (ring != null) ring.gameObject.SetActive(false);
            SetNosFlamesVisible(false);
            NosActive = false;

            if (won)
            {
                int xp = r.Event != null ? r.Event.Xp : 25;
                State.GainSkill("driving", 6);
                State.AddRep("street", 6);
                State.AddXp(xp);
                int payout = r.Prize > 0 ? r.Prize : r.Wager * 2;
                if (payout > 0) State.Earn(payout);
                UI.SoundSystem.I?.PlayCash();
                State.Toast($"RACE WON! 1ST PLACE in {r.Title}{(payout > 0 ? $" · +₦{payout:N0}" : "")}");
            }
            else
            {
                State.Toast($"{r.Event?.BossName ?? "Speedy's crew"} took the checkered flag");
            }

            if (Random.value < 0.3f) State.AddHeat(1, "LASTMA clocked the street race");
            if (r.Mission && HasTask && State.S.taskType == "race") FinishTask(won);
            ActiveRace = null;
            RefreshNav();
        }

        void SetNosFlamesVisible(bool show)
        {
            if (Vehicle == null)
            {
                if (nosFlameRoot != null) nosFlameRoot.gameObject.SetActive(false);
                return;
            }
            if (!show)
            {
                if (nosFlameRoot != null) nosFlameRoot.gameObject.SetActive(false);
                return;
            }
            if (nosFlameRoot == null || nosFlameRoot.parent != Vehicle.transform)
            {
                if (nosFlameRoot != null) Destroy(nosFlameRoot.gameObject);
                var root = new GameObject("NosExhaustFlames").transform;
                root.SetParent(Vehicle.transform, false);
                float backZ = -Vehicle.Class.len * 0.52f;
                root.localPosition = new Vector3(0f, 0.38f, backZ);

                var cyanMat = new Material(ShaderLib.Unlit) { color = new Color(0.05f, 0.88f, 1.0f, 0.95f) };
                var coreMat = new Material(ShaderLib.Unlit) { color = new Color(0.85f, 0.98f, 1.0f, 0.98f) };

                for (int side = -1; side <= 1; side += 2)
                {
                    var outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    outer.name = "NosFlameOuter";
                    outer.transform.SetParent(root, false);
                    outer.transform.localPosition = new Vector3(side * 0.42f, 0f, -0.45f);
                    outer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    outer.transform.localScale = new Vector3(0.18f, 0.48f, 0.18f);
                    outer.GetComponent<MeshRenderer>().sharedMaterial = cyanMat;
                    Destroy(outer.GetComponent<Collider>());

                    var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    inner.name = "NosFlameCore";
                    inner.transform.SetParent(root, false);
                    inner.transform.localPosition = new Vector3(side * 0.42f, 0f, -0.32f);
                    inner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    inner.transform.localScale = new Vector3(0.10f, 0.32f, 0.10f);
                    inner.GetComponent<MeshRenderer>().sharedMaterial = coreMat;
                    Destroy(inner.GetComponent<Collider>());
                }
                nosFlameRoot = root;
            }
            nosFlameRoot.gameObject.SetActive(true);
            float flicker = 0.85f + 0.3f * Mathf.Sin(Time.time * 48f);
            nosFlameRoot.localScale = new Vector3(1f, 1f, flicker);
        }

        void UpdateNosAndDrift(float dt)
        {
            if (Vehicle == null)
            {
                NosActive = false;
                SetNosFlamesVisible(false);
                return;
            }

            if (nearMissCooldown > 0f) nearMissCooldown -= dt;
            float speedMs = Mathf.Abs(Vehicle.Speed);
            float kmh = Vehicle.Kmh;
            float steer = Mathf.Abs(GameInput.Steer);
            bool drifting = (GameInput.Handbrake || steer > 0.65f) && speedMs > 6.5f;
            bool hasNosSystem = Vehicle.HasNos;
            bool hasFuel = State.S.fuel > 0.1f;

            // 1. Drift Scoring & NOS Recharge (NOS only recharges on premium racing / custom-built NOS cars)
            if (drifting)
            {
                if (hasNosSystem) NosCharge = Mathf.Min(100f, NosCharge + 18f * dt);
                if (ActiveRace != null)
                {
                    ActiveRace.DriftChainTimer = 2.0f;
                    float pts = speedMs * (GameInput.Handbrake ? 22f : 12f) * ActiveRace.DriftMultiplier * dt;
                    ActiveRace.CurrentDriftCombo += pts;
                    ActiveRace.DriftScore += pts;
                    if (ActiveRace.CurrentDriftCombo > 250f * ActiveRace.DriftMultiplier)
                        ActiveRace.DriftMultiplier = Mathf.Min(5, ActiveRace.DriftMultiplier + 1);
                }
            }
            else if (ActiveRace != null)
            {
                if (ActiveRace.DriftChainTimer > 0f)
                {
                    ActiveRace.DriftChainTimer -= dt;
                    if (ActiveRace.DriftChainTimer <= 0f)
                    {
                        ActiveRace.CurrentDriftCombo = 0f;
                        ActiveRace.DriftMultiplier = 1;
                    }
                }
            }

            if (!hasNosSystem || !hasFuel)
            {
                if (!hasNosSystem) NosCharge = 0f;
                NosActive = false;
                SetNosFlamesVisible(false);
                return;
            }

            // 2. High-speed clean driving & Traffic Near-Miss NOS bonus
            int nosStage = Vehicle.EffectiveNosStage;
            if (kmh > 65f) NosCharge = Mathf.Min(100f, NosCharge + (4.0f + nosStage * 1.2f) * dt);
            if (kmh > 50f && nearMissCooldown <= 0f && Traffic != null)
            {
                var nearTraffic = Traffic.Nearest(Vehicle.transform.position, 4.2f);
                if (nearTraffic != null)
                {
                    nearMissCooldown = 1.6f;
                    NosCharge = Mathf.Min(100f, NosCharge + 15f);
                    if (ActiveRace != null)
                    {
                        ActiveRace.DriftScore += 150f;
                        ActiveRace.StyleBanner = "NEAR MISS! +15% NOS · +150 PTS";
                        ActiveRace.StyleBannerTimer = 1.6f;
                    }
                }
            }

            // 3. NOS Nitrous Activation on Boost [Shift] (requires fuel & NOS system)
            bool wantsNos = GameInput.Sprint && GameInput.Gas > 0.05f && !GameInput.Handbrake;
            if (wantsNos && NosCharge > 1f)
            {
                NosActive = true;
                float drainRate = Mathf.Max(13f, 23f - nosStage * 3f);
                NosCharge = Mathf.Max(0f, NosCharge - drainRate * dt);
                float nosCap = Vehicle.Class.boost * (1.20f + nosStage * 0.08f) * (1f + Vehicle.EngineStage * 0.10f);
                if (Vehicle.Speed < nosCap)
                    Vehicle.Speed = Mathf.MoveTowards(Vehicle.Speed, nosCap, Vehicle.Class.accel * (0.78f + nosStage * 0.14f) * dt);
                SetNosFlamesVisible(true);
            }
            else
            {
                NosActive = false;
                SetNosFlamesVisible(false);
            }
        }

        public void ForceRaceUpdate(float dt) => UpdateRace(dt);

        void UpdateRace(float dt)
        {
            UpdateNosAndDrift(dt);

            var r = ActiveRace;
            if (r == null || r.Finished) return;
            r.T += dt;
            if (r.StyleBannerTimer > 0f) r.StyleBannerTimer = Mathf.Max(0f, r.StyleBannerTimer - dt);

            var cp = r.Cps[r.I];
            if (Vehicle != null && Vector3.Distance(Flat(Vehicle.transform.position), Flat(cp)) < 6.5f)
            {
                // Speedtrap Radar Camera flash
                if (r.Mode == "speedtrap")
                {
                    float trapSpeed = Mathf.Max(85f, Vehicle.Kmh);
                    r.LastSpeedtrapKmh = trapSpeed;
                    r.TotalSpeedtrapKmh += trapSpeed;
                    r.RivalSpeedtrapKmh += Random.Range(92f, 118f);
                    r.StyleBanner = $"SPEEDTRAP CAMERA #{r.I + 1}: {trapSpeed:0} KM/H!";
                    r.StyleBannerTimer = 2.0f;
                }

                // Knockout elimination at each checkpoint
                if (r.Mode == "knockout" && r.Rivals.Count > 0)
                {
                    for (int k = r.Rivals.Count - 1; k >= 1; k--)
                    {
                        if (!r.Rivals[k].Eliminated)
                        {
                            r.Rivals[k].Eliminated = true;
                            if (r.Rivals[k].Root != null) r.Rivals[k].Root.gameObject.SetActive(false);
                            r.StyleBanner = $"KNOCKOUT! {r.Rivals[k].Name.ToUpperInvariant()} ELIMINATED!";
                            r.StyleBannerTimer = 2.0f;
                            break;
                        }
                    }
                }

                r.I++;
                if (r.I >= r.Cps.Length)
                {
                    bool won = true;
                    if (r.Mode == "speedtrap" && r.TotalSpeedtrapKmh < r.RivalSpeedtrapKmh && r.T > 2f)
                        won = false;
                    EndRace(won);
                    return;
                }
                ring.position = r.Cps[r.I] + Vector3.up * 0.4f;
                State.Toast($"Checkpoint {r.I}/{r.Cps.Length}");
            }

            // Update AI Rivals
            int aheadCount = 0;
            float playerDistToNext = Vehicle != null ? Vector3.Distance(Flat(Vehicle.transform.position), Flat(r.Cps[Mathf.Clamp(r.I, 0, r.Cps.Length - 1)])) : 999f;
            var cam = Camera != null ? Camera.transform : null;

            for (int idx = 0; idx < r.Rivals.Count; idx++)
            {
                var rv = r.Rivals[idx];
                if (rv.Eliminated || rv.Root == null) continue;

                if (rv.TagMesh != null && cam != null)
                    rv.TagMesh.transform.rotation = Quaternion.LookRotation(rv.TagMesh.transform.position - cam.position, Vector3.up);

                if (r.T > 2f)
                {
                    var rc = r.Cps[Mathf.Clamp(rv.CpIndex, 0, r.Cps.Length - 1)];
                    var to = Flat(rc) - Flat(rv.Root.position);
                    float d = to.magnitude;
                    if (d < 3.5f)
                    {
                        rv.CpIndex++;
                        if ( idx == 0 ) r.RivalI = rv.CpIndex;
                        if (rv.CpIndex >= r.Cps.Length)
                        {
                            EndRace(false);
                            return;
                        }
                    }
                    else
                    {
                        var p = rv.Root.position + to / d * rv.Speed * dt;
                        p.y = Data.DeckHeightAt(p) + 0.11f;
                        rv.Root.position = p;
                        rv.Root.rotation = Quaternion.LookRotation(to / d, Vector3.up);
                    }
                }

                float rvDist = Vector3.Distance(Flat(rv.Root.position), Flat(r.Cps[Mathf.Clamp(rv.CpIndex, 0, r.Cps.Length - 1)]));
                if (rv.CpIndex > r.I || (rv.CpIndex == r.I && rvDist < playerDistToNext))
                    aheadCount++;
            }

            r.PlayerPosition = 1 + aheadCount;
            if (ring != null) ring.Rotate(Vector3.up, dt * 120f);
            if (HasTask && State.S.taskType == "race") State.S.taskObj = RaceStatus;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
