using System.Collections.Generic;
using UnityEngine;
using Rise.Data;
using Rise.Vehicles;

namespace Rise.World
{
    // Dynamic city events & street encounters: go-slow jams, road-construction diversions that close a lane
    // with cones, barriers and a DIVERSION sign, an overturned keke, the storm flood at the Ojuelegba underpass,
    // NEPA outages that take the street lights at night, and interactive roadside encounters (broken-down danfo,
    // roadside dispute, stranded executive).
    public class Events : MonoBehaviour
    {
        public class Jam { public RoadData road; public float from, to, until; public bool works, wreck, flood; }
        public class Encounter { public string kind, title, prompt, rep; public Vector3 pos; public int reward, repAmount, xp; public float until; }

        public class HistoricEvent
        {
            public string Id, Category, Title, Headline, Summary, CivicPrompt, CivicRep;
            public int StartYear, StartMonth, StartDay, EndYear, EndMonth, EndDay, CivicReward;
            public float PayMultiplier = 1f;
            public bool CurfewActive;
        }

        // Chronological historic milestones & trending news across Lagos and Nigeria (starting Jan 1, 1999).
        public static readonly HistoricEvent[] HistoricTimeline =
        {
            new HistoricEvent {
                Id = "transition_1999", Category = "civic", Title = "1999 Fourth Republic Transition",
                Headline = "LAGOS CHRONICLE: INEC Releases Transition Timetable as Nigeria Prepares for Return to Civilian Rule",
                Summary = "Voter registration opens across Surulere, Ikeja, and Lagos Island ahead of the historic February 1999 elections.",
                StartYear = 1999, StartMonth = 1, StartDay = 1, EndYear = 1999, EndMonth = 2, EndDay = 26,
                PayMultiplier = 1.05f, CurfewActive = false,
                CivicPrompt = "REGISTER VOTERS AT INEC TRANSITION BOOTH · +₦12,000", CivicReward = 12000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "election_1999", Category = "election", Title = "1999 Nigerian General Elections",
                Headline = "ELECTION 1999: Millions Vote Across Lagos as Obasanjo & Tinubu Win Historic Fourth Republic Mandate",
                Summary = "INEC polling units bustle from Tafawa Balewa Square to Alausa as Nigeria elects civilian leadership.",
                StartYear = 1999, StartMonth = 2, StartDay = 27, EndYear = 1999, EndMonth = 5, EndDay = 28,
                PayMultiplier = 1.10f, CurfewActive = true,
                CivicPrompt = "CAST VOTE & MONITOR INEC POLLING UNIT · +₦15,000", CivicReward = 15000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "democracy_day_1999", Category = "civic", Title = "Fourth Republic Inauguration (Democracy Day)",
                Headline = "DEMOCRACY DAY 1999: Fourth Republic Born! Celebrations Fill Tafawa Balewa Square & National Stadium",
                Summary = "Civilian governance returns on May 29, 1999; Lagos State ministries at Alausa launch new urban road and market reforms.",
                StartYear = 1999, StartMonth = 5, StartDay = 29, EndYear = 1999, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.12f, CurfewActive = false,
                CivicPrompt = "JOIN TBS DEMOCRACY DAY CIVIC PARADE · +₦14,000", CivicReward = 14000, CivicRep = "social"
            },
            new HistoricEvent {
                Id = "millennium_2000", Category = "culture", Title = "Y2K Millennium Dawn in Lagos",
                Headline = "YEAR 2000: Lagos Welcomes the New Millennium — Cybercafés & Nollywood Home Video Boom Across Surulere",
                Summary = "Y2K bug fears pass; Idumota Nollywood markets and Yaba cybercafés surge with new millennial business.",
                StartYear = 2000, StartMonth = 1, StartDay = 1, EndYear = 2000, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.10f, CurfewActive = false,
                CivicPrompt = "UPGRADE Y2K CYBERCAFÉ TERMINALS · +₦15,000", CivicReward = 15000, CivicRep = "business"
            },
            new HistoricEvent {
                Id = "gsm_revolution_2001", Category = "telecom", Title = "2001 GSM Mobile Telecom Revolution",
                Headline = "TRENDING: MTN & Econet Launch GSM Networks — SIM Cards & Nokia 3310s Transform Computer Village Ikeja",
                Summary = "Yellow umbrella GSM call booths spring up on every Lagos street corner as mobile phones go mainstream.",
                StartYear = 2001, StartMonth = 1, StartDay = 1, EndYear = 2002, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.15f, CurfewActive = false,
                CivicPrompt = "SUPPLY GSM SIM STARTER PACKS AT COMPUTER VILLAGE · +₦18,000", CivicReward = 18000, CivicRep = "business"
            },
            new HistoricEvent {
                Id = "election_2003", Category = "election", Title = "2003 Nigerian General Elections",
                Headline = "ELECTION 2003: Lagosians Head to the Polls for Historic Civilian-to-Civilian Transition",
                Summary = "High-spirited campaigns across Marina, Ikeja, and Surulere as Lagos State defends its mandate.",
                StartYear = 2003, StartMonth = 1, StartDay = 1, EndYear = 2003, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.10f, CurfewActive = true,
                CivicPrompt = "SERVE AS INEC COLLATION OBSERVER · +₦16,000", CivicReward = 16000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "cbn_consolidation_2004", Category = "economy", Title = "CBN ₦25B Banking Consolidation & EFCC Sweep",
                Headline = "ECONOMY: CBN Orders ₦25 Billion Bank Recapitalisation; EFCC Launches Anti-Graft Sweep on Broad Street",
                Summary = "89 banks merge into 25 mega-banks along Marina and Victoria Island; ATMs and e-banking expand citywide.",
                StartYear = 2004, StartMonth = 1, StartDay = 1, EndYear = 2006, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.20f, CurfewActive = false,
                CivicPrompt = "ESCORT CBN TREASURY CONSOLIDATION FILES · +₦20,000", CivicReward = 20000, CivicRep = "business"
            },
            new HistoricEvent {
                Id = "election_2007", Category = "election", Title = "2007 General Elections & Lagos BRT Launch",
                Headline = "ELECTION 2007 & INFRASTRUCTURE: Babatunde Fashola Elected Governor; Mile 12–CMS BRT Lanes Open",
                Summary = "Blue BRT buses debut on dedicated Ikorodu Road lanes as Lagos embarks on mega-city renewal.",
                StartYear = 2007, StartMonth = 1, StartDay = 1, EndYear = 2010, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.15f, CurfewActive = true,
                CivicPrompt = "INAUGURATE LAGOS BRT CORRIDOR & POLLING UNIT · +₦18,000", CivicReward = 18000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "election_2011", Category = "election", Title = "2011 Nigerian General Elections",
                Headline = "ELECTION 2011: Youth Social Media Wave & Record Turnout Across Lagos Polling Units",
                Summary = "BlackBerry Messenger and Twitter track live INEC results from Surulere to Lekki.",
                StartYear = 2011, StartMonth = 1, StartDay = 1, EndYear = 2011, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.12f, CurfewActive = true,
                CivicPrompt = "VERIFY INEC RESULT SHEETS AT POLLING BOOTH · +₦18,000", CivicReward = 18000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "occupy_nigeria_2012", Category = "civic", Title = "2012 Fuel Subsidy Protests & Lekki-Ikoyi Link Bridge",
                Headline = "BREAKING: Crowds Gather at Ojota Over Fuel Price Hike; Iconic Lekki-Ikoyi Cable Bridge Nears Completion",
                Summary = "Transport fares fluctuate before settlement; Lekki-Ikoyi suspension bridge transforms Island commutes.",
                StartYear = 2012, StartMonth = 1, StartDay = 1, EndYear = 2012, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.10f, CurfewActive = false,
                CivicPrompt = "ORGANISE COMMUNITY FUEL RELIEF & PEACE RALLY · +₦16,000", CivicReward = 16000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "afcon_2013", Category = "sports", Title = "Super Eagles Win AFCON 2013 Trophy",
                Headline = "SPORTS: Super Eagles Crowned African Champions! Victory Parties Rock Teslim Balogun Stadium & Afrika Shrine",
                Summary = "Sunday Mba's winner sparks all-night celebrations across Surulere, Bar Beach, and Victoria Island.",
                StartYear = 2013, StartMonth = 1, StartDay = 1, EndYear = 2013, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.18f, CurfewActive = false,
                CivicPrompt = "HOST SUPER EAGLES TROPHY VIEWING FANZONE · +₦20,000", CivicReward = 20000, CivicRep = "social"
            },
            new HistoricEvent {
                Id = "ebola_response_2014", Category = "pandemic", Title = "2014 Lagos Defeats Ebola Outbreak",
                Headline = "HEALTH ALERT: Heroic Lagos Medics & Contact Tracers Contain Ebola; WHO Declares Nigeria Ebola-Free",
                Summary = "Dr. Stella Adadevoh and Lagos rapid-response teams halt the outbreak with infrared thermometers and sanitiser stations.",
                StartYear = 2014, StartMonth = 1, StartDay = 1, EndYear = 2014, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.15f, CurfewActive = false,
                CivicPrompt = "DELIVER MEDICAL HYGIENE KITS TO CLINICS · +₦22,000", CivicReward = 22000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "election_2015", Category = "election", Title = "2015 Historic General Elections (PVC Card Readers)",
                Headline = "ELECTION 2015: Permanent Voter Cards (PVCs) Debut in Historic Democratic Alternation",
                Summary = "Peaceful handover and Lagos governorship transition energise civic participation across all 20 LGAs.",
                StartYear = 2015, StartMonth = 1, StartDay = 1, EndYear = 2015, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.15f, CurfewActive = true,
                CivicPrompt = "ACCREDIT VOTERS WITH PVC SMART CARD READER · +₦20,000", CivicReward = 20000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "fintech_wave_2016", Category = "economy", Title = "Yaba Silicon Lagoon & Fintech Startup Boom",
                Headline = "TECH & ECONOMY: Yaba 'Silicon Lagoon' Startups (Paystack, Flutterwave) Attract Global Billions After FX Crunch",
                Summary = "Co-working hubs in Yaba and Victoria Island power instant digital payments across every POS kiosk in Lagos.",
                StartYear = 2016, StartMonth = 1, StartDay = 1, EndYear = 2018, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.22f, CurfewActive = false,
                CivicPrompt = "DEPLOY FINTECH POS TERMINALS FOR MERCHANTS · +₦22,000", CivicReward = 22000, CivicRep = "business"
            },
            new HistoricEvent {
                Id = "election_2019", Category = "election", Title = "2019 Nigerian General Elections",
                Headline = "ELECTION 2019: Babajide Sanwo-Olu Elected Governor of Lagos Under THEMES Agenda",
                Summary = "Polls conclude across Mainland and Island; Blue Line rail and tech infrastructure projects accelerate.",
                StartYear = 2019, StartMonth = 1, StartDay = 1, EndYear = 2019, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.15f, CurfewActive = true,
                CivicPrompt = "ESCORT INEC BALLOT BOXES TO COLLATION CENTRE · +₦20,000", CivicReward = 20000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "covid19_lockdown_2020", Category = "pandemic", Title = "COVID-19 Pandemic, NCDC Lockdown & Relief Effort",
                Headline = "PANDEMIC ALERT (COVID-19): NCDC Declares Lagos Lockdown & Curfew — Nose Masks Mandatory, Essential Logistics Boom",
                Summary = "Isolation domes open at Onikan Stadium; highways quiet down during curfew while emergency medical & food dispatch riders earn +35% hazard pay.",
                StartYear = 2020, StartMonth = 1, StartDay = 1, EndYear = 2021, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.35f, CurfewActive = true,
                CivicPrompt = "DELIVER NCDC COVID-19 PPE & FOOD PALLIATIVES · +₦25,000", CivicReward = 25000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "election_2023", Category = "election", Title = "2023 General Elections & CBN Naira Redesign",
                Headline = "ELECTION 2023: BVAS Accreditation, CBN New Naira Notes & Historic Presidential / Lagos Polls",
                Summary = "Bola Ahmed Tinubu elected President; POS agents and digital transfers surge during the CBN cash swap.",
                StartYear = 2023, StartMonth = 1, StartDay = 1, EndYear = 2023, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.25f, CurfewActive = true,
                CivicPrompt = "UPLOAD BVAS POLLING UNIT RESULTS & ASSIST VOTERS · +₦24,000", CivicReward = 24000, CivicRep = "public"
            },
            new HistoricEvent {
                Id = "lrmt_rail_era_2024", Category = "infrastructure", Title = "LRMT Blue & Red Line Rail Era & Detty December Boom",
                Headline = "TRENDING: Lagos Blue & Red Line Electric Trains Carry Millions; Afrobeats & Detty December Top Global Charts",
                Summary = "Electric rail links Marina to Mile 2 and Agbado to Oyingbo; global diaspora flocks to Lagos beaches and concerts.",
                StartYear = 2024, StartMonth = 1, StartDay = 1, EndYear = 2035, EndMonth = 12, EndDay = 31,
                PayMultiplier = 1.28f, CurfewActive = false,
                CivicPrompt = "COORDINATE MARINA RAIL & DETTY DECEMBER VIP LOGISTICS · +₦25,000", CivicReward = 25000, CivicRep = "business"
            }
        };

        static readonly string[] DailyTrendingPool =
        {
            "CBN MONETARY REPORT: Central Bank of Nigeria Tinubu Square Tower announces new SME credit window for Lagos traders.",
            "EFCC WATCH: Operatives at Awolowo Road Ikoyi & Ikeja Zonal Commands recover billions in financial compliance sweep.",
            "TRAFFIC & INFRASTRUCTURE: LASTMA deploys rapid tow-trucks along Third Mainland Bridge and Eko Bridge corridors.",
            "TECH & HUSTLE: Computer Village Ikeja and Yaba tech hubs record surge in software, mobile hardware, and POS commerce.",
            "SPORTS DESK: Capacity crowd expected at Teslim Balogun Stadium Surulere for the Lagos Neighbourhood Cup Derby.",
            "CULTURE & AFROBEATS: New Afrika Shrine Ikeja and Victoria Island lounges host sold-out weekend live shows.",
            "MARITIME & TRADE: Apapa Wharf and Tin Can Island container terminals clear West African export cargo ahead of schedule.",
            "REAL ESTATE WATCH: Demand climbs for Surulere flats, Lekki duplexes, and Eko Atlantic / Banana Island waterfront villas."
        };

        HistoricEvent forcedHistoricEvent;
        public HistoricEvent ActiveHistoricEvent => forcedHistoricEvent ?? EventForDate(clock != null ? clock.Date : DayNightCycle.EpochDate);
        public bool IsPandemicActive => ActiveHistoricEvent != null && ActiveHistoricEvent.Category == "pandemic";
        public bool IsElectionActive => ActiveHistoricEvent != null && ActiveHistoricEvent.Category == "election";
        public string CurrentCategoryBadge => ActiveHistoricEvent != null ? ActiveHistoricEvent.Category.ToUpperInvariant() : "NEWS";
        public string CurrentHeadline => ActiveHistoricEvent != null ? ActiveHistoricEvent.Headline : TrendingHeadlineForDay(clock != null ? clock.Day : 1);
        public string DailyTrendingHeadline => TrendingHeadlineForDay(clock != null ? clock.Day : 1);

        public static string TrendingHeadlineForDay(int day) => DailyTrendingPool[Mathf.Abs(day - 1) % DailyTrendingPool.Length];

        public static HistoricEvent FindHistoricEvent(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var ev in HistoricTimeline) if (ev.Id == id) return ev;
            return null;
        }

        public static HistoricEvent EventForDate(System.DateTime date)
        {
            int y = date.Year, m = date.Month, d = date.Day;
            int stamp = y * 10000 + m * 100 + d;
            foreach (var ev in HistoricTimeline)
            {
                int s = ev.StartYear * 10000 + ev.StartMonth * 100 + ev.StartDay;
                int e = ev.EndYear * 10000 + ev.EndMonth * 100 + ev.EndDay;
                if (stamp >= s && stamp <= e) return ev;
            }
            // Dynamic quadrennial Nigerian election years (1999 + 4n) for any date not explicitly in range
            if ((y - 1999) >= 0 && (y - 1999) % 4 == 0 && m >= 2 && m <= 5)
            {
                return new HistoricEvent
                {
                    Id = $"election_{y}", Category = "election", Title = $"{y} Nigerian General Elections",
                    Headline = $"ELECTION {y}: INEC Opens Polling Units Across All 20 Lagos LGAs for Quadrennial Polls",
                    Summary = "Voters queue across Surulere, Ikeja, and Lagos Island to elect national and state leaders.",
                    StartYear = y, StartMonth = 2, StartDay = 1, EndYear = y, EndMonth = 5, EndDay = 29,
                    PayMultiplier = 1.15f, CurfewActive = true,
                    CivicPrompt = $"ASSIST INEC POLLING UNIT ({y} ELECTIONS) · +₦20,000", CivicReward = 20000, CivicRep = "public"
                };
            }
            return HistoricTimeline[HistoricTimeline.Length - 1];
        }

        public void OnDayChanged(System.DateTime date, int day)
        {
            var ev = EventForDate(date);
            if (ev != null && date.Year == ev.StartYear && date.Month == ev.StartMonth && date.Day == ev.StartDay)
                Notify?.Invoke("Historic Event", $"{ev.Title} ({date:MMM d, yyyy}): {ev.Headline}");
            else if (day % 2 == 0)
                Notify?.Invoke("Lagos Chronicle", TrendingHeadlineForDay(day));
        }

        // Triggers a historic milestone (e.g. "election_1999", "covid19_lockdown_2020", "gsm_revolution_2001", "election_2023"),
        // optionally jumps the game calendar to that date, and spawns a themed 3D civic encounter in the city.
        public bool TriggerHistoricEvent(string id, Vector3? atPos = null, bool jumpCalendar = true)
        {
            var ev = FindHistoricEvent(id);
            if (ev == null) return false;
            forcedHistoricEvent = ev;
            if (jumpCalendar && clock != null)
                clock.SetDate(ev.StartYear, ev.StartMonth, ev.StartDay, 9.5f);

            ClearEncounter();
            var road = data.FindRoad("v", 0f) ?? data.roads[0];
            Vector3 pos = atPos ?? road.Point(Mathf.Clamp(0f, road.from + 25f, road.to - 25f), road.k + road.width * 0.5f - 1.8f, 0.12f);
            pos.y = data.DeckHeightAt(pos) + 0.12f;

            ActiveEncounter = new Encounter
            {
                kind = "historic_" + ev.Category,
                title = ev.Title,
                prompt = ev.CivicPrompt,
                rep = string.IsNullOrEmpty(ev.CivicRep) ? "public" : ev.CivicRep,
                repAmount = 8,
                reward = ev.CivicReward > 0 ? ev.CivicReward : 18000,
                xp = 30,
                pos = pos,
                until = 300f,
            };

            // Build a themed 3D Historic Civic Station (INEC Polling Booth, NCDC COVID-19 Relief Tent, GSM Booth, or Civic Podium)
            string canopyHex = ev.Category == "election" ? "#1e7a46" : ev.Category == "pandemic" ? "#1976d2" : ev.Category == "telecom" ? "#ffcc00" : "#d9822b";
            var canopy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            canopy.name = "Historic Station Canopy";
            Destroy(canopy.GetComponent<Collider>());
            canopy.transform.SetParent(transform, false);
            canopy.transform.position = pos + Vector3.up * 2.4f;
            canopy.transform.localScale = new Vector3(3.2f, 0.22f, 3.2f);
            canopy.GetComponent<MeshRenderer>().sharedMaterial = Mat(canopyHex);
            encounterProps.Add(canopy);

            var desk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            desk.name = "Historic Station Desk";
            Destroy(desk.GetComponent<Collider>());
            desk.transform.SetParent(transform, false);
            desk.transform.position = pos + Vector3.up * 0.55f;
            desk.transform.localScale = new Vector3(1.8f, 0.9f, 0.9f);
            desk.GetComponent<MeshRenderer>().sharedMaterial = Mat("#f2efe9");
            encounterProps.Add(desk);

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beacon.name = "Historic Encounter Beacon";
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(transform, false);
            beacon.transform.position = pos + Vector3.up * 2.95f;
            beacon.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
            beacon.transform.localScale = new Vector3(0.26f, 0.26f, 0.26f);
            beacon.GetComponent<MeshRenderer>().sharedMaterial = Mat(canopyHex);
            Label(beacon.transform, $"[{ev.Category.ToUpperInvariant()}] {ev.Title.ToUpperInvariant()}");
            encounterProps.Add(beacon);

            Notify?.Invoke("Lagos Chronicle", $"{ev.Headline} — {ev.CivicPrompt}");
            return true;
        }

        public void ClearForcedHistoricEvent() => forcedHistoricEvent = null;

        public Jam Current { get; private set; }
        public Encounter ActiveEncounter { get; private set; }
        public float Outage { get; private set; }
        WorldData data; DayNightCycle clock; Weather weather;
        readonly List<GameObject> props = new List<GameObject>();
        readonly List<GameObject> encounterProps = new List<GameObject>();
        float jamT = 60f, outageT = 120f, encounterT = 75f;
        [System.NonSerialized] public System.Action<string, string> Notify;   // (who, text)

        public void Init(WorldData world, DayNightCycle dayNight, Weather w) { data = world; clock = dayNight; weather = w; }
        public void RestorePower() { Outage = 0f; }

        public bool InJam(RoadData road, float along) => Current != null && Current.road == road && along > Current.from && along < Current.to;
        public bool FloodedAt(Vector3 pos) => Current != null && Current.flood && Current.road != null && Mathf.Abs(Current.road.Across(pos) - Current.road.k) < 9f && InJam(Current.road, Current.road.Along(pos));

        void Update()
        {
            float dt = Time.deltaTime;
            float h = clock != null ? clock.Clock : 12f;
            bool night = clock != null && (h > 18.5f || h < 6f);
            bool lateNight = clock != null && (h >= 21.5f || h < 6f);
            if (Outage > 0f) { Outage -= dt; if (Outage <= 0f) { Outage = 0f; Notify?.Invoke("NEPA", "Up NEPA! Light don come back."); } }
            else { outageT -= dt; if (outageT <= 0f) { outageT = Random.Range(120f, 260f); if (night && Random.value < 0.6f) { Outage = Random.Range(50f, 120f); Notify?.Invoke("NEPA", "NEPA don take light! Generators dey hum across Surulere."); } } }

            if (Current != null)
            {
                Current.until -= dt;
                // Auto-clear daytime "go-slow" congestion once late night arrives so roads don't stay gridlocked at 2 AM
                if (lateNight && !Current.flood && !Current.works && !Current.wreck)
                {
                    Clear();
                    Notify?.Invoke("Traffic", "Night road don clear — free flow till morning.");
                }
                else if (Current.until <= 0f)
                {
                    bool works = Current.works;
                    Clear();
                    Notify?.Invoke(works ? "LASTMA" : "Traffic", works ? "Road works done — lane open again." : "Go-slow don clear.");
                }
            }
            else
            {
                if (weather != null && weather.Storm) { StartFlood(); return; }
                // Only spawn random go-slow and road-works hold-ups during daytime/evening commute hours (06:00–21:30)
                if (!lateNight)
                {
                    jamT -= dt;
                    if (jamT <= 0f) { jamT = Random.Range(90f, 200f); if (Random.value < 0.7f) StartJam(); }
                }
            }

            if (ActiveEncounter != null)
            {
                ActiveEncounter.until -= dt;
                if (ActiveEncounter.until <= 0f) ClearEncounter();
            }
            else
            {
                encounterT -= dt;
                if (encounterT <= 0f)
                {
                    encounterT = Random.Range(85f, 160f);
                    if (Random.value < 0.75f) StartEncounter();
                }
            }
        }

        public void StartEncounter(int? forceKind = null, Vector3? atPos = null)
        {
            ClearEncounter();
            var road = data.FindRoad("v", 0f) ?? data.roads[Random.Range(0, data.roads.Length)];
            Vector3 pos = atPos ?? road.Point(Mathf.Clamp(0f, road.from + 25f, road.to - 25f), road.k + road.width * 0.5f - 1.8f, 0.12f);
            pos.y = data.DeckHeightAt(pos) + 0.12f;
            int k = forceKind ?? Random.Range(0, 3);
            if (k == 0)
            {
                ActiveEncounter = new Encounter
                {
                    kind = "breakdown",
                    title = "Broken-Down Danfo",
                    prompt = "HELP PUSH BROKEN DANFO · +₦8,000",
                    rep = "public",
                    repAmount = 4,
                    reward = 8000,
                    xp = 15,
                    pos = pos,
                    until = 180f,
                };
                var cls = data.VehicleClass("danfo") ?? data.VehicleClass("car");
                if (cls != null)
                {
                    var v = VehicleBodies.Build(cls, transform, new System.Random(41), out _);
                    v.transform.position = pos;
                    encounterProps.Add(v);
                }
            }
            else if (k == 1)
            {
                ActiveEncounter = new Encounter
                {
                    kind = "dispute",
                    title = "Roadside Dispute",
                    prompt = "SETTLE ROADSIDE WAHALA · +₦10,000",
                    rep = "social",
                    repAmount = 5,
                    reward = 10000,
                    xp = 15,
                    pos = pos,
                    until = 180f,
                };
                var cls = data.VehicleClass("taxi") ?? data.VehicleClass("car");
                if (cls != null)
                {
                    var v = VehicleBodies.Build(cls, transform, new System.Random(42), out _);
                    v.transform.position = pos;
                    encounterProps.Add(v);
                }
            }
            else
            {
                ActiveEncounter = new Encounter
                {
                    kind = "vip_escort",
                    title = "Stranded Island Executive",
                    prompt = "ASSIST STRANDED EXECUTIVE · +₦15,000",
                    rep = "business",
                    repAmount = 5,
                    reward = 15000,
                    xp = 20,
                    pos = pos,
                    until = 180f,
                };
                var cls = data.VehicleClass("luxury") ?? data.VehicleClass("suv") ?? data.VehicleClass("car");
                if (cls != null)
                {
                    var v = VehicleBodies.Build(cls, transform, new System.Random(43), out _);
                    v.transform.position = pos;
                    encounterProps.Add(v);
                }
            }

            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beacon.name = "Encounter beacon";
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.SetParent(transform, false);
            beacon.transform.position = pos + Vector3.up * 2.35f;
            beacon.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
            beacon.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
            beacon.GetComponent<MeshRenderer>().sharedMaterial = Mat("#ffc52f");
            Label(beacon.transform, ActiveEncounter.title.ToUpper());
            encounterProps.Add(beacon);
            Notify?.Invoke("Street", $"{ActiveEncounter.title} spotted nearby — lend a hand for ₦{ActiveEncounter.reward:N0}.");
        }

        public void ClearEncounter()
        {
            ActiveEncounter = null;
            foreach (var p in encounterProps) if (p != null) Destroy(p);
            encounterProps.Clear();
        }

        public void StartFlood()
        {
            var road = data.FindRoad("h", -66f); if (road == null) return;
            Current = new Jam { road = road, from = -20f, to = 60f, until = 150f, flood = true };
            var water = GameObject.CreatePrimitive(PrimitiveType.Cube); water.name = "Flood"; Destroy(water.GetComponent<Collider>());
            water.transform.position = road.Point(20f, road.k, 0.22f); water.transform.localScale = new Vector3(80f, 0.12f, road.width + 6f);
            var m = new Material(ShaderLib.Lit) { color = new Color(0.35f, 0.3f, 0.2f, 1f) }; m.SetFloat("_Smoothness", 0.9f); water.GetComponent<MeshRenderer>().sharedMaterial = m;
            props.Add(water);
            Notify?.Invoke("Weather", "Storm! Ojuelegba underpass don flood — Itire Road is a go-slow.");
        }

        public void StartJam(RoadData pick = null, float? at = null, int? kind = null)
        {
            var road = pick ?? data.roads[Random.Range(0, data.roads.Length)];
            if (road.Length < 120f) return;
            float from = at ?? Random.Range(road.from + 20f, road.to - 80f);
            Current = new Jam { road = road, from = from, to = from + 60f, until = Random.Range(60f, 120f) };
            float r = kind.HasValue ? kind.Value * 0.3f + 0.01f : Random.value;
            string name = data.RoadNameAt(road, from + 30f);
            bool h = road.Horizontal;
            if (r < 0.3f)
            {
                Current.until = Random.Range(150f, 260f); Current.works = true;
                var cone = Mat("#ff6a1a"); var barrier = Mat("#bfb8a6"); var board = Mat("#ff6a1a");
                for (float c = from; c <= from + 60f; c += 6f) Prop(PrimitiveType.Cylinder, road.Point(c, road.k - 4.5f, 0.45f), new Vector3(0.5f, 0.35f, 0.5f), cone);
                for (float c = from + 3f; c <= from + 57f; c += 12f) Prop(PrimitiveType.Cube, road.Point(c, road.k - 2.2f, 0.55f), h ? new Vector3(8f, 0.9f, 0.6f) : new Vector3(0.6f, 0.9f, 8f), barrier);
                var sign = Prop(PrimitiveType.Cube, road.Point(from - 6f, road.k - 7f, 3f), h ? new Vector3(6.5f, 1.3f, 0.2f) : new Vector3(0.2f, 1.3f, 6.5f), board);
                Label(sign.transform, "ROAD WORKS · DIVERSION");
                Notify?.Invoke("LASTMA", $"Road construction on {name} — one lane closed, follow the diversion.");
            }
            else if (r < 0.6f)
            {
                Current.wreck = true;
                var keke = data.VehicleClass("keke");
                if (keke != null)
                {
                    var wreck = VehicleBodies.Build(keke, transform, new System.Random(9), out _);
                    wreck.transform.position = road.Point(from + 30f, road.k - 4.5f, 0.9f); wreck.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 82f);
                    props.Add(wreck);
                    for (float c = from + 22f; c <= from + 38f; c += 4f) Prop(PrimitiveType.Cylinder, road.Point(c, road.k - 6f, 0.45f), new Vector3(0.5f, 0.35f, 0.5f), Mat("#ff6a1a"));
                }
                Notify?.Invoke("Traffic", $"Accident on {name} — keke don tumble. Expect go-slow.");
            }
            else Notify?.Invoke("Traffic", $"Go-slow on {name} — danfos dey crawl. Find another route.");
        }

        static Material Mat(string hex) { var m = new Material(ShaderLib.Lit); ColorUtility.TryParseHtmlString(hex, out var c); m.color = c; return m; }
        GameObject Prop(PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type); go.transform.SetParent(transform, false); go.transform.position = pos; go.transform.localScale = scale; go.GetComponent<MeshRenderer>().sharedMaterial = m;
            props.Add(go); return go;
        }
        static void Label(Transform parent, string text)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("Label"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, parent.localScale.x), 1f / Mathf.Max(0.01f, parent.localScale.y), 1f / Mathf.Max(0.01f, parent.localScale.z));
            var tm = go.AddComponent<TextMesh>(); tm.text = text; tm.font = font; tm.fontSize = 36; tm.characterSize = 0.028f; tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = new Color(1.0f, 0.88f, 0.28f);
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            go.AddComponent<Entities.FacingLabel>();
        }

        public void Clear() { Current = null; foreach (var p in props) if (p != null) Destroy(p); props.Clear(); }
    }
}
