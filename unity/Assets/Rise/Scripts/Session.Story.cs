using System.Collections.Generic;
using UnityEngine;
using Rise.Data;
using Rise.Story;
using Rise.Vehicles;
using Rise.World;

namespace Rise
{
    // Story flow: the current mission, who to talk to, tasks (timed,
    // fragile cargo, escape, vehicle recovery), completion at the destination, failure on arrest, and the objective
    // the HUD and GPS show.
    public partial class Session
    {
        [System.NonSerialized] public Navigation Nav;
        [System.NonSerialized] public Law Law;
        VehicleController markedVehicle;

        public Mission CurrentMission => Missions.Current(State);
        public bool MissionAvailable { get { var m = CurrentMission; var st = State.S; return !st.done && (m.Requires == null || m.Requires(this)) && (string.IsNullOrEmpty(m.Arc) || m.Arc == st.arc); } }
        public bool MissionActive => MissionAvailable && !State.S.storyPaused;
        public bool HasTask => !string.IsNullOrEmpty(State.S.taskType);

        public Vector3? ResolveWaypoint(string id, out string label)
        {
            label = "";
            if (string.IsNullOrEmpty(id)) return null;
            if (id.StartsWith("pos:"))
            {
                var parts = id.Split(':');
                if (parts.Length >= 3 &&
                    float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float px) &&
                    float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float pz))
                {
                    var wPos = new Vector3(px, Data != null ? Data.DeckHeightAt(new Vector3(px, 0f, pz)) : 0f, pz);
                    var reg = Data != null ? Data.RegionAt(wPos) : null;
                    label = parts.Length >= 4 && !string.IsNullOrEmpty(parts[3]) ? parts[3] : (reg != null ? $"Custom Pin ({reg.name})" : "Custom Map Pin");
                    return wPos;
                }
            }
            if (id.StartsWith("veh:"))
            {
                string vid = id.Substring(4);
                foreach (var pv in Parked)
                    if (pv != null && pv.OwnedId == vid) { label = $"My {pv.Class.name}"; return pv.transform.position; }
            }
            if (Data.TryPlace(id, out var place)) { label = place.name; return place.World(); }
            if (Data.busStops != null)
                foreach (var bs in Data.busStops)
                    if (bs.id == id) { label = bs.name; return bs.World(); }
            var prop = Data.Property(id);
            if (prop != null) { label = prop.name; return prop.Door; }
            if (Data.businesses != null)
                foreach (var bz in Data.businesses)
                    if (bz.id == id) { label = bz.name; return bz.World(); }
            if (Contacts != null) foreach (var c in Contacts.Contacts) if (c.data.id == id) { label = c.data.name; return c.pos; }
            return null;
        }

        public void SetWaypoint(string id)
        {
            State.S.waypointId = id ?? "";
            if (ResolveWaypoint(State.S.waypointId, out var label).HasValue) State.Toast($"GPS set to {label}");
            else State.Toast("GPS cleared");
            RefreshNav();
        }

        public string ObjectiveTitle => ActiveIndoorMission != null ? ActiveIndoorMission.Title : InFootballMatch ? ActiveFootball.Event.Title : Racing ? ActiveRace.Title : CoopGetaway ? "Crew Co-op Getaway" : CoopConvoy ? "Crew Co-op Convoy" : ActiveActionOp != null ? ActiveActionOp.Title : HasTask && !string.IsNullOrEmpty(ActiveChainId) ? (ChainById(ActiveChainId)?.Title ?? "Neighbourhood Story") : HasTask && State.S.taskMission < 0 ? "Side mission" : HasTask ? Missions.All[Mathf.Clamp(State.S.taskMission, 0, Missions.All.Count - 1)].Title : !string.IsNullOrEmpty(State.S.waypointId) && ResolveWaypoint(State.S.waypointId, out _).HasValue ? "GPS Waypoint" : MissionAvailable ? CurrentMission.Title : State.S.done ? "Lagos is yours" : "Free roam";
        public string ObjectiveText => ActiveIndoorMission != null ? $"{ActiveIndoorMission.Objective} ({ActiveIndoorMission.Step}/{ActiveIndoorMission.TotalSteps})" : InFootballMatch ? $"{CurrentTournamentStageLabel} vs {CurrentOpponentClub} · {ActiveFootball.Event.HomeTag} {ActiveFootball.HomeScore} - {ActiveFootball.AwayScore} {ActiveFootball.Event.AwayTag} ({ActiveFootball.MatchClock:0}')" : Racing ? RaceStatus : CoopGetaway ? $"Lose Heat-4 police patrols with {CoopPartnerName} — {Mathf.Max(0f, 8f - CoopGetawayClear):0} s clear needed" : CoopConvoy ? $"Escort convoy checkpoint {Mathf.Min(CoopConvoyIndex + 1, 3)}/3 with {CoopPartnerName}" : HasTask ? State.S.taskObj : !string.IsNullOrEmpty(State.S.waypointId) && ResolveWaypoint(State.S.waypointId, out var wpName).HasValue ? $"GPS to {wpName}" : State.S.storyPaused ? "Story paused — talk to " + Data.ContactName(CurrentMission.Who) + " to resume" : MissionAvailable ? CurrentMission.Objective(this) : State.S.done ? "Build your empire: jobs, property, business." : "";

        // Where the GPS points: the task destination, custom waypoint, or the mission contact.
        public Vector3? ObjectivePosition
        {
            get
            {
                var st = State.S;
                if (Racing) return ActiveRace.Cps[ActiveRace.I];
                if (CoopConvoy && ActiveConvoyTarget.HasValue) return ActiveConvoyTarget;
                if (CoopGetaway) return null;
                if (HasTask)
                {
                    if (st.taskType == "steal" && !st.taskTaken && markedVehicle != null) return markedVehicle.transform.position;
                    if (st.taskType == "escape") return null;
                    if (Data.TryPlace(st.taskDest, out var d)) return d.World();
                    return null;
                }
                var wp = ResolveWaypoint(st.waypointId, out _);
                if (wp.HasValue) return wp;
                if (!MissionActive) return NearestPin(Focus.position);
                var m = CurrentMission;
                foreach (var c in Contacts.Contacts) if (c.data.id == m.Who) return c.pos;
                return Data.TryPlace(m.At, out var p) ? p.World() : (Vector3?)null;
            }
        }

        // Point the GPS at the current objective right away (the per-frame update would lag one frame).
        public void RefreshNav() { if (Nav == null) return; Nav.Target = ObjectivePosition; Nav.Refresh(Focus.position); }

        bool IsMissionContact(ContactSystem.Contact c) => MissionAvailable && !HasTask && CurrentMission.Who == c.data.id;

        // The story dialogue for the mission's contact: the lines, then the choices (or Continue), then the jobs.
        void OpenStoryDialogue(ContactSystem.Contact c)
        {
            var m = CurrentMission;
            State.S.storyPaused = false;
            var text = new System.Text.StringBuilder();
            foreach (var l in m.Lines(this)) text.Append(text.Length > 0 ? "\n" : "").Append(l.Text);
            var menu = new Menu { Title = c.data.name, Subtitle = m.Title, Line = text.ToString() };
            if (m.Choices != null && m.Choices.Length > 0)
                foreach (var ch in m.Choices) { var choice = ch; menu.Options.Add(new MenuOption { Label = ch.Label, Action = () => { if (choice.Apply == null || choice.Apply(this)) { State.Message(c.data.id, choice.Reply); CompleteMission(m); } } }); }
            else menu.Options.Add(new MenuOption { Label = "Continue", Action = () => CompleteMission(m) });
            menu.Options.Add(new MenuOption { Label = "Not now — I go find another hustle", Action = () => { State.S.storyPaused = true; State.Toast("Story paused. The objective card points you to jobs, property and business."); } });
            OpenMenu(menu);
        }

        void CompleteMission(Mission m)
        {
            int before = State.S.mission;
            if (m.Task != null) { StartTask(m, Missions.All.IndexOf(m)); return; }
            m.After?.Invoke(this);
            if (State.S.mission == before && !State.S.done) State.S.mission++;   // a choice may have moved the index itself (Crossroads)
            RefreshNav();
        }

        // ---- tasks ----
        void StartTask(Mission m, int index)
        {
            var t = m.Task; var st = State.S;
            st.taskType = t.Type; st.taskDest = t.Dest ?? ""; st.taskItem = t.Item ?? ""; st.taskObj = t.Obj; st.taskMission = index; st.taskClear = 0f; st.taskTaken = false; st.taskMaxKmh = t.MaxKmh;
            if (!string.IsNullOrEmpty(t.Item)) State.AddItem(t.Item);
            if (t.Type == "timed") st.taskDeadline = Clock.Clock + t.Minutes / 60f;
            if (t.Type == "escape") st.heat = Mathf.Max(st.heat, t.Heat);
            if (t.Type == "steal") SpawnMarkedVehicle(t.VehicleType);
            if (t.Type == "race") StartRace(0, true);
            State.Toast("Mission started · follow the GPS");
            RefreshNav();
        }

        // The sedan "near Bode Thomas" whose owner owes Baba K.
        void SpawnMarkedVehicle(string type)
        {
            var cls = Data.VehicleClass(type) ?? Data.VehicleClass("car");
            var road = Data.FindRoad("h", 0f) ?? Data.roads[0];
            var go = VehicleBodies.Build(cls, transform, new System.Random(3), out var wheels);
            var pos = road.Point(30f, road.k + road.width * 0.5f - 2.2f); pos.y = Data.DeckHeightAt(pos) + 0.12f;
            go.transform.position = pos; go.transform.rotation = Quaternion.LookRotation(road.Forward(1), Vector3.up);
            markedVehicle = go.AddComponent<VehicleController>(); markedVehicle.Setup(cls, wheels);
            Parked.Add(markedVehicle);
        }

        void UpdateTask(float dt)
        {
            if (!string.IsNullOrEmpty(State.S.waypointId))
            {
                var wp = ResolveWaypoint(State.S.waypointId, out var wpName);
                if (wp.HasValue && Vector2.Distance(new Vector2(wp.Value.x, wp.Value.z), new Vector2(Focus.position.x, Focus.position.z)) < 14f)
                {
                    State.S.waypointId = "";
                    State.Toast($"Arrived at {wpName}");
                    RefreshNav();
                }
            }
            if (!HasTask) return;
            var st = State.S;
            switch (st.taskType)
            {
                case "timed":
                    if (Clock.Clock > st.taskDeadline && !(Clock.Clock < 1f && st.taskDeadline > 23f)) FinishTask(false);
                    break;
                case "cargo":
                    if (Vehicle != null && Vehicle.Kmh > st.taskMaxKmh) { State.Toast("Too fast — the glassware shattered"); FinishTask(false); }
                    break;
                case "escape":
                    {
                        bool near = Law != null && Law.PoliceWithin(Focus.position, 60f);
                        st.taskClear = near ? 0f : st.taskClear + dt;
                        st.taskObj = $"Lose the police — {Mathf.Max(0f, 8f - st.taskClear):0} s clear needed{(near ? " · police nearby!" : "")}";
                        if (st.taskClear >= 8f) FinishTask(true);
                        else if (st.heat == 0) FinishTask(false);
                        break;
                    }
                case "steal":
                    if (!st.taskTaken && Vehicle != null && Vehicle == markedVehicle) { st.taskTaken = true; st.taskObj = "Drive the sedan to Dayo at Ladipo"; State.AddHeat(1, "Vehicle taken"); }
                    break;
            }
        }

        // Pressing E at the destination.
        public bool TaskDeliverable()
        {
            if (!HasTask) return false;
            var st = State.S;
            if (st.taskType != "timed" && st.taskType != "cargo" && st.taskType != "steal") return false;
            if (st.taskType == "steal" && (Vehicle == null || Vehicle != markedVehicle)) return false;
            return Data.TryPlace(st.taskDest, out var d) && (d.World() - Focus.position).magnitude < 13f;
        }

        public void FinishTask(bool ok)
        {
            var st = State.S;
            if (ActiveActionOp != null)
            {
                if (ok) { CompleteActionOp(); return; }
                ActiveActionOp = null;
                ClearShootoutEnemies();
                st.taskType = ""; st.taskDest = ""; st.taskItem = ""; st.taskObj = "";
                State.Toast("Action Mission failed");
                RefreshNav(); return;
            }
            if (st.taskMission < 0)
            {
                if (!string.IsNullOrEmpty(st.taskItem)) State.RemoveItem(st.taskItem);
                st.taskType = ""; st.taskDest = ""; st.taskItem = ""; st.taskObj = "";
                FinishRun(ok); RefreshNav(); return;
            }
            var m = Missions.All[Mathf.Clamp(st.taskMission, 0, Missions.All.Count - 1)];
            if (!string.IsNullOrEmpty(st.taskItem)) State.RemoveItem(st.taskItem);
            st.taskType = ""; st.taskDest = ""; st.taskItem = ""; st.taskObj = "";
            if (markedVehicle != null && st.taskType == "") { if (ok) markedVehicle.Owned = true; markedVehicle = null; }
            if (ok) { m.After?.Invoke(this); if (!st.done) st.mission++; }
            else { m.Fail?.Invoke(this); State.Toast("Mission failed"); }
            RefreshNav();
        }

        // Arrest ends escapes, recoveries and deliveries; the hot delivery is lost.
        public void FailTaskOnArrest()
        {
            if (HasTask) FinishTask(false);
            if (State.S.chase) { State.S.chase = false; State.RemoveItem("package"); State.S.mission = Mathf.Max(0, State.S.mission - 1); State.Message("amaka", "Dem catch you with the package? Wahala. Come back when the heat don cool, we go try again."); }
        }
    }
}
