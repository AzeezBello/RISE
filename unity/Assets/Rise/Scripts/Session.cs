using UnityEngine;
using Rise.Data;
using Rise.Player;
using Rise.Vehicles;
using Rise.World;

namespace Rise
{
    // The game loop around the player: interaction prompts (talk, work, enter / exit vehicles, buy from kiosks),
    // dialogue, shifts, driving input, vitals (stamina, fuel, driving skill), heat decay and autosave.
    public partial class Session : MonoBehaviour
    {
        // Runtime state only: nothing here is meant to be serialized into the scene or inspected as an asset.
        [System.NonSerialized] public WorldData Data; [System.NonSerialized] public GameState State; [System.NonSerialized] public DayNightCycle Clock;
        [System.NonSerialized] public PlayerController Player; [System.NonSerialized] public ThirdPersonCamera Camera;
        [System.NonSerialized] public TrafficSystem Traffic; [System.NonSerialized] public ContactSystem Contacts;
        [System.NonSerialized] public PedestrianSystem Pedestrians;

        [System.NonSerialized] public VehicleController Vehicle;        // the vehicle being driven, if any
        [System.NonSerialized] public string Prompt;                    // "TALK TO BABA K", "ENTER DANFO", …
        [System.NonSerialized] public JobData Working; [System.NonSerialized] public float WorkT, WorkDuration;   // shift in progress (real seconds)
        [System.NonSerialized] public readonly System.Collections.Generic.List<VehicleController> Parked = new System.Collections.Generic.List<VehicleController>();

        enum Target { None, Contact, Vehicle, TrafficVehicle, Kiosk, Vendor, Gate, Home, Business, BusStop, Deliver, Place, Hustler, GroundItem, Encounter }
        Target target; ContactSystem.Contact targetContact; VehicleController targetVehicle; TrafficSystem.Vehicle targetTraffic; Vector3 targetKiosk;
        PropertyData targetProperty; BusinessData targetBusiness; BusStopData targetStop; StreetLife.GroundItem targetItem;
        float saveT, heatT = 45f;
        bool sinking;
        Vector3 lastShorePos;

        [System.NonSerialized] public TrafficLights Lights;
        [System.NonSerialized] public Weather Weather;
        [System.NonSerialized] public Events Events;
        [System.NonSerialized] public World.QualityGovernor Governor;
        [System.NonSerialized] public Rise.Net.Account Account; [System.NonSerialized] public Rise.Net.CloudSave Cloud; [System.NonSerialized] public Rise.Net.Multiplayer Players;
        [System.NonSerialized] public bool Typing;   // a menu text field has the keyboard (set by the HUD)
        [System.NonSerialized] public bool ChatOpen; [System.NonSerialized] public string ChatDraft = "";
        [System.NonSerialized] public bool Paused;
        float cloudT, outOfFuelToastT;
        int lastDay;

        public void Init(WorldData data, GameState state, DayNightCycle clock, PlayerController player, ThirdPersonCamera camera, TrafficSystem traffic, ContactSystem contacts, TrafficLights lights)
        {
            Data = data; State = state; Clock = clock; Player = player; Camera = camera; Traffic = traffic; Contacts = contacts; Lights = lights;
            lastDay = clock.Day;
            lastShorePos = data.Spawn;
            SpawnParkedVehicles();
            Traffic.PlayerObstacle = Player.transform;
            Player.SetWeapon(BestWeaponId());
        }

        // After the network objects exist: rejoin the crew channel from the save.
        public void AfterNetInit() { if (InCrew) JoinCrewChannel(); }

        public Transform Focus => Vehicle != null ? Vehicle.transform : Player.transform;

        // A new game starts with one keke of your own on the kerb of Adeniran Ogunsanya Street; buy the rest from Dayo.
        void SpawnParkedVehicles()
        {
            if (State.S.vehicles.Count > 0) { SpawnOwnedFromSave(); return; }
            var road = Data.RoadAt(Data.Spawn, 2f) ?? Data.roads[0];
            var pos = road.Point(road.Along(Data.Spawn) + 32f, road.k + road.width * 0.5f - 2.2f);
            var keke = new OwnedVehicle { id = "starter-keke", type = "keke", x = pos.x, y = 0f, z = pos.z, yaw = Quaternion.LookRotation(road.Forward(1), Vector3.up).eulerAngles.y };
            State.S.vehicles.Add(keke);
            SpawnOwned(keke);
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            if (Clock != null) Clock.enabled = !paused;
            if (Traffic != null) Traffic.enabled = !paused;
            if (Weather != null) Weather.enabled = !paused;
            if (Events != null) Events.enabled = !paused;
            if (Player != null) Player.InputEnabled = !paused && ActiveMenu == null;
            if (!paused && ActiveMenu != null && ActiveMenu.Title == "GAME PAUSED")
                ActiveMenu = null;
        }

        public void ResumeGame()
        {
            SetPaused(false);
            ActiveMenu = null;
        }

        public void TogglePauseMenu()
        {
            if (Paused) ResumeGame();
            else OpenPauseMenu();
        }

        public void OpenPauseMenu()
        {
            PhoneOpen = false;
            SetPaused(true);
            var st = State.S;
            string seasonStr = Weather != null ? Weather.ActiveSeasonLabel : (Clock != null ? Clock.SeasonLabel : "DRY · HARMATTAN");
            var m = new Menu
            {
                Title = "GAME PAUSED",
                Subtitle = $"{Clock.ClockText} · {Clock.DateText} · {seasonStr} · {Data.RegionAt(Focus.position)?.name ?? "Lagos"}",
                Line = $"Cash ₦{st.cash:N0} · Bank ₦{st.bank:N0} · Fuel {st.fuel:0}% · Health {st.health:0}%"
            };
            m.Options.Add(new MenuOption { Label = "▶ Resume Game", Action = ResumeGame });
            m.Options.Add(new MenuOption { Label = "📱 Open Smartphone", Action = () => { SetPaused(false); OpenPhone(); } });
            m.Options.Add(new MenuOption { Label = "🗺️ Interactive Map & GPS Navigation", Action = () => { SetPaused(false); PhoneOpen = true; AppMap(""); } });
            m.Options.Add(new MenuOption { Label = "🏎️ NFS Underground Tuning & Mod Garage", Action = () => { SetPaused(false); OpenNfsGarageMenu(Vehicle); } });
            if (st.fuel < 99f)
            {
                m.Options.Add(new MenuOption
                {
                    Label = $"⛽ Emergency Roadside Jerrycan Refuel (100% Fuel) · ₦4,000 (now {st.fuel:0}%)",
                    Action = () => { EmergencyRefuel(4000); OpenPauseMenu(); }
                });
            }
            m.Options.Add(new MenuOption
            {
                Label = $"🌦️ Season Mode: {seasonStr} (tap to cycle Calendar / Rainy / Dry Harmattan)",
                Action = () =>
                {
                    if (Weather != null)
                    {
                        string next = Weather.ForcedSeason == "" ? "rainy" : Weather.ForcedSeason == "rainy" ? "dry" : "";
                        Weather.SetSeasonMode(next);
                        State.Toast($"Season set to {Weather.ActiveSeasonLabel}");
                    }
                    OpenPauseMenu();
                }
            });
            m.Options.Add(new MenuOption { Label = "⚙️ Settings (Audio / Graphics / Save)", Action = () => { SetPaused(false); PhoneOpen = true; AppSettings(); } });
            m.Options.Add(new MenuOption { Label = "💾 Save Game Now", Action = () => { AutoSave(); State.Toast("Game saved"); OpenPauseMenu(); } });
            OpenMenu(m);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            State.PruneToasts();
            if (InNfsGarageBay) UpdateNfsGarageBay(dt);

            if (Paused)
            {
                if (GameInput.PausePressed) { ResumeGame(); return; }
                if (ActiveMenu != null) { UpdateMenu(); if (ActiveMenu == null) SetPaused(false); }
                else SetPaused(false);
                return;
            }

            Vitals(dt);
            heatT -= dt; if (heatT <= 0f) { heatT = 45f; if (State.S.heat > 0) State.S.heat--; }
            saveT += dt; if (saveT > 15f) { saveT = 0f; AutoSave(); }

            State.TickEconomy(dt);
            if (Clock.Day != lastDay) { lastDay = Clock.Day; State.NewDay(Clock.Day); Events?.OnDayChanged(Clock.Date, Clock.Day); }
            UpdateTolls();
            UpdateTask(dt);
            UpdateShootout(dt);
            UpdateFootball(dt);
            UpdateRace(dt);
            UpdateFamily(dt);
            UpdateStreet();
            UpdateCrew(dt);
            if (Nav != null) { Nav.Target = InInterior ? null : ObjectivePosition; Nav.Tick(Focus.position); }
            Contacts.SetArrow(!HasTask && MissionActive ? CurrentMission.Who : null);

            if (Working != null) { UpdateShift(dt); return; }
            if (ChatOpen) { Player.InputEnabled = false; return; }   // the HUD text field owns the keyboard; Enter sends, Esc closes
            if (ActiveMenu == null && Players != null && GameInput.ChatPressed) { ChatOpen = true; ChatDraft = ""; return; }
            if (ActiveMenu == null && GameInput.PhonePressed) { OpenPhone(); return; }
            if (ActiveMenu == null && GameInput.MapPressed) { PhoneOpen = true; AppMap(""); return; }
            if (ActiveMenu == null && GameInput.PausePressed) { OpenPauseMenu(); return; }
            if (ActiveMenu != null)
            {
                if (GameInput.PausePressed) { CloseMenu(); PhoneOpen = false; if (InNfsGarageBay) LeaveNfsGarageBay(); return; }
                UpdateMenu();
                if (ActiveMenu == null && PhoneOpen) PhoneOpen = false;
                if (ActiveMenu == null && InNfsGarageBay) LeaveNfsGarageBay();
                return;
            }
            if (Vehicle != null) { UpdateDriving(dt); return; }
            UpdateOnFoot();
        }

        // ---- vitals ----
        void Vitals(float dt)
        {
            var s = State.S;
            if (Vehicle != null)
            {
                float v = Mathf.Abs(Vehicle.Speed);
                if (v > 0.5f) { s.fuel = Mathf.Max(0f, s.fuel - dt * 0.4f * (v / 24f)); State.GainSkill("driving", dt * 0.03f * Mathf.Min(1f, v / 15f)); }
                s.stamina = Mathf.Min(100f, s.stamina + dt * 6f);
            }
            else if (Player != null && Player.Swimming)
            {
                s.stamina = Mathf.Max(0f, s.stamina - 8f * dt);
                if (s.stamina <= 0f)
                {
                    s.health = Mathf.Max(0f, s.health - 12f * dt);
                    if (s.health <= 0f)
                    {
                        s.health = 50f; s.stamina = 40f;
                        Player.Swimming = false;
                        Player.Teleport(lastShorePos + Vector3.up * 0.3f, 0f);
                        State.Toast("Fishermen pulled you from the lagoon to shore.");
                    }
                }
                Player.CanSprint = false;
            }
            else
            {
                bool sprinting = Player.IsRunning && Player.Speed01 > 0.6f;
                float recovery = (10f + Mathf.Min(5f, s.skills.fitness * 0.05f)) * (State.HomeHas("tank") ? 1.25f : 1f);
                float sprintCost = 16f * (1f - Mathf.Min(0.25f, s.skills.strength * 0.0025f));
                s.stamina = Mathf.Clamp(s.stamina + (sprinting ? -sprintCost : recovery) * dt, 0f, 100f);
                if (sprinting) State.GainSkill("fitness", dt * 0.05f);
                Player.CanSprint = s.stamina > 0f;
            }
        }

        public void ForceOnFootUpdate() { if (Vehicle == null) UpdateOnFoot(); }

        public bool InWaterOffDeck(Vector3 pos) => Data.InWater(pos) && Data.DeckHeightAt(pos) < 0.05f;

        // ---- on foot ----
        void UpdateOnFoot()
        {
            Player.InputEnabled = true;
            if (InFootballMatch)
            {
                Player.Swimming = false;
                Player.CanSprint = State.S.stamina > 0f;
                target = Target.None;
                string rxName = ActiveFootball.BestReceiver != null ? $" (#{ActiveFootball.BestReceiver.Number} {ActiveFootball.BestReceiver.Name})" : "";
                Prompt = ActiveFootball.PlayerHasBall
                    ? $"PASS{rxName} [E]  ·  HOLD/TAP SHOOT [F / CLICK]  ·  SKILL / CHIP [G]  ·  TACTIC ({ActiveFootball.TacticLabel}) [R]"
                    : (ActiveFootball.BallCarrier != null && ActiveFootball.BallCarrier.IsHome)
                        ? $"1-2 PASS TO ME [E]  ·  COMMAND SHOT [F / CLICK]  ·  TACTIC ({ActiveFootball.TacticLabel}) [R]"
                        : $"SLIDE TACKLE [F / CLICK]  ·  PRESS BALL  ·  TACTIC ({ActiveFootball.TacticLabel}) [R]  ·  MENU [X]";

                if (ActiveFootball.PlayerHasBall)
                {
                    if (GameInput.AttackHeld)
                    {
                        ActiveFootball.ChargingShot = true;
                        ActiveFootball.ShotPowerCharge = Mathf.Clamp01(ActiveFootball.ShotPowerCharge + Time.deltaTime * 1.35f);
                        if (ActiveFootball.ShotPowerCharge >= 1.0f)
                        {
                            FootballShoot(0.85f + ActiveFootball.ShotPowerCharge * 0.60f);
                            ActiveFootball.ShotPowerCharge = 0f;
                        }
                        return;
                    }
                    if (ActiveFootball.ChargingShot && ActiveFootball.ShotPowerCharge > 0.04f)
                    {
                        float pwr = 0.85f + ActiveFootball.ShotPowerCharge * 0.60f;
                        ActiveFootball.ShotPowerCharge = 0f;
                        ActiveFootball.ChargingShot = false;
                        FootballShoot(pwr);
                        return;
                    }
                    if (GameInput.AttackPressed)
                    {
                        FootballShoot(1.18f);
                        return;
                    }
                }
                else if (GameInput.AttackPressed)
                {
                    if (!FootballShoot()) FootballTackle();
                    return;
                }
                if (GameInput.InteractPressed) { FootballPass(); return; }
                if (GameInput.PickupPressed || GameInput.CycleWeaponPressed) { FootballChip(); return; }
                if (GameInput.ReloadPressed) { CycleFootballTactic(); return; }
                if (GameInput.DeclinePressed) { OpenFootballMenu(); return; }
                return;
            }
            if (UpdateInteriorOnFoot()) return;
            var pos = Player.transform.position;
            bool swimming = InWaterOffDeck(pos);
            if (swimming && !Player.Swimming) State.Toast("You are in the lagoon — swim back to land");
            Player.Swimming = swimming;
            if (swimming) { Player.CanSprint = false; Prompt = null; target = Target.None; return; }
            lastShorePos = pos;
            target = Target.None; Prompt = null;

            targetVehicle = null; float best = 2.8f;
            foreach (var v in Parked)
            {
                if (v == null) continue;
                Vector3 local = v.transform.InverseTransformPoint(pos);
                float dx = Mathf.Max(0f, Mathf.Abs(local.x) - v.Class.wid * 0.5f);
                float dz = Mathf.Max(0f, Mathf.Abs(local.z) - v.Class.len * 0.5f);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < best) { best = d; targetVehicle = v; }
            }
            targetTraffic = Traffic.Nearest(pos, 3.6f);
            targetContact = Contacts.Nearest(pos, 3.4f);

            targetProperty = NearGate(pos); targetBusiness = NearBusiness(pos); targetStop = NearBusStop(pos);
            targetItem = Street != null ? Street.NearestGroundItem(pos, 2.6f) : null;

            if (TaskDeliverable()) { target = Target.Deliver; Prompt = DeliverLabel(); }
            else if (targetContact != null) { target = Target.Contact; Prompt = $"TALK TO {targetContact.data.name.ToUpper()}"; }
            else if (targetItem != null) { target = Target.GroundItem; Prompt = $"PICK UP {targetItem.label.ToUpper()} (E / G)"; }
            else if (targetVehicle != null) { target = Target.Vehicle; Prompt = $"ENTER {targetVehicle.Class.name.ToUpper()}"; }
            else if (targetProperty != null && IsHome(targetProperty)) { target = Target.Home; Prompt = "ENTER HOME"; }
            else if (targetProperty != null) { target = Target.Gate; Prompt = $"SEE AGENT KUNLE · {targetProperty.type.ToUpper()}"; }
            else if (targetBusiness != null) { target = Target.Business; Prompt = (State.Biz(targetBusiness.id) != null ? "MANAGE " : "BUY ") + targetBusiness.name.ToUpper(); }
            else if (targetTraffic != null && targetTraffic.speed < 6.5f) { target = Target.TrafficVehicle; Prompt = $"TAKE {targetTraffic.cls.name.ToUpper()}"; }
            else if ((targetPlace = NearPlace(pos)) != null) { target = Target.Place; Prompt = PlacePrompt(targetPlace); }
            else if (targetStop != null) { target = Target.BusStop; Prompt = $"TAKE DANFO FROM {targetStop.shortName}"; }
            else if (NearKiosk(pos, out targetKiosk)) { target = Target.Kiosk; Prompt = "POS KIOSK · PURE WATER, SUYA"; }
            else if (NearVendor(pos, out targetVendor)) { target = Target.Vendor; Prompt = "ROADSIDE VENDOR · PURE WATER, GALA, SUYA"; }
            else if (Street != null && (targetHustler = Street.NearestNightlife(pos, 3.5f)) != null) { target = Target.Hustler; Prompt = $"TALK TO {targetHustler.Value.name.ToUpper()}"; }
            else if (NearEncounter(pos)) { target = Target.Encounter; Prompt = Events.ActiveEncounter.prompt; }

            if (GameInput.CycleWeaponPressed) { CycleWeapon(); return; }
            if (GameInput.PickupPressed) { if (targetItem != null) PickupGroundItem(targetItem); else TryPickupOrCycleItem(); return; }
            if (GameInput.AttackPressed) { PerformStreetAttack(); return; }

            if (!Interact()) return;
            switch (target)
            {
                case Target.GroundItem: PickupGroundItem(targetItem); break;
                case Target.Vehicle: Enter(targetVehicle); break;
                case Target.TrafficVehicle: TryHijackTrafficVehicle(targetTraffic); break;
                case Target.Deliver: FinishTask(true); break;
                case Target.Contact: TalkTo(targetContact); break;
                case Target.Home: OpenHomeMenu(targetProperty); break;
                case Target.Gate: OpenGateMenu(targetProperty); break;
                case Target.Business: OpenBusinessMenu(targetBusiness); break;
                case Target.Place: OpenPlaceMenu(targetPlace); break;
                case Target.BusStop: OpenTransitMenu(targetStop); break;
                case Target.Kiosk: OpenKioskMenu(); break;
                case Target.Vendor: OpenVendorMenu(); break;
                case Target.Hustler: if (targetHustler.HasValue) OpenHustlerMenu(targetHustler.Value); break;
                case Target.Encounter: ResolveEncounter(); break;
            }
        }

        public bool NearEncounter(Vector3 pos) => Events != null && Events.ActiveEncounter != null && Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(Events.ActiveEncounter.pos.x, Events.ActiveEncounter.pos.z)) < 11f;

        public bool ResolveEncounter()
        {
            if (Events == null || Events.ActiveEncounter == null) return false;
            var enc = Events.ActiveEncounter;
            State.Earn(enc.reward);
            State.AddRep(enc.rep, enc.repAmount);
            State.AddXp(enc.xp);
            if (enc.kind == "breakdown") { State.S.stamina = Mathf.Max(5f, State.S.stamina - 8f); State.GainSkill("fitness", 2f); }
            else if (enc.kind == "dispute") State.GainSkill("charisma", 2f);
            else State.GainSkill("business", 2f);
            State.Toast($"{enc.title} resolved · +₦{enc.reward:N0} · +{enc.repAmount} {enc.rep} rep");
            Events.ClearEncounter();
            return true;
        }

        // Commercial vehicles (danfo, keke, okada, fuel tanker, etc.) require a fight weapon to hijack,
        // and the driver/conductor fight back before fleeing.
        public bool TryHijackTrafficVehicle(TrafficSystem.Vehicle tv)
        {
            if (tv == null) return false;
            if (tv.cls != null && tv.cls.commercial)
            {
                if (!HasWeapon())
                {
                    State.Toast("The driver and passengers will fight back. You need a fight weapon to hijack this vehicle.");
                    return false;
                }
                Player?.SwingWeapon();
                int dmg = Mathf.Max(6, Random.Range(24, 48) - WeaponPower() / 4);
                State.S.health = Mathf.Max(15f, State.S.health - dmg);
                State.AddHeat(2, "Armed commercial hijack");
                State.AddRep("public", -8);
                State.AddRep("street", -5);
                State.Toast($"Used {EquippedWeaponName} to force the driver out (-{dmg} health) and took the {tv.cls.name}!");
                var vc = Traffic.TakeVehicle(tv);
                Enter(vc);
                return true;
            }
            if (HasWeapon()) Player?.SwingWeapon();
            var plain = Traffic.TakeVehicle(tv);
            Enter(plain);
            State.AddHeat(1, "Vehicle taken");
            State.Toast($"Pulled the driver out and took the {tv.cls?.name ?? "vehicle"}.");
            return true;
        }

        string DeliverLabel()
        {
            var st = State.S;
            if (st.taskType == "steal") return "HAND THE SEDAN TO DAYO";
            if (st.taskMission < 0 && ActiveRun != null) return $"DELIVER THE {ActiveRun.Label.ToUpper()}";
            if (Data.TryPlace(st.taskDest, out var d)) return $"DELIVER TO {d.name.ToUpper()}";
            return "DELIVER";
        }

        // Story first, then Dayo's dealership, then politicians, fraudsters, hubs, and plain job dialogue.
        public void TalkTo(ContactSystem.Contact c)
        {
            if (IsMissionContact(c)) { OpenStoryDialogue(c); return; }
            if (IsFamily(c)) { OpenFamilyMenu(c); return; }
            if (c.data.id == "dayo") { OpenDealerMenu(c); return; }
            if (c.data.id == "speedy") { OpenRaceMenu(c); return; }
            if (c.data.id == "captain" || c.data.id == "ajcaptain") { OpenFootballMenu(); return; }
            if (IsPolitician(c)) { OpenPoliticianMenu(c); return; }
            if (IsFraudster(c)) { OpenFraudsterMenu(c); return; }
            if (IsHub(c)) { OpenHubMenu(c); return; }
            OpenDialogue(c);
        }

        bool NearKiosk(Vector3 pos, out Vector3 kiosk)
        {
            kiosk = Vector3.zero; float best = 3.2f * 3.2f;
            foreach (var k in Data.kiosks) { var w = k.World(); float d = (new Vector3(w.x, pos.y, w.z) - pos).sqrMagnitude; if (d < best) { best = d; kiosk = w; } }
            return best < 3.2f * 3.2f;
        }

        static bool Interact() => GameInput.InteractPressed;
        static bool Decline() => GameInput.DeclinePressed;

        // ---- dialogue and shifts ----
        public void OpenDialogue(ContactSystem.Contact c)
        {
            var menu = new Menu { Title = c.data.name, Subtitle = $"{c.data.role} · {State.RelTier(c.data.id)}", Line = GreetLine(c) };
            foreach (var j in Data.JobsAt(c.data.at))
            {
                bool ok = State.CanTake(j) && (!j.night || Clock.IsNight);
                string why = !string.IsNullOrEmpty(j.needCourse) ? $"needs the {j.needCourse} certificate" : State.S.skills.Get(j.skill) < j.min ? $"needs {j.skill} {j.min}" : j.night && !Clock.IsNight ? "night shift only" : "";
                var job = j;
                menu.Options.Add(new MenuOption { Label = ok ? $"Work shift — {j.title} · ₦{State.JobPay(j):N0} · {j.dur}h" : $"{j.title} ({why})", Enabled = ok, Action = () => StartShift(job) });
            }
            AddActionOpsToMenu(menu, c.data.id);
            AddDogOption(menu, c);
            AddRelationshipOptions(menu, c);
            menu.Options.Add(new MenuOption { Label = "Not now — I go find another hustle", Action = () => State.Toast("The story can wait. Jobs, courses, the gym and business dey.") });
            OpenMenu(menu);
        }

        public void StartShift(JobData job, float realSeconds = 3.5f)
        {
            Working = job; WorkT = 0f; WorkDuration = realSeconds; ActiveMenu = null;
        }

        // Scripted input (play test, cutscenes): when on, driving reads these instead of the keyboard / pad.
        [System.NonSerialized] public bool ScriptedInput; [System.NonSerialized] public float ScriptGas, ScriptBrake, ScriptSteer;
        public void Script(float gas, float brake, float steer) { ScriptedInput = true; ScriptGas = gas; ScriptBrake = brake; ScriptSteer = steer; }
        public void EndScript() => ScriptedInput = false;

        void UpdateShift(float dt)
        {
            Player.InputEnabled = false;
            float before = WorkT; WorkT += dt;
            // The shift takes `dur` game hours, compressed into WorkDuration real seconds.
            Clock.Advance(Working.dur * Mathf.Clamp01(WorkT / WorkDuration) - Working.dur * Mathf.Clamp01(before / WorkDuration));
            if (WorkT < WorkDuration) return;
            int pay = State.JobPay(Working);
            State.Earn(pay); State.GainSkill(Working.skill, 2f); State.AddXp(Working.xp);
            State.Toast($"Shift done · +₦{pay:N0}{(pay > Working.pay ? " (skill bonus)" : "")}");
            if (!string.IsNullOrEmpty(Working.by)) State.Message(Working.by, $"Thanks for the shift. ₦{Working.pay:N0} don enter your hand.");
            Working = null;
        }

        // ---- driving ----
        public void Enter(VehicleController v)
        {
            Player.TriggerVehicleEnterAnim();
            Vehicle = v; v.SetDriven(true);
            if (State != null && Data != null && Data.fabrics != null && Data.fabrics.Length > 0)
            {
                var look = State.Look;
                var fab = Data.fabrics[Mathf.Clamp(look.Fabric, 0, Data.fabrics.Length - 1)];
                v.StyleDriver(look.Skin, Entities.Mannequin.Hex(fab.baseColor));
            }
            Player.gameObject.SetActive(false);
            Camera.SetDriving(v.transform);
            Traffic.PlayerObstacle = v.transform;
            Prompt = null;
        }

        public void Exit()
        {
            var v = Vehicle; Vehicle = null;
            v.SetDriven(false);
            float sideDist = v.Class.wid * 0.5f + 1.2f;
            float endDist = v.Class.len * 0.5f + 1.2f;
            Vector3 vPos = v.transform.position;
            Vector3 right = v.transform.right;
            Vector3 fwd = v.transform.forward;
            Vector3[] candidates =
            {
                vPos - right * sideDist,
                vPos + right * sideDist,
                vPos - fwd * endDist,
                vPos + fwd * endDist,
                vPos - right * (sideDist + 1.4f),
                vPos + right * (sideDist + 1.4f),
            };
            Vector3 chosen = candidates[0];
            foreach (var c in candidates)
            {
                float groundY = Data != null ? Data.DeckHeightAt(c) : c.y;
                Vector3 p0 = new Vector3(c.x, groundY + 0.45f, c.z);
                Vector3 p1 = new Vector3(c.x, groundY + 1.45f, c.z);
                if (!Physics.CheckCapsule(p0, p1, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                {
                    chosen = new Vector3(c.x, groundY + 0.5f, c.z);
                    break;
                }
            }
            Player.Teleport(chosen, v.transform.eulerAngles.y);
            Player.gameObject.SetActive(true);
            Player.TriggerVehicleExitAnim();
            Camera.SetDriving(null);
            Traffic.PlayerObstacle = Player.transform;
            if (!v.Owned && !Parked.Contains(v)) Parked.Add(v);   // a taken vehicle can be re-entered where you left it
        }

        void UpdateDriving(float dt)
        {
            float h = GameInput.Steer, gas = GameInput.Gas, brake = GameInput.Brake;
            bool boost = GameInput.Sprint, hand = GameInput.Handbrake;
            if (ScriptedInput) { gas = ScriptGas; brake = ScriptBrake; h = ScriptSteer; boost = false; hand = false; }
            Vehicle.Wet = (Weather != null ? Weather.Wet : 1f) * (Events != null && Events.FloodedAt(Vehicle.transform.position) ? 0.45f : 1f);
            // Into the lagoon: the engine dies, the vehicle settles and you swim out.
            if (InWaterOffDeck(Vehicle.transform.position))
            {
                Vehicle.Speed = Mathf.MoveTowards(Vehicle.Speed, 0f, 18f * dt); Vehicle.Condition = Mathf.Max(0f, Vehicle.Condition - 20f * dt);
                if (!sinking) { sinking = true; State.Toast("The lagoon don swallow the motor!"); }
                if (Mathf.Abs(Vehicle.Speed) < 0.5f) { Exit(); sinking = false; return; }
            }
            else sinking = false;
            if (GameInput.HornPressed) UI.SoundSystem.I?.PlayHorn();
            Vehicle.Drive(gas, brake, boost, hand, h, State.S.fuel / 100f, dt);
            if (State.S.fuel <= 0.05f && gas > 0.1f && Time.time - outOfFuelToastT > 4.5f)
            {
                outOfFuelToastT = Time.time;
                State.Toast("Out of fuel (0%)! Engine stalled — use Emergency Jerrycan on your Phone [P] or Pause Menu [Esc].");
            }
            if (Player != null) Player.transform.position = Vehicle.transform.position;
            if (Vehicle.LastImpact > 2f)
            {
                UI.SoundSystem.I?.PlayHorn();
                Camera?.AddShake(Mathf.Clamp01(Vehicle.LastImpact / 16f));
                State.Toast(Vehicle.LastImpact > 8f ? "Crash! Vehicle damaged" : "Scrape");
                Vehicle.LastImpact = 0f;
            }
            bool slow = Mathf.Abs(Vehicle.Speed) < 2f;
            Prompt = null;
            if (slow && TaskDeliverable()) { Prompt = DeliverLabel(); if (Interact()) { FinishTask(true); return; } }
            else if (slow && NearFuel(Vehicle.transform.position) && State.S.fuel < 99f) { Prompt = $"REFUEL · ₦{Data.meta.refuel:N0}"; if (Interact()) { Refuel(); return; } }
            else if (slow && State.S.fuel <= 0.05f) { Prompt = "OUT OF FUEL · EXIT VEHICLE (OR OPEN PHONE [P] TO REFUEL)"; if (Interact()) Exit(); }
            else if (slow && Vehicle.Condition <= 0.5f) { Prompt = "VEHICLE WRECKED · EXIT OR OPEN PHONE [P] TO REPAIR"; if (Interact()) Exit(); }
            else if (slow) { Prompt = "EXIT VEHICLE"; if (Interact()) Exit(); }
        }

        // ---- save ----
        public void SubmitChat() { if (Players != null && !string.IsNullOrWhiteSpace(ChatDraft)) Players.SendChat(ChatDraft); ChatOpen = false; ChatDraft = ""; }

        // What other players see of you.
        public Rise.Net.Multiplayer.PosMsg NetState()
        {
            var t = Focus; var st = State.S;
            return new Rise.Net.Multiplayer.PosMsg { x = t.position.x, y = t.position.y, z = t.position.z, yaw = t.eulerAngles.y, vehicle = Vehicle != null ? Vehicle.Class.id : "", fabric = st.lookFabric, skin = st.lookSkin, body = st.lookBody, swimming = Player.Swimming };
        }

        public void AutoSave()
        {
            SaveVehicles();
            var t = Vehicle != null ? Vehicle.transform : Player.transform;
            State.Save(t.position, t.eulerAngles.y, Clock.Clock, Clock.Day);
            // Signed in: the same blob goes to the cloud, at most once a minute.
            if (Cloud != null && Account != null && Account.SignedIn && Time.time - cloudT > 60f) { cloudT = Time.time; Cloud.Push(State.S); }
        }

        void OnApplicationQuit() => AutoSave();
    }
}
