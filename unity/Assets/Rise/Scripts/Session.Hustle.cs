using System.Collections.Generic;
using UnityEngine;
using Rise.Data;
using Rise.Entities;
using Rise.World;

namespace Rise
{
    // District hustle hubs: repeatable timed deliveries offered by local contacts;
    // Community Grammar School and Yaba Tech (schoolMenu); street football tiers (footballMenu / playMatch).
    public partial class Session
    {
        public class HubRun { public string Text, Dest, Item, Label, Rep; public float Minutes; public int Reward, Heat; }
        static HubRun R(string text, string dest, string item, float minutes, int reward, string label, string rep) => new HubRun { Text = text, Dest = dest, Item = item, Minutes = minutes, Reward = reward, Label = label, Rep = rep };

        public static readonly Dictionary<string, (string name, string intro, HubRun[] runs)> Hubs = new Dictionary<string, (string name, string intro, HubRun[] runs)>
        {
            ["egbon"] = ("Surulere", null, new[] { R("Deliver a workshop parts crate to Ladipo Garage", "ladipo", "phone_parts", 35, 18000, "Parts Crate", "business"), R("Take community notices to the Iponri Logistics Yard", "marina", "event_flyers", 30, 15000, "Community Notices", "social") }),
            ["ikoyi_merchant"] = ("Ikoyi", null, new[] { R("Deliver a market basket to Ikoyi Club", "ikoyi-club", "market_basket", 30, 20000, "Market Basket", "business"), R("Take supplies to the Ikoyi Boulevard service desk", "ikoyi-boulevard", "school_supplies", 35, 24000, "Supplies", "business") }),
            ["lekki_keeper"] = ("Lekki–Epe", null, new[] { R("Carry field supplies to Lekki Conservation Centre", "lekki-conservation", "conservation_kit", 40, 22000, "Conservation Kit", "public"), R("Deliver school materials to Ajah Community School", "ajah-school", "school_supplies", 35, 18000, "School Supplies", "public") }),
            ["ikorodu_trader"] = ("Ikorodu", null, new[] { R("Take a market basket to Ikorodu Garage", "ikorodu-garage-town", "market_basket", 35, 16000, "Market Basket", "business"), R("Deliver school supplies to Ikorodu Senior Grammar School", "ikorodu-school", "school_supplies", 35, 18000, "School Supplies", "public") }),
            ["apapa_dispatch"] = ("Apapa Port", null, new[] { R("Deliver the dock manifest to Apapa Wharf", "apapa-wharf", "dock_manifest", 30, 24000, "Dock Manifest", "business"), R("Take supplies from the port to Apapa Community School", "apapa-school", "school_supplies", 35, 19000, "School Supplies", "public") }),
            ["oyingbo_trader"] = ("Oyingbo · Empire", null, new[] { R("Deliver a market basket to the Oyingbo Rail Terminal", "oyingbo-rail", "market_basket", 25, 14000, "Market Basket", "business"), R("Carry community notices to Empire", "empire", "event_flyers", 20, 12000, "Community Notices", "social") }),
            ["yaba_connector"] = ("Yaba · Alagomeji", null, new[] { R("Run laptop parts to the Yaba Sign Painting Studio", "yaba-painter", "laptop_parts", 30, 20000, "Laptop Parts", "business"), R("Deliver school supplies to Yaba College of Technology", "yabatech", "school_supplies", 35, 22000, "School Supplies", "public") }),
            ["agege_baker"] = ("Agege", null, new[] { R("Deliver fresh bread to Agege Bus Garage", "agege-garage", "fresh_bread", 25, 14000, "Fresh Bread", "business"), R("Take bread to the Agege Carpentry Workshop", "agege-carpenter", "fresh_bread", 20, 12000, "Fresh Bread", "business") }),
            ["ojo_trader"] = ("Ojo · Badagry", null, new[] { R("Deliver electronics parts to the Ojo Welding Yard", "ojo-welder", "phone_parts", 30, 18000, "Electronics Parts", "business"), R("Carry a market basket to Ojo Market", "ojo-market", "market_basket", 25, 14000, "Market Basket", "business") }),
            ["cv_oga"] = ("Computer Village", null, new[] { R("Deliver replacement phone parts to Ikeja Senior High School", "ikeja-school", "phone_parts", 30, 18000, "Phone Parts", "business"), R("Run laptop parts to the airport service desk", "ikeja-airport", "laptop_parts", 40, 26000, "Laptop Parts", "business"), R("Set up a POS terminal at Ikeja City Centre", "ikeja-city", "pos_terminal", 35, 22000, "POS Terminal", "business") }),
            ["balogun_osoanya"] = ("Balogun Market", "Osoanya — also called Oso-ahia — are commission agents who connect shoppers with market traders. Pick a customer order and earn your cut on delivery.", new[] { R("Carry a fabric order from Balogun Market to Shitta Market", "shitta", "fabric_order", 30, 18000, "Fabric Order", "business"), R("Deliver a customer parcel from Balogun Market to Yaba Market", "yaba", "market_basket", 35, 22000, "Customer Parcel", "business") }),
            ["yaba_osoanya"] = ("Yaba Market", "We connect Yaba shoppers with traders and earn commission for each completed order. Help us get this purchase to the buyer.", new[] { R("Deliver a Yaba Market order to Computer Village", "ikeja-tech", "phone_parts", 40, 26000, "Electronics Order", "business"), R("Carry a clothing parcel from Yaba Market to Tejuosho Market", "tejuosho", "fabric_order", 25, 16000, "Clothing Parcel", "business") }),
            ["computer_village_osoanya"] = ("Computer Village", "We be Osoanya, commission agents. We match customers with trusted phone and computer traders, then earn when the order reaches its buyer.", new[] { R("Deliver a phone repair order from Computer Village to Yaba Market", "yaba", "phone_parts", 40, 24000, "Repair Order", "business"), R("Carry a laptop order from Computer Village to Ikeja City Centre", "ikeja-city", "laptop_parts", 25, 18000, "Laptop Order", "business") }),
            ["sisi"] = ("AJ City", null, new[] { R("Take donated football boots to Baba Goal at the AJ City Street Pitch", "ajpitch", "football_boots", 25, 12000, "Football Boots", "street") }),
            ["makoko_fisher"] = ("Makoko", null, new[] { R("Carry the fresh fish crate to the Makoko Canoe Landing", "makoko-canoe-landing", "fish_crate", 18, 9000, "Fish Crate", "business"), R("Carry a basket of kiln-smoked fish from Makoko to Yaba Market", "yaba", "smoked_fish", 20, 13000, "Smoked Fish", "business"), R("Deliver fresh lagoon croaker on ice to The Yellow Chilli in VI", "yellowchilli", "fresh_croaker", 25, 18000, "Fresh Croaker", "business") }),
            ["shrine_host"] = ("New Afrika Shrine", null, new[] { R("Deliver audio cables to the Ikeja City Centre event crew", "ikeja-city", "event_cables", 45, 24000, "Event Cables", "social"), R("Take spare stage connectors to Kalakuta Republic", "kalakuta-republic", "event_cables", 50, 28000, "Event Cables", "social") }),
            ["kalakuta_guide"] = ("Kalakuta Republic", null, new[] { R("Deliver an archive box to the New Afrika Shrine history room", "new-afrika-shrine", "archive_box", 50, 28000, "Archive Box", "social") }),
        };

        [System.NonSerialized] public HubRun ActiveRun;   // side task in progress (uses the task fields with taskMission = -1)
        [System.NonSerialized] public string ActiveChainId = "";

        public class StoryChain
        {
            public string Id, Title, Who, District;
            public HubRun[] Steps;
        }

        public static readonly StoryChain[] StoryChains =
        {
            new StoryChain
            {
                Id = "surulere_chain", Title = "Heart of the Mainland", Who = "egbon", District = "Surulere",
                Steps = new[]
                {
                    R("Chapter 1 · Carry generator coils to Ladipo Garage", "ladipo", "phone_parts", 30, 24000, "Generator Coils", "business"),
                    R("Chapter 2 · Rush storm relief supplies to the Surulere clinic", "clinic", "school_supplies", 25, 34000, "Storm Relief Kit", "public"),
                    R("Chapter 3 · Deliver Night Watch radios to Area C Police Station", "police", "phone_parts", 30, 50000, "Night Watch Radios", "public"),
                }
            },
            new StoryChain
            {
                Id = "yaba_chain", Title = "Silicon Lagoon", Who = "yaba_connector", District = "Yaba · Alagomeji",
                Steps = new[]
                {
                    R("Chapter 1 · Deliver fibre spools to Yaba College of Technology", "yabatech", "laptop_parts", 30, 26000, "Fibre Spools", "business"),
                    R("Chapter 2 · Run a hardware prototype to Computer Village", "ikeja-tech", "laptop_parts", 35, 38000, "Hardware Prototype", "business"),
                    R("Chapter 3 · Courier the Series-A pitch deck to Victoria Island", "civic", "documents", 40, 60000, "Series-A Deck", "business"),
                }
            },
            new StoryChain
            {
                Id = "island_chain", Title = "Lagoon Gold", Who = "ikoyi_merchant", District = "Ikoyi · Lekki",
                Steps = new[]
                {
                    R("Chapter 1 · Take a diplomatic pouch to Five Cowrie Creek Jetty", "falomo-jetty", "documents", 25, 28000, "Diplomatic Pouch", "social"),
                    R("Chapter 2 · Deliver a luxury hamper to Banana Island Mansion", "banana-mansion", "market_basket", 30, 42000, "Luxury Hamper", "social"),
                    R("Chapter 3 · Rush the charter manifest to Dangote Refinery", "dangote-refinery", "dock_manifest", 50, 75000, "Refinery Manifest", "business"),
                }
            },
            new StoryChain
            {
                Id = "makoko_chain", Title = "People of the Water", Who = "makoko_fisher", District = "Makoko · Lagoon",
                Steps = new[]
                {
                    R("Chapter 1 · Deliver kiln-smoked fish to Oyingbo Rail Terminal", "oyingbo-rail", "smoked_fish", 25, 22000, "Smoked Catch", "business"),
                    R("Chapter 2 · Bring spare outboard parts to Makoko Canoe Landing", "makoko-canoe-landing", "outboard_parts", 20, 32000, "Outboard Spares", "public"),
                    R("Chapter 3 · Deliver the clinic cold box to Yaba Market", "yaba", "coldbox", 30, 48000, "Clinic Cold Box", "public"),
                }
            },
            new StoryChain
            {
                Id = "ikeja_chain", Title = "Kalakuta Rhythm", Who = "shrine_host", District = "Ikeja · Alausa",
                Steps = new[]
                {
                    R("Chapter 1 · Carry brass horns to Kalakuta Republic", "kalakuta-republic", "archive_box", 30, 26000, "Brass Horns", "social"),
                    R("Chapter 2 · Deliver master reels to Ikeja City Centre", "ikeja-city", "event_cables", 30, 38000, "Master Reels", "social"),
                    R("Chapter 3 · Rush headliner soundcheck gear to Murtala Muhammed Airport", "ikeja-airport", "event_cables", 40, 58000, "Soundcheck Gear", "social"),
                }
            },
            new StoryChain
            {
                Id = "alimosho_chain", Title = "West Mainland Engine", Who = "alimosho_hub", District = "Alimosho · Oshodi · Ladipo",
                Steps = new[]
                {
                    R("Chapter 1 · Move tokunbo gearboxes from Oshodi Interchange to Ladipo Auto Market", "ladipo-auto-hub", "auto_parts", 25, 28000, "Tokunbo Gearbox", "street"),
                    R("Chapter 2 · Deliver medical outreach crates to Alimosho General Hospital in Igando", "alimosho-hospital", "school_supplies", 30, 38000, "Igando Medical Crate", "public"),
                    R("Chapter 3 · Rush commercial dispatch manifests to Ogba & Fagba Plaza", "ifako-ogba-hub", "documents", 30, 54000, "Ogba Dispatch Manifest", "business"),
                }
            },
            new StoryChain
            {
                Id = "amuwo_chain", Title = "Container Kings", Who = "amuwo_trade_hub", District = "Festac · Trade Fair · Alaba",
                Steps = new[]
                {
                    R("Chapter 1 · Clear electronics bills of lading from Trade Fair ASPAMDA to Alaba Market", "ojo-alaba", "documents", 25, 30000, "Alaba Bill of Lading", "business"),
                    R("Chapter 2 · Deliver boutique electronics to Festival Mall in Festac ’77", "festac-77-mall", "phone_parts", 25, 40000, "Festival Mall Stock", "business"),
                    R("Chapter 3 · Escort bulk fuel allocation clearance to Mile 2 Tank Farm Depot", "mile2-tank-farm", "dock_manifest", 30, 62000, "Tank Farm Clearance", "business"),
                }
            },
            new StoryChain
            {
                Id = "kosofe_chain", Title = "Basket of the Megacity", Who = "kosofe_mile12_hub", District = "Kosofe · Mile 12 · Magodo",
                Steps = new[]
                {
                    R("Chapter 1 · Dispatch fresh foodstuff waybills from Mile 12 to Ojota Motor Park", "ojota-motor-park", "market_basket", 25, 26000, "Mile 12 Waybills", "business"),
                    R("Chapter 2 · Deliver executive catering crates to Magodo GRA Phase 2 Clubhouse", "magodo-phase2-club", "food_cooler", 25, 38000, "Magodo VIP Catering", "social"),
                    R("Chapter 3 · Rush lagoon ferry cargo manifests to Oworonshoki Rim Jetty", "oworonshoki-jetty", "dock_manifest", 30, 55000, "Oworonshoki Manifest", "business"),
                }
            },
            new StoryChain
            {
                Id = "ibeju_chain", Title = "Eastern Refinery Frontier", Who = "ibeju_epe_hub", District = "Ibeju-Lekki · Epe · Imota",
                Steps = new[]
                {
                    R("Chapter 1 · Carry export container seals from Dangote Refinery to Lekki Deep Sea Port", "lekki-deep-port", "dock_manifest", 25, 35000, "Deep Port Seals", "business"),
                    R("Chapter 2 · Deliver aviation logistics plans to Lekki–Epe International Airport", "lekki-epe-airport", "documents", 25, 46000, "Airport Logistics Plan", "public"),
                    R("Chapter 3 · Haul agricultural processing parts to Imota Rice Mill in Ikorodu Division", "imota-rice-mill", "auto_parts", 45, 72000, "Imota Mill Parts", "business"),
                }
            },
            new StoryChain
            {
                Id = "badagry_chain", Title = "ECOWAS Coastal Gateway", Who = "badagry_border_hub", District = "Badagry · Seme Border · Tarkwa Bay",
                Steps = new[]
                {
                    R("Chapter 1 · Escort ECOWAS customs manifests from Seme Border to Badagry Marina", "badagry-heritage", "dock_manifest", 25, 30000, "ECOWAS Manifest", "public"),
                    R("Chapter 2 · Deliver archival preservation crates to the First Storey Building", "first-storey-badagry", "archive_box", 25, 42000, "Heritage Preservation", "public"),
                    R("Chapter 3 · Rush marine shipyard spares to Tarkwa Bay & Snake Island Jetty", "tarkwa-snake-island", "outboard_parts", 35, 64000, "Snake Island Spares", "business"),
                }
            },
            new StoryChain
            {
                Id = "southwest_chain", Title = "Gateway to Pace Setter Empire", Who = "southwest_ogun_oyo_hub", District = "Ogun · Abeokuta · Ibadan",
                Steps = new[]
                {
                    R("Chapter 1 · Haul industrial manufacturing blueprints from Cocoa House to Ota & Agbara Factory Hub", "ota-industrial", "documents", 30, 38000, "Ota Factory Blueprint", "business"),
                    R("Chapter 2 · Deliver state industrial accords to Ogun State Secretariat at Oke-Mosan, Abeokuta", "oke-mosan", "sealed_envelope", 30, 48000, "Oke-Mosan Accord", "public"),
                    R("Chapter 3 · Rush AI & research hardware to University of Ibadan Main Gate in Agbowo", "ui-gate", "laptop_parts", 30, 68000, "UI Agbowo Research Kit", "business"),
                }
            },
            new StoryChain
            {
                Id = "abuja_chain", Title = "Three Arms Sovereign Mandate", Who = "abuja_federal_hub", District = "FCT Abuja · Three Arms & CBD",
                Steps = new[]
                {
                    R("Chapter 1 · Escort a federal infrastructure appropriation bill from NASS to Aso Rock Villa", "aso-rock-villa", "sealed_envelope", 25, 50000, "Aso Rock Appropriation", "public"),
                    R("Chapter 2 · Deliver sovereign monetary reserve ledgers to CBN Headquarters in Abuja CBD", "cbn-hq-abuja", "documents", 25, 64000, "CBN HQ Reserve Ledger", "business"),
                    R("Chapter 3 · File anti-corruption tribunal dossiers at EFCC National Headquarters in Jabi", "efcc-hq-abuja", "efcc_dossier", 30, 88000, "EFCC Grand Tribunal File", "public"),
                }
            },
            new StoryChain
            {
                Id = "niger_delta_chain", Title = "Bonny LNG & Creek Command", Who = "niger_delta_oil_hub", District = "Rivers · Delta · Edo",
                Steps = new[]
                {
                    R("Chapter 1 · Transport offshore LNG turbine valves from Bonny Island to Trans-Amadi Base", "trans-amadi-yard", "auto_parts", 25, 48000, "Bonny LNG Valves", "business"),
                    R("Chapter 2 · Escort petrochemical feedstock manifests to Warri Refinery & Escravos Jetty", "warri-refinery", "dock_manifest", 30, 62000, "Warri Refinery Manifest", "business"),
                    R("Chapter 3 · Deliver royal bronze heritage deeds to Benin Igun Street Bronze Guild", "benin-igun-bronze", "archive_box", 30, 80000, "Benin Royal Bronze Deed", "public"),
                }
            },
            new StoryChain
            {
                Id = "eastern_chain", Title = "Igba Boi to Industrial Titan", Who = "eastern_commerce_hub", District = "Anambra · Enugu · Abia",
                Steps = new[]
                {
                    R("Chapter 1 · Clear West African trade containers from Onitsha Main Market at Niger Bridgehead", "niger-bridge-gate", "dock_manifest", 25, 42000, "Niger Bridge Clearance", "business"),
                    R("Chapter 2 · Supply indigenous chassis assemblies to Nnewi Vehicle Manufacturing Plant", "nnewi-auto-plant", "auto_parts", 25, 58000, "Nnewi Chassis Supply", "business"),
                    R("Chapter 3 · Dispatch export shoe & garment bales to Ariaria International Market in Aba", "ariaria-market", "parcel", 30, 78000, "Ariaria Export Bale", "business"),
                }
            },
            new StoryChain
            {
                Id = "northern_chain", Title = "Trans-Saharan Caravans & Northern Shield", Who = "northern_trade_hub", District = "Kano · Kaduna · Borno",
                Steps = new[]
                {
                    R("Chapter 1 · Move bulk grain export waybills from Kurmi Market to Dawanau International Grains Market", "dawanau-grains", "documents", 25, 44000, "Dawanau Grain Waybill", "business"),
                    R("Chapter 2 · Deliver royal Durbar ceremonial tapestries to the Emir’s Palace in Kano City", "emirs-palace-kano", "fabric_order", 25, 58000, "Emir Durbar Tapestry", "social"),
                    R("Chapter 3 · Escort Northern Frontier logistics to Giwa Barracks & Lake Chad JTF Base", "jtf-fob-north", "sealed_envelope", 30, 85000, "JTF Frontier Dispatch", "public"),
                }
            },
        };

        public static StoryChain ChainForContact(string contactId)
        {
            foreach (var ch in StoryChains) if (ch.Who == contactId) return ch;
            return null;
        }

        public static StoryChain ChainById(string chainId)
        {
            foreach (var ch in StoryChains) if (ch.Id == chainId) return ch;
            return null;
        }

        public bool StartChainStep(string chainId)
        {
            if (HasTask) return false;
            var chain = ChainById(chainId);
            if (chain == null) return false;
            int step = State.ChainStep(chain.Id);
            if (step >= chain.Steps.Length) return false;
            ActiveChainId = chain.Id;
            StartRun(null, chain.Steps[step]);
            return true;
        }

        // Hard-coded hubs, plus any contact whose data carries runs (the Surulere street people, src/data/characters.js).
        bool IsHub(ContactSystem.Contact c) => Hubs.ContainsKey(c.data.id) || (c.data.runs != null && c.data.runs.Length > 0);

        static HubRun FromData(RunData r) => new HubRun { Text = r.text, Dest = r.dest, Item = r.item, Minutes = r.minutes, Reward = r.reward, Label = r.label, Rep = r.rep, Heat = r.heat };

        public (string name, string intro, HubRun[] runs) HubFor(ContactSystem.Contact c)
        {
            if (Hubs.TryGetValue(c.data.id, out var hub)) return hub;
            var runs = new HubRun[c.data.runs.Length];
            for (int i = 0; i < runs.Length; i++) runs[i] = FromData(c.data.runs[i]);
            return (c.data.role, c.data.line, runs);
        }

        void OpenHubMenu(ContactSystem.Contact c)
        {
            var hub = HubFor(c);
            bool dataHub = !Hubs.ContainsKey(c.data.id);
            var menu = new Menu { Title = c.data.name, Subtitle = dataHub ? $"{hub.name} · {State.RelTier(c.data.id)}" : hub.name, Line = dataHub ? hub.intro : $"{hub.intro ?? $"Welcome to {hub.name}."} Choose a local run. Deliveries pay when you reach the destination before the deadline." };
            if (!HasTask)
            {
                var chain = ChainForContact(c.data.id);
                if (chain != null)
                {
                    int step = State.ChainStep(chain.Id);
                    if (step < chain.Steps.Length)
                    {
                        var cr = chain.Steps[step];
                        menu.Options.Add(new MenuOption
                        {
                            Label = $"Story Chain ({chain.Title} {step + 1}/{chain.Steps.Length}) — {cr.Text} · ₦{cr.Reward:N0}",
                            Enabled = Data.TryPlace(cr.Dest, out _),
                            Action = () => StartChainStep(chain.Id),
                        });
                    }
                    else
                    {
                        menu.Options.Add(new MenuOption { Label = $"Story Chain ({chain.Title}) — Complete ✓", Enabled = false });
                    }
                }
                foreach (var run in hub.runs) { var r = run; menu.Options.Add(new MenuOption { Label = $"{run.Text} · ₦{run.Reward:N0} · {run.Minutes:0} min{(run.Heat > 0 ? " · risky" : "")}", Enabled = Data.TryPlace(run.Dest, out _), Action = () => StartRun(c, r) }); }
            }
            else menu.Options.Add(new MenuOption { Label = "Finish your current mission first", Enabled = false });
            foreach (var j in Data.JobsAt(c.data.at)) { var job = j; bool ok = State.CanTake(j); menu.Options.Add(new MenuOption { Label = ok ? $"Work shift — {j.title} · ₦{State.JobPay(j):N0} · {j.dur}h" : $"{j.title} (needs {j.skill} {j.min})", Enabled = ok, Action = () => StartShift(job) }); }
            AddActionOpsToMenu(menu, c.data.id);
            if (c.data.id == "berger_dispatch" && Data.states != null && Data.states.Length > 0)
                menu.Options.Add(new MenuOption { Label = $"Nigeria Interstate Directory · {Data.states.Length} States & Capitals", Action = () => OpenStatesDirectory() });
            AddRelationshipOptions(menu, c);
            menu.Options.Add(new MenuOption { Label = "Not now", Action = null });
            OpenMenu(menu);
        }

        public static string StateHubLandmarkId(string stateId, string zone) => stateId switch
        {
            "lagos" => "ikeja-city",
            "ogun" => "oke-mosan",
            "oyo" => "cocoa-house",
            "fct" => "nass-complex",
            "rivers" => "bonny-lng",
            "delta" => "warri-refinery",
            "edo" => "benin-igun-bronze",
            "anambra" => "onitsha-main-mkt",
            "enugu" => "enugu-lion-building",
            "abia" => "ariaria-market",
            "kano" => "kurmi-market",
            "kaduna" => "kaduna-kakuri-works",
            "borno" => "jtf-fob-north",
            _ => zone switch
            {
                "South-West" => "cocoa-house",
                "North-Central" => "nass-complex",
                "South-South" => "bonny-lng",
                "South-East" => "onitsha-main-mkt",
                "North-West" => "kurmi-market",
                "North-East" => "jtf-fob-north",
                _ => "berger-gate"
            }
        };

        public void OpenStatesDirectory()
        {
            var menu = new Menu
            {
                Title = "Nigeria · 36 States & FCT",
                Subtitle = "6 Geopolitical Zones · Local Domestic Flights, NRC Rail & Interstate Highways",
                Line = "Select any Nigerian State or FCT Abuja to fly via local domestic airline (for states with airports) or travel by interstate express coach."
            };
            menu.Options.Add(new MenuOption
            {
                Label = $"✈ Local Domestic Flights Departure Board ({StateAirports.Length} States with Airports)",
                Action = () => OpenStateFlightsMenu()
            });
            if (Data.states != null)
            {
                foreach (var st in Data.states)
                {
                    var s = st;
                    int fare = Mathf.Max(0, s.fare);
                    bool hasAirport = TryGetStateAirport(s.id, out var apSpec);
                    string airBadge = hasAirport ? $" · ✈ ₦{apSpec.Fare:N0}" : "";
                    string tag = s.status == "active"
                        ? (fare > 0 ? $"PLAYABLE HUB · Coach ₦{fare:N0}{airBadge}" : $"PLAYABLE HUB · LOCAL{airBadge}")
                        : $"ZONE HUB · {s.distanceKm} km · Coach ₦{fare:N0}{airBadge}";
                    menu.Options.Add(new MenuOption
                    {
                        Label = $"{s.name} ({s.capital}) · {s.zone} · {tag}",
                        Enabled = true,
                        Action = () => OpenStateTravelMenu(s)
                    });
                }
            }
            menu.Options.Add(new MenuOption { Label = "Back", Action = null });
            OpenMenu(menu);
        }

        public void OpenStateTravelMenu(StateData s)
        {
            int fare = Mathf.Max(0, s.fare);
            bool hasAirport = TryGetStateAirport(s.id, out var apSpec);
            var m = new Menu
            {
                Title = $"{s.name} ({s.capital}) · {s.zone}",
                Subtitle = $"“{s.slogan}” · {s.distanceKm} km via {s.corridor}",
                Line = hasAirport
                    ? $"{s.name} operates {apSpec.AirportName} ({apSpec.Airline}). Choose Local Domestic Flight or Interstate Road Coach:"
                    : $"{s.name} does not have a commercial airport — travel via Interstate Road Coach along {s.corridor}."
            };
            if (hasAirport)
            {
                m.Options.Add(new MenuOption
                {
                    Label = $"✈ Local Domestic Flight to {apSpec.AirportName} ({apSpec.Airline}) · ₦{apSpec.Fare:N0}",
                    Action = () => BookLocalStateFlight(apSpec)
                });
            }
            m.Options.Add(new MenuOption
            {
                Label = $"Interstate Express Road Coach to {s.name} ({s.capital}) · {(fare > 0 ? $"₦{fare:N0}" : "Local")}",
                Action = () =>
                {
                    string hubId = StateHubLandmarkId(s.id, s.zone);
                    if (Data.TryPlace(hubId, out var dest))
                    {
                        if (fare > 0 && !State.Pay(fare))
                        {
                            State.Toast($"Need ₦{fare:N0} for interstate travel to {s.name} ({s.capital})");
                            return;
                        }
                        if (Vehicle != null) Exit();
                        Player.Teleport(dest.World() + new Vector3(0f, 0.6f, -10f), Player.transform.eulerAngles.y);
                        Hours(Mathf.Clamp(s.distanceKm / 180f, 0.5f, 4f));
                        State.AddXp(15);
                        State.Toast($"Arrived in {s.name} ({s.capital}) — \"{s.slogan}\" via {s.corridor}");
                    }
                    else
                    {
                        State.Toast($"{s.name} State ({s.capital}) — \"{s.slogan}\" via {s.corridor}");
                    }
                }
            });
            m.Options.Add(new MenuOption { Label = "Back to 36 States Directory", Action = OpenStatesDirectory });
            OpenMenu(m);
        }

        public void StartRun(ContactSystem.Contact c, HubRun run)
        {
            if (c != null) ActiveChainId = "";
            var st = State.S;
            ActiveRun = run;
            st.taskType = "timed"; st.taskDest = run.Dest; st.taskItem = run.Item; st.taskObj = run.Text; st.taskMission = -1; st.taskDeadline = Clock.Clock + run.Minutes / 60f;
            State.AddItem(run.Item);
            State.Toast("Side mission started · follow the GPS");
            RefreshNav();
        }

        // Called by FinishTask when taskMission is -1.
        void FinishRun(bool ok)
        {
            var run = ActiveRun; ActiveRun = null;
            string chainId = ActiveChainId; ActiveChainId = "";
            if (run == null) return;
            if (ok)
            {
                State.Earn(run.Reward); State.AddRep(run.Rep, 3); State.AddXp(15); State.Toast($"{run.Label} delivered · +₦{run.Reward:N0}");
                if (run.Heat > 0) State.AddHeat(run.Heat, run.Label);
                if (!string.IsNullOrEmpty(chainId))
                {
                    var chain = ChainById(chainId);
                    int next = State.AdvanceChain(chainId);
                    State.AddXp(25);
                    if (chain != null)
                    {
                        if (next >= chain.Steps.Length)
                        {
                            State.Earn(25000);
                            State.AddRep(run.Rep, 8);
                            State.Toast($"{chain.Title} story chain complete! +₦25,000 bonus");
                            State.Message(chain.Who, $"You finished the whole {chain.Title} story in {chain.District}! +₦25,000 bonus for standing by us.");
                        }
                        else
                        {
                            State.Message(chain.Who, $"Chapter {next}/{chain.Steps.Length} of {chain.Title} done. Come see me when you are ready for Chapter {next + 1}.");
                        }
                    }
                }
            }
            else State.Toast($"{run.Label} not delivered in time");
        }

        // ---- schools ----
        void OpenSchoolMenu(LandmarkData p)
        {
            var cfg = Data.school; var st = State.S;
            if (p.id == "yabatech")
            {
                var menu = new Menu { Title = "Yaba College of Technology", Subtitle = "Computer Systems diploma", Line = st.collegeGraduated ? "Your Yaba Tech Diploma don complete. The Alagomeji Tech Hub dey hire." : st.collegeSemester >= 0 ? $"Welcome back. You don complete {st.collegeSemester} of {cfg.semesters} semesters for Computer Systems." : $"Enrol for the Computer Systems diploma, study {cfg.semesters} semesters, and qualify for IT support work." };
                if (st.collegeSemester < 0) menu.Options.Add(new MenuOption { Label = $"Enrol · Computer Systems diploma · ₦{cfg.enrollment:N0}", Action = () => Enrol() });
                else if (!st.collegeGraduated) menu.Options.Add(new MenuOption { Label = $"Study semester {st.collegeSemester + 1} · ₦{cfg.semester:N0} · {cfg.duration}h", Action = () => StudySemester() });
                menu.Options.Add(new MenuOption { Label = "Leave", Action = null });
                OpenMenu(menu);
                return;
            }
            var m = new Menu { Title = p != null ? p.name : "Community Grammar School", Subtitle = p != null && p.id == "ui-gate" ? "Premier University · Agbowo, Ibadan" : (p?.name ?? "Adult Education"), Line = p != null && p.id == "ui-gate" ? "Attend an executive seminar or recruit UI computer science & engineering talent." : "Evening adult classes — or support a student." };
            m.Options.Add(new MenuOption { Label = $"Evening class · ₦{cfg.eveningClass:N0}", Action = () => EveningClass() });
            if (p != null && p.id == "ui-gate")
            {
                m.Options.Add(new MenuOption
                {
                    Label = "UI Agbowo Tech & Policy Colloquium · ₦8,000 (+4 Business, +3 Charisma, +20 XP)",
                    Action = () =>
                    {
                        if (!State.Pay(8000)) { State.Toast("Need ₦8,000 for UI colloquium"); return; }
                        State.GainSkill("business", 4);
                        State.GainSkill("charisma", 3);
                        State.AddRep("public", 4);
                        State.AddXp(20);
                        Hours(2f);
                        State.Toast("Completed University of Ibadan colloquium · Business +4 · Charisma +3");
                    }
                });
            }
            m.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(m);
        }

        public bool Enrol()
        {
            if (State.S.collegeSemester >= 0) return false;
            if (!State.Pay(Data.school.enrollment)) { State.Toast("Not enough money to enrol"); return false; }
            State.S.collegeSemester = 0; Hours(1f); State.AddXp(5); State.Toast("Enrolled at Yaba Tech · Computer Systems");
            return true;
        }

        public bool StudySemester()
        {
            var cfg = Data.school;
            if (State.S.stamina < cfg.stamina) { State.Toast("Too tired to study — rest first"); return false; }
            if (!State.Pay(cfg.semester)) { State.Toast("Not enough money for semester fees"); return false; }
            State.S.stamina -= cfg.stamina; Hours(cfg.duration);
            return State.CompleteSemester();
        }

        public bool EveningClass()
        {
            var cfg = Data.school;
            if (State.S.stamina < 15f) { State.Toast("Too tired"); return false; }
            if (!State.Pay(cfg.eveningClass)) { State.Toast("Not enough money"); return false; }
            State.S.stamina -= 15f; Hours(2f);
            if (cfg.eveningSkill != null) foreach (var sp in cfg.eveningSkill) State.GainSkill(sp.skill, sp.points);
            State.AddXp(8); State.Toast("Class done");
            return true;
        }

        // ---- street football tiers (playMatch) ----
        public bool PlayMatch(int tier, bool stakes)
        {
            var c = Data.football; var st = State.S;
            tier = Mathf.Clamp(tier, 0, Mathf.Max(0, c.tiers.Length - 1));
            int prize = c.prizes != null && tier < c.prizes.Length ? c.prizes[tier] : 0;
            if (st.stamina < 40f) { State.Toast("Too tired to play"); return false; }
            if (stakes && prize == 0 && !State.Pay(c.wager)) { State.Toast("Not enough money for the wager"); return false; }
            float chance = 0.35f + st.skills.fitness * 0.006f - tier * 0.12f;
            bool won = Random.value < chance;
            st.stamina -= 40f; Hours(1.5f); State.GainSkill("fitness", 3); State.AddRep("social", 2);
            if (won)
            {
                st.footballWins++; State.AddRep("street", 3); State.AddXp(10);
                if (stakes) { int pay = prize > 0 ? prize : c.wager * 2; State.Earn(pay); State.Toast($"You won {c.tiers[tier]} · +₦{pay:N0}"); if (prize > 0) st.footballTier = Mathf.Min(tier + 1, c.tiers.Length - 1); }
                else State.Toast("Won the kickabout · Fitness up");
                if (st.footballWins == 3) { st.footballTier = Mathf.Max(st.footballTier, 1); State.Message("captain", "Omo, you sabi ball! Neighbourhood tournament dey next week. ₦20,000 prize."); }
                if (st.footballWins == 6) State.Message("amaka", "Heard you dey win tournaments. Iponri crews want a sponsor deal — come see me.");
            }
            else State.Toast(stakes ? "Lost the match" : "Lost the kickabout");
            return won;
        }

        // ---- EA Sports FC 26 / Dream League Soccer (DLS) Live 3D Football Match & Tournament Engine ----
        public class FootballEvent
        {
            public string Id, Title, Subtitle, HomeTag, AwayTag, PitchId;
            public string[] OpponentClubs;
            public string[] AwayColors;
            public int Rounds = 1;
            public int Wager, Prize, Xp;
            public int TierIndex;
        }

        public static readonly FootballEvent[] FootballEvents =
        {
            new FootballEvent
            {
                Id = "fc_kickabout",
                Title = "Surulere 5v5 Live Kickabout",
                Subtitle = "Friendly Live Match · Teslim Balogun Pitch",
                HomeTag = "SUR", AwayTag = "OJU", PitchId = "pitch",
                OpponentClubs = new[] { "Ojuelegba Stars" },
                AwayColors = new[] { "#e11d48" },
                Rounds = 1, Wager = 0, Prize = 10000, Xp = 20, TierIndex = 0
            },
            new FootballEvent
            {
                Id = "fc_street_wager",
                Title = "Teslim Turf Money Match",
                Subtitle = "High-Stakes 5v5 Clash · ₦5,000 Wager",
                HomeTag = "SUR", AwayTag = "ISL", PitchId = "pitch",
                OpponentClubs = new[] { "Lagos Island Raiders" },
                AwayColors = new[] { "#f59e0b" },
                Rounds = 1, Wager = 5000, Prize = 20000, Xp = 25, TierIndex = 0
            },
            new FootballEvent
            {
                Id = "fc_scout_trial",
                Title = "Super Eagles Scout Showcase Trial",
                Subtitle = "AJ City Pitch · Pro Talent Trial",
                HomeTag = "AJC", AwayTag = "APA", PitchId = "ajpitch",
                OpponentClubs = new[] { "Apapa Dockers FC" },
                AwayColors = new[] { "#2563eb" },
                Rounds = 1, Wager = 0, Prize = 35000, Xp = 35, TierIndex = 1
            },
            new FootballEvent
            {
                Id = "fc_neighbourhood_cup",
                Title = "Lagos Neighbourhood Cup (Tournament)",
                Subtitle = "Semi-Final & Final Knockout · ₦60,000 Prize",
                HomeTag = "SUR", AwayTag = "YAB", PitchId = "pitch",
                OpponentClubs = new[] { "Yaba Tech Blazers", "Ikoyi Royals FC" },
                AwayColors = new[] { "#9333ea", "#dc2626" },
                Rounds = 2, Wager = 5000, Prize = 60000, Xp = 50, TierIndex = 1
            },
            new FootballEvent
            {
                Id = "fc_champions_league",
                Title = "Eko Premier Champions Cup",
                Subtitle = "Quarter-Final, Semi-Final & Grand Final · ₦150,000 Prize",
                HomeTag = "EKO", AwayTag = "VIG", PitchId = "pitch",
                OpponentClubs = new[] { "VI Galacticos", "Lekki United FC", "Eko Atlantic Titans" },
                AwayColors = new[] { "#0284c7", "#d97706", "#e11d48" },
                Rounds = 3, Wager = 10000, Prize = 150000, Xp = 75, TierIndex = 2
            },
            new FootballEvent
            {
                Id = "fc_super_eagles",
                Title = "Super Eagles All-Star Invitational",
                Subtitle = "International Exhibition Cup · ₦220,000 Prize",
                HomeTag = "NGA", AwayTag = "WAF", PitchId = "pitch",
                OpponentClubs = new[] { "West Africa XI", "Continental All-Stars" },
                AwayColors = new[] { "#eab308", "#ef4444" },
                Rounds = 2, Wager = 15000, Prize = 220000, Xp = 90, TierIndex = 2
            },
        };

        public class Footballer
        {
            public Transform Root;
            public RigInstance Rig;
            public string Name, Role;
            public int Number;
            public bool IsHome, IsKeeper;
            public Vector3 FormationOffset;
            public Vector3 Velocity;
            public float ActionCooldown;
            public TextMesh MarkerText;
        }

        public class LiveFootballMatch
        {
            public FootballEvent Event;
            public int CurrentRound = 1;
            public int HomeScore, AwayScore;
            public float MatchClock;            // 0..90 match minutes
            public float MatchSpeed = 2.0f;     // match minutes per real second (45s full match)
            public Vector3 PitchCenter;
            public float PitchSurfaceY;
            public Transform MatchPropsRoot;
            public Transform BallRoot, BallMesh, BallRing, TargetRing;
            public Vector3 BallPos, BallVel;
            public float BallSpinYaw;
            public Footballer BallCarrier;      // null if loose or carried by player
            public Footballer BestReceiver;
            public Footballer GiveAndGoRunner;
            public float GiveAndGoTimer;
            public bool PlayerHasBall;
            public float PlayerControlLock;
            public float SkillBoostTimer;
            public int TacticMode;              // 0 = Balanced, 1 = All-Out Attack, 2 = Counter-Attack
            public string TacticLabel => TacticMode == 1 ? "ALL-OUT ATTACK" : TacticMode == 2 ? "COUNTER-ATTACK" : "BALANCED";
            public float GoalBannerTimer;
            public string GoalBannerText = "";
            public string Commentary = "Kick-off at the stadium! Pass [E], Hold/Tap Shoot [F / Click], Skill/Chip [G], Tactics [T]!";
            public float CommentaryTimer = 6f;
            public float ShotPowerCharge;
            public bool ChargingShot;
            public float LastShotPower;
            public float LastShotQualityTimer;
            public string LastShotQualityLabel = "";
            public string SavedWeapon = "";
            public readonly List<Footballer> Players = new List<Footballer>();
        }

        [System.NonSerialized] public LiveFootballMatch ActiveFootball;
        public bool InFootballMatch => ActiveFootball != null;

        public string CurrentOpponentClub
        {
            get
            {
                if (ActiveFootball == null || ActiveFootball.Event.OpponentClubs == null || ActiveFootball.Event.OpponentClubs.Length == 0) return "Rivals FC";
                int idx = Mathf.Clamp(ActiveFootball.CurrentRound - 1, 0, ActiveFootball.Event.OpponentClubs.Length - 1);
                return ActiveFootball.Event.OpponentClubs[idx];
            }
        }

        public string CurrentTournamentStageLabel
        {
            get
            {
                if (ActiveFootball == null) return "";
                int total = ActiveFootball.Event.Rounds;
                int cur = ActiveFootball.CurrentRound;
                if (total <= 1) return "LIVE MATCH";
                if (cur == total) return "GRAND FINAL";
                if (cur == total - 1) return "SEMI-FINAL";
                return $"QUARTER-FINAL ({cur}/{total})";
            }
        }

        public static FootballEvent FindFootballEvent(string id)
        {
            foreach (var e in FootballEvents) if (e.Id == id) return e;
            return FootballEvents[0];
        }

        public void OpenFootballMenu(LandmarkData place = null)
        {
            var c = Data.football;
            var st = State.S;
            int tier = Mathf.Clamp(st.footballTier, 0, Mathf.Max(0, c.tiers.Length - 1));
            int prize = c.prizes != null && tier < c.prizes.Length ? c.prizes[tier] : 0;
            string venue = place != null ? place.name : "Teslim Balogun & AJ City Stadiums";
            var menu = new Menu
            {
                Title = "Lagos Live Football · FC 26 / DLS Mode",
                Subtitle = $"Captain Tunde & Coach Baba Goal · {venue}",
                Line = st.footballWins > 0
                    ? $"Career Record: {st.footballWins} win{(st.footballWins > 1 ? "s" : "")} · Tier {st.footballTier + 1}. Step onto the 3D pitch for live 5v5 matches and knockout tournaments!"
                    : "Live 5v5 3D pitch gameplay! Dribble, thread through-balls [E], curl power shots [F / Click], rainbow-flick [G], and lift the Eko Champions Cup!"
            };

            if (InFootballMatch)
            {
                menu.Options.Add(new MenuOption
                {
                    Label = $"Resume Live Match ({ActiveFootball.Event.HomeTag} {ActiveFootball.HomeScore} - {ActiveFootball.AwayScore} {ActiveFootball.Event.AwayTag})",
                    Action = () => ActiveMenu = null
                });
                menu.Options.Add(new MenuOption
                {
                    Label = "Forfeit & Leave Current Match",
                    Action = () => EndLiveFootballMatch(false, true)
                });
            }
            else
            {
                foreach (var ev in FootballEvents)
                {
                    var captured = ev;
                    string roundTag = ev.Rounds > 1 ? $"{ev.Rounds}-Round Tournament" : "Live 5v5 Match";
                    string costTag = ev.Wager > 0 ? $" · Wager ₦{ev.Wager:N0}" : " · Free Entry";
                    menu.Options.Add(new MenuOption
                    {
                        Label = $"[LIVE 3D] {ev.Title} ({roundTag}{costTag} → ₦{ev.Prize:N0})",
                        Action = () => StartLiveFootballMatch(captured.Id)
                    });
                }
                menu.Options.Add(new MenuOption
                {
                    Label = $"Quick Sim: {c.tiers[tier]}{(prize > 0 ? $" (₦{prize:N0} prize)" : $" · ₦{c.wager:N0} wager")}",
                    Action = () => PlayMatch(tier, true)
                });
                menu.Options.Add(new MenuOption
                {
                    Label = "Quick Sim: Friendly kickabout (no money)",
                    Action = () => PlayMatch(0, false)
                });
            }

            menu.Options.Add(new MenuOption { Label = "Leave", Action = null });
            OpenMenu(menu);
        }

        public bool StartLiveFootballMatch(string eventId)
        {
            var ev = FindFootballEvent(eventId);
            if (ev == null) return false;
            if (InFootballMatch) EndLiveFootballMatch(false, true);
            if (Vehicle != null) Exit();
            if (InInterior) LeaveInterior();

            if (ev.Wager > 0 && !State.Pay(ev.Wager))
            {
                State.Toast($"Need ₦{ev.Wager:N0} entry fee for {ev.Title}");
                return false;
            }

            LandmarkData pitchLm = null;
            if (!string.IsNullOrEmpty(ev.PitchId) && Data.landmarks != null)
                foreach (var lm in Data.landmarks) if (lm.id == ev.PitchId) { pitchLm = lm; break; }
            if (pitchLm == null) pitchLm = Data.NearestLandmark("pitch", Player.transform.position, 5000f);
            Vector3 center = pitchLm != null ? pitchLm.World() : new Vector3(-170f, 0f, -100f);
            float surfaceY = center.y + 0.25f;

            string prevWeapon = State.S.equippedWeapon;
            Player.SetWeapon("");

            ActiveFootball = new LiveFootballMatch
            {
                Event = ev,
                CurrentRound = 1,
                HomeScore = 0,
                AwayScore = 0,
                MatchClock = 0f,
                PitchCenter = center,
                PitchSurfaceY = surfaceY,
                SavedWeapon = prevWeapon
            };

            BuildLivePitchArena(ActiveFootball);
            SpawnFootballSquads(ActiveFootball);
            ResetKickoffPositions(ActiveFootball, true);

            ActiveMenu = null;
            PhoneOpen = false;
            UI.SoundSystem.I?.PlayWhistle();
            State.Toast($"KICK-OFF! {ev.Title} · {CurrentTournamentStageLabel} vs {CurrentOpponentClub}");
            RefreshNav();
            return true;
        }

        void BuildLivePitchArena(LiveFootballMatch m)
        {
            var root = new GameObject("LiveFootballArena").transform;
            root.SetParent(transform, false);
            m.MatchPropsRoot = root;

            var whiteMat = new Material(ShaderLib.Unlit) { color = new Color(0.96f, 0.98f, 0.96f, 0.9f) };
            var netMat = new Material(ShaderLib.Unlit) { color = new Color(0.92f, 0.95f, 0.98f, 0.38f) };
            var boardMat = new Material(ShaderLib.Lit) { color = new Color(0.06f, 0.18f, 0.12f) };
            var goldMat = new Material(ShaderLib.Unlit) { color = new Color(0.98f, 0.78f, 0.18f) };
            var c = m.PitchCenter;
            float y = m.PitchSurfaceY + 0.01f;

            void LineBox(string label, Vector3 pos, Vector3 scale, Material mat)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = label;
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                go.transform.localScale = scale;
                go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                Destroy(go.GetComponent<Collider>());
            }

            LineBox("Touchline N", new Vector3(c.x, y, c.z + 14f), new Vector3(41.2f, 0.02f, 0.22f), whiteMat);
            LineBox("Touchline S", new Vector3(c.x, y, c.z - 14f), new Vector3(41.2f, 0.02f, 0.22f), whiteMat);
            LineBox("Goalline W", new Vector3(c.x - 20.5f, y, c.z), new Vector3(0.22f, 0.02f, 28.2f), whiteMat);
            LineBox("Goalline E", new Vector3(c.x + 20.5f, y, c.z), new Vector3(0.22f, 0.02f, 28.2f), whiteMat);

            for (int side = -1; side <= 1; side += 2)
            {
                float gx = c.x + side * 20.5f;
                float boxFrontX = c.x + side * 14.0f;
                float boxMidX = (gx + boxFrontX) * 0.5f;
                LineBox("Penalty Front", new Vector3(boxFrontX, y, c.z), new Vector3(0.2f, 0.02f, 13.6f), whiteMat);
                LineBox("Penalty Side N", new Vector3(boxMidX, y, c.z + 6.8f), new Vector3(6.5f, 0.02f, 0.2f), whiteMat);
                LineBox("Penalty Side S", new Vector3(boxMidX, y, c.z - 6.8f), new Vector3(6.5f, 0.02f, 0.2f), whiteMat);

                LineBox("Goal Net Back", new Vector3(gx + side * 1.35f, y + 1.25f, c.z), new Vector3(0.08f, 2.5f, 5.7f), netMat);
                LineBox("Goal Net Top", new Vector3(gx + side * 0.68f, y + 2.5f, c.z), new Vector3(1.35f, 0.08f, 5.7f), netMat);
                LineBox("Goal Net L", new Vector3(gx + side * 0.68f, y + 1.25f, c.z - 2.85f), new Vector3(1.35f, 2.5f, 0.08f), netMat);
                LineBox("Goal Net R", new Vector3(gx + side * 0.68f, y + 1.25f, c.z + 2.85f), new Vector3(1.35f, 2.5f, 0.08f), netMat);

                // 3D Cylindrical Goalposts & Crossbar
                LineBox("Post L", new Vector3(gx, y + 1.28f, c.z - 2.88f), new Vector3(0.14f, 2.56f, 0.14f), whiteMat);
                LineBox("Post R", new Vector3(gx, y + 1.28f, c.z + 2.88f), new Vector3(0.14f, 2.56f, 0.14f), whiteMat);
                LineBox("Crossbar", new Vector3(gx, y + 2.54f, c.z), new Vector3(0.14f, 0.14f, 5.9f), whiteMat);
                LineBox("Penalty Spot", new Vector3(c.x + side * 15.8f, y + 0.01f, c.z), new Vector3(0.32f, 0.02f, 0.32f), whiteMat);
            }

            // Halfway line & corner flags
            LineBox("Halfway Line", new Vector3(c.x, y, c.z), new Vector3(0.18f, 0.02f, 28.0f), whiteMat);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    LineBox("CornerPole", new Vector3(c.x + sx * 20.5f, y + 0.55f, c.z + sz * 14f), new Vector3(0.06f, 1.1f, 0.06f), whiteMat);
                    LineBox("CornerFlag", new Vector3(c.x + sx * 20.5f + 0.16f, y + 1.0f, c.z + sz * 14f), new Vector3(0.32f, 0.22f, 0.04f), goldMat);
                }

            LineBox("Center Spot", new Vector3(c.x, y + 0.01f, c.z), new Vector3(0.6f, 0.02f, 0.6f), goldMat);
            LineBox("LED Hoarding N", new Vector3(c.x, y + 0.45f, c.z + 15.2f), new Vector3(36f, 0.9f, 0.25f), boardMat);
            LineBox("LED Hoarding Trim", new Vector3(c.x, y + 0.92f, c.z + 15.2f), new Vector3(36f, 0.08f, 0.28f), goldMat);

            var ballRoot = new GameObject("MatchBall").transform;
            ballRoot.SetParent(root, false);
            m.BallRoot = ballRoot;

            var ballSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballSphere.name = "BallMesh";
            ballSphere.transform.SetParent(ballRoot, false);
            ballSphere.transform.localScale = Vector3.one * 0.46f;
            var ballMat = new Material(ShaderLib.Lit) { color = new Color(0.97f, 0.97f, 0.95f) };
            ballMat.SetFloat("_Smoothness", 0.7f);
            ballSphere.GetComponent<MeshRenderer>().sharedMaterial = ballMat;
            Destroy(ballSphere.GetComponent<Collider>());
            m.BallMesh = ballSphere.transform;

            var bandMat = new Material(ShaderLib.Lit) { color = new Color(0.12f, 0.14f, 0.18f) };
            for (int b = 0; b < 2; b++)
            {
                var stripe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stripe.transform.SetParent(ballSphere.transform, false);
                stripe.transform.localScale = new Vector3(1.03f, 0.08f, 1.03f);
                stripe.transform.localRotation = Quaternion.Euler(b * 90f, b * 45f, 0f);
                stripe.GetComponent<MeshRenderer>().sharedMaterial = bandMat;
                Destroy(stripe.GetComponent<Collider>());
            }

            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "BallPossessionRing";
            ringGo.transform.SetParent(root, false);
            ringGo.transform.localScale = new Vector3(1.35f, 0.02f, 1.35f);
            ringGo.GetComponent<MeshRenderer>().sharedMaterial = new Material(ShaderLib.Unlit) { color = new Color(0.16f, 0.92f, 0.52f, 0.85f) };
            Destroy(ringGo.GetComponent<Collider>());
            m.BallRing = ringGo.transform;

            var targetRingGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            targetRingGo.name = "PassTargetRing";
            targetRingGo.transform.SetParent(root, false);
            targetRingGo.transform.localScale = new Vector3(1.45f, 0.018f, 1.45f);
            targetRingGo.GetComponent<MeshRenderer>().sharedMaterial = new Material(ShaderLib.Unlit) { color = new Color(0.15f, 0.88f, 1.0f, 0.72f) };
            Destroy(targetRingGo.GetComponent<Collider>());
            targetRingGo.SetActive(false);
            m.TargetRing = targetRingGo.transform;
        }

        void SpawnFootballSquads(LiveFootballMatch m)
        {
            ClearFootballPlayers(m);

            Color homeTop = Mannequin.Hex("#16a34a");
            Color homeShorts = Mannequin.Hex("#f8fafc");
            Color homeGkTop = Mannequin.Hex("#eab308");

            string awayHex = m.Event.AwayColors != null && m.Event.AwayColors.Length > 0
                ? m.Event.AwayColors[Mathf.Clamp(m.CurrentRound - 1, 0, m.Event.AwayColors.Length - 1)]
                : "#e11d48";
            Color awayTop = Mannequin.Hex(awayHex);
            Color awayShorts = Mannequin.Hex("#1e293b");
            Color awayGkTop = Mannequin.Hex("#06b6d4");

            (string name, string role, int num, bool gk, Vector3 offset)[] homeRoster =
            {
                ("Nwabali", "GK", 1, true,  new Vector3(-19.2f, 0f, 0f)),
                ("Ekong",   "CB", 5, false, new Vector3(-10.5f, 0f, -4.5f)),
                ("Ndidi",   "CM", 4, false, new Vector3(-4.5f,  0f,  5.0f)),
                ("Victor O.", "ST", 9, false, new Vector3(4.5f, 0f, -3.2f)),
            };

            foreach (var h in homeRoster)
                m.Players.Add(CreateFootballer(m, h.name, h.role, h.num, true, h.gk, h.offset, h.gk ? homeGkTop : homeTop, homeShorts));

            (string name, string role, int num, bool gk, Vector3 offset)[] awayRoster =
            {
                ("Keeper",    "GK", 1,  true,  new Vector3(19.2f, 0f, 0f)),
                ("Koulibaly", "CB", 4,  false, new Vector3(11.0f, 0f, -4.2f)),
                ("Partey",    "CB", 6,  false, new Vector3(10.5f, 0f,  4.2f)),
                ("Kudus",     "CM", 8,  false, new Vector3(3.5f,  0f,  4.8f)),
                ("Mane",      "ST", 11, false, new Vector3(-3.0f, 0f, -2.5f)),
            };

            foreach (var a in awayRoster)
                m.Players.Add(CreateFootballer(m, a.name, a.role, a.num, false, a.gk, a.offset, a.gk ? awayGkTop : awayTop, awayShorts));
        }

        Footballer CreateFootballer(LiveFootballMatch m, string pName, string role, int number, bool isHome, bool isKeeper, Vector3 offset, Color jersey, Color shorts)
        {
            var go = new GameObject($"Footballer_{(isHome ? "HOME" : "AWAY")}_{number}_{pName}");
            go.transform.SetParent(m.MatchPropsRoot, false);
            var pos = m.PitchCenter + new Vector3(offset.x, 0f, offset.z);
            pos.y = m.PitchSurfaceY;
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, isHome ? 90f : -90f, 0f);

            var look = Look.Default;
            look.Outfit = 0;
            look.Fabric = isHome ? 2 : 4;
            look.Pants = shorts;
            look.Skin = Mannequin.Hex("#6b4228");
            look.Cap = jersey;
            look.HairStyle = (number % 3) + 1;
            look.Accessory = 0;

            RigInstance rig = null;
            if (CharacterRig.Available) rig = CharacterRig.Build(go.transform, look);
            else Mannequin.Build(go.transform, jersey, shorts, look.Skin, null);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var labelGo = new GameObject("FC26Indicator");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 36;
            tm.characterSize = 0.048f;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = isHome ? new Color(0.22f, 0.96f, 0.56f) : new Color(1.0f, 0.34f, 0.38f);
            tm.text = $"▼ #{number} {pName}";
            if (font != null) labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;

            return new Footballer
            {
                Root = go.transform,
                Rig = rig,
                Name = pName,
                Role = role,
                Number = number,
                IsHome = isHome,
                IsKeeper = isKeeper,
                FormationOffset = offset,
                MarkerText = tm
            };
        }

        void ClearFootballPlayers(LiveFootballMatch m)
        {
            if (m == null) return;
            foreach (var p in m.Players)
                if (p.Root != null) Destroy(p.Root.gameObject);
            m.Players.Clear();
        }

        void ResetKickoffPositions(LiveFootballMatch m, bool homeKickoff)
        {
            var c = m.PitchCenter;
            float y = m.PitchSurfaceY;
            m.BallPos = new Vector3(c.x, y + 0.23f, c.z);
            m.BallVel = Vector3.zero;
            m.BallCarrier = null;
            m.PlayerHasBall = false;
            m.PlayerControlLock = 0.15f;

            foreach (var p in m.Players)
            {
                if (p.Root == null) continue;
                var pos = c + new Vector3(p.FormationOffset.x, 0f, p.FormationOffset.z);
                pos.y = y;
                p.Root.position = pos;
                p.Root.rotation = Quaternion.Euler(0f, p.IsHome ? 90f : -90f, 0f);
                p.Velocity = Vector3.zero;
                p.ActionCooldown = 0.6f;
                p.Rig?.SetState("idle");
            }

            if (Player != null)
            {
                Vector3 playerStart = c + new Vector3(homeKickoff ? -0.6f : -4.2f, 0.05f, 0f);
                Player.Teleport(playerStart, 90f);
                if (homeKickoff)
                {
                    m.PlayerHasBall = true;
                    m.BallPos = playerStart + Vector3.right * 0.75f + Vector3.up * 0.23f;
                }
            }

            SyncBallTransform(m);
        }

        void SyncBallTransform(LiveFootballMatch m)
        {
            if (m.BallRoot != null) m.BallRoot.position = m.BallPos;
            if (m.BallRing != null)
            {
                m.BallRing.position = new Vector3(m.BallPos.x, m.PitchSurfaceY + 0.02f, m.BallPos.z);
                var mr = m.BallRing.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    mr.sharedMaterial.color = m.PlayerHasBall || (m.BallCarrier != null && m.BallCarrier.IsHome)
                        ? new Color(0.16f, 0.94f, 0.52f, 0.88f)
                        : m.BallCarrier != null
                            ? new Color(0.98f, 0.24f, 0.28f, 0.88f)
                            : new Color(0.98f, 0.82f, 0.20f, 0.85f);
                }
            }
            if (m.TargetRing != null)
            {
                if (m.PlayerHasBall && m.BestReceiver != null && m.BestReceiver.Root != null)
                {
                    if (!m.TargetRing.gameObject.activeSelf) m.TargetRing.gameObject.SetActive(true);
                    var rp = m.BestReceiver.Root.position;
                    m.TargetRing.position = new Vector3(rp.x, m.PitchSurfaceY + 0.022f, rp.z);
                }
                else if (m.TargetRing.gameObject.activeSelf)
                {
                    m.TargetRing.gameObject.SetActive(false);
                }
            }
        }

        public void CycleFootballTactic()
        {
            var m = ActiveFootball;
            if (m == null) return;
            m.TacticMode = (m.TacticMode + 1) % 3;
            SetFootballCommentary($"TEAM TACTICS: {m.TacticLabel} activated!");
            State.Toast($"Football Tactic: {m.TacticLabel}");
        }

        Footballer FindBestPassReceiver(LiveFootballMatch m)
        {
            if (m == null || Player == null) return null;
            Footballer bestMate = null;
            float bestScore = float.MinValue;
            Vector3 pPos = Player.transform.position;
            Vector3 aimDir = Player.transform.forward;
            aimDir.y = 0f;
            if (aimDir.sqrMagnitude < 0.01f) aimDir = Vector3.right;
            aimDir.Normalize();

            foreach (var p in m.Players)
            {
                if (!p.IsHome || p.IsKeeper || p.Root == null) continue;
                Vector3 toMate = p.Root.position - pPos;
                toMate.y = 0f;
                float d = toMate.magnitude;
                if (d < 1.8f) continue;
                float align = Vector3.Dot(aimDir, toMate.normalized);
                float forwardAdvantage = (p.Root.position.x - pPos.x) * 1.4f + align * 8.5f - d * 0.28f;
                if (forwardAdvantage > bestScore) { bestScore = forwardAdvantage; bestMate = p; }
            }
            return bestMate;
        }

        public bool FootballShoot(float powerBoost = 1f)
        {
            var m = ActiveFootball;
            if (m == null || Player == null) return false;

            // If an AI teammate has the ball, pressing Shoot commands them to fire on goal immediately!
            if (!m.PlayerHasBall && m.BallCarrier != null && m.BallCarrier.IsHome && m.BallCarrier.Root != null)
            {
                var shooter = m.BallCarrier;
                m.BallCarrier = null;
                shooter.ActionCooldown = 1.0f;
                Vector3 mateGoal = new Vector3(m.PitchCenter.x + 20.6f, m.PitchSurfaceY + 1.1f, m.PitchCenter.z + Random.Range(-2.1f, 2.1f));
                m.BallVel = (mateGoal - m.BallPos).normalized * 23f + Vector3.up * 2.6f;
                m.BallSpinYaw = Random.Range(-280f, 280f);
                UI.SoundSystem.I?.PlayKick();
                SetFootballCommentary($"You called SHOT! #{shooter.Number} {shooter.Name} unleashes a rocket at goal!");
                return true;
            }

            float distToBall = Vector3.Distance(Flat(Player.transform.position), Flat(m.BallPos));
            if (!m.PlayerHasBall && distToBall > 3.2f) return false;

            m.PlayerHasBall = false;
            m.BallCarrier = null;
            m.PlayerControlLock = 0.35f;
            m.ChargingShot = false;

            // Directional corner aiming from player facing / movement input
            float aimZ = Mathf.Clamp(Player.transform.forward.z * 2.6f + GameInput.Move.x * 0.8f, -2.35f, 2.35f);
            if (Mathf.Abs(aimZ) < 0.35f) aimZ = (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.1f, 2.15f);

            bool greenTimed = powerBoost >= 1.12f && powerBoost <= 1.38f;
            float skillFactor = 1f + Mathf.Clamp01(State.S.skills.fitness / 180f) * 0.35f;
            float shotSpeed = 23.5f * Mathf.Clamp(powerBoost, 0.85f, 1.48f) * skillFactor * (greenTimed ? 1.12f : 1f);
            float lift = greenTimed ? 2.85f : Mathf.Lerp(1.9f, 4.4f, Mathf.InverseLerp(0.85f, 1.45f, powerBoost));

            Vector3 goalTarget = new Vector3(m.PitchCenter.x + 20.6f, m.PitchSurfaceY + 1.15f, m.PitchCenter.z + aimZ);
            Vector3 toGoal = (goalTarget - m.BallPos).normalized;

            m.BallVel = toGoal * shotSpeed + Vector3.up * lift;
            m.BallSpinYaw = greenTimed ? -Mathf.Sign(aimZ) * 420f : Random.Range(-320f, 320f);
            m.LastShotPower = Mathf.Clamp01((powerBoost - 0.8f) / 0.65f);
            m.LastShotQualityTimer = 2.0f;
            m.LastShotQualityLabel = greenTimed ? "GREEN TIMED FINISH! TOP-CORNER CURLER!" : powerBoost > 1.38f ? "POWER BLAST!" : "FINESSE STRIKE!";

            Player.SwingWeapon();
            UI.SoundSystem.I?.PlayKick();
            SetFootballCommentary(greenTimed
                ? "GREEN TIMED FINISH! Curling strike bending into the top corner!"
                : "Power strike unleashed towards the corner!");
            return true;
        }

        public bool FootballPass()
        {
            var m = ActiveFootball;
            if (m == null || Player == null) return false;

            if (!m.PlayerHasBall && m.BallCarrier != null && m.BallCarrier.IsHome)
            {
                var passer = m.BallCarrier;
                m.BallCarrier = null;
                passer.ActionCooldown = 0.65f;
                m.GiveAndGoRunner = passer;
                m.GiveAndGoTimer = 3.2f;
                Vector3 toPlayer = (Player.transform.position + Player.transform.forward * 1.4f - m.BallPos);
                toPlayer.y = 0f;
                m.BallVel = toPlayer.normalized * 17.5f + Vector3.up * 1.1f;
                UI.SoundSystem.I?.PlayKick(0.45f);
                SetFootballCommentary($"{passer.Name} threads a crisp 1-2 pass to you and sprints into space!");
                return true;
            }

            float distToBall = Vector3.Distance(Flat(Player.transform.position), Flat(m.BallPos));
            if (!m.PlayerHasBall && distToBall > 2.8f) return false;

            Footballer bestMate = m.BestReceiver ?? FindBestPassReceiver(m);

            m.PlayerHasBall = false;
            m.BallCarrier = null;
            m.PlayerControlLock = 0.32f;
            m.ChargingShot = false;
            m.ShotPowerCharge = 0f;

            // Lead through-ball ahead of forward runners
            float leadX = bestMate != null && bestMate.Role == "ST" ? 2.4f : 1.5f;
            Vector3 targetPos = bestMate != null
                ? bestMate.Root.position + Vector3.right * leadX
                : Player.transform.position + Player.transform.forward * 10f;
            Vector3 dir = (targetPos - m.BallPos);
            dir.y = 0f;
            float passDist = dir.magnitude;
            m.BallVel = dir.normalized * Mathf.Clamp(13f + passDist * 0.48f, 14f, 21f) + Vector3.up * 0.85f;
            UI.SoundSystem.I?.PlayKick(0.45f);
            SetFootballCommentary(bestMate != null
                ? $"Through-ball played to #{bestMate.Number} {bestMate.Name}!"
                : "Through-ball played into space!");
            return true;
        }

        public bool FootballChip()
        {
            var m = ActiveFootball;
            if (m == null || Player == null) return false;
            float distToBall = Vector3.Distance(Flat(Player.transform.position), Flat(m.BallPos));
            if (!m.PlayerHasBall && distToBall > 2.8f) return false;

            float distToOpponentGoal = (m.PitchCenter.x + 20.5f) - m.BallPos.x;
            // Outside 16.5m, [G] performs a Jay-Jay Okocha Skill Move (Stepover / Elastico / Speed Burst) that stuns nearby defenders!
            if (m.PlayerHasBall && distToOpponentGoal > 16.5f)
            {
                m.SkillBoostTimer = 1.85f;
                int stunned = 0;
                foreach (var opp in m.Players)
                {
                    if (opp.IsHome || opp.Root == null) continue;
                    if (Vector3.Distance(Flat(Player.transform.position), Flat(opp.Root.position)) < 4.2f)
                    {
                        opp.ActionCooldown = 1.65f;
                        stunned++;
                    }
                }
                Vector3 burst = Player.transform.position + Player.transform.forward * 1.65f;
                burst.x = Mathf.Clamp(burst.x, m.PitchCenter.x - 19.8f, m.PitchCenter.x + 19.8f);
                burst.z = Mathf.Clamp(burst.z, m.PitchCenter.z - 13.5f, m.PitchCenter.z + 13.5f);
                Player.Teleport(new Vector3(burst.x, m.PitchSurfaceY + 0.05f, burst.z), Player.transform.eulerAngles.y);
                m.BallPos = Player.transform.position + Player.transform.forward * 0.85f + Vector3.up * 0.45f;
                UI.SoundSystem.I?.PlayKick(0.38f);
                SetFootballCommentary(stunned > 0
                    ? "JAY-JAY OKOCHA ELASTICO! Defender sent for suya — speed burst!"
                    : "Okocha stepover skill burst! Driving down the pitch!");
                return true;
            }

            m.PlayerHasBall = false;
            m.BallCarrier = null;
            m.PlayerControlLock = 0.35f;
            m.ChargingShot = false;
            m.ShotPowerCharge = 0f;

            Vector3 aim = new Vector3(m.PitchCenter.x + 20.5f, m.PitchSurfaceY, m.PitchCenter.z + Mathf.Clamp(Player.transform.forward.z * 1.8f, -1.8f, 1.8f)) - m.BallPos;
            aim.y = 0f;
            m.BallVel = aim.normalized * 14.8f + Vector3.up * 7.2f;
            Player.SwingWeapon();
            UI.SoundSystem.I?.PlayKick(0.55f);
            SetFootballCommentary("Audacious Okocha panenka / lofted chip over the keeper!");
            return true;
        }

        public bool FootballTackle()
        {
            var m = ActiveFootball;
            if (m == null || Player == null) return false;

            Player.Teleport(Player.transform.position + Player.transform.forward * 1.15f, Player.transform.eulerAngles.y);
            Player.SwingWeapon();

            float distToBall = Vector3.Distance(Flat(Player.transform.position), Flat(m.BallPos));
            if (distToBall <= 3.3f)
            {
                if (m.BallCarrier != null) m.BallCarrier.ActionCooldown = 1.15f;
                m.BallCarrier = null;
                m.PlayerHasBall = true;
                m.BallVel = Vector3.zero;
                UI.SoundSystem.I?.PlayKick(0.5f);
                SetFootballCommentary("Clean sliding tackle! You won possession back!");
                return true;
            }
            return false;
        }

        public void ScorePlayerGoal(string scorer = "Captain #10")
        {
            var m = ActiveFootball;
            if (m == null) return;
            OnFootballGoalScored(m, true, scorer);
        }

        void OnFootballGoalScored(LiveFootballMatch m, bool homeScored, string scorerName)
        {
            if (homeScored) m.HomeScore++;
            else m.AwayScore++;

            m.GoalBannerTimer = 2.4f;
            m.GoalBannerText = homeScored
                ? $"GOAL! {scorerName.ToUpperInvariant()} SCORES! ({m.Event.HomeTag} {m.HomeScore} - {m.AwayScore} {m.Event.AwayTag})"
                : $"GOAL CONCEDED · {CurrentOpponentClub.ToUpperInvariant()} ({m.Event.HomeTag} {m.HomeScore} - {m.AwayScore} {m.Event.AwayTag})";
            SetFootballCommentary(homeScored
                ? $"GOOOAAALLL! {scorerName} smashes it into the net! Crowd goes wild!"
                : $"{CurrentOpponentClub} equalizes on the counter-attack!");

            UI.SoundSystem.I?.PlayWhistle();
            if (homeScored) UI.SoundSystem.I?.PlayCrowdCheer();
            State.Toast(m.GoalBannerText);

            ResetKickoffPositions(m, !homeScored);
        }

        void SetFootballCommentary(string line)
        {
            if (ActiveFootball == null) return;
            ActiveFootball.Commentary = line;
            ActiveFootball.CommentaryTimer = 4.5f;
        }

        void UpdateFootball(float dt)
        {
            var m = ActiveFootball;
            if (m == null) return;

            if (m.GoalBannerTimer > 0f) m.GoalBannerTimer = Mathf.Max(0f, m.GoalBannerTimer - dt);
            if (m.PlayerControlLock > 0f) m.PlayerControlLock = Mathf.Max(0f, m.PlayerControlLock - dt);
            if (m.CommentaryTimer > 0f) m.CommentaryTimer = Mathf.Max(0f, m.CommentaryTimer - dt);
            if (m.GiveAndGoTimer > 0f) m.GiveAndGoTimer = Mathf.Max(0f, m.GiveAndGoTimer - dt);
            if (m.SkillBoostTimer > 0f) m.SkillBoostTimer = Mathf.Max(0f, m.SkillBoostTimer - dt);
            if (m.LastShotQualityTimer > 0f) m.LastShotQualityTimer = Mathf.Max(0f, m.LastShotQualityTimer - dt);
            m.BestReceiver = m.PlayerHasBall ? FindBestPassReceiver(m) : null;

            if (Player != null)
            {
                var pPos = Player.transform.position;
                float clampedX = Mathf.Clamp(pPos.x, m.PitchCenter.x - 21.6f, m.PitchCenter.x + 21.6f);
                float clampedZ = Mathf.Clamp(pPos.z, m.PitchCenter.z - 15.2f, m.PitchCenter.z + 15.2f);
                if (Mathf.Abs(clampedX - pPos.x) > 0.01f || Mathf.Abs(clampedZ - pPos.z) > 0.01f)
                    Player.Teleport(new Vector3(clampedX, m.PitchSurfaceY + 0.05f, clampedZ), Player.transform.eulerAngles.y);
            }

            float prevClock = m.MatchClock;
            m.MatchClock += dt * m.MatchSpeed;
            if (prevClock < 45f && m.MatchClock >= 45f)
            {
                UI.SoundSystem.I?.PlayWhistle();
                SetFootballCommentary("HALF-TIME WHISTLE! Second half underway!");
            }

            if (m.MatchClock >= 90f)
            {
                if (m.HomeScore == m.AwayScore && m.MatchClock < 105f)
                {
                    if (prevClock < 90f) SetFootballCommentary("90' TIED! Extra-Time Golden Goal — next goal wins!");
                }
                else
                {
                    bool won = m.HomeScore >= m.AwayScore;
                    CompleteLiveFootballRound(won);
                    return;
                }
            }

            if (m.PlayerHasBall && Player != null)
            {
                m.BallCarrier = null;
                float lead = Player.IsRunning ? 0.95f : 0.72f;
                Vector3 desired = Player.transform.position + Player.transform.forward * lead;
                desired.y = m.PitchSurfaceY + 0.23f;
                m.BallPos = Vector3.Lerp(m.BallPos, desired, 1f - Mathf.Exp(-18f * dt));
                m.BallVel = Player.transform.forward * (Player.IsRunning ? 8.2f : 4.5f);
                if (m.BallMesh != null && Player.Speed01 > 0.05f)
                    m.BallMesh.Rotate(Vector3.right, Player.Speed01 * 520f * dt, Space.Self);
            }
            else if (m.BallCarrier != null && m.BallCarrier.Root != null)
            {
                var carrier = m.BallCarrier;
                Vector3 desired = carrier.Root.position + carrier.Root.forward * 0.75f;
                desired.y = m.PitchSurfaceY + 0.23f;
                m.BallPos = Vector3.Lerp(m.BallPos, desired, 1f - Mathf.Exp(-16f * dt));
                m.BallVel = carrier.Root.forward * 5.5f;
                if (m.BallMesh != null) m.BallMesh.Rotate(Vector3.right, 380f * dt, Space.Self);
            }
            else
            {
                m.BallPos += m.BallVel * dt;
                if (m.BallPos.y > m.PitchSurfaceY + 0.23f)
                {
                    m.BallVel.y -= 18.5f * dt;
                    Vector3 side = Vector3.Cross(Vector3.up, m.BallVel.normalized);
                    m.BallVel += side * (m.BallSpinYaw * 0.008f * dt);
                }
                if (m.BallPos.y <= m.PitchSurfaceY + 0.23f)
                {
                    m.BallPos.y = m.PitchSurfaceY + 0.23f;
                    if (m.BallVel.y < -1.8f) m.BallVel.y = -m.BallVel.y * 0.48f;
                    else m.BallVel.y = 0f;
                    m.BallVel.x = Mathf.MoveTowards(m.BallVel.x, 0f, 5.2f * dt);
                    m.BallVel.z = Mathf.MoveTowards(m.BallVel.z, 0f, 5.2f * dt);
                }
                if (m.BallMesh != null && m.BallVel.sqrMagnitude > 0.2f)
                    m.BallMesh.Rotate(Vector3.right, m.BallVel.magnitude * 65f * dt, Space.Self);

                float relX = m.BallPos.x - m.PitchCenter.x;
                float relZ = m.BallPos.z - m.PitchCenter.z;
                float relY = m.BallPos.y - m.PitchSurfaceY;

                // Woodwork / Crossbar / Post clang physics
                if (Mathf.Abs(relX) >= 19.9f && Mathf.Abs(relX) <= 20.6f)
                {
                    bool hitPost = Mathf.Abs(Mathf.Abs(relZ) - 2.92f) < 0.22f && relY <= 2.65f;
                    bool hitBar = Mathf.Abs(relZ) <= 3.0f && Mathf.Abs(relY - 2.54f) < 0.18f;
                    if (hitPost || hitBar)
                    {
                        m.BallVel.x = -m.BallVel.x * 0.65f;
                        m.BallVel.z += Random.Range(-3.5f, 3.5f);
                        UI.SoundSystem.I?.PlayKick(0.75f);
                        SetFootballCommentary(hitBar ? "OFF THE CROSSBAR! So close!" : "OFF THE GOALPOST! Woodwork denies the goal!");
                    }
                }

                if (relX >= 20.1f && Mathf.Abs(relZ) <= 2.90f && relY <= 2.52f)
                {
                    OnFootballGoalScored(m, true, "Captain #10");
                    return;
                }
                if (relX <= -20.1f && Mathf.Abs(relZ) <= 2.90f && relY <= 2.52f)
                {
                    OnFootballGoalScored(m, false, CurrentOpponentClub);
                    return;
                }

                if (Mathf.Abs(relZ) > 14.0f)
                {
                    m.BallPos.z = m.PitchCenter.z + Mathf.Sign(relZ) * 13.9f;
                    m.BallVel.z = -m.BallVel.z * 0.6f;
                }
                if (Mathf.Abs(relX) > 20.5f)
                {
                    m.BallPos.x = m.PitchCenter.x + Mathf.Sign(relX) * 20.3f;
                    m.BallVel.x = -m.BallVel.x * 0.55f;
                }

                if (Player != null && m.PlayerControlLock <= 0f)
                {
                    float dPlayer = Vector3.Distance(Flat(Player.transform.position), Flat(m.BallPos));
                    if (dPlayer < 1.55f && m.BallPos.y - m.PitchSurfaceY < 1.5f)
                    {
                        m.PlayerHasBall = true;
                        m.BallCarrier = null;
                        SetFootballCommentary("You control the ball! [E] Pass · [F / Click] Shoot · [G] Chip!");
                    }
                }
            }

            UpdateFootballAi(m, dt);
            SyncBallTransform(m);
        }

        void UpdateFootballAi(LiveFootballMatch m, float dt)
        {
            var c = m.PitchCenter;
            float y = m.PitchSurfaceY;
            var cam = Camera != null ? Camera.transform : null;

            foreach (var p in m.Players)
            {
                if (p.Root == null) continue;
                if (p.ActionCooldown > 0f) p.ActionCooldown = Mathf.Max(0f, p.ActionCooldown - dt);

                if (p.MarkerText != null && cam != null)
                {
                    p.MarkerText.transform.rotation = Quaternion.LookRotation(p.MarkerText.transform.position - cam.position, Vector3.up);
                    bool hasBall = m.BallCarrier == p;
                    p.MarkerText.text = hasBall ? $"▼ ★ #{p.Number} {p.Name}" : $"▼ #{p.Number} {p.Name}";
                }

                var pos = p.Root.position;

                if (p.IsKeeper)
                {
                    float gkX = c.x + (p.IsHome ? -19.5f : 19.5f);
                    float targetZ = Mathf.Clamp(m.BallPos.z, c.z - 2.35f, c.z + 2.35f);
                    Vector3 gkTarget = new Vector3(gkX, y, targetZ);
                    p.Root.position = Vector3.MoveTowards(pos, gkTarget, 6.2f * dt);
                    Vector3 lookBall = Flat(m.BallPos) - Flat(p.Root.position);
                    if (lookBall.sqrMagnitude > 0.01f) p.Root.rotation = Quaternion.LookRotation(lookBall.normalized, Vector3.up);
                    p.Rig?.SetState((gkTarget - pos).sqrMagnitude > 0.01f ? "walk" : "idle");

                    if (!m.PlayerHasBall && m.BallCarrier == null && p.ActionCooldown <= 0f)
                    {
                        float dBall = Vector3.Distance(Flat(p.Root.position), Flat(m.BallPos));
                        float ballSpeed = m.BallVel.magnitude;
                        float saveRadius = p.IsHome ? 1.85f : (ballSpeed > 21f ? 0.92f : 1.45f);
                        if (dBall < saveRadius && m.BallPos.y - y < 2.2f)
                        {
                            p.ActionCooldown = 1.0f;
                            float clearDirX = p.IsHome ? 1f : -1f;
                            m.BallVel = new Vector3(clearDirX * 15f, 3.8f, Random.Range(-5f, 5f));
                            UI.SoundSystem.I?.PlayKick(0.5f);
                            SetFootballCommentary($"{p.Name} makes a diving parry!");
                        }
                    }
                    continue;
                }

                if (m.BallCarrier == p)
                {
                    float goalDirX = p.IsHome ? 1f : -1f;
                    Vector3 attackGoal = new Vector3(c.x + goalDirX * 20.5f, y, c.z);
                    Vector3 toGoal = (attackGoal - pos);
                    toGoal.y = 0f;
                    float distGoal = toGoal.magnitude;

                    p.Root.position = Vector3.MoveTowards(pos, attackGoal, (p.IsHome ? 6.4f : 5.6f) * dt);
                    if (toGoal.sqrMagnitude > 0.01f)
                        p.Root.rotation = Quaternion.Slerp(p.Root.rotation, Quaternion.LookRotation(toGoal.normalized, Vector3.up), 10f * dt);
                    p.Rig?.SetState("run", 1.0f);

                    if (p.IsHome && p.ActionCooldown <= 0f)
                    {
                        if (distGoal < 11.5f)
                        {
                            m.BallCarrier = null;
                            p.ActionCooldown = 1.2f;
                            Vector3 shotAim = new Vector3(c.x + 20.6f, y + 1.0f, c.z + Random.Range(-1.8f, 1.8f)) - m.BallPos;
                            m.BallVel = shotAim.normalized * 22f + Vector3.up * 2.5f;
                            UI.SoundSystem.I?.PlayKick();
                            SetFootballCommentary($"{p.Name} fires a strike at goal!");
                        }
                        else if (Player != null && Player.transform.position.x > pos.x + 1.5f && Random.value < 1.8f * dt)
                        {
                            m.BallCarrier = null;
                            p.ActionCooldown = 1.0f;
                            Vector3 toP = (Player.transform.position + Player.transform.forward * 1.2f - m.BallPos);
                            toP.y = 0f;
                            m.BallVel = toP.normalized * 16.5f + Vector3.up * 0.9f;
                            UI.SoundSystem.I?.PlayKick(0.45f);
                            SetFootballCommentary($"{p.Name} passes the ball to your feet!");
                        }
                    }
                    else if (!p.IsHome && p.ActionCooldown <= 0f)
                    {
                        if (distGoal < 11.0f)
                        {
                            m.BallCarrier = null;
                            p.ActionCooldown = 1.4f;
                            Vector3 shotAim = new Vector3(c.x - 20.6f, y + 0.9f, c.z + Random.Range(-2.2f, 2.2f)) - m.BallPos;
                            m.BallVel = shotAim.normalized * 17.5f + Vector3.up * 2.2f;
                            UI.SoundSystem.I?.PlayKick();
                            SetFootballCommentary($"{p.Name} shoots towards your goal!");
                        }
                    }
                    continue;
                }

                float dToBall = Vector3.Distance(Flat(pos), Flat(m.BallPos));
                bool teamHasBall = p.IsHome
                    ? (m.PlayerHasBall || (m.BallCarrier != null && m.BallCarrier.IsHome))
                    : (m.BallCarrier != null && !m.BallCarrier.IsHome);

                // Find if this player is the closest defender to the ball so only 1 defender presses while others hold shape
                bool isClosestPresser = false;
                if (!teamHasBall && dToBall < 11.5f)
                {
                    isClosestPresser = true;
                    foreach (var mate in m.Players)
                    {
                        if (mate == p || mate.IsHome != p.IsHome || mate.IsKeeper || mate.Root == null) continue;
                        if (Vector3.Distance(Flat(mate.Root.position), Flat(m.BallPos)) < dToBall - 0.4f)
                        {
                            isClosestPresser = false;
                            break;
                        }
                    }
                }

                Vector3 desiredPos;
                if (isClosestPresser)
                {
                    desiredPos = new Vector3(m.BallPos.x, y, m.BallPos.z);
                }
                else if (p.IsHome && m.GiveAndGoTimer > 0f && m.GiveAndGoRunner == p)
                {
                    // 1-2 Give-and-Go overlapping sprint toward opponent box!
                    desiredPos = new Vector3(Mathf.Min(c.x + 16.5f, pos.x + 9.0f), y, Mathf.Clamp(p.FormationOffset.z * 0.65f + c.z, c.z - 10f, c.z + 10f));
                }
                else
                {
                    float tacticBiasX = p.IsHome ? (m.TacticMode == 1 ? 4.5f : m.TacticMode == 2 ? -3.5f : 0.8f) : 0f;
                    float attackRun = (p.IsHome && teamHasBall && p.Role == "ST") ? 5.2f : (p.IsHome && teamHasBall && p.Role == "CM") ? 2.8f : 0f;
                    float ballShiftX = Mathf.Clamp((m.BallPos.x - c.x) * 0.52f + tacticBiasX + attackRun, -9.5f, 11.5f);
                    float laneZ = c.z + p.FormationOffset.z;
                    if (p.IsHome && teamHasBall && Player != null && Mathf.Abs(Player.transform.position.z - laneZ) < 2.2f)
                        laneZ += p.FormationOffset.z >= 0f ? 2.8f : -2.8f; // spread wide to open passing lane
                    desiredPos = new Vector3(
                        Mathf.Clamp(c.x + p.FormationOffset.x + ballShiftX, c.x - 18.5f, c.x + 18.5f),
                        y,
                        Mathf.Clamp(laneZ, c.z - 12.5f, c.z + 12.5f));
                }

                Vector3 moveVec = desiredPos - pos;
                moveVec.y = 0f;
                float moveSpeed = isClosestPresser ? (p.IsHome ? 6.2f : 5.4f) : (p.IsHome && teamHasBall && p.Role == "ST" ? 5.8f : 4.8f);
                if (moveVec.sqrMagnitude > 0.15f)
                {
                    p.Root.position = Vector3.MoveTowards(pos, desiredPos, moveSpeed * dt);
                    p.Root.rotation = Quaternion.Slerp(p.Root.rotation, Quaternion.LookRotation(moveVec.normalized, Vector3.up), 8f * dt);
                    p.Rig?.SetState(moveSpeed > 5.0f ? "run" : "walk", 0.95f);
                }
                else p.Rig?.SetState("idle");

                if (p.ActionCooldown <= 0f && dToBall < 1.25f && m.BallPos.y - y < 1.4f)
                {
                    if (m.BallCarrier == null && !m.PlayerHasBall)
                    {
                        m.BallCarrier = p;
                        p.ActionCooldown = 0.5f;
                    }
                    else if (!p.IsHome && m.PlayerHasBall && m.PlayerControlLock <= 0f && dToBall < 1.05f && Random.value < 1.1f * dt)
                    {
                        m.PlayerHasBall = false;
                        m.BallCarrier = p;
                        p.ActionCooldown = 0.8f;
                        SetFootballCommentary($"{p.Name} dispossessed you! Press [F / Click] to tackle back!");
                    }
                }
            }
        }

        public void CompleteLiveFootballRound(bool won)
        {
            var m = ActiveFootball;
            if (m == null) return;
            UI.SoundSystem.I?.PlayWhistle();

            if (won && m.CurrentRound < m.Event.Rounds)
            {
                m.CurrentRound++;
                m.HomeScore = 0;
                m.AwayScore = 0;
                m.MatchClock = 0f;
                SpawnFootballSquads(m);
                ResetKickoffPositions(m, true);
                UI.SoundSystem.I?.PlayCrowdCheer();
                State.Toast($"ROUND WON! Advancing to {CurrentTournamentStageLabel} vs {CurrentOpponentClub}!");
                SetFootballCommentary($"{CurrentTournamentStageLabel} underway vs {CurrentOpponentClub}!");
                return;
            }

            EndLiveFootballMatch(won, false);
        }

        public void EndLiveFootballMatch(bool won, bool forfeited)
        {
            var m = ActiveFootball;
            if (m == null) return;

            var ev = m.Event;
            int finalHome = m.HomeScore;
            int finalAway = m.AwayScore;
            string savedWeapon = m.SavedWeapon;

            if (m.MatchPropsRoot != null) Destroy(m.MatchPropsRoot.gameObject);
            ActiveFootball = null;

            if (Player != null) Player.SetWeapon(savedWeapon);

            if (forfeited)
            {
                State.Toast("Left the football pitch");
                RefreshNav();
                return;
            }

            var st = State.S;
            st.stamina = Mathf.Max(10f, st.stamina - 25f);
            Hours(1.5f);
            State.GainSkill("fitness", won ? 6f : 3f);
            State.AddRep("social", 3);

            if (won)
            {
                st.footballWins++;
                st.footballTier = Mathf.Max(st.footballTier, Mathf.Min(ev.TierIndex + 1, 2));
                State.Earn(ev.Prize);
                State.AddXp(ev.Xp);
                State.AddRep("street", 6);
                State.AddRep("public", 5);
                UI.SoundSystem.I?.PlayCrowdCheer();
                State.Toast($"FULL TIME {finalHome}-{finalAway}! WON {ev.Title.ToUpperInvariant()} · +₦{ev.Prize:N0} · +{ev.Xp} XP");

                if (st.footballWins == 3)
                    State.Message("captain", "Omo, you sabi ball! Neighbourhood tournament dey open for you now.");
                if (st.footballWins >= 6 || ev.Id == "fc_champions_league")
                    State.Message("amaka", "Saw you lift the trophy under the Teslim Balogun floodlights! Iponri crews want a sponsor deal.");
            }
            else
            {
                State.Toast($"FULL TIME {finalHome}-{finalAway} · Lost to {ev.OpponentClubs[0]}. Train fitness and run it back!");
            }

            RefreshNav();
        }
    }
}
