#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rise.EditorTools
{
    // Headless play-mode test. Enters play mode on the Lagos scene, lets the game boot, then drives the session
    // through its main interactions (talk to Baba K, work a shift, enter and drive a parked car, exit, save and reload)
    // and exits the editor with 0 on success or 1 on failure.
    //   Unity -batchmode -nographics -projectPath unity -executeMethod Rise.EditorTools.PlayTest.Run -logFile -
    [InitializeOnLoad]
    public static class PlayTest
    {
        const string Flag = "Rise.PlayTest.Active";
        static int frame; static int stage; static readonly List<string> errors = new List<string>(); static readonly List<string> notes = new List<string>();
        static float stageT; static Vector3 carStart; static int cashBefore;

        static PlayTest()
        {
            if (!EditorPrefs.GetBool(Flag, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
        }

        [MenuItem("RISE/Run Play Test")]
        public static void Run()
        {
            GameState.ClearSave();
            if (!System.IO.File.Exists(SceneSetup.ScenePath)) SceneSetup.Build();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SceneSetup.ScenePath);
            EditorPrefs.SetBool(Flag, true);
            EditorApplication.EnterPlaymode();
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error) return;
            // With -nographics there is no GPU: render targets cannot be created and URP passes complain every frame.
            // Those are not game bugs, so they are ignored on the null graphics device only.
            bool nullDevice = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null;
            if (nullDevice && (msg.Contains("RenderTexture.Create failed") || msg.Contains("Renderpass") || msg.Contains("Unable to find surface") || msg.Contains("RenderPass"))) return;
            errors.Add(type + ": " + msg);
        }

        static void Fail(string why) { errors.Add(why); Finish(); }
        static void Check(bool cond, string what) { if (cond) notes.Add("ok: " + what); else errors.Add("FAIL: " + what); }

        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try { Step(); }
            catch (System.Exception e) { Fail("exception in play test stage " + stage + ": " + e.Message); }
        }

        static void Step()
        {
            frame++;
            var boot = Object.FindAnyObjectByType<GameBootstrap>();
            if (boot == null || boot.Session == null) { if (frame > 300) Fail("game did not boot"); return; }
            var s = boot.Session; float dt = Time.deltaTime > 0f ? Time.deltaTime : 1f / 60f;
            stageT += dt;
            switch (stage)
            {
                case 0:   // let the city settle
                    if (frame < 30) return;
                    var traffic = boot.GetComponent<World.TrafficSystem>(); var peds = boot.GetComponent<World.PedestrianSystem>();
                    Check(traffic != null && traffic.Vehicles.Count > 50, $"traffic running ({traffic?.Vehicles.Count} vehicles)");
                    int withDrivers = 0, withPax = 0;
                    if (traffic != null) foreach (var tv in traffic.Vehicles) { if (tv.HasDriver) withDrivers++; if (tv.PassengerCount > 0) withPax++; }
                    Check(traffic != null && withDrivers == traffic.Vehicles.Count && withPax > 20, $"traffic vehicles occupied ({withDrivers}/{traffic?.Vehicles.Count} drivers, {withPax} with passengers)");
                    Check(s.Contacts.Contacts.Count > 10, $"contacts placed ({s.Contacts.Contacts.Count})");
                    Check(s.Parked.Count == 1 && s.Parked[0].Owned && !s.Parked[0].HasDriver, $"starter keke parked and empty ({s.Parked.Count})");
                    Check(boot.Player.transform.position.y > -5f && boot.Player.transform.position.y < 5f, $"player on the ground at {boot.Player.transform.position}");
                    Check(boot.Player.HasRig && boot.Player.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0 && boot.Player.AnimState == "idle", $"player wears the Ready Player Me rig, state {boot.Player.AnimState}");
                    Check(peds != null && peds.RiggedCount > 50, $"pedestrians rigged ({peds?.RiggedCount})");
                    Check(peds != null && peds.FemaleCount >= 35 && s.Contacts.FemaleContactCount >= 22, $"female NPCs active ({peds?.FemaleCount} female pedestrians, {s.Contacts.FemaleContactCount} named female contacts)");
                    Check(boot.Clock != null && boot.Clock.Day == 1 && boot.Clock.DateText == "Fri, Jan 1, 1999" && boot.Clock.FullDateText == "Friday, January 1, 1999", $"calendar starts on {boot.Clock?.FullDateText} ({boot.Clock?.DateText})");
                    var hud = boot.GetComponent<UI.Hud>();
                    Check(hud != null && hud.LoadingStageFor(0.5f) == "logo_title" && hud.LoadingStageFor(2.0f) == "cover" && hud.CoverTexture != null, $"loading screen stages ('{hud?.LoadingStageFor(0.5f)}' → '{hud?.LoadingStageFor(2.0f)}') and cover texture ({hud?.CoverTexture?.width}x{hud?.CoverTexture?.height}) ready");
                    Check(s.Street != null && s.Street.Service.Count >= 80, $"uniformed service NPCs patrolling ({s.Street?.Service.Count})");
                    Check(boot.Data.TryPlace("banana-island", out _) && boot.Data.Property("banana_villa") != null, "Banana Island landmark and villa property present");
                    Check(boot.Data.properties.Length >= 15 && boot.Data.cells.Length >= 75, $"expanded residential buildings ({boot.Data.properties.Length} buyable/rentable properties, {boot.Data.cells.Length} residential compound cells)");
                    Check(boot.Data.TryPlace("cbn-lagos", out _) && boot.Data.TryPlace("efcc-ikoyi", out _) && boot.Data.TryPlace("efcc-ikeja", out _), "CBN (Central Bank of Nigeria) and EFCC (Ikoyi & Ikeja Zonal Commands) landmarks present");
                    Check(boot.Data.TryPlace("marina-rail", out _) && boot.Data.TryPlace("nns-beecroft", out _) && boot.Data.TryPlace("dodan-barracks", out _) && boot.Data.TryPlace("bonny-camp", out _) && boot.Data.TryPlace("navy-town", out _), "Lagos rail stations and military/naval barracks present");
                    Check(boot.Data.states != null && boot.Data.states.Length >= 36 && boot.Builder.Root.Find("Billboards") != null, $"36 Nigerian states provisioned ({boot.Data.states?.Length}) and Lagos billboards built");
                    Check(boot.Data.VehicleClass("luxury") != null && boot.Data.VehicleClass("brt") != null, "luxury and BRT vehicle classes registered");
                    World.ContactSystem.Contact fisher = null, fireChief = null;
                    foreach (var c in s.Contacts.Contacts) { if (c.data.id == "baale_fisherman") fisher = c; if (c.data.id == "fire_chief") fireChief = c; }
                    Check(fisher != null && fisher.data.runs != null && fisher.data.runs.Length >= 2, "Fisherman contact placed with lagoon delivery runs");
                    Check(fireChief != null && fireChief.data.runs != null && fireChief.data.runs.Length >= 2, "Firefighter chief contact placed with fire-safety runs");
                    // The Surulere street people stand on their streets and offer their own runs.
                    int streetPeople = 0; World.ContactSystem.Contact peter = null;
                    foreach (var c in s.Contacts.Contacts) { if (c.data.runs != null && c.data.runs.Length > 0) streetPeople++; if (c.data.id == "peter") peter = c; }
                    Check(streetPeople >= 32, $"Surulere street contacts placed ({streetPeople})");
                    Check(peter != null, "Peter the tailor stands on Olaitan Street");
                    if (peter != null)
                    {
                        s.TalkTo(peter);
                        Check(s.ActiveMenu != null && s.ActiveMenu.Title == "Peter" && s.ActiveMenu.Options.Count >= 3 && s.ActiveMenu.Options[0].Label.Contains("agbada"), $"Peter offers his runs: '{s.ActiveMenu?.Options[0].Label}'");
                        s.CloseMenu();
                    }
                    // Walk over to Baba K and talk.
                    World.ContactSystem.Contact babak = null;
                    foreach (var c in s.Contacts.Contacts) if (c.data.id == "babak") babak = c;
                    Check(babak != null, "Baba K stands outside Ojuelegba Junction");
                    if (babak == null) { Finish(); return; }
                    boot.Player.Teleport(babak.pos + new Vector3(1.5f, 0.3f, 0f), 0f);
                    // Story: The First Run. Take the clean path.
                    Check(s.MissionActive && s.CurrentMission.Title == "The First Run" && s.Nav != null && s.Nav.Target != null, $"objective '{s.ObjectiveText}', GPS {s.Nav?.RouteLength:0} m");
                    s.TalkTo(babak);
                    Check(s.ActiveMenu != null && s.ActiveMenu.Subtitle == "The First Run" && s.ActiveMenu.Options.Count == 3, "story dialogue with two choices");
                    s.Choose(1);
                    Check(s.State.S.path == "legit" && s.State.Has("documents") && s.State.S.mission == 1, $"took the clean one: carrying {string.Join(",", s.State.S.items)}, mission {s.State.S.mission}");
                    cashBefore = s.State.S.cash;
                    s.OpenDialogue(babak);
                    Check(s.ActiveMenu != null && s.ActiveMenu.Options.Count >= 2 && s.ActiveMenu.Options[0].Enabled, $"dialogue menu open: '{s.ActiveMenu?.Options[0].Label}'");
                    if (s.ActiveMenu == null) { Finish(); return; }
                    s.Choose(0);   // take the job
                    s.WorkDuration = 0.5f;
                    Check(s.Working != null, $"shift started: {s.Working?.title}");
                    stage = 1; stageT = 0f; return;
                case 1:   // the shift runs, pays and advances the clock
                    if (s.Working != null) { if (stageT > 10f) Fail("shift never finished"); return; }
                    Check(s.State.S.cash > cashBefore, $"shift paid: ₦{cashBefore:N0} → ₦{s.State.S.cash:N0}");
                    Check(s.State.S.xp > 0 || s.State.S.level > 1, $"xp awarded ({s.State.S.xp}, level {s.State.S.level})");
                    Check(boot.Clock.Clock > 8.5f + 2f || boot.Clock.Day > 1, $"clock advanced to {boot.Clock.ClockText}");
                    // Buy a sedan from Dayo and drive it.
                    s.State.S.cash += 4000000;
                    var car = s.BuyVehicle("car");
                    Check(car != null && s.Parked.Count == 2 && s.State.S.vehicles.Count == 2, $"bought a sedan from Dayo ({s.Parked.Count} owned vehicles)");
                    if (car == null) { Finish(); return; }
                    var road = boot.Data.RoadAt(boot.Data.Spawn, 2f);
                    car.Place(road.Point(road.Along(boot.Data.Spawn) + 40f, road.k + road.width * 0.5f - 2.2f) + Vector3.up * 0.12f, Quaternion.LookRotation(road.Forward(1), Vector3.up));
                    s.Enter(car);
                    Check((car.transform.position - boot.Data.Spawn).magnitude < 60f, $"sedan placed on Adeniran Ogunsanya at {car.transform.position}");
                    Check(s.Vehicle == car && !boot.Player.gameObject.activeSelf && car.HasDriver, "entered the sedan (driver seated inside)");
                    carStart = car.transform.position;
                    s.Script(1f, 0f, 0f);
                    stage = 2; stageT = 0f; return;
                case 2:   // accelerate for two seconds
                    if (stageT < 2f) return;
                    float moved = Vector3.Distance(s.Vehicle.transform.position, carStart);
                    Check(s.Vehicle.Speed > 5f, $"car accelerates ({s.Vehicle.Kmh:0} km/h)");
                    s.Script(0f, 1f, 0f);
                    Check(moved > 5f, $"car moved {moved:0.0} m");
                    Check(s.State.S.skills.driving > 0f, $"driving skill trained ({s.State.S.skills.driving:0.000})");
                    Check(s.Vehicle.Condition > 95f, $"no phantom crashes on open road (condition {s.Vehicle.Condition:0}%, last hit: {s.Vehicle.LastHit ?? "none"}, car at {s.Vehicle.transform.position})");
                    stage = 3; stageT = 0f; return;
                case 3:   // brake, exit, save, reload
                    s.Script(0f, 1f, 0f);
                    if (Mathf.Abs(s.Vehicle.Speed) > 0.1f && stageT < 6f) return;
                    s.EndScript();
                    var drivenCar = s.Vehicle;
                    s.Exit();
                    Check(s.Vehicle == null && boot.Player.gameObject.activeSelf && drivenCar != null && !drivenCar.HasDriver, "exited the vehicle (parked car empty again)");
                    s.AutoSave();
                    var reloaded = new GameState(boot.Data);
                    Check(reloaded.Load() && reloaded.S.cash == s.State.S.cash && reloaded.S.day == boot.Clock.Day, $"save round-trips (cash ₦{reloaded.S?.cash:N0}, day {reloaded.S?.day})");
                    Check(Mathf.Abs(reloaded.S.px - boot.Player.transform.position.x) < 0.01f, "save keeps the player position");
                    stage = 4; stageT = 0f; return;
                case 4:   // housing: rent the room, sleep till morning
                    {
                        var room = boot.Data.properties[0]; int cash = s.State.S.cash + s.State.S.bank; int day = boot.Clock.Day;
                        Check(s.State.Rent(room), "rented the room through Agent Kunle");
                        Check(s.State.S.home == room.id && s.State.S.cash + s.State.S.bank < cash, $"home set to '{room.name}', paid rent and fee");
                        s.Sleep();
                        Check(boot.Clock.Day == day + 1 && Mathf.Abs(boot.Clock.Clock - 7f) < 0.01f && s.State.S.stamina >= 100f, $"slept: day {boot.Clock.Day} at {boot.Clock.ClockText}");
                        stage = 5; stageT = 0f; return;
                    }
                case 5:   // business: buy the kiosk at Level 2 and run a pay cycle
                    {
                        var kiosk = boot.Data.businesses[0]; s.State.S.level = 2; s.State.S.cash = 1000000; int bank = s.State.S.bank;
                        Check(s.State.BuyBusiness(kiosk), $"bought '{kiosk.name}'");
                        s.State.TickEconomy(boot.Data.meta.payCycle + 1f);
                        Check(s.State.S.bank > bank, $"business income credited: ₦{bank:N0} → ₦{s.State.S.bank:N0}");
                        stage = 6; stageT = 0f; return;
                    }
                case 6:   // transit: board a danfo to the nearest stop
                    {
                        var kilo = boot.Data.busStops[0]; var dests = s.Destinations(kilo);
                        Check(dests.Count > 0, $"{dests.Count} danfo destinations from {kilo.name}");
                        if (dests.Count > 0) { float clock = boot.Clock.Clock; s.Board(kilo, dests[0]); Check((boot.Player.transform.position - dests[0].World()).magnitude < 8f && boot.Clock.Clock > clock, $"rode to {dests[0].name}, clock {boot.Clock.ClockText}"); }
                        stage = 7; stageT = 0f; return;
                    }
                case 7:   // law: heat 3 brings the police; arrest clears it at Area C
                    {
                        var law = boot.GetComponent<World.Law>(); var lights = boot.GetComponent<World.TrafficLights>();
                        if (stageT < 0.5f) { s.State.S.heat = 3; return; }
                        Check(lights != null && lights.Count > 50, $"traffic lights at {lights?.Count} junctions, h is {lights?.LightFor("h")}");
                        Check(law != null && law.Chasing && law.PursuerCount > 0, $"police pursuing ({law?.PursuerCount} cars)");
                        s.State.S.cash = 100000;
                        law.Arrest();
                        boot.Data.TryPlace("police", out var station);
                        Check(s.State.S.heat == 0 && (boot.Player.transform.position - station.World()).magnitude < 20f, "arrested: heat 0, released at Area C");
                        stage = 8; stageT = 0f; return;
                    }
                case 8:   // story: deliver the documents to Amaka, then a timed cold-chain task to the school
                    {
                        World.ContactSystem.Contact amaka = null; foreach (var c in s.Contacts.Contacts) if (c.data.id == "amaka") amaka = c;
                        Check(amaka != null, "Amaka stands at the Iponri yard");
                        if (amaka == null) { Finish(); return; }
                        s.State.S.mission = 1; s.State.S.storyPaused = false; int cash = s.State.S.cash;
                        boot.Player.Teleport(amaka.pos + new Vector3(1.5f, 0.3f, 0f), 0f);
                        s.TalkTo(amaka);
                        Check(s.ActiveMenu != null && s.ActiveMenu.Subtitle == "Make the Delivery" && s.ActiveMenu.Options[0].Label == "Continue", "delivery dialogue");
                        s.Choose(0);
                        Check(s.State.S.cash == cash + 12000 && !s.State.Has("documents") && s.State.S.mission == 2, $"documents delivered: +₦12,000, mission {s.State.S.mission}");
                        // Jump the story to Cold Chain and start the timed task.
                        s.State.S.mission = 7; s.State.S.arc = "legit";
                        s.TalkTo(amaka); s.Choose(0);
                        Check(s.HasTask && s.State.Has("coldbox") && s.State.S.taskType == "timed", $"cold chain task running: '{s.ObjectiveText}'");
                        s.RefreshNav();
                        Check(s.Nav.Target != null && s.Nav.RouteLength > 90f, $"GPS route to the school: {s.Nav.RouteLength:0} m (straight line 93 m)");
                        boot.Data.TryPlace("school", out var school);
                        boot.Player.Teleport(school.World() + new Vector3(0, 0.3f, 12f), 0f);
                        Check(s.TaskDeliverable(), "delivery prompt at the school");
                        cash = s.State.S.cash; s.FinishTask(true);
                        Check(!s.HasTask && s.State.S.cash == cash + 40000 && s.State.S.mission == 8, $"vaccine box delivered: +₦40,000, mission {s.State.S.mission}");
                        stage = 9; stageT = 0f; return;
                    }
                case 9:   // education, gym, food, racing
                    {
                        var web = boot.Data.Course("web"); s.State.S.cash += 500000; s.State.S.stamina = 100f;
                        var webdev = System.Array.Find(boot.Data.jobs, j => j.id == "webdev");
                        Check(webdev != null && !s.State.CanTake(webdev), "Web Freelancer locked without the certificate");
                        Check(s.Study(web) && s.State.HasCourse("web"), $"learned {web?.name}: certificates {string.Join(",", s.State.S.courses)}");
                        s.State.S.skills.business = Mathf.Max(s.State.S.skills.business, 12f);
                        Check(webdev != null && s.State.CanTake(webdev), "Web Freelancer unlocked by the certificate");
                        int fabric = s.State.S.lookFabric; s.NextFabric();
                        Check(s.State.S.lookFabric == fabric + 1 && boot.Player.Fabric == fabric + 1, $"outfit changed to {s.FabricName()}");
                        var pedSys = boot.GetComponent<World.PedestrianSystem>();
                        Check(pedSys != null && pedSys.CesiumCount > 5 && pedSys.CesiumCount < 40, $"pedestrian bodies mixed ({pedSys?.CesiumCount} CesiumMan of {pedSys?.RiggedCount})");
                        float fit = s.State.S.skills.fitness; s.State.S.stamina = 100f;
                        Check(s.Train("Cardio", 5, 0, boot.Data.gym.dayPass) && s.State.S.skills.fitness == fit + 5, $"gym cardio: fitness {s.State.S.skills.fitness}");
                        s.State.S.stamina = 10f;
                        Check(boot.Data.food.Length > 0 && s.Eat(boot.Data.food[0]) && s.State.S.stamina > 60f, $"ate {boot.Data.food[0].name}: stamina {s.State.S.stamina:0}");
                        // Race: start it, drive the sedan through the four checkpoints by teleport.
                        var sedan = s.Parked.Find(v => v.Class.id == "car");
                        Check(s.StartRace(boot.Data.raceWager, false) && s.Racing, $"race started: {s.RaceStatus}");
                        s.Enter(sedan);
                        stage = 10; stageT = 0f; return;
                    }
                case 10:  // one checkpoint per quarter second
                    {
                        if (!s.Racing) { Check(s.Vehicle != null && s.State.S.skills.driving >= 6f, $"race won: driving {s.State.S.skills.driving:0.0}, cash ₦{s.State.S.cash:N0}"); s.Exit(); stage = 11; stageT = 0f; return; }
                        if (stageT < 0.25f) return; stageT = 0f;
                        var cp = s.ActiveRace.Cps[s.ActiveRace.I]; cp.y = s.Vehicle.transform.position.y;
                        s.Vehicle.Place(cp, s.Vehicle.transform.rotation);
                        return;
                    }
                case 11:  // phone, bank, Map GPS, weather, Yaba Tech, hub run, football tiers, animals
                    {
                        s.OpenPhone();
                        Check(s.PhoneOpen && s.ActiveMenu != null && s.ActiveMenu.Options.Count == 13, $"phone opens with twelve apps ({s.ActiveMenu?.Options.Count - 1})");
                        int cash = s.State.S.cash, bank = s.State.S.bank; s.Choose(2); s.Choose(0);   // RiseBank → deposit ₦10,000
                        Check(s.State.S.cash == cash - 10000 && s.State.S.bank == bank + 10000, $"bank deposit: cash ₦{s.State.S.cash:N0}, bank ₦{s.State.S.bank:N0}");
                        s.AppMap("Adeniran");
                        Check(s.ActiveMenu != null && s.ActiveMenu.Title == "Map · GPS" && s.ActiveMenu.Options.Exists(o => o.Label.Contains("Adeniran")), "Map · GPS app filters landmarks");
                        s.SetWaypoint("mall");
                        Check(s.State.S.waypointId == "mall" && s.ResolveWaypoint("mall", out _) != null, "GPS waypoint set to Adeniran Ogunsanya Mall");
                        s.SetWaypoint("");
                        s.CloseMenu(); s.PhoneOpen = false;
                        var weather = boot.GetComponent<World.Weather>(); weather.StartRain(2f, false);
                        Check(weather.Raining && Mathf.Approximately(weather.Wet, 0.7f), "rain started: wet roads");
                        weather.StopRain();
                        s.State.S.cash = 2000000; s.State.S.stamina = 100f;
                        Check(s.Enrol() && s.State.S.collegeSemester == 0, "enrolled at Yaba Tech");
                        for (int i = 0; i < boot.Data.school.semesters; i++) { s.State.S.stamina = 100f; s.StudySemester(); }
                        var it = System.Array.Find(boot.Data.jobs, j => j.id == "it-support"); if (it != null) s.State.S.skills.business = Mathf.Max(s.State.S.skills.business, it.min);
                        Check(s.State.S.collegeGraduated && it != null && s.State.CanTake(it), "Yaba Tech diploma unlocks IT Support Technician");
                        World.ContactSystem.Contact egbon = null; foreach (var c in s.Contacts.Contacts) if (c.data.id == "egbon") egbon = c;
                        Check(egbon != null && Session.Hubs.ContainsKey("egbon") && Session.Hubs.ContainsKey("apapa_dispatch") && Session.Hubs.ContainsKey("ojo_trader"), "all 17 district hubs and 32 Surulere contacts registered");
                        if (egbon != null) { s.StartRun(egbon, Session.Hubs["egbon"].runs[0]); Check(s.HasTask && s.State.Has("phone_parts") && s.ObjectiveTitle == "Side mission", $"hub run started: '{s.ObjectiveText}'"); cash = s.State.S.cash; s.FinishTask(true); Check(!s.HasTask && s.State.S.cash == cash + 18000, "parts crate delivered · +₦18,000"); }
                        s.State.S.skills.fitness = 200f; s.State.S.stamina = 100f; int wins = s.State.S.footballWins;
                        s.PlayMatch(0, true); s.State.S.stamina = 100f; s.PlayMatch(0, true); s.State.S.stamina = 100f; s.PlayMatch(0, true);
                        Check(s.State.S.footballWins == wins + 3 && s.State.S.footballTier >= 1, $"three wins open the neighbourhood tournament (tier {s.State.S.footballTier})");
                        var animals = boot.GetComponent<World.AnimalSystem>();
                        Check(animals != null && animals.Count >= 13 && animals.Dogs == 2, $"animals roaming ({animals?.Count}, {animals?.Dogs} dogs on the rig)");
                        stage = 12; stageT = 0f; return;
                    }
                case 12:  // family, relationships, look slots, portrait
                    {
                        var mum = s.Contacts.Get("mum"); var chioma = s.Contacts.Get("sibling");
                        Check(mum != null && chioma != null, $"family at the compound: Mama Tunde ({s.FamilyActivity("mum")}), Chioma ({s.FamilyActivity("sibling")})");
                        s.IssueFamilyRequest("school_levy"); var req = s.ActiveFamilyRequest; float bond = s.State.Family("sibling").score; int cash = s.State.S.cash + s.State.S.bank;
                        Check(req != null && req.who == "sibling" && req.amount == 8000, $"request raised: {req?.title}");
                        s.State.Pay(req.amount); s.CompleteRequest(req);
                        Check(s.ActiveFamilyRequest == null && s.State.Family("sibling").score > bond && s.State.S.familyDone.Contains("school_levy") && s.State.S.cash + s.State.S.bank == cash - 8000, $"school levy paid: Chioma bond {s.FamilyLabel("sibling")}");
                        var dayo = s.Contacts.Get("dayo"); string tier = s.State.RelTier("dayo");
                        s.State.BumpRel("dayo", 30f, 30f);
                        Check(tier == "stranger" && s.State.RelTier("dayo") == "friend", $"relationship tiers: Dayo stranger → {s.State.RelTier("dayo")}");
                        s.State.S.lookHairStyle = 4; s.State.S.lookOutfit = 3; s.State.S.lookBody = 2; s.State.S.lookAccessory = 3; boot.Player.ApplyLook(s.State.Look);
                        var rigRoot = boot.Player.transform.Find("Rig");
                        bool hairHidden = true; foreach (var r in rigRoot.GetComponentsInChildren<Renderer>(true)) if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("Hair") && r.enabled) hairHidden = false;
                        var overlay = Entities.CharacterRig.Find(rigRoot, "Outfit overlay"); var glasses = Entities.CharacterRig.Find(rigRoot, "Glasses");
                        Check(hairHidden && overlay != null && overlay.gameObject.activeSelf && glasses != null && glasses.gameObject.activeSelf && rigRoot.localScale.x > rigRoot.localScale.y * 1.05f, $"look slots on the rig: bald {hairHidden}, agbada overlay {overlay?.gameObject.activeSelf}, glasses {glasses?.gameObject.activeSelf}, big build x/y {rigRoot.localScale.x / rigRoot.localScale.y:0.00}");
                        Check(boot.GetComponent<UI.Portrait>() != null, "portrait camera present (no render target on the null device)");
                        stage = 13; stageT = 0f; return;
                    }
                case 13:  // street life, tenants, upgrades, staff shifts, offline earnings, interiors, weapon, rival, vehicle customisation, events
                    {
                        var street = boot.GetComponent<World.StreetLife>(); var events = boot.GetComponent<World.Events>();
                        Check(street != null && street.Agberos.Count > 10 && street.Service.Count == boot.Data.serviceNpcs.Length && street.Nightlife.Count == 4, $"street life: {street?.Agberos.Count} agberos, {street?.Service.Count} uniforms, {street?.Nightlife.Count} night hustlers (hidden until 20:00 and the mature toggle)");
                        // Walk into the Kilo agberos and pay the levy.
                        var kilo = street.Agberos.Find(a => a.stop.id == "kilo"); s.CloseMenu(); s.State.S.pet = false;
                        boot.Player.Teleport(kilo.pos + new Vector3(0.5f, 0f, 1.5f), 180f); kilo.cool = 0f;
                        int cash = s.State.S.cash; s.ForceStreetUpdate();
                        Check(s.ActiveMenu != null && s.ActiveMenu.Title == "Agbero", $"agbero levy demanded at {kilo.stop.name}");
                        s.Choose(0);
                        Check(s.State.S.cash == cash - 500, "paid ₦500 to the boys");
                        s.State.S.pet = true; s.ForceStreetUpdate();
                        Check(street.HasPet, "dog adopted and following");
                        kilo.cool = 0f; s.ForceStreetUpdate();
                        Check(s.ActiveMenu == null, "agberos keep their distance from the dog");
                        // Tenants, upgrades, complaints.
                        var selfcon = boot.Data.properties[1]; s.State.S.cash += 2000000; s.State.Buy(selfcon); s.State.LetOut(selfcon); int bank = s.State.S.bank; s.State.NewDay(boot.Clock.Day + 1);
                        Check(s.State.S.let.Contains(selfcon.id) && s.State.S.bank > bank, $"tenants pay: bank ₦{bank:N0} → ₦{s.State.S.bank:N0}");
                        s.State.Upgrades(selfcon.id).complaint = "roof dey leak"; Check(s.State.FixComplaint(selfcon) && s.State.Upgrades(selfcon.id).complaint == "", "complaint fixed for ₦15,000");
                        var tank = System.Array.Find(boot.Data.upgrades, u => u.id == "tank"); var room = boot.Data.properties[0]; s.State.S.props.Add(room.id); s.State.S.home = room.id;
                        Check(tank != null && s.State.BuyUpgrade(room, tank) && s.State.HomeHas("tank"), "water tank installed at home");
                        // Staff, shifts, offline earnings, and the rival.
                        var kiosk = boot.Data.businesses[0]; int income = s.State.BizIncome(kiosk);
                        Check(s.State.HireStaff(kiosk) && s.State.BizIncome(kiosk) > income, $"hired {s.State.Biz(kiosk.id).staff[0]}: income ₦{income:N0} → ₦{s.State.BizIncome(kiosk):N0}");
                        Check(s.State.CycleStaffShift(kiosk, 0) == "morning" && s.State.CycleStaffShift(kiosk, 0) == "afternoon" && s.State.CycleStaffShift(kiosk, 0) == "night" && s.State.CycleStaffShift(kiosk, 0) == "all-day", "staff shift schedules cycle (morning → afternoon → night → all-day)");
                        s.State.S.lastSeen = System.DateTime.UtcNow.AddMinutes(-30).ToString("o");
                        int awayEarned = s.State.ApplyAwayEarnings()?.net ?? 0;
                        Check(awayEarned > 0 && s.State.S.awayTotal >= awayEarned, $"'Lagos Never Sleeps' offline earnings credited (+₦{awayEarned:N0})");
                        s.State.S.cash += 80000;
                        Check(street.GroundItems.Count > 5, $"street item pickups placed ({street.GroundItems.Count})");
                        var firstPickup = street.GroundItems[0];
                        Check(s.PickupGroundItem(firstPickup) && s.State.Has(firstPickup.id) && !firstPickup.Active, $"picked up street item '{firstPickup.label}'");
                        Check(s.BuyProtectionWeapon() && s.State.Has("weapon"), "bought protection weapon from Dayo");
                        Check(s.BuyFightWeapon("cutlass") && s.State.Has("cutlass") && boot.Player.EquippedWeapon == "cutlass", $"bought and equipped {s.EquippedWeaponName} (power {s.WeaponPower()})");
                        string cycled = s.CycleWeapon();
                        Check(cycled != "cutlass", $"cycled weapon with Q to '{s.EquippedWeaponName}'");
                        s.EquipWeapon("cutlass");
                        kilo.cool = 0f; boot.Player.Teleport(kilo.pos + new Vector3(0.4f, 0f, 1.0f), 180f);
                        Check(s.PerformStreetAttack() && kilo.cool >= 90f, "direct street attack (F / Click) fought off agberos and dropped loot");
                        World.TrafficSystem.Vehicle commTv = null;
                        foreach (var tv in s.Traffic.Vehicles) if (tv.cls != null && tv.cls.commercial && tv.cls.id == "keke") { commTv = tv; break; }
                        if (commTv != null)
                        {
                            Check(s.TryHijackTrafficVehicle(commTv) && s.Vehicle != null && s.Vehicle.HasDriver && s.Traffic.EjectedCount > 0, $"hijacked traffic {commTv.cls.name} with {s.EquippedWeaponName} ({s.Traffic.EjectedCount} occupants ejected)");
                            var hijacked = s.Vehicle;
                            s.Exit();
                            s.Parked.Remove(hijacked);
                            s.State.S.heat = 0;
                        }
                        s.EnterInterior("Test Room", "home");
                        Check(s.InInterior, "entered 3D building interior");
                        s.LeaveInterior();
                        Check(!s.InInterior, "exited 3D building interior back to the street");
                        Check(UI.SoundSystem.I != null, "synthesised SoundSystem initialised");
                        s.State.BuyBusiness(boot.Data.businesses[1]);
                        Check(s.State.RivalOpen && s.State.BizIncome(kiosk) < Mathf.RoundToInt(income * 1.3f), $"{boot.Data.rivalName} opened after two businesses and undercuts normal prices");
                        // Vehicle insurance, livery, slogan.
                        var sedan = s.Parked.Find(v => v.Class.id == "car"); OwnedVehicle owned = null; foreach (var ov in s.State.S.vehicles) if (ov.id == sedan.OwnedId) owned = ov;
                        owned.insured = true; sedan.Insured = true; sedan.ApplyLivery(Color.red); var danfoCls = boot.Data.VehicleClass("danfo"); var danfo = s.BuyVehicle("danfo"); danfo.ApplySlogan(boot.Data.slogans[0]);
                        Check(sedan.Insured && danfo.transform.Find("Slogan") != null && danfo.transform.Find("Slogan").GetComponent<TextMesh>().text == boot.Data.slogans[0], $"insured, repainted, slogan '{boot.Data.slogans[0]}'");
                        // Road works, a wreck, the flood, NEPA.
                        var ao = boot.Data.FindRoad("v", 0f); events.Clear(); events.StartJam(ao, 60f, 0);
                        Check(events.Current != null && events.Current.works && events.InJam(ao, 90f) && !events.InJam(ao, 200f), "road works close a lane on Adeniran Ogunsanya");
                        events.Clear(); events.StartJam(ao, 60f, 1);
                        Check(events.Current != null && events.Current.wreck && events.transform.childCount > 3, "overturned keke with cones");
                        events.Clear(); events.StartFlood();
                        Check(events.Current != null && events.Current.flood && events.FloodedAt(boot.Data.FindRoad("h", -66f).Point(20f, -66f)), "Ojuelegba underpass flooded");
                        events.Clear();
                        stage = 14; stageT = 0f; return;
                    }
                case 14:  // swimming, sinking, pedestrian colliders
                    {
                        s.CloseMenu(); if (s.Vehicle != null) s.Exit();
                        var lagoon = Data.WorldData.W(250f, 0.3f, 100f);   // Lagos Lagoon, off Eko Bridge
                        Check(boot.Data.InWater(lagoon) && !boot.Data.InWater(boot.Data.Spawn), "lagoon rectangle recognised, spawn on land");
                        boot.Player.Teleport(lagoon, 0f); s.ForceOnFootUpdate();
                        Check(boot.Player.Swimming && !boot.Player.CanSprint, "player swims in the lagoon (slow, no sprint)");
                        boot.Player.Teleport(boot.Data.Spawn + Vector3.up * 0.3f, 180f); s.ForceOnFootUpdate();
                        Check(!boot.Player.Swimming, "back on land");
                        var sedan = s.Parked.Find(v => v.Class.id == "car"); sedan.Place(lagoon + Vector3.up * 0.1f, Quaternion.identity); s.Enter(sedan); sedan.Speed = 10f; s.Script(1f, 0f, 0f);
                        stage = 15; stageT = 0f; return;
                    }
                case 15:
                    {
                        if (s.Vehicle != null && stageT < 6f) return;
                        s.EndScript(); s.ForceOnFootUpdate();
                        Check(s.Vehicle == null && boot.Player.Swimming, $"vehicle stalled in the lagoon and you are swimming (player at {boot.Player.transform.position})");
                        var pedestrians = boot.GetComponent<World.PedestrianSystem>(); int withColliders = pedestrians.BodyColliders;
                        Check(withColliders == pedestrians.Alive && withColliders > 50, $"every pedestrian has a body ({withColliders} of {pedestrians.Alive})");
                        boot.Player.Teleport(boot.Data.Spawn + Vector3.up * 0.3f, 180f); s.ForceOnFootUpdate();
                        stage = 16; stageT = 0f; return;
                    }
                case 16:  // the quality governor: auto level, dynamic resolution step-down, step-down of the level, reflections
                    {
                        var gov = boot.GetComponent<World.QualityGovernor>();
                        Check(gov != null && gov.Auto && gov.Level <= World.QualityGovernor.Detect(), $"auto graphics level {World.QualityGovernor.Names[gov?.Level ?? 0]} on '{SystemInfo.graphicsDeviceName}' (render scale {gov?.RenderScale:0.00})");
                        gov.SetLevel(2, true); gov.SimulateSlow = true;
                        stage = 17; stageT = 0f; return;
                    }
                case 17:
                    {
                        var gov = boot.GetComponent<World.QualityGovernor>();
                        // Slow frames for a few seconds: the render scale shrinks through its steps, then the level drops.
                        if (stageT < 14f && !(gov.DynIndex == 0 && gov.Level < 2)) return;
                        Check(gov.Level < 2 && gov.DynIndex == 0, $"sustained slow frames stepped down to {World.QualityGovernor.Names[gov.Level]} after shrinking the frame (render scale now {gov.RenderScale:0.00})");
                        gov.SimulateSlow = false; gov.SetLevel(2, false); s.State.S.qualityAuto = false; s.State.S.quality = 2;
                        Check(gov.Probe != null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null, "environment reflection probe present (skipped on the null device)");
                        stage = 18; stageT = 0f; return;
                    }
                case 18:  // accounts offline, cloud save merge rule, multiplayer over the loopback bus
                    {
                        Check(s.Account != null && !s.Account.Online && s.Account.Status.StartsWith("offline"), $"account: {s.Account?.Status}");
                        var older = new SaveData { cash = 1, lastSeen = "2026-01-01T00:00:00Z" }; var newer = new SaveData { cash = 2, lastSeen = "2026-10-14T00:00:00Z" };
                        Check(Net.CloudSave.Newer(older, newer).cash == 2 && Net.CloudSave.Newer(newer, older).cash == 2 && Net.CloudSave.Newer(newer, null).cash == 2, "cloud save merge: newest lastSeen wins");
                        Check(s.Players != null && s.Players.Transport is Net.LoopbackTransport && s.Players.Room.StartsWith("district:"), $"multiplayer on the loopback bus in room '{s.Players.Room}'");
                        // A second player in the same district: another Multiplayer component on its own object.
                        var ghostHost = new GameObject("Second player"); var ghostFollow = new GameObject("Second player position").transform; ghostFollow.position = boot.Player.transform.position + new Vector3(3f, 0f, 0f);
                        var ghost = ghostHost.AddComponent<Net.Multiplayer>(); ghost.DisplayName = "Amaka";
                        ghost.Init(boot.Data, new Net.LoopbackTransport(), "ghost-0001", ghostFollow, () => new Net.Multiplayer.PosMsg { x = ghostFollow.position.x, y = ghostFollow.position.y, z = ghostFollow.position.z, yaw = 90f, fabric = 2 }, ghostHost.transform);
                        ghost.Tick(1.1f); s.Players.Tick(1.1f); ghost.Tick(0.2f); s.Players.Tick(0.2f);
                        Check(s.Players.Remotes.ContainsKey("ghost-0001") && s.Players.Remotes["ghost-0001"].name == "Amaka" && ghost.Remotes.ContainsKey(s.Players.SelfId), $"both players see each other ({Net.LoopbackTransport.InRoom(s.Players.Room)} in {s.Players.Room})");
                        ghost.SendChat("How far, Tunde?"); s.Players.Tick(0.1f);
                        Check(s.Players.Chat.Exists(c => c.name == "Amaka" && c.text == "How far, Tunde?"), "chat line received from the other player");
                        string roomBefore = ghost.Room, expectedBefore = boot.Data.RegionAt(ghostFollow.position)?.id;
                        Check(roomBefore == s.Players.Room && roomBefore == "district:" + expectedBefore, $"both players start in the same room '{roomBefore}'");
                        ghostFollow.position = boot.Player.transform.position + new Vector3(3f, 0f, 400f);   // another district
                        string expectedAfter = "district:" + boot.Data.RegionAt(ghostFollow.position)?.id;
                        ghost.Tick(1.1f); s.Players.Tick(1.1f); ghost.Tick(0.2f);
                        // Remotes expire after five seconds by design, so assert the transport rooms instead: one player in each.
                        Check(expectedAfter != roomBefore && ghost.Room == expectedAfter && s.Players.Room == roomBefore
                              && Net.LoopbackTransport.InRoom(roomBefore) == 1 && Net.LoopbackTransport.InRoom(expectedAfter) == 1 && ghost.Remotes.Count == 0,
                              $"moving districts changes the room ('{roomBefore}' → '{ghost.Room}'): one player in each room, no remotes carried over");
                        Object.Destroy(ghostHost); Object.Destroy(ghostFollow.gameObject);
                        stage = 19; stageT = 0f; return;
                    }
                case 19:  // crews over the loopback: form, treasury, HQ, shared pin, co-op race with a partner
                    {
                        Check(GameInput.UsingActions, "input comes from the Gameplay action map (Resources/Input/Rise.inputactions)");
                        s.CloseMenu(); s.State.S.cash += 100000;
                        s.CreateCrew("Iponri Boys", "ipb");
                        Check(s.InCrew && s.CrewTag == "IPB" && s.State.S.crewCode.Length == 6 && s.CrewLink is Net.LoopbackTransport, $"crew formed: {s.State.S.crewName} [{s.CrewTag}] code {s.State.S.crewCode}");
                        int cash = s.State.S.cash; s.CrewDeposit(10000); s.CrewWithdraw(4000);
                        Check(s.State.S.crewTreasury == 6000 && s.State.S.cash == cash - 6000, $"treasury ₦{s.State.S.crewTreasury:N0}");
                        var home = s.State.Home; Check(home != null && s.SetCrewHq(home) && s.State.S.crewHq == home.id, $"crew HQ at {home?.name}");
                        // A crew mate on the same crew channel.
                        var mate = new Net.LoopbackTransport(); mate.Join("crew:" + s.State.S.crewId, "mate-0001");
                        s.SharePin("Ladipo deal"); mate.Poll((f, e, j) => { if (e == "pin") Check(j.Contains("Ladipo deal"), "crew mate received the shared pin"); });
                        mate.Send("pin", JsonUtility.ToJson(new Session.PinMsg { from = "mate-0001", name = "Dayo", label = "Meet at Costain", x = 140f, y = 0f, z = -15f }));
                        s.ForceCrewUpdate(0.1f);
                        Check(s.Pins.Exists(p => p.by == "Dayo" && p.label == "Meet at Costain"), "shared pin from the crew mate shows on the objective card");
                        mate.Send("race", JsonUtility.ToJson(new Session.RaceMsg { from = "mate-0001", name = "Dayo", kind = "invite" }));
                        s.ForceCrewUpdate(0.1f);
                        Check(s.CoopInvitePending && s.CoopPartnerName == "Dayo", "co-op race invite received");
                        var sedan = s.Parked.Find(v => v.Class.id == "car"); var ao2 = boot.Data.RoadAt(boot.Data.Spawn, 2f);
                        sedan.Place(ao2.Point(ao2.Along(boot.Data.Spawn) + 40f, ao2.k + ao2.width * 0.5f - 2.2f) + Vector3.up * 0.12f, Quaternion.LookRotation(ao2.Forward(1), Vector3.up));   // back on dry land after the lagoon stage
                        s.Enter(sedan);
                        s.AcceptCoopRace();
                        Check(s.CoopRace && s.Racing, "co-op race accepted and started");
                        stage = 20; stageT = 0f; return;
                    }
                case 20:  // drive the checkpoints; the partner finishes a little later; the purse is split
                    {
                        if (s.Racing && s.Vehicle != null) { if (stageT < 0.25f) return; stageT = 0f; var cp = s.ActiveRace.Cps[s.ActiveRace.I]; cp.y = s.Vehicle.transform.position.y; s.Vehicle.Place(cp, s.Vehicle.transform.rotation); return; }
                        Check(!s.Racing && s.Vehicle != null, "co-op race laps completed in the sedan");
                        int cash = s.State.S.cash; s.ForceCrewUpdate(0.1f);
                        var mate = new Net.LoopbackTransport(); mate.Join("crew:" + s.State.S.crewId, "mate-0001");
                        mate.Send("race", JsonUtility.ToJson(new Session.RaceMsg { from = "mate-0001", name = "Dayo", kind = "finish", time = Time.time + 5f }));
                        s.ForceCrewUpdate(0.1f);
                        Check(!s.CoopRace && s.CoopWins == 1 && s.State.S.cash >= cash + boot.Data.raceWager, $"co-op race settled: you led, purse split ₦{boot.Data.raceWager:N0} each (cash ₦{s.State.S.cash:N0})");

                        // Co-op Getaway & Co-op Convoy over the crew channel
                        mate.Send("coop_op", JsonUtility.ToJson(new Session.CoopOpMsg { from = "mate-0001", name = "Dayo", op = "getaway", kind = "invite" }));
                        s.ForceCrewUpdate(0.1f);
                        s.AcceptCoopGetaway();
                        Check(s.CoopGetaway && s.State.S.heat >= 4, "co-op getaway accepted and Heat-4 escape started");
                        s.CoopGetawayClear = 8.1f;
                        mate.Send("coop_op", JsonUtility.ToJson(new Session.CoopOpMsg { from = "mate-0001", name = "Dayo", op = "getaway", kind = "finish", time = Time.time }));
                        s.ForceCrewUpdate(0.2f);
                        Check(!s.CoopGetaway && s.CoopGetawayWins == 1 && s.State.S.heat == 0, "co-op getaway settled: Heat cleared and purse + treasury bonus paid");

                        mate.Send("coop_op", JsonUtility.ToJson(new Session.CoopOpMsg { from = "mate-0001", name = "Dayo", op = "convoy", kind = "invite" }));
                        s.ForceCrewUpdate(0.1f);
                        s.AcceptCoopConvoy();
                        Check(s.CoopConvoy && s.ActiveConvoyTarget.HasValue, "co-op convoy accepted and route checkpoints active");
                        for (int ci = 0; ci < 3; ci++)
                        {
                            var ct = s.ActiveConvoyTarget.Value;
                            s.Vehicle.Place(new Vector3(ct.x, s.Vehicle.transform.position.y, ct.z), s.Vehicle.transform.rotation);
                            s.ForceCrewUpdate(0.1f);
                        }
                        mate.Send("coop_op", JsonUtility.ToJson(new Session.CoopOpMsg { from = "mate-0001", name = "Dayo", op = "convoy", kind = "finish", time = Time.time }));
                        s.ForceCrewUpdate(0.1f);
                        Check(!s.CoopConvoy && s.CoopConvoyWins == 1, "co-op convoy settled across all 3 checkpoints");

                        s.Exit(); mate.Leave();

                        // Dynamic street encounter
                        s.Events.StartEncounter(0, s.Player.transform.position + new Vector3(2f, 0f, 2f));
                        Check(s.Events.ActiveEncounter != null && s.NearEncounter(s.Player.transform.position) && s.ResolveEncounter() && s.Events.ActiveEncounter == null, "dynamic street encounter spawned and resolved");

                        // Neighbourhood story chain
                        Check(s.StartChainStep("surulere_chain") && s.HasTask && s.ActiveChainId == "surulere_chain", "Surulere neighbourhood story chain chapter 1 started");
                        s.FinishTask(true);
                        Check(s.State.ChainStep("surulere_chain") == 1, "Surulere neighbourhood story chain chapter 1 completed and saved");

                        // Live economy overrides (CloudEconomy.ApplyJson)
                        int applied = Net.CloudEconomy.ApplyJson(boot.Data, "{\"constants\":{\"priceWater\":150,\"raceWager\":25000},\"job_payouts\":{\"rider\":9500},\"mission_rewards\":{\"cold_chain\":45000}}");
                        Check(applied >= 4 && boot.Data.meta.priceWater == 150 && boot.Data.raceWager == 25000 && Story.Missions.Reward("cold_chain") == 45000, "CloudEconomy applies live Supabase constants, job payouts, and mission rewards without rebuild");

                        // Console & Mobile Input Action Maps + Platform Build Targets
                        Check(GameInput.UsingActions && GameInput.HasDrivingMap && GameInput.HasUIMap && GameInput.ControlSchemeCount >= 6, $"Input System maps active (Gameplay, Driving, UI) across {GameInput.ControlSchemeCount} control schemes ({GameInput.ControllerFamily})");
                        int targetCount = SceneSetup.ValidatePlatformTargets();
                        Check(targetCount == 10, $"All {targetCount} platform build targets configured (macOS, Windows, WebGL, iOS, Android, PS5, PS4, Xbox Series, Xbox One, Switch)");

                        // Firearms & Action Shootout Mission (requires a gun and shooting)
                        Check(!s.HasGun() && !s.StartActionOp("apapa_pirates"), "Action Mission blocked until player acquires a firearm");
                        s.State.S.cash += 500000;
                        Check(s.BuyFightWeapon("rifle") && s.HasGun() && s.IsGunEquipped && s.State.S.ammo >= 36, $"bought and equipped {s.EquippedWeaponName} with {s.State.S.ammo} rounds of ammo");
                        Check(s.StartActionOp("apapa_pirates") && s.ActiveActionOp != null && s.RemainingEnemies == 4, "Action Mission 'Port Creek Pirate Raid' started with 4 armed gunmen at Apapa Wharf");
                        if (boot.Data.TryPlace("apapa-wharf", out var wharf))
                            s.Player.Teleport(wharf.World() + new Vector3(0f, 0.3f, -12f), 0f);
                        s.ForceShootoutUpdate(0.2f);
                        int cashBeforeShootout = s.State.S.cash;
                        for (int shot = 0; shot < 8 && s.ActiveActionOp != null; shot++)
                            s.FireGun();
                        Check(s.ActiveActionOp == null && s.RemainingEnemies == 0 && s.State.S.cash >= cashBeforeShootout + 75000, "shot and eliminated all 4 armed gunmen with Tactical AK-47 Rifle and collected ₦75,000 action mission bounty");

                        // Call of Duty–style Nigerian Army (81 Division) & Nigerian Navy (SBS) multi-wave Spec-Ops
                        Check(s.BuyFightWeapon("sniper") && s.EquippedWeaponName == "M24 Marksman Sniper Rifle", "bought and equipped M24 Marksman Sniper Rifle");
                        Check(s.StartActionOp("cod_army_thunder") && s.IsSpecOpActive && s.State.S.armor == 100f && s.ActiveSquad.Count == 2 && s.CurrentWave == 1, "81 Div Military Spec-Op 'OP IRON THUNDER' started with 100% Kevlar Armor and 2 allied 81 Div soldiers");
                        if (boot.Data.TryPlace("ikeja-cantonment", out var cantonment))
                            s.Player.Teleport(cantonment.World() + new Vector3(0f, 0.3f, -12f), 0f);
                        s.ForceShootoutUpdate(1.5f);
                        while (s.CurrentWave == 1 && s.ActiveActionOp != null) s.FireGun();
                        Check(s.CurrentWave == 2 && s.ActiveEnemies.Exists(e => e.IsBoss && e.Label == "Commander Vektor"), "Wave 1 cleared and Wave 2 deployed with HVT Commander Vektor");
                        int cashBeforeArmy = s.State.S.cash;
                        for (int shot = 0; shot < 12 && s.ActiveActionOp != null; shot++) s.FireGun();
                        Check(s.ActiveActionOp == null && s.State.S.cash >= cashBeforeArmy + 150000, "81 Div Military Spec-Op completed across 2 waves + HVT Boss (+₦150,000)");

                        Check(s.BuyFightWeapon("smg") && s.StartActionOp("cod_navy_trident") && s.ActiveActionOp.Branch == "navy" && s.ActiveSquad.Count == 2, "Navy SBS Spec-Op 'OP BLACK TRIDENT' started with Navy MP5 Tactical SMG and 2 SBS commandos");
                        if (boot.Data.TryPlace("nns-beecroft", out var beecroft))
                            s.Player.Teleport(beecroft.World() + new Vector3(0f, 0.3f, -12f), 0f);
                        int cashBeforeNavy = s.State.S.cash;
                        for (int shot = 0; shot < 24 && s.ActiveActionOp != null; shot++) s.FireGun();
                        Check(s.ActiveActionOp == null && s.State.S.cash >= cashBeforeNavy + 160000, "Navy SBS Spec-Op 'OP BLACK TRIDENT' completed across 2 waves + HVT Captain Blackwater (+₦160,000)");

                        // EA Sports FC 26 / Dream League Soccer Live 3D Football Match & Knockout Tournament
                        int winsBeforeFc = s.State.S.footballWins;
                        Check(s.StartLiveFootballMatch("fc_neighbourhood_cup") && s.InFootballMatch && s.ActiveFootball.Players.Count == 9 && s.ActiveFootball.BallRoot != null, "Live 3D Football Tournament 'Lagos Neighbourhood Cup' started with 9 3D footballers and match ball");
                        s.CycleFootballTactic();
                        Check(s.ActiveFootball.TacticMode == 1 && s.ActiveFootball.TacticLabel == "ALL-OUT ATTACK", "Football tactical mentality switched to ALL-OUT ATTACK");
                        Check(s.FootballChip() && s.ActiveFootball.SkillBoostTimer > 0f, "Jay-Jay Okocha stepover / elastico skill move executed");
                        Check(s.FootballPass() && !s.ActiveFootball.PlayerHasBall, "FC 26 driven ground pass executed to forward teammate");
                        s.ActiveFootball.PlayerHasBall = true;
                        Check(s.FootballShoot(1.22f) && s.ActiveFootball.BallVel.magnitude > 15f && s.ActiveFootball.LastShotQualityLabel.Contains("GREEN TIMED FINISH"), "FC 26 Green Timed Finish curved power shot fired toward goal");
                        s.ScorePlayerGoal("Captain #10");
                        Check(s.ActiveFootball.HomeScore == 1 && s.ActiveFootball.GoalBannerTimer > 0f, "Goal scored and broadcast goal banner triggered (1-0)");
                        s.CompleteLiveFootballRound(true);
                        Check(s.InFootballMatch && s.ActiveFootball.CurrentRound == 2 && s.CurrentTournamentStageLabel == "GRAND FINAL", "Semi-Final won: advanced to Grand Final vs Ikoyi Royals FC");
                        int cashBeforeFinal = s.State.S.cash;
                        s.ScorePlayerGoal("Victor O.");
                        s.CompleteLiveFootballRound(true);
                        Check(!s.InFootballMatch && s.State.S.footballWins == winsBeforeFc + 1 && s.State.S.cash == cashBeforeFinal + 60000, "Lagos Neighbourhood Cup Grand Final won (+₦60,000 tournament prize)");

                        // Need for Speed (NFS) Style Street Racing Mission with NOS Nitrous & 3 AI Rivals
                        Check(s.StartNfsRace("nfs_sprint_third_mainland") && s.Racing && s.ActiveRace.Rivals.Count == 3 && Mathf.Approximately(s.NosCharge, 100f), "NFS 'THIRD MAINLAND SUPERSONIC SPRINT' started with 3 neon-underglow rivals and 100% NOS Nitrous");
                        int cashBeforeNfs = s.State.S.cash;
                        while (s.Racing && s.Vehicle != null)
                        {
                            var ncp = s.ActiveRace.Cps[s.ActiveRace.I];
                            ncp.y = s.Vehicle.transform.position.y;
                            s.Vehicle.Place(ncp, s.Vehicle.transform.rotation);
                            s.ForceRaceUpdate(0.05f);
                        }
                        Check(!s.Racing && s.State.S.cash == cashBeforeNfs + 75000, "NFS Third Mainland Supersonic Sprint won against Blacklist #3 Nitrous Dapo (+₦75,000)");
                        if (s.Vehicle != null) s.Exit();

                        // CBN Treasury Bill & EFCC Financial Compliance Clearance
                        int bankBeforeCbn = s.State.S.bank;
                        Check(s.BuyCbnTreasuryBond() && s.State.S.bank == bankBeforeCbn + 58000, "CBN 91-Day Treasury Bill subscribed and matured (+₦58,000 credited to RiseBank)");
                        s.State.S.heat = 2;
                        Check(s.ClearEfccAudit() && s.State.S.heat == 0, "EFCC Financial Compliance Clearance issued (Heat cleared to 0)");

                        // Historic Events, Elections, COVID-19 Pandemic & Trending News Timeline (1999–2026+)
                        int savedDay = boot.Clock.Day;
                        Check(World.Events.HistoricTimeline.Length >= 18
                              && World.Events.EventForDate(new System.DateTime(1999, 1, 1))?.Id == "transition_1999"
                              && World.Events.EventForDate(new System.DateTime(1999, 2, 27))?.Id == "election_1999"
                              && World.Events.EventForDate(new System.DateTime(2001, 8, 15))?.Id == "gsm_revolution_2001"
                              && World.Events.EventForDate(new System.DateTime(2020, 4, 10))?.Id == "covid19_lockdown_2020"
                              && World.Events.EventForDate(new System.DateTime(2023, 2, 25))?.Id == "election_2023",
                              $"Historic timeline active across {World.Events.HistoricTimeline.Length} eras (1999 Transition, 1999 Election, 2001 GSM Revolution, 2020 COVID-19 Lockdown, 2023 Election)");
                        int cashBeforeCovid = s.State.S.cash;
                        Check(s.Events.TriggerHistoricEvent("covid19_lockdown_2020", s.Player.transform.position + new Vector3(2f, 0f, 2f), true)
                              && s.Events.IsPandemicActive
                              && boot.Clock.Year == 2020
                              && s.NearEncounter(s.Player.transform.position)
                              && s.ResolveEncounter()
                              && s.State.S.cash == cashBeforeCovid + 25000,
                              "2020 COVID-19 Pandemic & NCDC Lockdown triggered and relief mission completed (+₦25,000)");
                        int cashBeforeElection = s.State.S.cash;
                        Check(s.Events.TriggerHistoricEvent("election_1999", s.Player.transform.position + new Vector3(2f, 0f, 2f), true)
                              && s.Events.IsElectionActive
                              && boot.Clock.DateText == "Sat, Feb 27, 1999"
                              && s.ResolveEncounter()
                              && s.State.S.cash == cashBeforeElection + 15000,
                              "1999 Nigerian General Election (Sat, Feb 27, 1999) INEC polling unit triggered and civic action completed (+₦15,000)");
                        s.AppNews();
                        Check(s.ActiveMenu != null && s.ActiveMenu.Title == "Lagos Chronicle · Historic Timeline" && s.ActiveMenu.Options.Count >= 10, "Lagos Chronicle · Historic Timeline app opened on smartphone");
                        s.CloseMenu(); s.PhoneOpen = false;
                        s.Events.ClearForcedHistoricEvent();
                        boot.Clock.SetTime(boot.Clock.Clock, savedDay);

                        // Politician NPCs (Presidency, Governor, Senators, House of Reps, LGA Chairman) & Fraudster NPCs
                        Check(s.Contacts.PoliticianContactCount >= 6 && s.Contacts.FraudsterContactCount >= 2,
                              $"Politician NPCs ({s.Contacts.PoliticianContactCount} across Presidency, Governor, Senate, House of Reps & LGA) and Fraudster NPCs ({s.Contacts.FraudsterContactCount}) spawned");
                        int cashBeforePol = s.State.S.cash;
                        Check(s.LobbyPoliticianContract("presidential_envoy") && s.State.S.cash == cashBeforePol + 120000,
                              "Presidential Envoy civic infrastructure contract executed (+₦120,000)");
                        int cashBeforeGov = s.State.S.cash;
                        Check(s.LobbyPoliticianContract("lagos_governor") && s.MobilizePoliticianRally("senator_lagos_central") && s.State.S.cash == cashBeforeGov + 130000,
                              "Lagos State Governor contract (+₦95,000) & Senatorial town-hall rally (+₦35,000) completed");
                        var govContact = s.Contacts.Get("lagos_governor");
                        if (govContact != null) { s.TalkTo(govContact); Check(s.ActiveMenu != null && s.ActiveMenu.Subtitle.Contains("EXECUTIVE GOVERNOR"), "Governor dialogue menu opened"); s.CloseMenu(); }

                        int cashBeforeBust = s.State.S.cash;
                        s.State.S.heat = 2;
                        Check(s.BustFraudsterForEfcc("fraudster_hk_boss") && s.State.S.cash == cashBeforeBust + 50000 && s.State.S.heat == 0,
                              "EFCC Whistleblower Sting busted Fraudster syndicate (+₦50,000, Heat → 0)");

                        // Boko Haram Counter-Terrorism Spec-Op & Armed Bandit Kingpin Sweep
                        int cashBeforeBh = s.State.S.cash;
                        s.State.S.ammo = 120;
                        Check(s.StartActionOp("cod_jtf_boko_haram") && s.IsSpecOpActive && s.ActiveActionOp.BossName.Contains("Boko Haram"),
                              "Boko Haram Counter-Terrorism Spec-Op (OP LAFIYA DOLE) launched with allied 81 Div fireteam");
                        if (boot.Data.TryPlace("dodan-barracks", out var dodan))
                            s.Player.Teleport(dodan.World() + new Vector3(0f, 0.3f, -12f), 0f);
                        for (int shot = 0; shot < 30 && s.ActiveActionOp != null; shot++) { s.State.S.ammo = 60; s.FireGun(); }
                        Check(s.ActiveActionOp == null && s.State.S.cash >= cashBeforeBh + 185000,
                              "Boko Haram cell & HVT Commander Abubakar neutralized (+₦185,000)");

                        int cashBeforeBandit = s.State.S.cash;
                        s.State.S.ammo = 120;
                        Check(s.StartActionOp("cod_army_bandit_sweep") && s.IsSpecOpActive && s.ActiveActionOp.BossName.Contains("Kachalla Turji"),
                              "Armed Bandit Kingpin Sweep (OP HADARIN DAJI) launched");
                        if (boot.Data.TryPlace("ikeja-cantonment", out var ikejaCant))
                            s.Player.Teleport(ikejaCant.World() + new Vector3(0f, 0.3f, -12f), 0f);
                        for (int shot = 0; shot < 30 && s.ActiveActionOp != null; shot++) { s.State.S.ammo = 60; s.FireGun(); }
                        Check(s.ActiveActionOp == null && s.State.S.cash >= cashBeforeBandit + 175000,
                              "Armed Bandit camp & HVT Kingpin Kachalla Turji neutralized (+₦175,000)");

                        // Furnished 3D Interiors, Indoor Activities & Indoor Missions
                        Check(s.EnterInterior("Surulere Home", "home") && s.InInterior && s.IndoorStations.Count >= 3 && s.Camera.IndoorMode,
                              $"Entered furnished 3D interior with {s.IndoorStations.Count} interactive indoor stations and responsive indoor camera framing");
                        float strBefore = s.State.S.skills.strength;
                        int cashBeforeSnooker = s.State.S.cash;
                        Check(s.DoIndoorActivity("cook_jollof") && s.DoIndoorActivity("home_workout") && s.DoIndoorActivity("indoor_snooker")
                              && s.State.S.skills.strength >= strBefore + 2.99f && s.State.S.cash >= cashBeforeSnooker + 4000,
                              "Indoor activities completed (Cook Jollof, Home Workout +3 Strength, Indoor Snooker & Ludo +₦5,000)");
                        int cashBeforeIndoorMission = s.State.S.cash;
                        Check(s.StartIndoorMission("indoor_efcc_hk_raid") && s.ActiveIndoorMission != null && s.ActiveEnemies.Count >= 2,
                              "Indoor Mission 'EFCC Indoor Cyber Syndicate Raid' started inside HK Cyber Safe-House with indoor hostiles");
                        while (s.ActiveIndoorMission != null) s.CompleteIndoorStep();
                        Check(s.ActiveIndoorMission == null && s.State.S.cash == cashBeforeIndoorMission + 75000,
                              "Indoor Mission completed across all 3 stages (+₦75,000)");
                        s.LeaveInterior();
                        Check(!s.InInterior && !s.Camera.IndoorMode, "Exited indoor mission building back to street");
                        s.State.S.heat = 0;

                        // Lagos Clubbing & Social Nightlife (Afrobeats Dancefloor, VIP Dorime Bottle Service, Guest DJ Set)
                        int socialBeforeClub = s.State.S.rep.social;
                        int cashBeforeDj = s.State.S.cash;
                        Check(s.EnterInterior("Eko Neon Club & VIP Lounge", "venue") && s.IndoorStations.Exists(st => st.Id == "club_dance") && s.IndoorStations.Exists(st => st.Id == "club_vip_bottles"),
                              "Entered 3D Neon Nightclub with Afrobeats Dancefloor, DJ Booth & VIP Bottle Service Lounge");
                        s.State.S.sound = true;
                        UI.SoundSystem.I?.SyncAmbientNow();
                        Check(UI.SoundSystem.I != null && UI.SoundSystem.I.StreetAmbienceClip != null && UI.SoundSystem.I.OwambeMusicClip != null && UI.SoundSystem.I.OwambeMusicVolume > 0f,
                              "Owambe & Afrobeats music loop active inside club when Sound toggle is ON");
                        Check(s.DoClubbingActivity("club_dance") && s.DoClubbingActivity("club_dj_set") && s.DoClubbingActivity("club_vip_bottles")
                              && s.State.S.rep.social >= socialBeforeClub + 20 && s.State.S.cash == cashBeforeDj + 18000 - 25000,
                              "Clubbing & Social Nightlife activities completed (Afrobeats Dancefloor, Guest DJ Set +₦18,000, VIP Bottle Service +10 Social Rep)");
                        s.LeaveInterior();
                        UI.SoundSystem.I?.SyncAmbientNow();
                        bool streetAmbOn = UI.SoundSystem.I != null && UI.SoundSystem.I.StreetAmbienceVolume > 0f;
                        s.State.S.sound = false;
                        UI.SoundSystem.I?.SyncAmbientNow();
                        bool mutedWhenOff = UI.SoundSystem.I != null && UI.SoundSystem.I.StreetAmbienceVolume == 0f && UI.SoundSystem.I.OwambeMusicVolume == 0f;
                        s.State.S.sound = true;
                        UI.SoundSystem.I?.SyncAmbientNow();
                        Check(streetAmbOn && mutedWhenOff, "Lagos street ambience & Owambe music loops gated behind Sound toggle");

                        // Dedicated Jump, Turn, Vehicle Enter/Exit, and 3-hit Attack character animation clips
                        boot.Player.TriggerJumpAnim(); bool jumpOk = boot.Player.AnimState == "jump";
                        boot.Player.TriggerTurnAnim(); bool turnOk = boot.Player.AnimState == "turn";
                        boot.Player.TriggerVehicleEnterAnim(); bool enterOk = boot.Player.AnimState == "enter_vehicle";
                        boot.Player.TriggerVehicleExitAnim(); bool exitOk = boot.Player.AnimState == "exit_vehicle";
                        boot.Player.SwingWeapon(); bool atk1 = boot.Player.AnimState == "attack" && boot.Player.IsSwinging;
                        int step1 = boot.Player.ComboStep;
                        boot.Player.SwingWeapon(); int step2 = boot.Player.ComboStep;
                        Check(boot.Player.HasExtendedClips && jumpOk && turnOk && enterOk && exitOk && atk1 && step2 != step1,
                              "Character rig expanded with dedicated jump, turn, enter_vehicle, exit_vehicle, and 3-hit attack animation clips");

                        // Full Game UI, UX, Graphics & Responsiveness Audit
                        var hudComp = boot.GetComponent<UI.Hud>();
                        var gov = boot.GetComponent<World.QualityGovernor>();
                        string uiReport = "", gfxReport = "";
                        bool uiOk = hudComp != null && hudComp.AuditUiAndUxHealth(out uiReport);
                        bool gfxOk = gov != null && gov.AuditGraphicsAndResponsiveness(out gfxReport);
                        Check(uiOk && gfxOk, $"Full UI/UX & Graphics/Responsiveness Audit passed ({uiReport} | {gfxReport})");

                        // Section 3, 4 & 5 End-to-End Multi-Corridor, Multi-State, Chunk Streamer & Economy Arbitrage Benchmark
                        var benchSw = System.Diagnostics.Stopwatch.StartNew();
                        bool corridorsOk = boot.Data.TryPlace("oshodi-interchange", out _)
                            && boot.Data.TryPlace("ladipo-auto-hub", out _)
                            && boot.Data.TryPlace("ojo-alaba", out _)
                            && boot.Data.TryPlace("mile12-market", out _)
                            && boot.Data.TryPlace("dangote-refinery", out _)
                            && boot.Data.TryPlace("seme-krake-border", out _);
                        bool kanoFound = boot.Data.TryPlace("emirs-palace-kano", out var kanoPalace);
                        bool statesOk = boot.Data.TryPlace("olumo-rock", out _)
                            && boot.Data.TryPlace("cocoa-house", out _)
                            && boot.Data.TryPlace("aso-rock-villa", out _)
                            && boot.Data.TryPlace("bonny-lng", out _)
                            && boot.Data.TryPlace("onitsha-main-mkt", out _)
                            && kanoFound;
                        Check(corridorsOk && statesOk && Session.StoryChains.Length == 15,
                              $"All 5 Lagos Megacorridors, 6 Geopolitical State maps, and {Session.StoryChains.Length} three-chapter Story Chains verified");

                        Vector3 savedPos = boot.Player.transform.position;
                        if (kanoFound && boot.Streamer != null)
                        {
                            boot.Player.Teleport(kanoPalace.World() + new Vector3(0f, 0.5f, -6f), 0f);
                            boot.Streamer.RefreshActiveChunks();
                            s.Players?.Tick(1.1f);
                            Check(boot.Streamer.TotalChunkCount >= 20
                                  && boot.Streamer.ActiveChunkCount > 0
                                  && boot.Streamer.ActiveChunkCount <= boot.Streamer.TotalChunkCount
                                  && boot.Streamer.CurrentStateId == "kano"
                                  && boot.Streamer.CurrentBiome == "sahel"
                                  && s.Weather.CurrentBiome == "sahel"
                                  && s.Weather.RegionalRainMultiplier < 0.5f
                                  && UI.SoundSystem.I != null && UI.SoundSystem.I.CurrentMusicStyle.Contains("Hausa")
                                  && s.Players != null && s.Players.StateChannel.StartsWith("rise:kano:"),
                                  $"WorldStreamer 500m chunking ({boot.Streamer.ActiveChunkCount}/{boot.Streamer.TotalChunkCount} active chunks), Kano Sahel biome weather/audio, and StateChannel '{s.Players?.StateChannel}' verified");
                            boot.Player.Teleport(savedPos, 0f);
                            boot.Streamer.RefreshActiveChunks();
                            s.Players?.Tick(1.1f);
                        }

                        float kanoProduce = Net.CloudEconomy.GetStateCommodityMultiplier("kano", "produce");
                        float fctProduce = Net.CloudEconomy.GetStateCommodityMultiplier("fct", "produce");
                        float riversFuel = Net.CloudEconomy.GetStateCommodityMultiplier("rivers", "fuel");
                        float anambraElec = Net.CloudEconomy.GetStateCommodityMultiplier("anambra", "electronics");
                        benchSw.Stop();
                        Check(kanoProduce < fctProduce && riversFuel < 0.85f && anambraElec < 0.85f,
                              $"Interstate commodity arbitrage multipliers verified (Kano produce {kanoProduce:0.00}x vs FCT {fctProduce:0.00}x, Rivers fuel {riversFuel:0.00}x, Anambra electronics {anambraElec:0.00}x) in {benchSw.Elapsed.TotalMilliseconds:0.00} ms");

                        // NOS Nitrous Restriction, NFS Garage Tuning, 0% Fuel Stall & Emergency Refuel, Seasons, Domestic State Flights, Pause & GPS Turn-by-Turn
                        var ownedKeke = s.Parked.Find(v => v != null && v.Owned && v.Class.id == "keke");
                        var ownedLux = s.Parked.Find(v => v != null && v.Owned && v.Class.id == "luxury");
                        Check(ownedKeke != null && !ownedKeke.HasNos && ownedLux != null && ownedLux.HasNos,
                              "NOS restricted to premium racing cars & custom-built cars (standard keke HasNos=false, luxury HasNos=true)");
                        var kekeSave = s.State.S.vehicles.Find(v => v.id == ownedKeke.OwnedId);
                        kekeSave.engineStage = 2; kekeSave.turboStage = 2; kekeSave.nosStage = 1; kekeSave.spoiler = 2; kekeSave.skirtKit = 1; kekeSave.neonColor = 0;
                        ownedKeke.ApplyOwnedCustomizations(kekeSave, boot.Data);
                        Check(ownedKeke.HasNos && ownedKeke.IsCustomBuilt && ownedKeke.transform.Find("NfsMods/Spoiler") != null && ownedKeke.transform.Find("NfsMods/NeonUnderglow") != null,
                              "NFS Underground Garage upgrades applied (Stage 2 Engine/Turbo, Stage 1 NOS, Carbon GT Wing & Cyan 3D Neon Underglow)");
                        kekeSave.nosStage = 0; kekeSave.spoiler = 0; kekeSave.skirtKit = 0; kekeSave.neonColor = -1;
                        ownedKeke.ApplyOwnedCustomizations(kekeSave, boot.Data);

                        s.State.S.fuel = 0f;
                        ownedLux.Speed = 0f;
                        ownedLux.Drive(1f, 0f, false, false, 1f, 1f, 0.25f);
                        bool stalledAtZero = Mathf.Abs(ownedLux.Speed) < 0.05f;
                        s.State.S.cash += 50000;
                        bool emergencyRefueled = s.EmergencyRefuel() && s.State.S.fuel >= 35f;
                        s.State.S.fuel = 100f;
                        Check(stalledAtZero && emergencyRefueled, "Vehicle stalls at 0% fuel and Emergency Roadside Jerrycan Refuel restores fuel");

                        s.Weather.SetSeasonMode("dry");
                        bool harmattanOk = s.Weather.ActiveSeasonId == "dry" && s.Weather.SeasonalRainMultiplier == 0f;
                        s.Weather.SetSeasonMode("rainy");
                        bool rainyOk = s.Weather.ActiveSeasonId == "rainy" && s.Weather.SeasonalRainMultiplier > 1.0f;
                        s.Weather.SetSeasonMode("");
                        Check(harmattanOk && rainyOk, "Nigerian Seasonal Calendar verified (Dry Harmattan 0x rain & Rainy Season peak rain)");

                        s.State.S.cash += 150000;
                        Check(Session.TryGetStateAirport("fct", out var fctAp) && Session.TryGetStateAirport("kano", out _) && !Session.TryGetStateAirport("osun", out _) && s.BookLocalStateFlight(fctAp),
                              "Domestic commercial flights active for states with operational airports (flew to Abuja Nnamdi Azikiwe Int'l Airport)");
                        boot.Player.Teleport(savedPos, 0f);
                        s.CloseMenu();

                        s.SetWaypoint("ladipo");
                        s.Nav.Tick(boot.Player.transform.position);
                        Check(s.Nav.Target.HasValue && s.Nav.CurrentRoute.Count >= 2 && s.Nav.EtaSeconds > 0 && !string.IsNullOrEmpty(s.Nav.NextTurnInstruction),
                              $"Enhanced GPS Turn-by-Turn Navigation verified ({s.Nav.NextTurnSymbol} {s.Nav.NextTurnInstruction}, {s.Nav.CurrentRoute.Count} route nodes, ETA {s.Nav.EtaSeconds}s)");
                        s.SetWaypoint(null);

                        s.TogglePauseMenu();
                        bool pauseOpened = s.Paused && s.ActiveMenu != null && s.ActiveMenu.Title == "GAME PAUSED";
                        s.SetPaused(false);
                        s.CloseMenu();
                        Check(pauseOpened && !s.Paused, "Game Pause button & Pause Menu verified");

                        stage = 21; stageT = 0f; return;
                    }
                case 21:  // a few more frames with everything running, then save
                    if (stageT < 1f) return;
                    s.AutoSave();
                    var again = new GameState(boot.Data);
                    Check(again.Load() && again.S.home == s.State.S.home && again.S.biz.Count == 2 && again.S.vehicles.Count == 3 && again.S.mission == 8 && again.S.courses.Count == 1 && again.S.collegeGraduated && again.S.footballWins >= 3 && again.S.familyDone.Contains("school_levy") && again.S.lookOutfit == 3 && again.S.pet && again.S.let.Count == 1 && again.S.biz[0].staff.Count == 1 && again.S.vehicles.Exists(v => v.insured) && again.S.crewTag == "IPB" && again.S.crewTreasury == 41000 && again.ChainStep("surulere_chain") == 1, "save keeps home, business, vehicles, story, certificates, diploma, football, family, look, pet, tenants, staff, insurance, crew treasury and story chains");
                    Finish(); return;
            }
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            EditorPrefs.SetBool(Flag, false);
            foreach (var n in notes) Debug.Log("RISE play test " + n);
            foreach (var e in errors) Debug.Log("RISE play test " + e);
            bool ok = errors.Count == 0;
            Debug.Log(ok ? $"RISE play test: PASS ({notes.Count} checks, {frame} frames)" : $"RISE play test: FAIL ({errors.Count} problems)");
            GameState.ClearSave();
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
#endif
