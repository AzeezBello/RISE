using System.Text;
using UnityEngine;
using Rise.Data;

namespace Rise
{
    // The smartphone: Messages, Jobs, Bank, Property, Businesses, Character, Contacts,
    // Inventory, Crew, Account, Settings, and Map · GPS, as pages of the choice card so they work with keyboard and pad alike. P / Select opens it.
    public partial class Session
    {
        public bool PhoneOpen;

        public void OpenPhone()
        {
            PhoneOpen = true;
            var ev = Events?.ActiveHistoricEvent;
            string eraTag = ev != null ? $" · [{ev.Category.ToUpperInvariant()}] {ev.Title}" : "";
            var m = new Menu { Title = "Rise: Hustle or Die Trying", Subtitle = $"{Clock.ClockText} · {Clock.DateText} (Day {Clock.Day}) · {Data.RegionAt(Focus.position)?.name}{eraTag}", Line = $"₦{State.S.cash:N0} cash · ₦{State.S.bank:N0} bank · Level {State.S.level} {State.Title}" };
            int unread = State.Messages.Count;
            m.Options.Add(new MenuOption { Label = $"Messages ({unread})", Action = AppMessages });
            m.Options.Add(new MenuOption { Label = "Jobs", Action = AppJobs });
            m.Options.Add(new MenuOption { Label = "RiseBank", Action = AppBank });
            m.Options.Add(new MenuOption { Label = State.S.level >= Data.meta.propertyLevel ? "Property" : $"Property (Level {Data.meta.propertyLevel})", Enabled = State.S.level >= Data.meta.propertyLevel, Action = AppProperty });
            m.Options.Add(new MenuOption { Label = State.S.level >= Data.meta.propertyLevel ? "Businesses" : $"Businesses (Level {Data.meta.propertyLevel})", Enabled = State.S.level >= Data.meta.propertyLevel, Action = AppBusinesses });
            m.Options.Add(new MenuOption { Label = "Character", Action = AppCharacter });
            m.Options.Add(new MenuOption { Label = "Contacts", Action = AppContacts });
            m.Options.Add(new MenuOption { Label = "Inventory", Action = AppInventory });
            m.Options.Add(new MenuOption { Label = InCrew ? $"Crew · {State.S.crewName} [{CrewTag}]" : "Crew", Action = AppCrew });
            m.Options.Add(new MenuOption { Label = Account != null && Account.SignedIn ? $"Account · {Account.Email}" : "Account", Action = AppAccount });
            m.Options.Add(new MenuOption { Label = "Settings", Action = AppSettings });
            m.Options.Add(new MenuOption { Label = "Map · GPS", Action = () => AppMap("") });
            m.Options.Add(new MenuOption { Label = "Put the phone away", Action = () => PhoneOpen = false });
            OpenMenu(m);
        }

        void Page(string title, string subtitle, string body, params MenuOption[] options)
        {
            var m = new Menu { Title = title, Subtitle = subtitle, Line = body };
            foreach (var o in options) m.Options.Add(o);
            m.Options.Add(new MenuOption { Label = "Back", Action = OpenPhone });
            OpenMenu(m);
        }

        static string CompassDirection(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from; d.y = 0f;
            if (d.sqrMagnitude < 1f) return "HERE";
            float deg = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
            string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
            return dirs[Mathf.RoundToInt(deg / 45f) % 8];
        }

        public void AppMap(string filter = "")
        {
            string q = (filter ?? "").Trim().ToLowerInvariant();
            ResolveWaypoint(State.S.waypointId, out var currentWp);
            var origin = Focus.position;
            var curRegion = Data.RegionAt(origin);
            string turnText = Nav != null && Nav.Target != null && !string.IsNullOrEmpty(Nav.NextTurnInstruction)
                ? $" · {Nav.NextTurnSymbol} {Nav.NextTurnInstruction} ({Nav.RouteLength:0}m)"
                : "";
            var m = new Menu
            {
                Title = "Map · GPS",
                Subtitle = !string.IsNullOrEmpty(currentWp)
                    ? $"Active GPS: {currentWp}{turnText}"
                    : $"You are in {curRegion?.name ?? "Surulere"} · {Data.landmarks.Length} landmarks · {Data.busStops.Length} bus stops · {Contacts.Contacts.Count} contacts",
                Line = "Click any location on the interactive GPS map canvas or choose a destination below for turn-by-turn road navigation."
            };
            m.Fields.Add(new MenuField { Label = "Search Lagos", Value = filter ?? "" });
            m.Options.Add(new MenuOption { Label = "Search / filter list", Action = () => AppMap(m.Fields[0].Value) });
            if (!string.IsNullOrEmpty(State.S.waypointId))
                m.Options.Add(new MenuOption { Label = $"✕ Clear Active GPS ({currentWp})", Action = () => { SetWaypoint(""); AppMap(m.Fields[0].Value); } });

            // Quick-Tap Smart GPS Presets when no text filter is active
            if (string.IsNullOrEmpty(q))
            {
                if (Data.TryPlace("fuel", out var fuelPlace))
                {
                    float dFuel = Vector3.Distance(origin, fuelPlace.World());
                    m.Options.Add(new MenuOption { Label = $"⛽ Quick GPS → Nearest Filling Station ({fuelPlace.name} · {dFuel:0} m)", Action = () => { SetWaypoint("fuel"); PhoneOpen = false; } });
                }
                if (Data.TryPlace("ladipo", out var ladipoPlace))
                {
                    float dLad = Vector3.Distance(origin, ladipoPlace.World());
                    m.Options.Add(new MenuOption { Label = $"🏎️ Quick GPS → NFS Garage & Ladipo Auto Hub ({dLad:0} m)", Action = () => { SetWaypoint("ladipo"); PhoneOpen = false; } });
                }
                var nearAirport = Data.NearestLandmark("airport", origin, 99999f);
                if (nearAirport != null)
                {
                    float dAir = Vector3.Distance(origin, nearAirport.World());
                    m.Options.Add(new MenuOption { Label = $"✈ Quick GPS → Nearest Airport ({nearAirport.shortName ?? nearAirport.name} · {dAir:0} m)", Action = () => { SetWaypoint(nearAirport.id); PhoneOpen = false; } });
                }
                var nearBank = Data.NearestLandmark("bank", origin, 99999f);
                if (nearBank != null)
                {
                    float dBank = Vector3.Distance(origin, nearBank.World());
                    m.Options.Add(new MenuOption { Label = $"🏦 Quick GPS → Nearest Bank / ATM ({nearBank.shortName ?? nearBank.name} · {dBank:0} m)", Action = () => { SetWaypoint(nearBank.id); PhoneOpen = false; } });
                }
                if (State.Home != null)
                {
                    float dHome = Vector3.Distance(origin, State.Home.Door);
                    m.Options.Add(new MenuOption { Label = $"🏠 Quick GPS → My Home ({State.Home.name} · {dHome:0} m)", Action = () => { SetWaypoint(State.Home.id); PhoneOpen = false; } });
                }
                foreach (var pv in Parked)
                {
                    if (pv != null && pv.Owned && !string.IsNullOrEmpty(pv.OwnedId) && pv != Vehicle)
                    {
                        float dVeh = Vector3.Distance(origin, pv.transform.position);
                        string vid = pv.OwnedId;
                        m.Options.Add(new MenuOption { Label = $"🚗 Quick GPS → My {pv.Class.name} ({dVeh:0} m)", Action = () => { SetWaypoint("veh:" + vid); PhoneOpen = false; } });
                        break;
                    }
                }
            }

            var entries = new System.Collections.Generic.List<(string id, string label, float dist, string dir)>();
            foreach (var l in Data.landmarks)
                if (string.IsNullOrEmpty(q) || l.name.ToLowerInvariant().Contains(q) || (l.shortName != null && l.shortName.ToLowerInvariant().Contains(q)) || (l.kind != null && l.kind.ToLowerInvariant().Contains(q)))
                {
                    var w = l.World();
                    entries.Add((l.id, $"Landmark · {l.name}", Vector3.Distance(origin, w), CompassDirection(origin, w)));
                }
            foreach (var b in Data.busStops)
                if (string.IsNullOrEmpty(q) || b.name.ToLowerInvariant().Contains(q) || b.shortName.ToLowerInvariant().Contains(q))
                {
                    var w = b.World();
                    entries.Add((b.id, $"Bus Stop · {b.name}", Vector3.Distance(origin, w), CompassDirection(origin, w)));
                }
            foreach (var p in Data.properties)
                if (string.IsNullOrEmpty(q) || p.name.ToLowerInvariant().Contains(q))
                {
                    var w = p.Door;
                    entries.Add((p.id, $"Property · {p.name}", Vector3.Distance(origin, w), CompassDirection(origin, w)));
                }
            foreach (var c in Contacts.Contacts)
                if (string.IsNullOrEmpty(q) || c.data.name.ToLowerInvariant().Contains(q) || (c.data.role != null && c.data.role.ToLowerInvariant().Contains(q)))
                {
                    entries.Add((c.data.id, $"Contact · {c.data.name} ({c.data.role})", Vector3.Distance(origin, c.pos), CompassDirection(origin, c.pos)));
                }

            entries.Sort((a, b) => a.dist.CompareTo(b.dist));
            int count = Mathf.Min(12, entries.Count);
            for (int i = 0; i < count; i++)
            {
                var entry = entries[i];
                string distStr = entry.dist < 1000f ? $"{entry.dist:0} m" : $"{entry.dist / 1000f:0.1} km";
                m.Options.Add(new MenuOption { Label = $"Set GPS → {entry.label} ({distStr} {entry.dir})", Action = () => { SetWaypoint(entry.id); PhoneOpen = false; } });
            }
            m.Options.Add(new MenuOption { Label = "Back", Action = OpenPhone });
            OpenMenu(m);
        }

        void AppMessages()
        {
            var sb = new StringBuilder();
            if (Events != null && Events.ActiveHistoricEvent != null)
            {
                var ev = Events.ActiveHistoricEvent;
                sb.Append("[").Append(ev.Category.ToUpperInvariant()).Append("] ").Append(ev.Headline).Append('\n');
                sb.Append("TRENDING: ").Append(Events.DailyTrendingHeadline).Append("\n\n");
            }
            int from = Mathf.Max(0, State.Messages.Count - 6);
            for (int i = State.Messages.Count - 1; i >= from; i--) sb.Append(Data.ContactName(State.Messages[i].from)).Append(": ").Append(State.Messages[i].text).Append('\n');
            Page("Messages", $"{State.Messages.Count} messages · {Clock.DateText}", sb.Length > 0 ? sb.ToString().TrimEnd() : "No messages yet. Lagos go soon call you.",
                new MenuOption { Label = "Lagos Chronicle · Trending News & Historic Events (1999–2026)", Action = AppNews });
        }

        public void AppNews()
        {
            var ev = Events?.ActiveHistoricEvent ?? World.Events.EventForDate(Clock.Date);
            var sb = new StringBuilder();
            sb.Append($"DATE: {Clock.FullDateText} (Day {Clock.Day})\n");
            if (ev != null)
            {
                sb.Append($"ERA [{ev.Category.ToUpperInvariant()}]: {ev.Title} ({ev.StartYear}–{ev.EndYear})\n");
                sb.Append($"HEADLINE: {ev.Headline}\n");
                sb.Append($"{ev.Summary}\n");
                sb.Append($"ERA EFFECTS: Civic Pay Bonus +{(ev.PayMultiplier - 1f) * 100f:0}% · {(ev.CurfewActive ? "Curfew / Movement Advisory Active" : "Normal City Movement")}\n");
            }
            sb.Append($"DAILY TRENDING: {World.Events.TrendingHeadlineForDay(Clock.Day)}");

            var opts = new System.Collections.Generic.List<MenuOption>();
            if (ev != null && Events != null)
            {
                opts.Add(new MenuOption
                {
                    Label = $"Participate in {ev.Title}: {ev.CivicPrompt}",
                    Action = () =>
                    {
                        Events.TriggerHistoricEvent(ev.Id, Player.transform.position + Player.transform.forward * 4f, false);
                        PhoneOpen = false;
                        CloseMenu();
                    }
                });
            }
            string[] featuredEras = { "transition_1999", "election_1999", "democracy_day_1999", "gsm_revolution_2001", "cbn_consolidation_2004", "afcon_2013", "ebola_response_2014", "election_2015", "covid19_lockdown_2020", "election_2023", "lrmt_rail_era_2024" };
            foreach (var eraId in featuredEras)
            {
                var item = World.Events.FindHistoricEvent(eraId);
                if (item == null) continue;
                var targetEv = item;
                opts.Add(new MenuOption
                {
                    Label = $"[{targetEv.StartYear} {targetEv.Category.ToUpperInvariant()}] {targetEv.Title} · +₦{targetEv.CivicReward:N0}",
                    Action = () =>
                    {
                        Events?.TriggerHistoricEvent(targetEv.Id, Player.transform.position + Player.transform.forward * 4f, true);
                        PhoneOpen = false;
                        CloseMenu();
                    }
                });
            }
            Page("Lagos Chronicle · Historic Timeline", $"{Clock.DateText} · {(ev != null ? ev.Title : "Lagos News")}", sb.ToString(), opts.ToArray());
        }

        void AppJobs()
        {
            var sb = new StringBuilder();
            foreach (var j in Data.jobs)
            {
                bool ok = State.CanTake(j);
                sb.Append(ok ? "✓ " : "✗ ").Append(j.title).Append(" · ").Append(j.where).Append(" · ₦").Append(State.JobPay(j).ToString("N0")).Append(" · ").Append(j.dur).Append("h");
                if (!ok) sb.Append(!string.IsNullOrEmpty(j.needCourse) && !State.HasCourse(j.needCourse) ? $" (needs {j.needCourse} certificate)" : $" (needs {j.skill} {j.min})");
                sb.Append('\n');
            }
            Page("Jobs", "Talk to the contact at the place to work a shift", sb.ToString().TrimEnd(),
                new MenuOption { Label = $"Tactical Action Ops & Armory ({State.S.ammo} ammo)", Action = AppActionOps },
                new MenuOption { Label = "Indoor Activities & Indoor Missions (Raid / Vault / Boxing)", Action = OpenIndoorMenu },
                new MenuOption { Label = "Lagos Clubbing & Social Nightlife (VIP Lounge / DJ / Owambe)", Action = OpenNightlifeHubMenu },
                new MenuOption { Label = "Live 3D Football & Tournaments (FC 26 / DLS Mode)", Action = () => OpenFootballMenu() },
                new MenuOption { Label = "NFS Street Racing Underground (Circuit / Drift / Speedtrap)", Action = () => OpenRaceMenu() },
                new MenuOption { Label = "NFS Underground Tuning & Mod Garage (Performance · NOS · Aero · Paint)", Action = () => { PhoneOpen = false; OpenNfsGarageMenu(Vehicle); } },
                new MenuOption { Label = $"Local Domestic Flights Departure Board ({StateAirports.Length} States with Airports)", Action = () => { PhoneOpen = false; OpenStateFlightsMenu(); } });
        }

        void AppBank()
        {
            var st = State.S;
            Page("RiseBank", $"₦{st.cash:N0} cash · ₦{st.bank:N0} in the bank", "Transfers are instant. Businesses and tenants pay into the bank; fines come from cash first.",
                new MenuOption { Label = "Deposit ₦10,000", Enabled = st.cash >= 10000, Action = () => { State.Deposit(10000); AppBank(); } },
                new MenuOption { Label = "Deposit all cash", Enabled = st.cash > 0, Action = () => { State.Deposit(st.cash); AppBank(); } },
                new MenuOption { Label = "Withdraw ₦10,000", Enabled = st.bank >= 10000, Action = () => { State.Withdraw(10000); AppBank(); } },
                new MenuOption { Label = "Withdraw ₦50,000", Enabled = st.bank >= 50000, Action = () => { State.Withdraw(50000); AppBank(); } });
        }

        void AppProperty()
        {
            var st = State.S; var sb = new StringBuilder();
            foreach (var p in Data.properties)
            {
                string status = st.home == p.id ? "HOME" : st.props.Contains(p.id) ? "owned" : st.rentedId == p.id ? $"rented till day {st.rentedUntil}" : p.buy > 0 ? $"rent ₦{p.rent:N0} · buy ₦{p.buy:N0}" : $"rent ₦{p.rent:N0}";
                sb.Append(p.name).Append(" — ").Append(status).Append('\n');
            }
            Page("Property", st.home != "" ? $"Home: {State.Home?.name}" : "No home yet — see Agent Kunle at any gate", sb.ToString().TrimEnd());
        }

        void AppBusinesses()
        {
            var sb = new StringBuilder();
            var opts = new System.Collections.Generic.List<MenuOption>();
            foreach (var b in Data.businesses)
            {
                var biz = b;
                var o = State.Biz(b.id);
                sb.Append(b.name).Append(" — ").Append(o == null ? $"for sale ₦{State.BizPrice(b):N0} · ₦{b.income:N0}/cycle" : $"yours · stock {o.stock:0}% · prices {o.price} · staff {(o.staff.Count > 0 ? string.Join(", ", o.staff) : "none")} · ₦{State.BizIncome(b):N0}/cycle").Append('\n');
                if (o != null)
                    opts.Add(new MenuOption { Label = $"Manage {b.name} (stock {o.stock:0}%, prices {o.price})", Action = () => OpenBusinessMenu(biz) });
                else
                    opts.Add(new MenuOption { Label = $"Buy {b.name} · ₦{State.BizPrice(b):N0}", Enabled = State.S.level >= Data.meta.propertyLevel, Action = () => { State.BuyBusiness(biz); AppBusinesses(); } });
            }
            Page("Businesses", $"{State.S.biz.Count} owned · next pay in {Mathf.Max(0f, State.S.payIn):0} s{(State.RivalOpen ? $" · {Data.rivalName} opened nearby" : "")}", sb.ToString().TrimEnd(), opts.ToArray());
        }

        void AppCharacter()
        {
            var st = State.S; var sk = st.skills;
            string Pick(string[] list, int i, string fallback) => list != null && list.Length > 0 ? list[Mathf.Clamp(i, 0, list.Length - 1)] : fallback;
            string body = $"Skills  driving {sk.driving:0}  business {sk.business:0}  charisma {sk.charisma:0}  fitness {sk.fitness:0}  strength {sk.strength:0}\n" +
                          $"Reputation  public {st.rep.publicRep}  business {st.rep.business}  street {st.rep.street}  social {st.rep.social}\n" +
                          $"Certificates  {(st.courses.Count > 0 ? string.Join(", ", st.courses) : "none")}{(st.collegeGraduated ? ", Yaba Tech diploma" : st.collegeSemester >= 0 ? $", Yaba Tech semester {st.collegeSemester}/{Data.school.semesters}" : "")}\n" +
                          $"Look  {Pick(Data.outfits, st.lookOutfit, "Ankara shirt")} in {FabricName()} · {Pick(Data.bodyTypes, st.lookBody, "Average")} · hair {Pick(Data.hairStyles, st.lookHairStyle, "Short")} · {Pick(Data.facialHair, st.lookFacialHair, "None")} · {Pick(Data.accessories, st.lookAccessory, "None")}\n" +
                          $"Skin {st.lookSkin + 1}/{Mathf.Max(1, Data.skins.Length)} · trousers {st.lookPants + 1}/{Mathf.Max(1, Data.pants.Length)} · hair colour {Pick(Data.hairColors, st.lookHair, "")} · face {Pick(Data.faces, st.lookFace, "Oval")} (face shape is stored; the rig has one face)";
            void Apply() { Player.ApplyLook(State.Look); AppCharacter(); }
            Page("Character", $"Level {st.level} {State.Title} · {st.xp}/100 XP", body,
                new MenuOption { Label = "Next outfit", Action = () => { st.lookOutfit = (st.lookOutfit + 1) % Mathf.Max(1, Data.outfits.Length); Apply(); } },
                new MenuOption { Label = "Next fabric", Action = () => { NextFabric(); AppCharacter(); } },
                new MenuOption { Label = "Next body type", Action = () => { st.lookBody = (st.lookBody + 1) % Mathf.Max(1, Data.bodyTypes.Length); Apply(); } },
                new MenuOption { Label = "Next hair style / colour", Action = () => { st.lookHairStyle = (st.lookHairStyle + 1) % Mathf.Max(1, Data.hairStyles.Length); if (st.lookHairStyle == 0) st.lookHair = (st.lookHair + 1) % Mathf.Max(1, Data.hairColors.Length); Apply(); } },
                new MenuOption { Label = "Next facial hair", Action = () => { st.lookFacialHair = (st.lookFacialHair + 1) % Mathf.Max(1, Data.facialHair.Length); Apply(); } },
                new MenuOption { Label = "Next accessory", Action = () => { st.lookAccessory = (st.lookAccessory + 1) % Mathf.Max(1, Data.accessories.Length); st.lookCap = st.lookAccessory == 1 ? 1 : st.lookAccessory == 2 ? 2 : 0; Apply(); } },
                new MenuOption { Label = "Next skin tone / trousers", Action = () => { st.lookSkin = (st.lookSkin + 1) % Mathf.Max(1, Data.skins.Length); if (st.lookSkin == 0) st.lookPants = (st.lookPants + 1) % Mathf.Max(1, Data.pants.Length); Apply(); } });
        }

        void AppContacts()
        {
            var sb = new StringBuilder();
            foreach (var c in Contacts.Contacts) sb.Append(c.data.name).Append(" · ").Append(c.data.role).Append(" · ").Append(IsFamily(c) ? $"bond {FamilyLabel(c.data.id)} · {FamilyActivity(c.data.id)}" : State.RelTier(c.data.id)).Append(Hubs.ContainsKey(c.data.id) ? " · local runs" : "").Append(Data.JobsAt(c.data.at).Count > 0 ? " · jobs" : "").Append('\n');
            Page("Contacts", $"{Contacts.Contacts.Count} people who know you", sb.ToString().TrimEnd());
        }

        void AppInventory()
        {
            var st = State.S; var sb = new StringBuilder();
            sb.Append(st.items.Count > 0 ? "Carrying: " + string.Join(", ", st.items.ConvertAll(Data.ItemName)) : "Nothing in your hands.").Append('\n');
            sb.Append($"Equipped weapon: {EquippedWeaponName}{(WeaponPower() > 0 ? $" (power +{WeaponPower()})" : "")} · Ammo: {st.ammo} rounds").Append('\n');
            sb.Append("Vehicles: ").Append(st.vehicles.Count > 0 ? string.Join(", ", st.vehicles.ConvertAll(v => Data.VehicleClass(v.type)?.name ?? v.type)) : "none").Append('\n');
            sb.Append($"Fuel {st.fuel:0}% · stamina {st.stamina:0}% · health {st.health:0}%");
            var opts = new System.Collections.Generic.List<MenuOption>();
            foreach (var w in FightWeapons)
            {
                if (!State.Has(w.Id)) continue;
                var wSpec = w; bool eq = st.equippedWeapon == w.Id;
                opts.Add(new MenuOption { Label = eq ? $"{w.Name} · in hand{(w.IsGun ? $" ({st.ammo} ammo)" : "")}" : $"Equip {w.Name} (power +{w.Power}{(w.IsGun ? ", firearm" : "")})", Enabled = !eq, Action = () => { EquipWeapon(wSpec.Id); AppInventory(); } });
            }
            if (!string.IsNullOrEmpty(st.equippedWeapon))
                opts.Add(new MenuOption { Label = "Holster weapon (hide from hand)", Action = () => { EquipWeapon(""); AppInventory(); } });
            if (st.fuel < 99f)
                opts.Add(new MenuOption { Label = $"⛽ Emergency Roadside Jerrycan Refuel (100% Fuel) · ₦4,000 (now {st.fuel:0}%)", Action = () => { EmergencyRefuel(4000); AppInventory(); } });
            opts.Add(new MenuOption { Label = "🏎️ NFS Underground Tuning & Mod Garage (Performance · NOS · Aero · Paint)", Action = () => { PhoneOpen = false; OpenNfsGarageMenu(Vehicle); } });
            opts.Add(new MenuOption { Label = $"Tactical Action Ops & Armory ({st.ammo} ammo)", Action = AppActionOps });
            Page("Inventory", "What you carry and own", sb.ToString(), opts.ToArray());
        }

        public void AppActionOps()
        {
            var st = State.S;
            var opts = new System.Collections.Generic.List<MenuOption>();
            opts.Add(new MenuOption
            {
                Label = st.armor >= 100f ? "Kevlar Tactical Vest · 100% Armor Equipped" : $"Buy Kevlar Tactical Vest (+100 Armor) · ₦15,000 (now {st.armor:0}%)",
                Enabled = st.armor < 100f,
                Action = () => { BuyTacticalArmor(15000); AppActionOps(); }
            });
            foreach (var gId in new[] { "pistol", "smg", "shotgun", "rifle", "sniper", "lmg" })
            {
                var spec = FindWeaponSpec(gId);
                if (!spec.HasValue) continue;
                var w = spec.Value; string id = w.Id;
                bool owned = State.Has(id); bool eq = st.equippedWeapon == id;
                opts.Add(new MenuOption
                {
                    Label = owned ? $"Firearm: {w.Name} · {(eq ? $"equipped ({st.ammo} ammo)" : "equip")}" : $"Buy {w.Name} · ₦{w.Price:N0} ({w.Desc})",
                    Enabled = !eq,
                    Action = () => { BuyFightWeapon(id); AppActionOps(); }
                });
            }
            opts.Add(new MenuOption
            {
                Label = $"Buy Ammo Box (30 rounds) · ₦6,000 (carrying {st.ammo} rounds)",
                Action = () => { BuyAmmo(30, 6000); AppActionOps(); }
            });
            foreach (var op in ActionOps)
            {
                var curOp = op;
                string tag = curOp.Branch == "army" ? $"[81 Div · {curOp.Waves} Waves]" : curOp.Branch == "navy" ? $"[Navy SBS · {curOp.Waves} Waves]" : "[Action Op]";
                opts.Add(new MenuOption
                {
                    Label = $"{tag} {curOp.Title} · ₦{curOp.Reward:N0}{(HasGun() ? "" : " [Need Firearm]")}",
                    Enabled = !HasTask,
                    Action = () => { if (StartActionOp(curOp.Id)) PhoneOpen = false; }
                });
            }
            Page("Tactical Ops & Armory", $"{EquippedWeaponName} · {st.ammo} rounds · Armor {st.armor:0}%",
                "Street Shootouts plus Call of Duty–style Nigerian Army (81 Div) & Nigerian Navy (SBS) multi-wave Spec-Ops with allied fireteams and HVT commanders.",
                opts.ToArray());
        }

        void AppCrew()
        {
            var st = State.S;
            if (!InCrew)
            {
                var m = new Menu { Title = "Crew", Subtitle = "No crew yet", Line = "Form a crew and share the code, or join one with a code. Crews share a treasury, an HQ and GPS pins, and race together." };
                m.Fields.Add(new MenuField { Label = "Name / code" }); m.Fields.Add(new MenuField { Label = "Tag (2–4)" });
                m.Options.Add(new MenuOption { Label = "Form a crew with this name and tag", Action = () => { CreateCrew(m.Fields[0].Value, m.Fields[1].Value); AppCrew(); } });
                m.Options.Add(new MenuOption { Label = "Join the crew with this code", Action = () => { JoinCrew(m.Fields[0].Value); AppCrew(); } });
                m.Options.Add(new MenuOption { Label = "Back", Action = OpenPhone });
                OpenMenu(m);
                return;
            }
            var hq = string.IsNullOrEmpty(st.crewHq) ? null : Data.Property(st.crewHq);
            string body = $"Code {st.crewCode} · treasury ₦{st.crewTreasury:N0} · HQ {(hq != null ? hq.name : "none")}\n" +
                          $"Pins shared: {Pins.Count} · co-op wins {CoopWins} (getaways {CoopGetawayWins}, convoys {CoopConvoyWins})" +
                          $"{(CoopInvitePending ? $"\n{CoopPartnerName} invites you to a co-op race" : "")}" +
                          $"{(CoopOpInviteKind == "getaway" ? $"\n{CoopPartnerName} invites you to a Co-op Getaway" : "")}" +
                          $"{(CoopOpInviteKind == "convoy" ? $"\n{CoopPartnerName} invites you to a Co-op Convoy" : "")}" +
                          $"{(CoopRace ? "\nCo-op race in progress" : "")}" +
                          $"{(CoopGetaway ? $"\nCo-op Getaway active · {Mathf.Max(0f, 8f - CoopGetawayClear):0}s clear needed" : "")}" +
                          $"{(CoopConvoy ? $"\nCo-op Convoy active · checkpoint {CoopConvoyIndex + 1}/3" : "")}";
            Page($"{st.crewName} [{st.crewTag}]", "Your crew", body,
                new MenuOption { Label = "Deposit ₦10,000 to the treasury", Enabled = st.cash + st.bank >= 10000, Action = () => { CrewDeposit(10000); AppCrew(); } },
                new MenuOption { Label = "Withdraw ₦10,000", Enabled = st.crewTreasury >= 10000, Action = () => { CrewWithdraw(10000); AppCrew(); } },
                new MenuOption { Label = hq != null ? $"HQ: {hq.name}" : "Make my home the HQ", Enabled = hq == null && State.Home != null && State.Owns(State.Home), Action = () => { SetCrewHq(State.Home); AppCrew(); } },
                new MenuOption { Label = "Pin my position for the crew", Action = () => { SharePin(Data.RegionAt(Focus.position)?.name ?? "here"); AppCrew(); } },
                new MenuOption { Label = CoopInvitePending ? $"Accept {CoopPartnerName}'s co-op race" : "Invite the crew to a co-op race", Action = () => { if (CoopInvitePending) AcceptCoopRace(); else InviteCoopRace(); } },
                new MenuOption { Label = CoopOpInviteKind == "getaway" ? $"Accept {CoopPartnerName}'s Co-op Getaway" : "Invite the crew to a Co-op Getaway", Action = () => { if (CoopOpInviteKind == "getaway") AcceptCoopGetaway(); else InviteCoopGetaway(); } },
                new MenuOption { Label = CoopOpInviteKind == "convoy" ? $"Accept {CoopPartnerName}'s Co-op Convoy" : "Invite the crew to a Co-op Convoy", Action = () => { if (CoopOpInviteKind == "convoy") AcceptCoopConvoy(); else InviteCoopConvoy(); } },
                new MenuOption { Label = "Leave the crew", Action = () => { LeaveCrew(); AppCrew(); } });
        }

        void AppAccount()
        {
            if (Account == null || !Account.Online)
            {
                Page("Account", "Offline", "No backend configured. Copy Resources/cloud.example.json to cloud.json with your Supabase project URL and anon key, and apply supabase/schema.sql to the project. Your progress saves locally either way.");
                return;
            }
            if (Account.SignedIn)
            {
                Page("Account", Account.Status, $"Cloud save: {(Cloud != null ? Cloud.Status : "—")}\nMultiplayer: {(Players != null ? Players.Status : "off")}\nPress T in the city to chat with players in your district.",
                    new MenuOption { Label = "Save to the cloud now", Action = () => { AutoSave(); Cloud?.Push(State.S, ok => State.Toast(ok ? "Saved to the cloud" : "Cloud save failed")); } },
                    new MenuOption { Label = "Load the cloud save", Action = () => Cloud?.Pull(save => { if (save == null) { State.Toast(Cloud.Status); return; } State.S = save; State.Toast("Cloud save loaded — restart to apply the world state"); }) },
                    new MenuOption { Label = "Sign out", Action = () => { Account.SignOut(); AppAccount(); } });
                return;
            }
            var m = new Menu { Title = "Account", Subtitle = Account.Status, Line = "Sign in to save to the cloud and meet other players. Click a field, type, then choose." };
            m.Fields.Add(new MenuField { Label = "Email" }); m.Fields.Add(new MenuField { Label = "Password", Password = true });
            m.Options.Add(new MenuOption { Label = "Sign in", Action = () => Account.SignIn(m.Fields[0].Value, m.Fields[1].Value, (ok, msg) => { State.Toast(msg); AppAccount(); }) });
            m.Options.Add(new MenuOption { Label = "Create account", Action = () => Account.SignUp(m.Fields[0].Value, m.Fields[1].Value, (ok, msg) => { State.Toast(msg); AppAccount(); }) });
            m.Options.Add(new MenuOption { Label = "Back", Action = OpenPhone });
            OpenMenu(m);
        }

        void AppSettings()
        {
            var st = State.S; string[] q = World.QualityGovernor.Names;
            string level = Governor != null ? q[Governor.Level] : q[Mathf.Clamp(st.quality, 0, 2)];
            string perf = Governor != null ? $" · {Governor.Fps:0} fps · render scale {Governor.RenderScale * 100f:0} %" : "";
            string seasonStr = Weather != null ? Weather.ActiveSeasonLabel : (Clock != null ? Clock.SeasonLabel : "DRY · HARMATTAN");
            Page("Settings", $"Graphics {(st.qualityAuto ? "Auto → " : "")}{level}{perf} · Season: {seasonStr} · sound {(st.sound ? "on" : "off")}",
                "Auto reads the GPU and steps down whenever frames stay slow; dynamic resolution moves in four steps and climbs back when it can. The cinematic look is the HDR, bloom and tone-mapping pass. " + SystemInfo.graphicsDeviceName,
                new MenuOption { Label = st.qualityAuto ? "Graphics: Auto (tap for Low)" : $"Graphics: {level} (tap for next)", Action = () => { if (st.qualityAuto) { st.qualityAuto = false; st.quality = 0; } else { st.quality = (st.quality + 1) % 3; if (st.quality == 0) st.qualityAuto = true; } ApplyQuality(); AppSettings(); } },
                new MenuOption { Label = "Toggle cinematic look", Action = () => { st.cinematic = !st.cinematic; ApplyQuality(); AppSettings(); } },
                new MenuOption { Label = $"Sound: {(st.sound ? "on" : "off")}", Action = () => { st.sound = !st.sound; AppSettings(); } },
                new MenuOption { Label = $"Season Mode: {seasonStr} (tap to cycle Calendar / Rainy / Dry Harmattan)", Action = () => { if (Weather != null) { string next = Weather.ForcedSeason == "" ? "rainy" : Weather.ForcedSeason == "rainy" ? "dry" : ""; Weather.SetSeasonMode(next); State.Toast($"Season set to {Weather.ActiveSeasonLabel}"); } AppSettings(); } },
                new MenuOption { Label = $"Mature content (night hustlers): {(st.mature ? "on" : "off")}", Action = () => { st.mature = !st.mature; AppSettings(); } },
                new MenuOption { Label = "Save now", Action = () => { AutoSave(); State.Toast("Saved"); AppSettings(); } },
                new MenuOption { Label = "New game (erases the save)", Action = () => { GameState.ClearSave(); State.Toast("Save erased — restart to begin again"); AppSettings(); } });
        }

        public void ApplyQuality()
        {
            var st = State.S;
            if (Governor == null) return;
            Governor.SetCinematic(st.cinematic);
            if (st.qualityAuto) Governor.SetLevel(World.QualityGovernor.Detect(), true); else Governor.SetLevel(st.quality, false);
            st.quality = Governor.Level;
        }
    }
}
