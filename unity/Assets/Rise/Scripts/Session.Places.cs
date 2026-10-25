using UnityEngine;
using Rise.Data;

namespace Rise
{
    // Living City places: the gym, Chicken Republic, the mall and cinema, the cyber café with its courses and gigs,
    // street football, banks, night venues, the owambe hall, churches and mosques, hotels, Area C police station,
    // PHCN offices, roadside vendors, and enterable building interiors.
    public partial class Session
    {
        LandmarkData targetPlace;
        Vector3 targetVendor;

        public bool VenueOpen => Clock != null && (Clock.Clock >= Data.meta.venueOpen || Clock.Clock < Data.meta.venueClose);
        public bool OwambeOpen => Clock != null && Clock.Clock >= 19f && Clock.Clock < 23.5f;

        LandmarkData NearPlace(Vector3 pos) =>
            Data.NearestLandmark("gym", pos, 12f) ?? Data.NearestLandmark("restaurant", pos, 12f) ?? Data.NearestLandmark("mall", pos, 18f) ??
            Data.NearestLandmark("cafe", pos, 12f) ?? Data.NearestLandmark("pitch", pos, 28f) ?? Data.NearestLandmark("school", pos, 12f) ??
            Data.NearestLandmark("bank", pos, 11f) ?? Data.NearestLandmark("venue", pos, 11f) ?? Data.NearestLandmark("owambe", pos, 16f) ??
            Data.NearestLandmark("worship", pos, 12f) ?? Data.NearestLandmark("hotel", pos, 13f) ??
            (State.S.heat > 0 ? Data.NearestLandmark("police", pos, 13f) : null) ?? Data.NearestLandmark("nepa", pos, 12f) ??
            Data.NearestLandmark("army", pos, 22f) ?? Data.NearestLandmark("navy", pos, 22f) ??
            Data.NearestLandmark("cbn", pos, 20f) ?? Data.NearestLandmark("efcc", pos, 20f) ??
            Data.NearestLandmark("hospital", pos, 16f) ?? Data.NearestLandmark("airport", pos, 22f) ??
            Data.NearestLandmark("museum", pos, 15f) ?? Data.NearestLandmark("market", pos, 16f) ??
            Data.NearestLandmark("warehouse", pos, 16f) ?? Data.NearestLandmark("garage", pos, 16f) ??
            Data.NearestLandmark("rail", pos, 16f) ?? Data.NearestLandmark("checkpoint", pos, 16f) ??
            Data.NearestLandmark("service", pos, 14f);

        bool NearVendor(Vector3 pos, out Vector3 vendor)
        {
            vendor = Vector3.zero; float best = 3.5f * 3.5f;
            if (Data.vendors == null) return false;
            foreach (var v in Data.vendors) { var w = v.World(); float d = (new Vector3(w.x, pos.y, w.z) - pos).sqrMagnitude; if (d < best) { best = d; vendor = w; } }
            return best < 3.5f * 3.5f;
        }

        string PlacePrompt(LandmarkData p) => p.kind switch
        {
            "gym" => "ENTER GYM",
            "restaurant" => $"ENTER {p.name.ToUpper()}",
            "mall" => "ENTER MALL",
            "cafe" => "ENTER CYBER CAFÉ",
            "pitch" => "JOIN THE FOOTBALL",
            "school" => p.id == "yabatech" ? "ENTER YABA TECH" : "VISIT SCHOOL · EVENING CLASS",
            "bank" => $"ENTER {p.shortName}",
            "venue" => VenueOpen ? $"ENTER {p.name.ToUpper()} · ₦{(p.cost > 0 ? p.cost : 5000):N0}" : $"{p.name.ToUpper()} OPENS AT {Data.meta.venueOpen:0}:00",
            "owambe" => OwambeOpen ? $"JOIN THE OWAMBE · ₦{(p.cost > 0 ? p.cost : 3000):N0}" : "OWAMBE TONIGHT FROM 19:00",
            "worship" => $"ENTER {p.name.ToUpper()} · PRAY",
            "hotel" => $"CHECK IN · {p.name.ToUpper()} · ₦{(p.cost > 0 ? p.cost : 15000):N0}",
            "police" => $"SETTLE FINE · ₦{State.S.heat * Data.meta.fineRate:N0}",
            "nepa" => Events != null && Events.Outage > 0f ? "PAY FOR DIESEL · ₦5,000" : "ENTER PHCN OFFICE",
            "army" => $"ENTER {p.shortName} · MILITARY OPS",
            "navy" => $"ENTER {p.shortName} · NAVAL OPS",
            "cbn" => $"ENTER {p.shortName} · CENTRAL BANK TREASURY",
            "efcc" => $"ENTER {p.shortName} · ANTI-GRAFT COMMAND",
            "hospital" => $"ENTER {p.shortName} · MEDICAL CARE",
            "airport" => $"ENTER {p.shortName} · FLIGHT TERMINAL",
            "museum" => $"VISIT {p.shortName} · HERITAGE TOUR",
            "market" => $"ENTER {p.shortName} · TRADE & SUPPLY",
            "warehouse" => $"ENTER {p.shortName} · DEPOT & HAULAGE",
            "garage" => $"ENTER {p.shortName} · TRANSPORT HUB",
            "rail" => $"BOARD TRAIN · {p.shortName} (METRO & NRC)",
            "checkpoint" => $"INSPECT AT {p.shortName} · FRSC & CUSTOMS",
            "service" => $"ENTER {p.shortName} · CIVIC & EMERGENCY OPS",
            _ => $"ENTER {p.shortName}"
        };

        void OpenPlaceMenu(LandmarkData p)
        {
            switch (p.kind)
            {
                case "gym": OpenGymMenu(p); break;
                case "restaurant": OpenFoodMenu(p); break;
                case "mall": OpenMallMenu(p); break;
                case "cafe": OpenCafeMenu(p); break;
                case "pitch": OpenFootballMenu(p); break;
                case "school": OpenSchoolMenu(p); break;
                case "bank": AppBank(); break;
                case "venue": OpenVenueMenu(p); break;
                case "owambe": OpenOwambeMenu(p); break;
                case "worship": OpenWorshipMenu(p); break;
                case "hotel": OpenHotelMenu(p); break;
                case "police": OpenPoliceMenu(p); break;
                case "nepa": OpenNepaMenu(p); break;
                case "army":
                case "navy": OpenBarracksMenu(p); break;
                case "cbn": OpenCbnMenu(p); break;
                case "efcc": OpenEfccMenu(p); break;
                case "hospital": OpenHospitalMenu(p); break;
                case "airport": OpenAirportMenu(p); break;
                case "museum": OpenMuseumMenu(p); break;
                case "market": OpenMarketMenu(p); break;
                case "warehouse": OpenDepotMenu(p); break;
                case "garage": OpenInterchangeMenu(p); break;
                case "rail": OpenRailMenu(p); break;
                case "checkpoint": OpenCheckpointMenu(p); break;
                case "service": OpenDepotMenu(p); break;
            }
        }

        void Hours(float h) => Clock.Advance(h);

        // ---- enterable building interiors, indoor activities & indoor missions ----
        static readonly Vector3 RoomCenter = new Vector3(0f, 160f, 0f);
        GameObject interiorRoot;
        Vector3 interiorOutdoorPos;
        float interiorOutdoorYaw;
        string interiorName, interiorKind;
        System.Action interiorService;
        public bool InInterior => interiorRoot != null;

        public class IndoorStation
        {
            public string Id, Label, Prompt;
            public Vector3 LocalPos;
            public System.Action OnInteract;
        }
        public readonly System.Collections.Generic.List<IndoorStation> IndoorStations = new System.Collections.Generic.List<IndoorStation>();

        public class IndoorMission
        {
            public string Id, Title, BuildingName, BuildingKind, Objective, RepKind, HostileKind;
            public int Reward, Xp, RepGain, HostileCount, Step, TotalSteps;
        }

        public IndoorMission ActiveIndoorMission { get; private set; }
        GameObject indoorMissionProp;

        public static readonly IndoorMission[] IndoorMissions =
        {
            new IndoorMission
            {
                Id = "indoor_efcc_hk_raid",
                Title = "EFCC Indoor Cyber Syndicate Raid",
                BuildingName = "HK Cyber Safe-House",
                BuildingKind = "cafe",
                Objective = "Eliminate indoor syndicate guards and seize 3 encrypted 419 server terminals",
                Reward = 75000, Xp = 45, RepKind = "public", RepGain = 10, HostileCount = 2, HostileKind = "bandit", TotalSteps = 3
            },
            new IndoorMission
            {
                Id = "indoor_cbn_vault_defense",
                Title = "CBN Indoor Bullion Vault Lockdown",
                BuildingName = "CBN Sub-Treasury Vault",
                BuildingKind = "bank",
                Objective = "Repel indoor Boko Haram infiltrators and lock down 3 bullion vault consoles",
                Reward = 95000, Xp = 55, RepKind = "business", RepGain = 12, HostileCount = 3, HostileKind = "boko_haram", TotalSteps = 3
            },
            new IndoorMission
            {
                Id = "indoor_boxing_title",
                Title = "Surulere Indoor Boxing Championship",
                BuildingName = "Surulere Championship Ring",
                BuildingKind = "gym",
                Objective = "Defeat indoor contenders and ring the championship bell across 3 rounds",
                Reward = 60000, Xp = 40, RepKind = "street", RepGain = 10, HostileCount = 2, HostileKind = "bandit", TotalSteps = 3
            },
            new IndoorMission
            {
                Id = "indoor_politician_summit",
                Title = "State House Indoor VIP Security Sweep",
                BuildingName = "State House Situation Room",
                BuildingKind = "hotel",
                Objective = "Clear hostile infiltrators and verify 3 diplomatic security checkpoints indoors",
                Reward = 85000, Xp = 50, RepKind = "public", RepGain = 12, HostileCount = 2, HostileKind = "boko_haram", TotalSteps = 3
            }
        };

        public bool EnterInterior(string name, string kind, System.Action onUseService = null)
        {
            if (Vehicle != null) Exit();
            if (Player == null) return false;
            if (!InInterior)
            {
                interiorOutdoorPos = Player.transform.position;
                interiorOutdoorYaw = Player.transform.eulerAngles.y;
            }
            else
            {
                Destroy(interiorRoot);
                interiorRoot = null;
            }
            interiorName = name;
            interiorKind = kind;
            interiorService = onUseService;
            IndoorStations.Clear();

            interiorRoot = new GameObject("Interior " + name);
            var lit = ShaderLib.Lit;
            Material Mat(Color c)
            {
                var m = new Material(lit) { color = c };
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                m.SetFloat("_Smoothness", 0.2f);
                return m;
            }
            void RoomBox(string n, Vector3 localPos, Vector3 scale, Color c, bool col = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = n; go.transform.SetParent(interiorRoot.transform, false);
                go.transform.position = RoomCenter + localPos; go.transform.localScale = scale;
                go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
                if (!col) Destroy(go.GetComponent<Collider>());
            }
            void RoomLabel(string text, Vector3 localPos, Color col)
            {
                var lgo = new GameObject("Label " + text);
                lgo.transform.SetParent(interiorRoot.transform, false);
                lgo.transform.position = RoomCenter + localPos;
                var tm = lgo.AddComponent<TextMesh>();
                tm.text = text; tm.characterSize = 0.11f; tm.fontSize = 36;
                tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
                tm.color = col;
            }

            bool home = kind == "home";
            bool club = kind == "venue" || kind == "owambe";
            Color floorCol = home ? new Color(0.42f, 0.31f, 0.22f) : club ? new Color(0.16f, 0.11f, 0.22f) : new Color(0.48f, 0.45f, 0.40f);
            Color wallCol = club ? new Color(0.24f, 0.15f, 0.32f) : new Color(0.82f, 0.78f, 0.70f);
            Color trimCol = club ? new Color(0.78f, 0.22f, 0.85f) : new Color(0.38f, 0.28f, 0.20f);
            RoomBox("Floor", new Vector3(0f, -1.0f, 0f), new Vector3(home ? 16f : 20f, 2.0f, home ? 14f : 18f), floorCol);
            RoomBox("Ceiling", new Vector3(0f, 4.2f, 0f), new Vector3(home ? 14f : 18f, 0.2f, home ? 12f : 16f), club ? new Color(0.14f, 0.10f, 0.20f) : new Color(0.88f, 0.86f, 0.82f));
            float hw = home ? 7f : 9f, hd = home ? 6f : 8f;
            RoomBox("NorthWall", new Vector3(0f, 2f, -hd), new Vector3(hw * 2f, 4.2f, 0.3f), wallCol);
            RoomBox("SouthWall", new Vector3(0f, 2f, hd), new Vector3(hw * 2f, 4.2f, 0.3f), wallCol);
            RoomBox("WestWall", new Vector3(-hw, 2f, 0f), new Vector3(0.3f, 4.2f, hd * 2f), wallCol);
            RoomBox("EastWall", new Vector3(hw, 2f, 0f), new Vector3(0.3f, 4.2f, hd * 2f), wallCol);
            // Wall wainscoting / neon club cornice strips & central carpet runner
            RoomBox("NorthTrim", new Vector3(0f, 3.1f, -hd + 0.18f), new Vector3(hw * 1.9f, 0.14f, 0.08f), trimCol, false);
            RoomBox("WestTrim", new Vector3(-hw + 0.18f, 3.1f, 0f), new Vector3(0.08f, 0.14f, hd * 1.9f), trimCol, false);
            RoomBox("EastTrim", new Vector3(hw - 0.18f, 3.1f, 0f), new Vector3(0.08f, 0.14f, hd * 1.9f), trimCol, false);
            RoomBox("CenterRug", new Vector3(0f, 0.02f, 1.2f), new Vector3(3.4f, 0.03f, 5.2f), club ? new Color(0.48f, 0.12f, 0.38f) : new Color(0.62f, 0.22f, 0.18f), false);
            RoomBox("Door", new Vector3(0f, 1.1f, hd - 0.2f), new Vector3(1.4f, 2.2f, 0.18f), new Color(0.29f, 0.19f, 0.12f), false);
            RoomLabel("EXIT TO STREET", new Vector3(0f, 2.5f, hd - 0.35f), new Color(0.55f, 0.95f, 0.65f));
            RoomBox(home ? "Bed" : "Counter", new Vector3(0f, 0.55f, -2.4f), home ? new Vector3(3.2f, 0.8f, 2.2f) : new Vector3(5.6f, 1.1f, 1.4f), home ? new Color(0.75f, 0.28f, 0.24f) : club ? new Color(0.32f, 0.16f, 0.38f) : new Color(0.46f, 0.33f, 0.24f));
            if (!home)
            {
                RoomBox("BackBarShelf", new Vector3(0f, 1.8f, -hd + 0.45f), new Vector3(5.4f, 2.0f, 0.45f), club ? new Color(0.22f, 0.10f, 0.28f) : new Color(0.35f, 0.25f, 0.18f), false);
                RoomBox("BackBarGlow", new Vector3(0f, 2.2f, -hd + 0.70f), new Vector3(4.8f, 0.18f, 0.08f), trimCol, false);
            }
            RoomLabel(home ? "BED · SLEEP" : name.ToUpperInvariant(), new Vector3(0f, 1.65f, -2.4f), new Color(1f, 0.86f, 0.35f));

            // Left Wing: Context-Sensitive Indoor Activity Station
            Vector3 leftPos = new Vector3(-hw + 2.2f, 0f, 0.2f);
            if (home)
            {
                RoomBox("KitchenCounter", leftPos + new Vector3(0f, 0.5f, 0f), new Vector3(1.6f, 1.0f, 2.4f), new Color(0.72f, 0.34f, 0.18f));
                RoomLabel("KITCHEN · COOK JOLLOF", leftPos + new Vector3(0f, 1.55f, 0f), new Color(0.35f, 1f, 0.55f));
                IndoorStations.Add(new IndoorStation { Id = "cook_jollof", Label = "Kitchen Cooker", Prompt = "COOK JOLLOF & DODO (+45 STAMINA, +15 HEALTH)", LocalPos = leftPos, OnInteract = () => DoIndoorActivity("cook_jollof") });
            }
            else if (kind == "gym")
            {
                RoomBox("BoxingRing", leftPos + new Vector3(0f, 0.25f, 0f), new Vector3(2.8f, 0.5f, 2.8f), new Color(0.78f, 0.18f, 0.18f));
                RoomLabel("SPARRING RING", leftPos + new Vector3(0f, 1.55f, 0f), new Color(1f, 0.45f, 0.45f));
                IndoorStations.Add(new IndoorStation { Id = "indoor_boxing", Label = "Boxing Ring", Prompt = "SPAR IN BOXING RING (+4 STRENGTH, +₦4,000)", LocalPos = leftPos, OnInteract = () => DoIndoorActivity("indoor_boxing") });
            }
            else if (kind == "cafe")
            {
                RoomBox("ServerRack", leftPos + new Vector3(0f, 0.9f, 0f), new Vector3(1.4f, 1.8f, 1.8f), new Color(0.16f, 0.26f, 0.38f));
                RoomLabel("SERVER RACK · GIG", leftPos + new Vector3(0f, 2.1f, 0f), new Color(0.45f, 0.85f, 1f));
                IndoorStations.Add(new IndoorStation { Id = "cafe_server_fix", Label = "Server Terminal", Prompt = "DEBUG SERVER RACK (+₦8,000, +3 BUSINESS)", LocalPos = leftPos, OnInteract = () => DoIndoorActivity("cafe_server_fix") });
            }
            else if (kind == "bank" || kind == "police")
            {
                RoomBox("AuditTerminal", leftPos + new Vector3(0f, 0.65f, 0f), new Vector3(1.6f, 1.3f, 1.6f), new Color(0.18f, 0.36f, 0.24f));
                RoomLabel("FORENSIC AUDIT DESK", leftPos + new Vector3(0f, 1.7f, 0f), new Color(0.35f, 1f, 0.55f));
                IndoorStations.Add(new IndoorStation { Id = "vault_audit", Label = "Audit Desk", Prompt = "AUDIT LEDGER TERMINAL (+₦12,000, +3 PUBLIC REP)", LocalPos = leftPos, OnInteract = () => DoIndoorActivity("vault_audit") });
            }
            else if (kind == "venue" || kind == "owambe")
            {
                RoomBox("DanceFloor", leftPos + new Vector3(0f, 0.08f, -1.2f), new Vector3(3.0f, 0.16f, 3.0f), new Color(0.65f, 0.12f, 0.55f));
                RoomBox("DjBooth", leftPos + new Vector3(0f, 0.65f, -2.5f), new Vector3(2.2f, 1.3f, 0.9f), new Color(0.14f, 0.14f, 0.22f));
                RoomLabel("AFROBEATS DANCEFLOOR & DJ BOOTH", leftPos + new Vector3(0f, 1.85f, -1.2f), new Color(1f, 0.45f, 0.92f));
                IndoorStations.Add(new IndoorStation { Id = "club_dance", Label = "Afrobeats Dancefloor", Prompt = "DANCE & HYPE DJ SET (+100% STAMINA, +4 SOCIAL REP)", LocalPos = leftPos + new Vector3(0f, 0f, -1.2f), OnInteract = () => DoClubbingActivity("club_dance") });

                RoomBox("VipLounge", leftPos + new Vector3(0f, 0.45f, 2.0f), new Vector3(2.2f, 0.9f, 2.0f), new Color(0.75f, 0.58f, 0.14f));
                RoomLabel("VIP BOTTLE SERVICE LOUNGE", leftPos + new Vector3(0f, 1.6f, 2.0f), new Color(1f, 0.86f, 0.28f));
                IndoorStations.Add(new IndoorStation { Id = "club_vip_bottles", Label = "VIP Lounge Table", Prompt = "POP VIP BOTTLES (₦25,000 → +10 SOCIAL, +6 BUSINESS REP)", LocalPos = leftPos + new Vector3(0f, 0f, 2.0f), OnInteract = () => DoClubbingActivity("club_vip_bottles") });
            }
            else
            {
                RoomBox("SnookerTable", leftPos + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.9f, 2.6f), new Color(0.12f, 0.48f, 0.24f));
                RoomLabel("SNOOKER & LUDO TABLE", leftPos + new Vector3(0f, 1.45f, 0f), new Color(0.35f, 1f, 0.55f));
                IndoorStations.Add(new IndoorStation { Id = "indoor_snooker", Label = "Snooker & Ludo Table", Prompt = "PLAY SNOOKER & LUDO (+₦5,000, +3 SOCIAL REP)", LocalPos = leftPos, OnInteract = () => DoIndoorActivity("indoor_snooker") });
            }

            // Right Wing: Home Workout + Indoor Missions Board
            Vector3 rightPos = new Vector3(hw - 2.2f, 0f, 0.2f);
            if (home)
            {
                RoomBox("WorkoutMat", rightPos + new Vector3(0f, 0.15f, -1.2f), new Vector3(1.8f, 0.3f, 1.8f), new Color(0.22f, 0.38f, 0.62f));
                RoomLabel("HOME WORKOUT", rightPos + new Vector3(0f, 1.25f, -1.2f), new Color(0.45f, 0.85f, 1f));
                IndoorStations.Add(new IndoorStation { Id = "home_workout", Label = "Workout Bench", Prompt = "HOME WORKOUT (+3 STRENGTH, +2 FITNESS)", LocalPos = rightPos + new Vector3(0f, 0f, -1.2f), OnInteract = () => DoIndoorActivity("home_workout") });
            }
            RoomBox("OpsBoard", rightPos + new Vector3(0f, 0.75f, 1.4f), new Vector3(1.5f, 1.5f, 0.9f), new Color(0.25f, 0.22f, 0.16f));
            RoomLabel("INDOOR MISSIONS & ACTIVITIES", rightPos + new Vector3(0f, 1.8f, 1.4f), new Color(1f, 0.78f, 0.22f));
            IndoorStations.Add(new IndoorStation { Id = "indoor_ops_board", Label = "Indoor Operations Board", Prompt = "INDOOR ACTIVITIES & MISSIONS MENU", LocalPos = rightPos + new Vector3(0f, 0f, 1.4f), OnInteract = OpenIndoorMenu });

            var lightGo = new GameObject("RoomLight"); lightGo.transform.SetParent(interiorRoot.transform, false);
            lightGo.transform.position = RoomCenter + new Vector3(0f, 3.6f, 0f);
            var l = lightGo.AddComponent<Light>(); l.type = LightType.Point; l.range = 28f; l.intensity = 2.1f; l.color = new Color(1f, 0.94f, 0.82f);

            Physics.SyncTransforms();
            ActiveMenu = null;
            Player.MinFloorY = RoomCenter.y;
            Player.Teleport(RoomCenter + new Vector3(0f, 0.05f, hd - 2f), 180f);
            if (Camera != null) { Camera.Yaw = 180f; Camera.SetIndoor(true); }
            State.Toast($"Inside {name}");
            return true;
        }

        public void LeaveInterior()
        {
            if (!InInterior) return;
            if (ActiveIndoorMission != null)
            {
                ActiveIndoorMission = null;
                ClearShootoutEnemies();
            }
            IndoorStations.Clear();
            Destroy(interiorRoot);
            interiorRoot = null;
            Player.MinFloorY = null;
            Player.Teleport(interiorOutdoorPos, interiorOutdoorYaw);
            if (Camera != null) { Camera.Yaw = interiorOutdoorYaw; Camera.SetIndoor(false); }
            State.Toast("Back on the street");
        }

        public bool DoIndoorActivity(string id)
        {
            var st = State.S;
            switch (id)
            {
                case "cook_jollof":
                    if (st.cash + st.bank >= 1000) State.Pay(1000);
                    st.stamina = Mathf.Min(100f, st.stamina + 45f);
                    st.health = Mathf.Min(100f, st.health + 15f);
                    Hours(0.5f);
                    State.AddXp(8);
                    State.Toast("Smoky Party Jollof & Dodo cooked · Stamina +45 · Health +15");
                    return true;
                case "home_workout":
                    if (st.stamina < 15f) { State.Toast("Too tired — eat or rest first"); return false; }
                    st.stamina = Mathf.Max(0f, st.stamina - 15f);
                    State.GainSkill("strength", 3f);
                    State.GainSkill("fitness", 2f);
                    Hours(0.5f);
                    State.AddXp(8);
                    State.Toast("Home push-ups & dumbbells done · Strength +3 · Fitness +2");
                    return true;
                case "watch_tv":
                    st.stamina = Mathf.Min(100f, st.stamina + 25f);
                    State.GainSkill("charisma", 2f);
                    Hours(0.5f);
                    State.AddXp(6);
                    State.Toast("Watched Channels TV & SuperStory · Stamina +25 · Charisma +2");
                    return true;
                case "indoor_boxing":
                    st.stamina = Mathf.Max(10f, st.stamina - 15f);
                    State.GainSkill("strength", 4f);
                    State.GainSkill("fitness", 4f);
                    State.Earn(4000);
                    State.AddRep("street", 3);
                    State.AddXp(12);
                    State.Toast("Won indoor sparring bout · +₦4,000 · Strength +4 · Fitness +4");
                    return true;
                case "indoor_snooker":
                    State.Earn(5000);
                    State.AddRep("social", 3);
                    State.GainSkill("charisma", 2f);
                    Hours(0.5f);
                    State.AddXp(10);
                    State.Toast("Cleared the Snooker & Ludo table · +₦5,000 · Social Rep +3");
                    return true;
                case "cafe_server_fix":
                    State.Earn(8000);
                    State.GainSkill("business", 3f);
                    State.AddRep("business", 3);
                    Hours(0.5f);
                    State.AddXp(14);
                    State.Toast("Patched Cyber Café server rack · +₦8,000 · Business +3");
                    return true;
                case "vault_audit":
                    State.Earn(12000);
                    State.AddRep("public", 3);
                    State.GainSkill("business", 2f);
                    Hours(0.5f);
                    State.AddXp(15);
                    State.Toast("Completed indoor forensic ledger audit · +₦12,000 · Public Rep +3");
                    return true;
            }
            return false;
        }

        public bool StartIndoorMission(string id)
        {
            IndoorMission found = null;
            foreach (var m in IndoorMissions) if (m.Id == id) { found = m; break; }
            if (found == null) return false;

            EnterInterior(found.BuildingName, found.BuildingKind, OpenIndoorMenu);
            ActiveIndoorMission = new IndoorMission
            {
                Id = found.Id,
                Title = found.Title,
                BuildingName = found.BuildingName,
                BuildingKind = found.BuildingKind,
                Objective = found.Objective,
                Reward = found.Reward,
                Xp = found.Xp,
                RepKind = found.RepKind,
                RepGain = found.RepGain,
                HostileCount = found.HostileCount,
                HostileKind = found.HostileKind,
                Step = 0,
                TotalSteps = found.TotalSteps
            };

            if (indoorMissionProp != null) Destroy(indoorMissionProp);
            indoorMissionProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            indoorMissionProp.name = "IndoorMissionTerminal";
            indoorMissionProp.transform.SetParent(interiorRoot.transform, false);
            indoorMissionProp.transform.position = RoomCenter + new Vector3(0f, 0.7f, 0.4f);
            indoorMissionProp.transform.localScale = new Vector3(1.1f, 1.4f, 1.1f);
            var mat = new Material(ShaderLib.Lit) { color = new Color(0.15f, 0.65f, 0.35f) };
            indoorMissionProp.GetComponent<MeshRenderer>().sharedMaterial = mat;

            ClearShootoutEnemies();
            for (int i = 0; i < found.HostileCount; i++)
            {
                Vector3 hPos = RoomCenter + new Vector3((i % 2 == 0 ? -3.2f : 3.2f), 0.1f, -1.0f - i * 0.8f);
                string label = found.HostileKind == "boko_haram" ? "Boko Haram Infiltrator" : "Syndicate Enforcer";
                SpawnIndoorHostile(hPos, label, i == 0 ? "rifle" : "pistol", i == 0 ? 120f : 90f, i == 0, interiorRoot.transform);
            }

            State.Toast($"INDOOR MISSION: {found.Title} · {found.Objective}");
            return true;
        }

        public bool CompleteIndoorStep()
        {
            if (ActiveIndoorMission == null) return false;
            ActiveIndoorMission.Step++;
            if (ActiveIndoorMission.Step < ActiveIndoorMission.TotalSteps)
            {
                State.Toast($"{ActiveIndoorMission.Title}: Step {ActiveIndoorMission.Step}/{ActiveIndoorMission.TotalSteps} secured!");
                return true;
            }
            var done = ActiveIndoorMission;
            ActiveIndoorMission = null;
            ClearShootoutEnemies();
            if (indoorMissionProp != null) Destroy(indoorMissionProp);
            State.Earn(done.Reward);
            State.AddXp(done.Xp);
            if (!string.IsNullOrEmpty(done.RepKind)) State.AddRep(done.RepKind, done.RepGain);
            State.Toast($"{done.Title} COMPLETE! +₦{done.Reward:N0} · +{done.RepGain} {done.RepKind} rep");
            return true;
        }

        public void CheckIndoorMissionProgress()
        {
            if (ActiveIndoorMission == null) return;
            CompleteIndoorStep();
        }

        public void OpenIndoorMenu()
        {
            var m = new Menu
            {
                Title = InInterior ? $"{interiorName} · Indoor Operations" : "Indoor Activities & Missions",
                Subtitle = ActiveIndoorMission != null ? $"Active: {ActiveIndoorMission.Title} ({ActiveIndoorMission.Step}/{ActiveIndoorMission.TotalSteps})" : "Step inside any building to train, cook, play snooker, or launch indoor tactical missions",
                Line = "Choose an indoor activity for instant skill/vital boosts or deploy into a multi-stage 3D indoor mission."
            };
            m.Options.Add(new MenuOption { Label = "Activity: Cook Party Jollof & Dodo (+45 Stamina, +15 Health)", Action = () => DoIndoorActivity("cook_jollof") });
            m.Options.Add(new MenuOption { Label = "Activity: Home Push-Ups & Dumbbells (+3 Strength, +2 Fitness)", Action = () => DoIndoorActivity("home_workout") });
            m.Options.Add(new MenuOption { Label = "Activity: Watch Lagos TV Lounge (+25 Stamina, +2 Charisma)", Action = () => DoIndoorActivity("watch_tv") });
            m.Options.Add(new MenuOption { Label = "Activity: Spar in Indoor Boxing Ring (+₦4,000, +4 Strength)", Action = () => DoIndoorActivity("indoor_boxing") });
            m.Options.Add(new MenuOption { Label = "Activity: Play Indoor Snooker & Ludo (+₦5,000, +3 Social Rep)", Action = () => DoIndoorActivity("indoor_snooker") });
            m.Options.Add(new MenuOption { Label = "Activity: Debug Cyber Café Server Rack (+₦8,000, +3 Business)", Action = () => DoIndoorActivity("cafe_server_fix") });
            m.Options.Add(new MenuOption { Label = "Activity: Forensic Vault Ledger Audit (+₦12,000, +3 Public Rep)", Action = () => DoIndoorActivity("vault_audit") });
            foreach (var im in IndoorMissions)
            {
                var mission = im;
                m.Options.Add(new MenuOption
                {
                    Label = $"[Indoor Mission] {mission.Title} · +₦{mission.Reward:N0}",
                    Action = () => { PhoneOpen = false; StartIndoorMission(mission.Id); }
                });
            }
            if (InInterior) m.Options.Add(new MenuOption { Label = "Step outside to the street", Action = LeaveInterior });
            m.Options.Add(new MenuOption { Label = "Close", Action = null });
            OpenMenu(m);
        }

        bool UpdateInteriorOnFoot()
        {
            if (!InInterior) return false;
            if (GameInput.CycleWeaponPressed) { CycleWeapon(); return true; }
            if (GameInput.AttackPressed) { PerformStreetAttack(); return true; }

            float hd = interiorKind == "home" ? 6f : 8f;
            Player.MinFloorY = RoomCenter.y;
            var pos = Player.transform.position;
            if (pos.y < RoomCenter.y - 0.1f || Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(RoomCenter.x, RoomCenter.z)) > 15f)
            {
                Physics.SyncTransforms();
                Player.Teleport(RoomCenter + new Vector3(0f, 0.05f, hd - 2f), 180f);
                Camera?.Snap();
                pos = Player.transform.position;
            }
            float dDoor = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(RoomCenter.x, RoomCenter.z + hd - 0.4f));
            float dDesk = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(RoomCenter.x, RoomCenter.z - 2.4f));
            Prompt = null;

            if (ActiveIndoorMission != null)
            {
                float dTerm = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(RoomCenter.x, RoomCenter.z + 0.4f));
                if (dTerm < 3.0f)
                {
                    Prompt = $"SECURE OBJECTIVE ({ActiveIndoorMission.Step + 1}/{ActiveIndoorMission.TotalSteps})";
                    if (Interact()) CompleteIndoorStep();
                    return true;
                }
            }

            IndoorStation nearestSt = null;
            float bestSt = 2.6f;
            foreach (var st in IndoorStations)
            {
                float d = Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(RoomCenter.x + st.LocalPos.x, RoomCenter.z + st.LocalPos.z));
                if (d < bestSt) { bestSt = d; nearestSt = st; }
            }

            if (dDoor < 2.6f)
            {
                Prompt = "LEAVE BUILDING";
                if (Interact()) LeaveInterior();
            }
            else if (nearestSt != null)
            {
                Prompt = nearestSt.Prompt;
                if (Interact()) nearestSt.OnInteract?.Invoke();
            }
            else if (ActiveIndoorMission != null)
            {
                Prompt = $"{ActiveIndoorMission.Title.ToUpperInvariant()} ({ActiveIndoorMission.Step}/{ActiveIndoorMission.TotalSteps})";
                if (Interact()) CompleteIndoorStep();
            }
            else
            {
                Prompt = interiorKind == "home" ? "SLEEP TILL MORNING" : $"USE {interiorName.ToUpper()}";
                if (Interact())
                {
                    if (interiorKind == "home") Sleep();
                    else if (interiorService != null) interiorService.Invoke();
                    else OpenIndoorMenu();
                }
            }
            return true;
        }

        // ---- Politician NPCs (Presidency, Governor, Senators, House of Reps, LGA Chairman) & Fraudster NPCs ----
        public bool IsPolitician(World.ContactSystem.Contact c) => c != null && World.ContactSystem.IsPoliticianContact(c.data?.id);
        public bool IsFraudster(World.ContactSystem.Contact c) => c != null && World.ContactSystem.IsFraudsterContact(c.data?.id);

        public static string PoliticianTierLabel(string id) => id switch
        {
            "presidential_envoy" => "FEDERAL PRESIDENCY · DODAN BARRACKS",
            "lagos_governor" => "EXECUTIVE GOVERNOR · ALAUSA STATE HOUSE",
            "senator_lagos_central" => "NIGERIAN SENATE · LAGOS CENTRAL",
            "senator_lagos_west" => "NIGERIAN SENATE · LAGOS WEST",
            "hon_rep_surulere" => "HOUSE OF REPRESENTATIVES · SURULERE",
            "lga_chairman_surulere" => "LOCAL GOVERNMENT CHAIRMAN · SURULERE LGA",
            _ => "GOVERNMENT OFFICIAL"
        };

        public static int PoliticianGrantAmount(string id) => id switch
        {
            "presidential_envoy" => 120000,
            "lagos_governor" => 95000,
            "senator_lagos_central" => 75000,
            "senator_lagos_west" => 75000,
            "hon_rep_surulere" => 60000,
            "lga_chairman_surulere" => 45000,
            _ => 50000
        };

        public bool LobbyPoliticianContract(string id)
        {
            int grant = PoliticianGrantAmount(id);
            State.Earn(grant);
            State.AddRep("public", 8);
            State.AddRep("business", 6);
            State.GainSkill("charisma", 3f);
            State.GainSkill("business", 2f);
            if (State.S.heat > 0) State.S.heat = Mathf.Max(0, State.S.heat - 1);
            State.AddXp(25);
            State.Toast($"{PoliticianTierLabel(id)}: Civic Infrastructure Contract executed · +₦{grant:N0} · Public Rep +8");
            return true;
        }

        public bool MobilizePoliticianRally(string id)
        {
            State.Earn(35000);
            State.AddRep("public", 6);
            State.AddRep("social", 5);
            State.GainSkill("charisma", 4f);
            State.AddXp(18);
            State.Toast($"Grassroots Town-Hall & Campaign Rally mobilized · +₦35,000 · Charisma +4");
            return true;
        }

        public void OpenPoliticianMenu(World.ContactSystem.Contact c)
        {
            string id = c.data.id;
            int grant = PoliticianGrantAmount(id);
            var m = new Menu
            {
                Title = $"{c.data.name}",
                Subtitle = $"{PoliticianTierLabel(id)} · {c.data.role}",
                Line = GreetLine(c)
            };
            m.Options.Add(new MenuOption
            {
                Label = $"Execute Constituency & Infrastructure Contract · +₦{grant:N0} (+8 Public Rep, −1 Heat)",
                Action = () => LobbyPoliticianContract(id)
            });
            m.Options.Add(new MenuOption
            {
                Label = "Mobilize Grassroots Town-Hall & Campaign Rally · +₦35,000 (+6 Public Rep, +4 Charisma)",
                Action = () => MobilizePoliticianRally(id)
            });
            m.Options.Add(new MenuOption
            {
                Label = "[Indoor Mission] State House VIP Security Sweep · +₦85,000",
                Action = () => StartIndoorMission("indoor_politician_summit")
            });
            var polHub = HubFor(c);
            if (polHub.runs != null)
            {
                foreach (var r in polHub.runs)
                {
                    var run = r;
                    string destName = Data.TryPlace(run.Dest, out var dp) ? dp.name : run.Dest;
                    m.Options.Add(new MenuOption
                    {
                        Label = $"Official Dispatch: {run.Label} → {destName} · ₦{run.Reward:N0}",
                        Enabled = !HasTask,
                        Action = () => StartRun(c, run)
                    });
                }
            }
            AddRelationshipOptions(m, c);
            m.Options.Add(new MenuOption { Label = "Take your leave, Excellency", Action = null });
            OpenMenu(m);
        }

        public bool BustFraudsterForEfcc(string id)
        {
            State.Earn(50000);
            State.S.heat = 0;
            State.AddRep("public", 8);
            State.AddRep("business", 4);
            State.AddXp(25);
            State.Toast("EFCC Whistleblower Sting: Fraudster ledger handed to EFCC · +₦50,000 · Heat cleared to 0");
            return true;
        }

        public void OpenFraudsterMenu(World.ContactSystem.Contact c)
        {
            string id = c.data.id;
            var m = new Menu
            {
                Title = c.data.name,
                Subtitle = $"UNDERWORLD SYNDICATE · {c.data.role}",
                Line = GreetLine(c)
            };
            m.Options.Add(new MenuOption
            {
                Label = "EFCC Sting: Turn Over Syndicate Ledger to EFCC · +₦50,000 Bounty (Heat → 0, +8 Public Rep)",
                Action = () => BustFraudsterForEfcc(id)
            });
            m.Options.Add(new MenuOption
            {
                Label = "[Indoor Mission] Raid HK Cyber Safe-House Indoors · +₦75,000",
                Action = () => StartIndoorMission("indoor_efcc_hk_raid")
            });
            var fraudHub = HubFor(c);
            if (fraudHub.runs != null)
            {
                foreach (var r in fraudHub.runs)
                {
                    var run = r;
                    string destName = Data.TryPlace(run.Dest, out var dp) ? dp.name : run.Dest;
                    m.Options.Add(new MenuOption
                    {
                        Label = $"Intercept Courier Run: {run.Label} → {destName} · ₦{run.Reward:N0}",
                        Enabled = !HasTask,
                        Action = () => StartRun(c, run)
                    });
                }
            }
            AddRelationshipOptions(m, c);
            m.Options.Add(new MenuOption { Label = "Walk away clean — I no dey do 419", Action = () => State.Toast("You kept your hands clean.") });
            OpenMenu(m);
        }

        // ---- gym ----
        bool GymMember => State.S.gymUntil > Clock.Day;

        public bool Train(string kind, int fitness, int strength, int cost)
        {
            var st = State.S;
            if (st.stamina < 35f) { State.Toast("Too tired to train — eat or rest first"); return false; }
            if (cost > 0 && !State.Pay(cost)) { State.Toast("Not enough money"); return false; }
            st.stamina = Mathf.Max(0f, st.stamina - 35f); st.gymSessions++;
            if (fitness > 0) State.GainSkill("fitness", fitness);
            if (strength > 0) State.GainSkill("strength", strength);
            Hours(1f); State.AddXp(4);
            State.Toast($"{kind} done{(fitness > 0 ? $" · Fitness +{fitness}" : "")}{(strength > 0 ? $" · Strength +{strength}" : "")}");
            if (st.gymSessions == 5) State.Message("coach", "Five sessions. You dey build. Boxing class dey open for you now.");
            return true;
        }

        void OpenGymMenu(LandmarkData p = null)
        {
            var c = Data.gym; bool m = GymMember;
            var menu = new Menu { Title = "Coach Emeka", Subtitle = "Surulere Fitness Gym", Line = m ? $"Member till day {State.S.gymUntil}. Fitness helps you recover faster; strength makes sprinting cost less. Wetin we dey train today?" : $"Day pass ₦{c.dayPass:N0}, monthly ₦{c.monthly:N0}. Fitness helps recovery; strength makes sprinting cost less." };
            menu.Options.Add(new MenuOption { Label = m ? "Strength training" : $"Strength training · day pass ₦{c.dayPass:N0}", Action = () => Train("Strength training", 2, 5, m ? 0 : c.dayPass) });
            menu.Options.Add(new MenuOption { Label = m ? "Cardio" : $"Cardio · day pass ₦{c.dayPass:N0}", Action = () => { if (Train("Cardio", 5, 0, m ? 0 : c.dayPass)) State.S.stamina = Mathf.Min(100f, State.S.stamina + 10f); } });
            menu.Options.Add(new MenuOption { Label = $"Personal trainer · ₦{c.trainer:N0}", Action = () => Train("Trainer session", 5, 6, c.trainer) });
            if (State.S.skills.fitness >= c.boxingMin) menu.Options.Add(new MenuOption { Label = "Boxing class", Action = () => { if (Train("Boxing", 3, 4, m ? 0 : c.dayPass)) State.AddRep("street", 3); } });
            if (!m) menu.Options.Add(new MenuOption { Label = $"Monthly membership · ₦{c.monthly:N0}", Action = () => { if (!State.Pay(c.monthly)) { State.Toast("Not enough money"); return; } State.S.gymUntil = Clock.Day + 30; State.AddRep("social", 2); State.Toast("Member for 30 days"); } });
            if (!InInterior) menu.Options.Add(new MenuOption { Label = "Step inside the gym", Action = () => EnterInterior(p?.name ?? "Surulere Fitness Gym", "gym", () => OpenGymMenu(p)) });
            menu.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(menu);
        }

        // ---- restaurant ----
        void OpenFoodMenu(LandmarkData p)
        {
            var menu = new Menu { Title = "Cashier", Subtitle = p.name, Line = $"Welcome to {p.name}. Wetin you dey chop?" };
            foreach (var f in Data.food)
            {
                if (f.name == "Business lunch" && State.S.biz.Count == 0) continue;
                var item = f;
                menu.Options.Add(new MenuOption { Label = $"{f.name} · ₦{f.price:N0}", Action = () => Eat(item) });
            }
            if (!InInterior) menu.Options.Add(new MenuOption { Label = $"Step inside {p.name}", Action = () => EnterInterior(p.name, "restaurant", () => OpenFoodMenu(p)) });
            menu.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(menu);
        }

        public bool Eat(FoodData f)
        {
            if (!State.Pay(f.price)) { State.Toast("Not enough money"); return false; }
            State.S.stamina = Mathf.Min(100f, State.S.stamina + f.stamina); State.S.health = Mathf.Min(100f, State.S.health + f.business); Hours(0.5f);
            if (f.name == "Business lunch") { State.AddRep("business", 3); State.GainSkill("charisma", 2); State.Toast("Good meeting · Business reputation up"); }
            else State.Toast($"{f.name} · energy +{f.stamina}");
            return true;
        }

        // ---- mall and cinema ----
        void OpenMallMenu(LandmarkData p = null)
        {
            var c = Data.mall;
            var menu = new Menu { Title = "Mall Attendant", Subtitle = p?.name ?? "Adeniran Ogunsanya Mall", Line = "Clothing, electronics, supermarket, barber — or the cinema upstairs." };
            menu.Options.Add(new MenuOption { Label = $"Buy an outfit · ₦{c.outfit:N0}", Action = () => { if (!State.Pay(c.outfit)) { State.Toast("Not enough money"); return; } State.S.outfits++; NextFabric(); State.GainSkill("charisma", 3); State.AddRep("social", 2); State.Toast($"Fresh fit · {FabricName()} · Charisma +3"); } });
            menu.Options.Add(new MenuOption { Label = State.Has("laptop") ? "Laptop · owned" : $"Laptop · ₦{c.laptop:N0}", Enabled = !State.Has("laptop"), Action = () => { if (!State.Pay(c.laptop)) { State.Toast("Not enough money"); return; } State.AddItem("laptop"); State.Toast("Laptop bought — freelance gigs pay double at the cyber café"); } });
            menu.Options.Add(new MenuOption { Label = $"Supermarket run · ₦{c.groceries:N0}", Action = () => { if (!State.Pay(c.groceries)) { State.Toast("Not enough money"); return; } State.AddItem("water"); State.AddItem("suya"); State.S.stamina = Mathf.Min(100f, State.S.stamina + 30f); State.Toast("Pure water and suya in your bag · stamina +30"); } });
            menu.Options.Add(new MenuOption { Label = State.Has("pepper") ? "Pepper Spray · equip" : "Pepper Spray · ₦5,000", Action = () => BuyFightWeapon("pepper") });
            menu.Options.Add(new MenuOption { Label = State.Has("knuckles") ? "Brass Knuckles · equip" : "Brass Knuckles · ₦8,000", Action = () => BuyFightWeapon("knuckles") });
            menu.Options.Add(new MenuOption { Label = $"Barber · ₦{c.barber:N0}", Action = () => { if (!State.Pay(c.barber)) { State.Toast("Not enough money"); return; } State.S.lookHair = (State.S.lookHair + 1) % 5; Player.ApplyLook(State.Look); State.GainSkill("charisma", 1); State.Toast("Fresh cut"); } });
            menu.Options.Add(new MenuOption { Label = "Cinema", Action = OpenCinemaMenu });
            if (!InInterior) menu.Options.Add(new MenuOption { Label = "Step inside the mall", Action = () => EnterInterior(p?.name ?? "Adeniran Ogunsanya Mall", "mall", () => OpenMallMenu(p)) });
            menu.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(menu);
        }

        void OpenCinemaMenu()
        {
            var c = Data.mall;
            var menu = new Menu { Title = "Cinema", Subtitle = "Upstairs at the mall", Line = $"Now showing: {string.Join(", ", c.movies ?? new string[0])}. Ticket ₦{c.ticket:N0}." };
            string[] from = { "amaka", "babak", "nkechi" };
            string[] lines = { "You watched King of Boys? That one na Lagos politics. We go talk.", "Danfo Chronicles! That film na my life story.", "Lagos Never Sleeps... true talk. Come buy pure water." };
            for (int i = 0; i < (c.movies?.Length ?? 0); i++)
            {
                int idx = i; string movie = c.movies[i];
                menu.Options.Add(new MenuOption { Label = $"{movie} · ₦{c.ticket:N0}", Action = () =>
                {
                    if (!State.Pay(c.ticket)) { State.Toast("Not enough money"); return; }
                    Hours(2f); State.S.stamina = Mathf.Min(100f, State.S.stamina + 20f); State.AddRep("social", 3); State.AddXp(5);
                    if (idx < from.Length) State.Message(from[idx], lines[idx]);
                    State.Toast($"{movie} · good night out");
                } });
            }
            menu.Options.Add(new MenuOption { Label = "Back", Action = () => OpenMallMenu() });
            OpenMenu(menu);
        }

        // ---- cyber café ----
        void OpenCafeMenu(LandmarkData p = null)
        {
            var c = Data.cafe; int skilled = State.S.courses.Count; int gig = c.gig * (State.Has("laptop") ? 2 : 1);
            var menu = new Menu { Title = "Cyber Café Oga", Subtitle = p?.name ?? "Surulere Cyber Café", Line = skilled > 0 ? $"You get {skilled} certificate{(skilled > 1 ? "s" : "")}. Freelance gig ₦{gig:N0}. Courses still dey." : "Learn a trade for the digital economy. Certificates open better jobs." };
            foreach (var course in Data.courses)
            {
                if (State.HasCourse(course.id)) continue;
                var cr = course;
                menu.Options.Add(new MenuOption { Label = $"Learn {course.name} · ₦{course.cost:N0} · {course.hours}h", Action = () => Study(cr) });
            }
            if (skilled > 0) menu.Options.Add(new MenuOption { Label = $"Freelance gig · ₦{gig:N0}", Action = () =>
            {
                if (State.S.stamina < 25f) { State.Toast("Too tired to focus"); return; }
                State.Earn(gig); Hours(2f); State.S.stamina -= 25f; State.GainSkill("business", 3); State.AddRep("business", 2); State.AddXp(12); State.Toast($"Gig delivered · +₦{gig:N0}");
            } });
            menu.Options.Add(new MenuOption { Label = $"Browse jobs · ₦{c.browse:N0}", Action = () => { if (!State.Pay(c.browse)) { State.Toast("No money"); return; } Hours(0.5f); State.GainSkill("business", 1); State.Toast("Applications sent — the contacts dey wait"); } });
            if (!InInterior) menu.Options.Add(new MenuOption { Label = "Step inside the cyber café", Action = () => EnterInterior(p?.name ?? "Surulere Cyber Café", "cafe", () => OpenCafeMenu(p)) });
            menu.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(menu);
        }

        public void NextFabric() { if (Data.fabrics.Length == 0) return; State.S.lookFabric = (State.S.lookFabric + 1) % Data.fabrics.Length; Player.ApplyLook(State.Look); }
        public string FabricName() => Data.fabrics.Length > 0 ? Data.fabrics[Mathf.Clamp(State.S.lookFabric, 0, Data.fabrics.Length - 1)].name : "outfit";

        public bool Study(CourseData c)
        {
            if (State.S.stamina < 20f) { State.Toast("Too tired to study — rest first"); return false; }
            if (!State.Pay(c.cost)) { State.Toast("Not enough money"); return false; }
            State.S.stamina -= 20f; Hours(c.hours > 0 ? c.hours : 3f);
            return State.CompleteCourse(c);
        }

        // ---- roadside vendor ----
        void OpenVendorMenu()
        {
            var m = new Menu { Title = "Roadside Vendor", Subtitle = "Pure water · Gala · Suya · Street gear", Line = "Pure water! Gala! Plantain chips! Cold one!" };
            m.Options.Add(new MenuOption { Label = "Pure Water · ₦100", Action = () => { if (!State.Pay(100)) { State.Toast("No money"); return; } State.AddItem("water"); State.Toast("Pure water added"); } });
            m.Options.Add(new MenuOption { Label = "Gala & Coke · ₦800 (stamina +25)", Action = () => { if (!State.Pay(800)) { State.Toast("No money"); return; } State.S.stamina = Mathf.Min(100f, State.S.stamina + 25f); State.Toast("Energy +25"); } });
            m.Options.Add(new MenuOption { Label = "Suya · ₦1,500", Action = () => { if (!State.Pay(1500)) { State.Toast("No money"); return; } State.AddItem("suya"); State.Toast("Suya added"); } });
            m.Options.Add(new MenuOption { Label = State.Has("plank") ? "2x4 Nail Plank · equip" : "2x4 Nail Plank · ₦4,000", Action = () => BuyFightWeapon("plank") });
            m.Options.Add(new MenuOption { Label = State.Has("koboko") ? "Koboko Horsewhip · equip" : "Koboko Horsewhip · ₦6,000", Action = () => BuyFightWeapon("koboko") });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        // ---- night venues, clubbing, social nightlife, owambe, worship, hotel, police, NEPA ----
        public bool DoClubbingActivity(string id)
        {
            var st = State.S;
            switch (id)
            {
                case "club_dance":
                    st.stamina = 100f;
                    st.health = Mathf.Min(100f, st.health + 10f);
                    State.AddRep("social", 4);
                    State.GainSkill("charisma", 2f);
                    State.GainSkill("fitness", 2f);
                    Hours(1f);
                    State.AddXp(12);
                    State.Toast("Afrobeats dancefloor session! Stamina 100% · Social Rep +4 · Charisma +2");
                    return true;
                case "club_vip_bottles":
                    if (!State.Pay(25000)) { State.Toast("Need ₦25,000 for VIP Dorime Bottle Service"); return false; }
                    st.stamina = 100f;
                    st.health = 100f;
                    State.AddRep("social", 10);
                    State.AddRep("business", 6);
                    State.GainSkill("charisma", 4f);
                    Hours(1.5f);
                    State.AddXp(25);
                    State.Message("amaka", "Dorime at the VIP section! The whole Victoria Island and Surulere table saw you tonight.");
                    State.Toast("VIP Champagne & Dorime Bottle Service! +10 Social Rep · +6 Business Rep · Charisma +4");
                    return true;
                case "club_dj_set":
                    State.Earn(18000);
                    State.AddRep("social", 6);
                    State.GainSkill("charisma", 3f);
                    Hours(1f);
                    State.AddXp(20);
                    State.Toast("Hyped the club & spun an Afrobeats guest DJ set · +₦18,000 · Social Rep +6");
                    return true;
                case "club_spray_naira":
                    if (!State.Pay(10000)) { State.Toast("Need ₦10,000 to spray Naira bundles"); return false; }
                    st.stamina = 100f;
                    State.AddRep("social", 8);
                    State.AddRep("public", 4);
                    State.GainSkill("charisma", 3f);
                    Hours(1f);
                    State.AddXp(18);
                    State.Toast("Sprayed crisp Naira bundles on the dancefloor · +8 Social Rep · +4 Public Rep");
                    return true;
                case "social_suya_hangout":
                    if (!State.Pay(3500)) { State.Toast("Need ₦3,500 for Late-Night Suya & Chapman"); return false; }
                    st.stamina = Mathf.Min(100f, st.stamina + 50f);
                    st.health = Mathf.Min(100f, st.health + 20f);
                    State.AddRep("social", 4);
                    Hours(0.5f);
                    State.AddXp(10);
                    State.Toast("Late-night Suya & Chapman hangout with the crew · +50 Stamina · +4 Social Rep");
                    return true;
            }
            return false;
        }

        public void OpenNightlifeHubMenu()
        {
            var m = new Menu
            {
                Title = "Lagos Clubbing & Social Nightlife",
                Subtitle = $"Social Rep {State.S.rep.social} · Charisma {State.S.skills.charisma:0} · {(VenueOpen ? "Clubs & Lounges OPEN" : "Daytime Socials & VIP Lounges Available")}",
                Line = "From Surulere night lounges to Victoria Island VIP tables and Saturday Owambe halls — dance, pop bottles, spin an Afrobeats DJ set, or network."
            };
            m.Options.Add(new MenuOption { Label = "Hit the Afrobeats Dancefloor (Stamina 100%, +4 Social Rep, +2 Charisma)", Action = () => DoClubbingActivity("club_dance") });
            m.Options.Add(new MenuOption { Label = "VIP Dorime Bottle Service & Networking · ₦25,000 (+10 Social, +6 Business Rep)", Action = () => DoClubbingActivity("club_vip_bottles") });
            m.Options.Add(new MenuOption { Label = "Perform Guest DJ & Hypeman Set · +₦18,000 (+6 Social Rep, +3 Charisma)", Action = () => DoClubbingActivity("club_dj_set") });
            m.Options.Add(new MenuOption { Label = "Spray Crisp Naira at the Party · ₦10,000 (+8 Social, +4 Public Rep)", Action = () => DoClubbingActivity("club_spray_naira") });
            m.Options.Add(new MenuOption { Label = "Late-Night Suya & Chapman Crew Hangout · ₦3,500 (+50 Stamina, +4 Social Rep)", Action = () => DoClubbingActivity("social_suya_hangout") });
            if (!InInterior)
                m.Options.Add(new MenuOption { Label = "Step inside 3D Neon Nightclub & VIP Lounge", Action = () => EnterInterior("Eko Neon Club & VIP Lounge", "venue", OpenNightlifeHubMenu) });
            m.Options.Add(new MenuOption { Label = "Close", Action = null });
            OpenMenu(m);
        }

        void OpenVenueMenu(LandmarkData p)
        {
            int cost = p.cost > 0 ? p.cost : 5000;
            var m = new Menu
            {
                Title = p.name,
                Subtitle = $"Nightclub & Social Lounge · Social Rep {State.S.rep.social}",
                Line = VenueOpen ? "Afrobeats dey bump, cold drinks and VIP bottle service dey! Come unwind and network." : $"{p.name} peak hours start at {Data.meta.venueOpen:0}:00 — VIP lounge and indoor club floor open."
            };
            m.Options.Add(new MenuOption { Label = $"Club Entry & Night Out · ₦{cost:N0} (stamina 100%, +10 health, +2 Social Rep)", Enabled = VenueOpen, Action = () => { if (!State.Pay(cost)) { State.Toast("Not enough money"); return; } State.S.stamina = 100f; State.S.health = Mathf.Min(100f, State.S.health + 10f); State.AddXp(5); State.AddRep("social", 2); State.Toast($"Good vibes at {p.name} · stamina restored"); } });
            m.Options.Add(new MenuOption { Label = "Dance on the Afrobeats Floor (+100% Stamina, +4 Social Rep, +2 Charisma)", Action = () => DoClubbingActivity("club_dance") });
            m.Options.Add(new MenuOption { Label = "VIP Dorime Bottle Service · ₦25,000 (+10 Social Rep, +6 Business Rep)", Action = () => DoClubbingActivity("club_vip_bottles") });
            m.Options.Add(new MenuOption { Label = "Host Guest DJ & Hypeman Set · +₦18,000 (+6 Social Rep)", Action = () => DoClubbingActivity("club_dj_set") });
            m.Options.Add(new MenuOption { Label = "Late-Night Suya & Chapman Hangout · ₦3,500 (+50 Stamina, +4 Social Rep)", Action = () => DoClubbingActivity("social_suya_hangout") });
            if (!InInterior) m.Options.Add(new MenuOption { Label = $"Step inside {p.name} (3D Dancefloor & VIP Lounge)", Action = () => EnterInterior(p.name, "venue", () => OpenVenueMenu(p)) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        void OpenOwambeMenu(LandmarkData p)
        {
            int cost = p.cost > 0 ? p.cost : 3000;
            bool ok = OwambeOpen && State.S.partyDay != Clock.Day;
            var m = new Menu { Title = p.name, Subtitle = "Saturday Owambe & High-Society Party", Line = !OwambeOpen ? "Main Owambe band starts at 19:00, or step inside the hall for social events." : State.S.partyDay == Clock.Day ? "You don spray enough for tonight." : "Live jùjú band, aso-ebi, small chops — spray some naira and network!" };
            m.Options.Add(new MenuOption { Label = $"Join the owambe & spray · ₦{cost:N0}", Enabled = ok, Action = () => { if (!State.Pay(cost)) { State.Toast("Not enough money to spray"); return; } State.S.partyDay = Clock.Day; State.S.stamina = 100f; State.AddXp(15); State.AddRep("social", 8); State.GainSkill("charisma", 3f); State.Toast("You spray, you dance, you network · +15 XP"); State.Message("amaka", "Saw you at the owambe. You sabi dance! Call me when you need work."); } });
            m.Options.Add(new MenuOption { Label = "Spray Crisp Naira Bundles · ₦10,000 (+8 Social Rep, +4 Public Rep)", Action = () => DoClubbingActivity("club_spray_naira") });
            m.Options.Add(new MenuOption { Label = "VIP High-Table Bottle Service · ₦25,000 (+10 Social, +6 Business Rep)", Action = () => DoClubbingActivity("club_vip_bottles") });
            if (!InInterior) m.Options.Add(new MenuOption { Label = $"Step inside {p.name}", Action = () => EnterInterior(p.name, "owambe", () => OpenOwambeMenu(p)) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public bool Pray(LandmarkData p)
        {
            if (State.S.prayedDay == Clock.Day) { State.Toast("You already prayed today"); return false; }
            State.S.prayedDay = Clock.Day;
            State.S.stamina = Mathf.Min(100f, State.S.stamina + 30f);
            if (State.S.heat > 0) State.S.heat--;
            State.AddXp(5); State.AddRep("public", 3);
            State.Toast($"{p.name} · peace of mind, heat eased");
            return true;
        }

        void OpenWorshipMenu(LandmarkData p)
        {
            var m = new Menu { Title = p.name, Subtitle = "Place of worship", Line = State.S.prayedDay == Clock.Day ? "You already prayed today. Go in peace." : "Step inside for quiet reflection and prayer." };
            m.Options.Add(new MenuOption { Label = "Pray (+30 stamina, −1 heat)", Enabled = State.S.prayedDay != Clock.Day, Action = () => Pray(p) });
            if (!InInterior) m.Options.Add(new MenuOption { Label = $"Step inside {p.name}", Action = () => EnterInterior(p.name, "worship", () => Pray(p)) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        void OpenHotelMenu(LandmarkData p)
        {
            int cost = p.cost > 0 ? p.cost : 15000;
            var m = new Menu { Title = p.name, Subtitle = "Hotel & Suites", Line = $"Check in for the night · ₦{cost:N0}. Rest until 07:00 with full health and stamina." };
            m.Options.Add(new MenuOption { Label = $"Check in · ₦{cost:N0}", Action = () => { if (!State.Pay(cost)) { State.Toast("Not enough money"); return; } Sleep(); } });
            if (!InInterior) m.Options.Add(new MenuOption { Label = $"Step inside {p.name}", Action = () => EnterInterior(p.name, "hotel", () => { if (State.Pay(cost)) Sleep(); }) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        void OpenPoliceMenu(LandmarkData p)
        {
            int fine = State.S.heat * Data.meta.fineRate;
            var m = new Menu { Title = p.name, Subtitle = "Desk Sergeant", Line = State.S.heat > 0 ? $"Settle your fine of ₦{fine:N0} to clear your record." : "No warrant on your head today. Drive carefully." };
            if (State.S.heat > 0) m.Options.Add(new MenuOption { Label = $"Settle fine · ₦{fine:N0}", Action = () => { if (!State.Pay(fine)) { State.Toast("Not enough money to settle the fine"); return; } State.S.heat = 0; State.Toast($"Record cleared · ₦{fine:N0} paid"); } });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        void OpenNepaMenu(LandmarkData p)
        {
            bool outage = Events != null && Events.Outage > 0f;
            var m = new Menu { Title = p.name, Subtitle = "PHCN Undertaking", Line = outage ? "Transformer dey on generator backup. Drop ₦5,000 for diesel make light return now." : "PHCN: No outage for now. Pay your bill." };
            if (outage) m.Options.Add(new MenuOption { Label = "Pay for diesel · ₦5,000 (restore light)", Action = () => { if (!State.Pay(5000)) { State.Toast("Not enough money"); return; } Events.RestorePower(); State.Toast("Up NEPA! Light don come back."); } });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        // ---- Central Bank of Nigeria (CBN) & Economic and Financial Crimes Commission (EFCC) ----
        int cbnBondDay = -1;
        int efccWhistleDay = -1;

        public bool BuyCbnTreasuryBond()
        {
            if (cbnBondDay == Clock.Day) { State.Toast("CBN Treasury Bill already subscribed today"); return false; }
            if (!State.Pay(50000)) { State.Toast("Need ₦50,000 to subscribe to a CBN Treasury Bill"); return false; }
            cbnBondDay = Clock.Day;
            State.S.bank += 58000;
            State.GainSkill("business", 3f);
            State.AddRep("business", 5);
            State.AddRep("public", 2);
            State.AddXp(15);
            State.Toast("CBN 91-Day Treasury Bill matured · +₦58,000 credited to RiseBank · Business +3");
            return true;
        }

        public void OpenCbnMenu(LandmarkData p = null)
        {
            var m = new Menu
            {
                Title = p != null ? p.name : "Central Bank of Nigeria · Tinubu Square",
                Subtitle = "Dr. Ngozi Okafor · CBN Treasury & Bullion Operations",
                Line = $"Cash ₦{State.S.cash:N0} · Bank ₦{State.S.bank:N0}. Subscribe to CBN Treasury Bills, manage interbank reserves, or escort armored bullion."
            };
            m.Options.Add(new MenuOption
            {
                Label = cbnBondDay == Clock.Day ? "CBN Treasury Bill · Subscribed Today" : "Subscribe CBN Treasury Bill · ₦50,000 → ₦58,000 in Bank (+5 Business Rep)",
                Enabled = cbnBondDay != Clock.Day,
                Action = () => BuyCbnTreasuryBond()
            });
            m.Options.Add(new MenuOption
            {
                Label = "Open Interbank Treasury Window (RiseBank)",
                Action = AppBank
            });
            m.Options.Add(new MenuOption
            {
                Label = $"[CBN Action Op] CBN Bullion Convoy Defense · ₦110,000{(HasGun() ? "" : " [Need Firearm]")}",
                Enabled = !HasTask,
                Action = () => StartActionOp("cbn_bullion_defense")
            });
            if (!InInterior)
                m.Options.Add(new MenuOption { Label = "Step inside CBN Banking Hall", Action = () => EnterInterior(p?.name ?? "Central Bank of Nigeria", "bank", () => OpenCbnMenu(p)) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public bool ClearEfccAudit()
        {
            int fee = Mathf.Max(8000, State.S.heat * 8000);
            if (!State.Pay(fee)) { State.Toast($"Need ₦{fee:N0} for EFCC Tax & Compliance Clearance"); return false; }
            State.S.heat = 0;
            State.AddRep("public", 4);
            State.AddRep("business", 3);
            State.AddXp(10);
            State.Toast("EFCC Financial Clearance Certificate issued · Heat 0 · Public Rep +4");
            return true;
        }

        public void OpenEfccMenu(LandmarkData p = null)
        {
            int clearanceFee = Mathf.Max(8000, State.S.heat * 8000);
            var m = new Menu
            {
                Title = p != null ? p.name : "EFCC Zonal Command · Ikoyi",
                Subtitle = "Commander Halima Bello · Economic and Financial Crimes Commission",
                Line = State.S.heat > 0
                    ? $"EFCC financial watch active (Heat {State.S.heat}). File compliance clearance (₦{clearanceFee:N0}) or join an anti-graft operation."
                    : "Record clean with the Eagle! File tax compliance, report 419 syndicates, or join an EFCC tactical raid."
            };
            m.Options.Add(new MenuOption
            {
                Label = $"EFCC Tax & Financial Compliance Clearance · ₦{clearanceFee:N0} (Heat → 0, +4 Public Rep)",
                Action = () => ClearEfccAudit()
            });
            m.Options.Add(new MenuOption
            {
                Label = efccWhistleDay == Clock.Day ? "Anti-Fraud Whistleblower Dossier · Filed Today" : "File Anti-Fraud Whistleblower Intel · +₦15,000 Bounty (+3 Public Rep)",
                Enabled = efccWhistleDay != Clock.Day,
                Action = () =>
                {
                    efccWhistleDay = Clock.Day;
                    State.Earn(15000);
                    State.AddRep("public", 3);
                    State.AddXp(12);
                    State.Toast("EFCC Whistleblower Bounty paid · +₦15,000");
                }
            });
            m.Options.Add(new MenuOption
            {
                Label = $"[EFCC Action Op] EFCC Syndicate Asset Raid · ₦115,000{(HasGun() ? "" : " [Need Firearm]")}",
                Enabled = !HasTask,
                Action = () => StartActionOp("efcc_cyber_raid")
            });
            if (!InInterior)
                m.Options.Add(new MenuOption { Label = "Step inside EFCC Zonal Command", Action = () => EnterInterior(p?.name ?? "EFCC Zonal Command", "police", () => OpenEfccMenu(p)) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public void OpenHospitalMenu(LandmarkData p)
        {
            var m = new Menu
            {
                Title = p?.name ?? "General Hospital",
                Subtitle = "Emergency Ward & Community Outreach",
                Line = $"Health {State.S.health:0}/100 · Stamina {State.S.stamina:0}/100. Receive full medical treatment or sponsor free maternal & emergency care."
            };
            m.Options.Add(new MenuOption
            {
                Label = "Full Medical Checkup & IV Drip · ₦4,000 (+100 Health, +50 Stamina)",
                Action = () =>
                {
                    if (!State.Pay(4000)) { State.Toast("Need ₦4,000 for medical checkup"); return; }
                    State.S.health = 100f;
                    State.S.stamina = Mathf.Min(100f, State.S.stamina + 50f);
                    Hours(1f);
                    State.Toast("Full medical checkup complete · Health 100%");
                }
            });
            m.Options.Add(new MenuOption
            {
                Label = "Sponsor Ward Outreach Supplies · ₦10,000 (+5 Public Rep, +20 XP)",
                Action = () =>
                {
                    if (!State.Pay(10000)) { State.Toast("Need ₦10,000 to sponsor hospital outreach"); return; }
                    State.AddRep("public", 5);
                    State.AddRep("social", 3);
                    State.AddXp(20);
                    State.Toast("Hospital outreach sponsored · Public Rep +5");
                }
            });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public void OpenAirportMenu(LandmarkData p)
        {
            var m = new Menu
            {
                Title = p?.name ?? "Nigeria Aviation Terminal",
                Subtitle = "Domestic Commercial Airlines (Air Peace · Ibom Air) & Executive Jet Charter",
                Line = "Book a commercial domestic flight across Nigeria’s 6 Geopolitical Zones or charter a Private Executive Jet."
            };
            (string id, string label, int commercialFare)[] airports =
            {
                ("ikeja-airport", "Murtala Muhammed Int'l Airport (Lagos MMA)", 15000),
                ("lekki-epe-airport", "Lekki–Epe International Airport (Lagos East)", 15000),
                ("abuja-airport", "Nnamdi Azikiwe Int'l Airport (FCT Abuja)", 28000),
                ("ph-airport", "Port Harcourt Int'l Airport (Rivers · Niger Delta)", 26000),
                ("enugu-anambra-airport", "Akanu Ibiam / Anambra Airport (Enugu & Onitsha)", 25000),
                ("kano-airport", "Mallam Aminu Kano Int'l Airport (Kano & North)", 32000),
            };
            foreach (var ap in airports)
            {
                if (p != null && p.id == ap.id) continue;
                if (!Data.TryPlace(ap.id, out var destPlace)) continue;
                var targetPos = destPlace.World() + new Vector3(0f, 0.6f, -10f);
                var destName = ap.label;
                int fare = ap.commercialFare;
                m.Options.Add(new MenuOption
                {
                    Label = $"Commercial Flight to {destName} · ₦{fare:N0}",
                    Action = () =>
                    {
                        if (!State.Pay(fare)) { State.Toast($"Need ₦{fare:N0} for commercial flight"); return; }
                        if (Vehicle != null) Exit();
                        Player.Teleport(targetPos, Player.transform.eulerAngles.y);
                        Hours(1.0f);
                        State.AddXp(15);
                        State.Toast($"Landed at {destName}");
                    }
                });
            }
            if (Data.TryPlace(p != null && p.id == "abuja-airport" ? "ikeja-airport" : "abuja-airport", out var vipDest))
            {
                string vipLabel = p != null && p.id == "abuja-airport" ? "Lagos MMA VIP Presidential Wing" : "Abuja Presidential Wing (FCT)";
                var vipPos = vipDest.World() + new Vector3(0f, 0.6f, -10f);
                m.Options.Add(new MenuOption
                {
                    Label = $"Charter Executive Private Jet to {vipLabel} · ₦75,000 (+5 Business Rep, +3 Social Rep)",
                    Action = () =>
                    {
                        if (!State.Pay(75000)) { State.Toast("Need ₦75,000 for Private Jet Charter"); return; }
                        if (Vehicle != null) Exit();
                        Player.Teleport(vipPos, Player.transform.eulerAngles.y);
                        Hours(0.6f);
                        State.AddRep("business", 5);
                        State.AddRep("social", 3);
                        State.AddXp(25);
                        State.Toast($"Private Jet touched down at {vipLabel} · Business Rep +5");
                    }
                });
            }
            m.Options.Add(new MenuOption
            {
                Label = "Nigeria 36 States & FCT Interstate Directory",
                Action = OpenStatesDirectory
            });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public void OpenMuseumMenu(LandmarkData p)
        {
            var m = new Menu
            {
                Title = p?.name ?? "Heritage Museum",
                Subtitle = "Nigerian History, Afrobeat & Coastal Archives",
                Line = "Explore archival exhibits, oral histories, and cultural monuments (+Charisma, +Social & Public Rep)."
            };
            m.Options.Add(new MenuOption
            {
                Label = "Guided Heritage & History Tour · ₦3,000 (+2 Charisma, +3 Public Rep, +15 XP)",
                Action = () =>
                {
                    if (!State.Pay(3000)) { State.Toast("Need ₦3,000 for guided tour"); return; }
                    State.GainSkill("charisma", 2);
                    State.AddRep("public", 3);
                    State.AddRep("social", 2);
                    State.AddXp(15);
                    Hours(1.5f);
                    State.Toast($"Completed heritage tour at {p?.name ?? "the museum"} · Charisma +2");
                }
            });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public void OpenMarketMenu(LandmarkData p)
        {
            bool isAuto = p != null && p.id.Contains("ladipo");
            string st = Data.StateAt(p != null ? p.World() : Focus.position);
            string commodity = p != null && (p.id.Contains("alaba") || p.id.Contains("onitsha") || p.id.Contains("ariaria")) ? "electronics" : "produce";
            float mult = Net.CloudEconomy.GetStateCommodityMultiplier(st, commodity);
            int lotCost = Mathf.RoundToInt(12000 * mult);
            int lotRev = Mathf.RoundToInt(17500 / Mathf.Max(0.68f, mult));
            var m = new Menu
            {
                Title = p?.name ?? "Lagos Mega Market",
                Subtitle = isAuto ? "Tokunbo Auto Spare Parts, Respray & Tuning" : $"Wholesale {commodity.ToUpperInvariant()} Trade · [{st.ToUpperInvariant()} Index {mult:0.00}×]",
                Line = isAuto
                    ? "Repair or respray your vehicle at Ladipo prices, or buy bulk tokunbo spare parts."
                    : $"Source wholesale market crates at {st.ToUpperInvariant()} regional prices (₦{lotCost:N0}), flip to interstate retailers, or step inside."
            };
            if (isAuto && Vehicle != null)
            {
                m.Options.Add(new MenuOption
                {
                    Label = "Ladipo Express Overhaul & Respray · ₦8,000 (100% Condition, Clear 1 Heat)",
                    Action = () =>
                    {
                        if (!State.Pay(8000)) { State.Toast("Need ₦8,000 for Ladipo overhaul"); return; }
                        Vehicle.Condition = 100f;
                        if (State.S.heat > 0) State.S.heat = Mathf.Max(0, State.S.heat - 1);
                        State.GainSkill("driving", 1);
                        State.Toast("Ladipo overhaul complete · Vehicle 100%");
                    }
                });
            }
            m.Options.Add(new MenuOption
            {
                Label = $"Buy Wholesale Trade Lot & Flip ({st.ToUpperInvariant()} {mult:0.00}×) · ₦{lotCost:N0} → +₦{lotRev:N0} (+2 Business Skill)",
                Action = () =>
                {
                    if (State.S.stamina < 20f) { State.Toast("Too tired to hustle the market"); return; }
                    if (!State.Pay(lotCost)) { State.Toast($"Need ₦{lotCost:N0} capital for wholesale lot"); return; }
                    State.S.stamina -= 20f;
                    Hours(1.5f);
                    int revenue = lotRev + Mathf.RoundToInt(State.S.skills.business * 60f);
                    State.Earn(revenue);
                    State.GainSkill("business", 2);
                    State.AddRep("business", 3);
                    State.AddXp(16);
                    State.Toast($"Flipped wholesale lot at {p?.shortName ?? "Market"} ({st.ToUpperInvariant()}) · +₦{revenue:N0}");
                }
            });
            if (!InInterior)
                m.Options.Add(new MenuOption { Label = $"Step inside {p?.shortName ?? "Market"} Trade Hall", Action = () => EnterInterior(p?.name ?? "Market Hall", "shop", () => OpenMarketMenu(p)) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public void OpenDepotMenu(LandmarkData p)
        {
            string st = Data.StateAt(p != null ? p.World() : Focus.position);
            float fuelMult = Net.CloudEconomy.GetStateCommodityMultiplier(st, "fuel");
            int fuelCost = Mathf.RoundToInt(1500 * fuelMult);
            var m = new Menu
            {
                Title = p?.name ?? "Industrial Depot & Terminal",
                Subtitle = $"Refinery, Deep Sea Port, Tank Farm & Agro-Mill · [{st.ToUpperInvariant()} Fuel Index {fuelMult:0.00}×]",
                Line = "Refuel vehicles at regional bulk depot rates, inspect industrial logistics, or step inside the operations floor."
            };
            if (Vehicle != null)
            {
                m.Options.Add(new MenuOption
                {
                    Label = $"Bulk Depot Fuel & Maintenance ({st.ToUpperInvariant()} {fuelMult:0.00}×) · ₦{fuelCost:N0} (Full Tank & 100% Condition)",
                    Action = () =>
                    {
                        if (!State.Pay(fuelCost)) { State.Toast($"Need ₦{fuelCost:N0} for depot refuel"); return; }
                        State.S.fuel = 100f;
                        if (Vehicle != null) Vehicle.Condition = 100f;
                        State.Toast($"Refuelled & serviced at {st.ToUpperInvariant()} industrial depot");
                    }
                });
            }
            foreach (var j in Data.JobsAt(p?.id ?? ""))
            {
                var job = j;
                bool ok = State.CanTake(j);
                m.Options.Add(new MenuOption
                {
                    Label = ok ? $"Work Shift — {j.title} · ₦{State.JobPay(j):N0}" : $"{j.title} (needs {j.skill} {j.min})",
                    Enabled = ok,
                    Action = () => StartShift(job)
                });
            }
            if (!InInterior)
                m.Options.Add(new MenuOption { Label = $"Step inside {p?.shortName ?? "Depot"} Operations Centre", Action = () => EnterInterior(p?.name ?? "Industrial Depot", "office", () => OpenDepotMenu(p)) });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public void OpenInterchangeMenu(LandmarkData p)
        {
            var m = new Menu
            {
                Title = p?.name ?? "Transport Interchange",
                Subtitle = "BRT, Danfo & Interstate Luxury Bus Terminal",
                Line = "Board an express BRT shuttle across Lagos megacorridors or take an interstate luxury coach across Nigeria."
            };
            (string id, string label, int fare)[] hubs =
            {
                ("oshodi-interchange", "Oshodi 3-Terminal Interchange (Lagos)", 1000),
                ("ojota-motor-park", "Ojota Interstate Motor Park (Lagos)", 1000),
                ("mile-2", "Mile 2 Transport Hub (Lagos)", 1000),
                ("ajah-town", "Ajah Bus Terminal (Lekki)", 1000),
                ("ikorodu-garage-town", "Ikorodu Garage (Lagos)", 1000),
                ("agege-garage", "Agege Bus Garage (Lagos)", 1000),
                ("panseke-bus", "Panseke Motor Park (Abeokuta · Ogun)", 4000),
                ("iwo-road-bus", "Iwo Road Mega Terminal (Ibadan · Oyo)", 6000),
                ("utako-bus", "Utako Luxury Bus Terminal (FCT Abuja)", 18000),
                ("upper-iweka-bus", "Upper Iweka Motor Park (Onitsha · Anambra)", 15000),
                ("waterlines-bus", "Waterlines Junction (Port Harcourt · Rivers)", 16000),
                ("sabon-gari-park", "Sabon Gari Luxury Park (Kano)", 22000),
            };
            foreach (var h in hubs)
            {
                if (p != null && p.id == h.id) continue;
                if (!Data.TryPlace(h.id, out var dest)) continue;
                var targetPos = dest.World() + new Vector3(0f, 0.6f, -8f);
                var label = h.label;
                int fare = h.fare;
                m.Options.Add(new MenuOption
                {
                    Label = $"{(fare <= 1000 ? "Express BRT" : "Interstate Coach")} to {label} · ₦{fare:N0}",
                    Action = () =>
                    {
                        if (!State.Pay(fare)) { State.Toast($"Need ₦{fare:N0} for bus fare"); return; }
                        if (Vehicle != null) Exit();
                        Player.Teleport(targetPos, Player.transform.eulerAngles.y);
                        Hours(fare <= 1000 ? 0.4f : 1.5f);
                        State.Toast($"Arrived at {label}");
                    }
                });
            }
            m.Options.Add(new MenuOption { Label = "Nigeria 36 States Interstate Coach Directory", Action = OpenStatesDirectory });
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public void OpenRailMenu(LandmarkData p)
        {
            var m = new Menu
            {
                Title = p?.name ?? "NRC & Lagos Metro Rail Station",
                Subtitle = "Lagos Blue/Red Line Metro & NRC Standard-Gauge Interstate Railway",
                Line = "Board a Lagos Metropolitan electric train (Marina · National Theatre · Ebute Metta) or an NRC Standard-Gauge express train to Abeokuta, Ibadan, Abuja, and Kano."
            };
            (string id, string label, int fare)[] stations =
            {
                ("marina-rail", "Marina Blue Line Station (Lagos Island)", 500),
                ("theatre-rail", "National Theatre Station (Costain / Iganmu)", 500),
                ("ebute-rail", "Mobolaji Johnson NRC Station (Ebute Metta)", 500),
                ("abeokuta-rail", "Abeokuta Laderin NRC Station (Ogun State)", 3500),
                ("ibadan-rail", "Obafemi Awolowo NRC Station · Moniya (Ibadan, Oyo)", 5000),
                ("abuja-rail", "Idu NRC Standard-Gauge Terminal (FCT Abuja)", 10000),
                ("kano-rail", "Kaduna–Kano NRC Standard-Gauge Terminal (Kano)", 12000),
            };
            foreach (var st in stations)
            {
                if (p != null && p.id == st.id) continue;
                if (!Data.TryPlace(st.id, out var dest)) continue;
                var targetPos = dest.World() + new Vector3(0f, 0.6f, -8f);
                var label = st.label;
                int fare = st.fare;
                m.Options.Add(new MenuOption
                {
                    Label = $"{(fare <= 500 ? "Lagos Metro Train" : "NRC Standard-Gauge Express")} to {label} · ₦{fare:N0}",
                    Action = () =>
                    {
                        if (!State.Pay(fare)) { State.Toast($"Need ₦{fare:N0} for rail ticket"); return; }
                        if (Vehicle != null) Exit();
                        Player.Teleport(targetPos, Player.transform.eulerAngles.y);
                        Hours(fare <= 500 ? 0.25f : 1.0f);
                        State.AddXp(10);
                        State.Toast($"Arrived by rail at {label}");
                    }
                });
            }
            m.Options.Add(new MenuOption { Label = "Leave Station", Action = null });
            OpenMenu(m);
        }

        public void OpenCheckpointMenu(LandmarkData p)
        {
            var m = new Menu
            {
                Title = p?.name ?? "Interstate Highway Checkpoint",
                Subtitle = "FRSC · Nigeria Customs Service · NDLEA · Military JTF Border Gate",
                Line = $"Heat {State.S.heat} · Inspect vehicle papers & waybills, clear interstate customs manifests, or run a highway border escort."
            };
            m.Options.Add(new MenuOption
            {
                Label = "Present Vehicle Papers for FRSC Inspection (Clear 1 Heat, +2 Public Rep)",
                Action = () =>
                {
                    if (State.S.heat > 0) State.S.heat = Mathf.Max(0, State.S.heat - 1);
                    State.AddRep("public", 2);
                    State.AddXp(10);
                    State.Toast($"FRSC & Customs inspection passed at {p?.shortName ?? "Checkpoint"} · Public Rep +2");
                }
            });
            m.Options.Add(new MenuOption
            {
                Label = "Stamp Interstate Customs Manifesto · ₦5,000 (+3 Business Rep, +15 XP)",
                Action = () =>
                {
                    if (!State.Pay(5000)) { State.Toast("Need ₦5,000 for Customs manifesto stamp"); return; }
                    State.AddRep("business", 3);
                    State.AddRep("public", 2);
                    State.AddXp(15);
                    State.Toast("Interstate Customs Manifesto stamped · Business Rep +3");
                }
            });
            m.Options.Add(new MenuOption
            {
                Label = "Join Highway Anti-Smuggling Patrol · +₦28,000 (+4 Public Rep, +20 XP)",
                Action = () =>
                {
                    if (State.S.stamina < 20f) { State.Toast("Too tired for highway patrol"); return; }
                    State.S.stamina -= 20f;
                    Hours(1.5f);
                    State.Earn(28000);
                    State.AddRep("public", 4);
                    State.GainSkill("driving", 2);
                    State.AddXp(20);
                    State.Toast($"Completed highway border patrol at {p?.shortName ?? "Checkpoint"} · +₦28,000");
                }
            });
            m.Options.Add(new MenuOption { Label = "Nigeria 36 States & FCT Highway Directory", Action = OpenStatesDirectory });
            m.Options.Add(new MenuOption { Label = "Proceed on Highway", Action = null });
            OpenMenu(m);
        }
    }
}
