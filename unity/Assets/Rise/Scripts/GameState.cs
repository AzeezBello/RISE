using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Rise.Data;

namespace Rise
{
    // The saved player state: money, XP and level, heat, vitals, skills, time and
    // position. Saved as JSON under Application.persistentDataPath.
    [Serializable]
    public class SkillSet { public float driving, business, charisma, fitness, strength;
        public float Get(string k) => k == "driving" ? driving : k == "business" ? business : k == "charisma" ? charisma : k == "fitness" ? fitness : k == "strength" ? strength : 0f;
        public void Add(string k, float n) { switch (k) { case "driving": driving += n; break; case "business": business += n; break; case "charisma": charisma += n; break; case "fitness": fitness += n; break; case "strength": strength += n; break; } }
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public int cash, bank, xp, level = 1, heat, day = 1;
        public float health = 100f, stamina = 100f, fuel = 100f, clock = 8.5f;
        public float px, py, pz, yaw;
        public SkillSet skills = new SkillSet();
        public string lastSeen;
        // Housing and business
        public string home = "", rentedId = ""; public int rentedUntil;
        public List<string> props = new List<string>();
        public List<OwnedBusiness> biz = new List<OwnedBusiness>();
        public float payIn = 60f;
        // Story: mission index, the prologue path, the arc after Crossroads, flags.
        public int mission; public string path = "", arc = ""; public bool chase, kioskDeal, done, storyPaused;
        public List<string> items = new List<string>();
        public string equippedWeapon = "";
        public int ammo = 36;
        public float armor;
        public RepSet rep = new RepSet();
        // Live task (one at a time): type is "" when none.
        public string taskType = "", taskDest = "", taskItem = "", taskObj = ""; public float taskDeadline, taskMaxKmh, taskClear; public int taskMission; public bool taskTaken;
        public List<OwnedVehicle> vehicles = new List<OwnedVehicle>();
        // Education and activities
        public List<string> courses = new List<string>();
        public int gymUntil, gymSessions, outfits;
        public int lookFabric, lookSkin = 2, lookPants, lookHair, lookCap = 0;   // indices into the exported wardrobe lists
        public int lookFace, lookBody = 1, lookHairStyle = 1, lookFacialHair = 1; // default: Fade haircut + Beard
        public int footballWins, footballTier;
        public int collegeSemester = -1; public bool collegeGraduated;            // -1 = not enrolled
        public int quality = 2; public bool cinematic = true, qualityAuto = true;   // 0 low, 1 medium, 2 high; auto picks and steps down
        public int lookOutfit, lookAccessory = 4;                                   // outfit silhouette; accessory 4 = gold chain
        // Family and contact relationships
        public List<FamilyRel> familyRel = new List<FamilyRel>();
        public List<string> familyDone = new List<string>();
        public string familyRequest = ""; public int familyRequestDay = -1;
        public List<ContactRel> contactRel = new List<ContactRel>();
        // Street and property life
        public bool pet, mature, sound = true;
        public string waypointId = "";
        public int partyDay = -1, prayedDay = -1, awayTotal;
        public List<string> let = new List<string>();                      // owned homes let to tenants
        public List<PropertyUpgrades> upgrades = new List<PropertyUpgrades>();
        public bool rivalTold;
        // Crew (PLAN Phase 5)
        public string crewId = "", crewName = "", crewTag = "", crewCode = "", crewHq = ""; public int crewTreasury;
        // Neighbourhood story chains (PLAN Alpha 1.1)
        public List<ChainSave> chains = new List<ChainSave>();
    }

    [Serializable] public class ChainSave { public string id; public int step; }

    [Serializable] public class FamilyRel { public string id; public float score = 50f, food = 100f, companionship = 100f, education = 100f; public int lastVisitDay; }
    [Serializable] public class ContactRel { public string id; public float trust = 20f, respect = 15f; public int talkDay = -1; }

    [Serializable] public class RepSet { public int publicRep, business, street, social;
        public int Get(string k) => k == "public" ? publicRep : k == "business" ? business : k == "street" ? street : k == "social" ? social : 0;
        public void Add(string k, int n) { switch (k) { case "public": publicRep += n; break; case "business": business += n; break; case "street": street += n; break; case "social": social += n; break; } }
    }
    [Serializable] public class OwnedVehicle
    {
        public string id, type;
        public float cond = 100f, x, y, z, yaw;
        public bool insured;
        public int livery = -1, slogan = -1;
        // NFS Garage tuning & visual mods
        public int engineStage, turboStage, suspensionStage, nosStage;
        public int spoiler, skirtKit, roofScoop, rims, neonColor = -1, paintFinish;
        public string customHex = "";
    }

    [Serializable] public class OwnedBusiness { public string id; public float stock = 100f; public string price = "normal"; public List<string> staff = new List<string>(); public List<string> shifts = new List<string>(); }
    [Serializable] public class PropertyUpgrades { public string id; public List<string> items = new List<string>(); public string complaint = ""; }

    public class GameState
    {
        public SaveData S;
        readonly WorldData data;
        public readonly List<(string text, float until)> Toasts = new List<(string, float)>();
        public readonly List<(string from, string text)> Messages = new List<(string, string)>();

        public static string SavePath => Path.Combine(Application.persistentDataPath, "rise-save.json");
        // Saves written before the rename: read once, then the next autosave writes the new file.
        static string LegacySavePath => Path.Combine(Application.persistentDataPath, "naijarise-save.json");

        public GameState(WorldData world) { data = world; }

        public void NewGame()
        {
            S = new SaveData { cash = data.meta.startCash, bank = data.meta.startBank, clock = data.meta.startClock };
            var sp = data.Spawn; S.px = sp.x; S.py = sp.y; S.pz = sp.z; S.yaw = 180f;
        }

        public bool Load()
        {
            try
            {
                string path = File.Exists(SavePath) ? SavePath : File.Exists(LegacySavePath) ? LegacySavePath : null;
                if (path == null) return false;
                S = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
                S.skills ??= new SkillSet(); S.rep ??= new RepSet(); S.items ??= new List<string>(); S.vehicles ??= new List<OwnedVehicle>();
                S.path ??= ""; S.arc ??= ""; S.taskType ??= ""; S.taskDest ??= ""; S.taskItem ??= ""; S.taskObj ??= ""; S.courses ??= new List<string>();
                S.familyRel ??= new List<FamilyRel>(); S.familyDone ??= new List<string>(); S.familyRequest ??= ""; S.contactRel ??= new List<ContactRel>(); S.let ??= new List<string>(); S.upgrades ??= new List<PropertyUpgrades>(); S.crewId ??= ""; S.crewName ??= ""; S.crewTag ??= ""; S.crewCode ??= ""; S.crewHq ??= ""; S.waypointId ??= ""; S.chains ??= new List<ChainSave>();
                foreach (var b in S.biz) { b.staff ??= new List<string>(); b.shifts ??= new List<string>(); while (b.shifts.Count < b.staff.Count) b.shifts.Add("all-day"); }
                return S != null;
            }
            catch (Exception e) { Debug.LogWarning("Save could not be read: " + e.Message); return false; }
        }

        public void Save(Vector3 pos, float yaw, float clock, int day)
        {
            S.px = pos.x; S.py = pos.y; S.pz = pos.z; S.yaw = yaw; S.clock = clock; S.day = day; S.lastSeen = DateTime.UtcNow.ToString("o");
            if (DiagFlags.Has("nosave")) return;   // diagnostic runs (screenshots) must not overwrite a real save
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(S)); }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); }
        }

        public static void ClearSave() { try { if (File.Exists(SavePath)) File.Delete(SavePath); if (File.Exists(LegacySavePath)) File.Delete(LegacySavePath); } catch { } }

        // ---- economy ----
        public string Title => data.TitleFor(S.level);

        public bool Pay(int n)
        {
            if (S.cash + S.bank < n) return false;
            int c = Mathf.Min(S.cash, n); S.cash -= c; S.bank -= n - c;
            UI.SoundSystem.I?.PlayCash();
            return true;
        }

        public void Earn(int n) { S.cash += n; UI.SoundSystem.I?.PlayCash(); }

        public void AddXp(int n)
        {
            S.xp += n;
            while (S.xp >= 100)
            {
                S.xp -= 100; S.level++;
                var unlock = data.UnlockFor(S.level);
                Toast($"LEVEL UP — Level {S.level}{(unlock != null ? " · " + unlock : "")}");
                Message("bank", $"Congratulations on reaching Level {S.level}. {unlock ?? "Keep rising."}");
            }
        }

        public void GainSkill(string skill, float n) => S.skills.Add(skill, n);
        public int JobPay(JobData j) => Mathf.RoundToInt(j.pay * (1f + S.skills.Get(j.skill) * 0.01f));   // +1% per skill point
        public bool HasCourse(string id) => !string.IsNullOrEmpty(id) && (S.courses.Contains(id) || (id == "yabatech" && S.collegeGraduated));
        public bool CanTake(JobData j) => S.skills.Get(j.skill) >= j.min && (string.IsNullOrEmpty(j.needCourse) || HasCourse(j.needCourse));

        public void AddRep(string k, int n) => S.rep.Add(k, n);
        public bool Has(string item) => S.items.Contains(item);
        public void AddItem(string item) { if (!string.IsNullOrEmpty(item) && !S.items.Contains(item)) S.items.Add(item); }
        public void RemoveItem(string item) => S.items.Remove(item);
        public int ChainStep(string id) { if (S.chains != null) foreach (var c in S.chains) if (c.id == id) return c.step; return 0; }
        public int AdvanceChain(string id) { S.chains ??= new List<ChainSave>(); foreach (var c in S.chains) if (c.id == id) return ++c.step; S.chains.Add(new ChainSave { id = id, step = 1 }); return 1; }
        public int BizPrice(BusinessData b) => S.kioskDeal && b.id == "kiosk" ? Mathf.RoundToInt(b.price * 0.6f) : b.price;

        public void AddHeat(int n, string why)
        {
            int before = S.heat;
            S.heat = Mathf.Clamp(S.heat + n, 0, 5);
            if (!string.IsNullOrEmpty(why) && S.heat != before) Toast($"{why} · Heat {(n > 0 ? "+" : "")}{n}");
        }

        public void Toast(string text) => Toasts.Add((text, Time.time + 3.5f));
        public void Message(string from, string text) { Messages.Add((from, text)); Toast($"{data.ContactName(from)}: {text}"); }
        // ---- housing ----
        public PropertyData Home => string.IsNullOrEmpty(S.home) ? null : data.Property(S.home);
        public bool Owns(PropertyData p) => S.props.Contains(p.id);

        public bool Rent(PropertyData p)
        {
            int fee = Mathf.RoundToInt(p.rent * data.meta.agentFeeRate);
            if (!Pay(p.rent + fee)) { Toast($"You need ₦{p.rent + fee:N0} (rent + ₦{fee:N0} agent fee)"); return false; }
            S.rentedId = p.id; S.rentedUntil = S.day + data.meta.leaseDays; S.home = p.id;
            Toast($"Lease signed · {p.type} for {data.meta.leaseDays} days");
            Message("landlord", $"Welcome. {p.type} na yours for {data.meta.leaseDays} days. No late rent, no wahala. Agent Kunle don collect him ₦{fee:N0}.");
            return true;
        }

        public bool Buy(PropertyData p)
        {
            if (p.buy <= 0) { Toast("This one na rent only"); return false; }
            if (S.level < data.meta.propertyLevel) { Toast($"Property unlocks at Level {data.meta.propertyLevel}"); return false; }
            if (!Pay(p.buy)) { Toast($"You need ₦{p.buy:N0} across cash and bank"); return false; }
            S.props.Add(p.id);
            if (S.rentedId == p.id) S.rentedId = "";
            if (string.IsNullOrEmpty(S.home)) S.home = p.id;
            Toast($"{p.name} is yours");
            Message("landlord", $"Papers signed. {p.name} na your own now.");
            return true;
        }

        public PropertyUpgrades Upgrades(string propertyId) { foreach (var u in S.upgrades) if (u.id == propertyId) return u; var n = new PropertyUpgrades { id = propertyId }; S.upgrades.Add(n); return n; }
        public bool HasUpgrade(string propertyId, string upgrade) => !string.IsNullOrEmpty(propertyId) && Upgrades(propertyId).items.Contains(upgrade);
        public bool HomeHas(string upgrade) => HasUpgrade(S.home, upgrade);
        public bool BuyUpgrade(PropertyData p, UpgradeData u)
        {
            if (HasUpgrade(p.id, u.id)) { Toast("Already installed"); return false; }
            if (!Pay(u.price)) { Toast($"You need ₦{u.price:N0}"); return false; }
            Upgrades(p.id).items.Add(u.id); Toast($"{u.name} installed · {u.desc}"); return true;
        }
        public void LetOut(PropertyData p) { if (!S.let.Contains(p.id)) S.let.Add(p.id); if (S.home == p.id) S.home = ""; Toast($"{p.name} let to tenants · ₦{Mathf.RoundToInt(p.rent * data.tenantShare):N0} a day"); }
        public void TakeBack(PropertyData p) { S.let.Remove(p.id); Toast($"Tenants given notice at {p.name}"); }
        public bool FixComplaint(PropertyData p)
        {
            var u = Upgrades(p.id); if (string.IsNullOrEmpty(u.complaint)) return false;
            if (!Pay(15000)) { Toast("You need ₦15,000 for the repair"); return false; }
            u.complaint = ""; Toast($"{p.name} fixed · full rent again"); return true;
        }

        // Called when the game day rolls over: tenants pay, maintenance is deducted, complaints come up, leases run out.
        public void NewDay(int day)
        {
            S.day = day;
            int income = 0;
            foreach (var id in S.let) { var p = data.Property(id); if (p == null) continue; income += Mathf.RoundToInt(p.rent * data.tenantShare * (string.IsNullOrEmpty(Upgrades(id).complaint) ? 1f : 0.5f)); }
            if (income > 0) { S.bank += income; Toast($"RiseBank · Credit alert · ₦{income:N0} rent from your tenants"); }
            string[] complaints = { "the pumping machine don spoil", "roof dey leak", "NEPA wire don cut", "soakaway don full" };
            foreach (var id in S.let) { var u = Upgrades(id); if (string.IsNullOrEmpty(u.complaint) && UnityEngine.Random.value < 0.2f) { u.complaint = complaints[UnityEngine.Random.Range(0, complaints.Length)]; Message("landlord", $"Your tenants for {data.Property(id)?.name} talk say {u.complaint}. Dem go pay half rent until you fix am."); } }
            if (!string.IsNullOrEmpty(S.rentedId) && day > S.rentedUntil)
            {
                var p = data.Property(S.rentedId);
                Toast($"Your lease on {(p != null ? p.name : "your place")} has ended");
                if (S.home == S.rentedId) S.home = "";
                S.rentedId = "";
            }
            else if (!string.IsNullOrEmpty(S.rentedId) && S.rentedUntil - day == 3) Toast("Your lease ends in 3 days");
        }

        // ---- businesses ----
        public static readonly string[] ShiftIds = { "all-day", "morning", "afternoon", "night" };
        public static string ShiftLabel(string shift) => shift == "morning" ? "Morning · 06–14" : shift == "afternoon" ? "Afternoon · 14–22" : shift == "night" ? "Night · 22–06" : "All day";

        public OwnedBusiness Biz(string id) { foreach (var b in S.biz) if (b.id == id) return b; return null; }
        public float PriceEffect(string price) => price == "low" ? data.meta.priceLow : price == "high" ? data.meta.priceHigh : 1f;
        public bool RivalOpen => S.biz.Count >= data.rivalAfter;

        public string StaffShift(OwnedBusiness o, int idx)
        {
            if (o == null || idx < 0 || idx >= o.staff.Count) return "all-day";
            o.shifts ??= new List<string>();
            while (o.shifts.Count < o.staff.Count) o.shifts.Add("all-day");
            return string.IsNullOrEmpty(o.shifts[idx]) ? "all-day" : o.shifts[idx];
        }

        public bool StaffIsOnDuty(OwnedBusiness o, int idx, float? clockOverride = null)
        {
            if (o == null || idx < 0 || idx >= o.staff.Count) return false;
            float hour = Mathf.Repeat(clockOverride ?? S.clock, 24f);
            string s = StaffShift(o, idx);
            if (s == "morning") return hour >= 6f && hour < 14f;
            if (s == "afternoon") return hour >= 14f && hour < 22f;
            if (s == "night") return hour >= 22f || hour < 6f;
            return true;
        }

        public int StaffOnDuty(OwnedBusiness o, float? clockOverride = null)
        {
            if (o == null) return 0;
            int n = 0;
            for (int i = 0; i < o.staff.Count; i++) if (StaffIsOnDuty(o, i, clockOverride)) n++;
            return n;
        }

        public string CycleStaffShift(BusinessData b, int idx)
        {
            var o = Biz(b.id); if (o == null || idx < 0 || idx >= o.staff.Count) return "all-day";
            string cur = StaffShift(o, idx);
            int next = (Array.IndexOf(ShiftIds, cur) + 1) % ShiftIds.Length;
            o.shifts[idx] = ShiftIds[next];
            Toast($"{o.staff[idx]} · {ShiftLabel(o.shifts[idx])} shift");
            return o.shifts[idx];
        }

        public int BizIncome(BusinessData b, float? clockOverride = null)
        {
            var o = Biz(b.id); if (o == null) return 0;
            float rival = RivalOpen && o.price != "low" ? data.rivalPenalty : 1f;
            int onDuty = StaffOnDuty(o, clockOverride);
            return Mathf.RoundToInt(b.income * (1f + onDuty * data.staffBonus) * PriceEffect(o.price) * (1f + S.skills.business * 0.005f) * (o.stock / 100f) * rival);
        }

        public int TotalBizIncome(float? clockOverride = null)
        {
            int total = 0;
            foreach (var o in S.biz) { var b = data.Business(o.id); if (b != null) total += BizIncome(b, clockOverride); }
            return total;
        }

        public bool HireStaff(BusinessData b)
        {
            var o = Biz(b.id); if (o == null) return false;
            if (o.staff.Count >= data.maxStaff) { Toast("Full roster"); return false; }
            if (!Pay(data.staffCost)) { Toast("Not enough money to hire"); return false; }
            string name = data.staffNames.Length > 0 ? data.staffNames[(o.staff.Count + b.id.Length) % data.staffNames.Length] : "Staff";
            o.staff.Add(name);
            o.shifts ??= new List<string>();
            o.shifts.Add("all-day");
            GainSkill("business", 2f);
            Toast($"{name} joins {b.name} · +{Mathf.RoundToInt(data.staffBonus * 100)}% income");
            return true;
        }
        public int RestockCost(BusinessData b) => Mathf.RoundToInt(b.income * 1.5f);

        public bool BuyBusiness(BusinessData b)
        {
            if (Biz(b.id) != null) { Toast("You own this one already"); return false; }
            if (S.level < data.meta.propertyLevel) { Toast($"Businesses unlock at Level {data.meta.propertyLevel}"); return false; }
            if (!Pay(BizPrice(b))) { Toast($"You need ₦{BizPrice(b):N0} across cash and bank"); return false; }
            S.biz.Add(new OwnedBusiness { id = b.id });
            AddRep("business", 8); GainSkill("business", 5f);
            Toast($"{b.name} is yours · ₦{BizIncome(b):N0} per pay cycle");
            Message("bank", $"Business account opened for {b.name}. Income lands in your RiseBank account.");
            return true;
        }

        public bool Restock(BusinessData b)
        {
            var o = Biz(b.id); if (o == null) return false;
            if (!Pay(RestockCost(b))) { Toast("No money to restock"); return false; }
            o.stock = 100f; Toast($"{b.name} restocked"); return true;
        }

        public void CyclePrice(BusinessData b) { var o = Biz(b.id); if (o == null) return; o.price = o.price == "low" ? "normal" : o.price == "normal" ? "high" : "low"; AddRep("business", o.price == "high" ? -3 : o.price == "low" ? 2 : 0); Toast($"{b.name} · prices {o.price}"); }

        // Owned businesses pay into the bank every pay cycle and drain stock.
        public void TickEconomy(float dt)
        {
            if (S.biz.Count == 0) return;
            S.payIn -= dt;
            if (S.payIn > 0f) return;
            S.payIn = data.meta.payCycle;
            int total = 0;
            foreach (var o in S.biz)
            {
                var b = data.Business(o.id); if (b == null) continue;
                total += BizIncome(b);
                o.stock = Mathf.Max(0f, o.stock - 15f);
                if (Mathf.Approximately(o.stock, 30f)) Toast($"{b.name} is running low on stock");
            }
            S.bank += total;
            if (RivalOpen && !S.rivalTold) { S.rivalTold = true; Message("nkechi", $"{data.rivalName} don open shop near your own. E dey undercut you. Drop your prices if you wan keep customers."); }
            Toast($"RiseBank · Credit alert · ₦{total:N0} business income");
        }

        // "Lagos Never Sleeps": businesses and tenants keep earning while the player is away.
        public (int minutes, int biz, int rent, int upkeep, int net, int days)? ApplyAwayEarnings(DateTime? nowUtc = null)
        {
            if (string.IsNullOrEmpty(S.lastSeen)) return null;
            if (!DateTime.TryParse(S.lastSeen, null, System.Globalization.DateTimeStyles.RoundtripKind, out var seen)) return null;
            double elapsed = ((nowUtc ?? DateTime.UtcNow) - seen.ToUniversalTime()).TotalMinutes;
            float minutes = Mathf.Min(data.meta.awayCapMinutes, (float)elapsed);
            if (minutes < data.meta.awayMinMinutes) return null;
            int biz = Mathf.RoundToInt(TotalBizIncome() * minutes);
            int days = Mathf.FloorToInt(minutes / 18f);
            int rent = 0, upkeep = 0;
            foreach (var id in S.let) { var p = data.Property(id); if (p != null) rent += Mathf.RoundToInt(p.rent * data.tenantShare * days); }
            foreach (var id in S.props) { var p = data.Property(id); if (p != null) upkeep += Mathf.RoundToInt((p.rent / 60f) * days); }
            int net = biz + rent - upkeep;
            S.bank += net;
            S.awayTotal += net;
            if (net > 0) Message("bank", $"Welcome back. While you were away ({Mathf.RoundToInt(minutes)} min), your businesses and tenants earned ₦{net:N0}.");
            return (Mathf.RoundToInt(minutes), biz, rent, upkeep, net, days);
        }

        // ---- relationships ----
        public ContactRel Rel(string id) { foreach (var r in S.contactRel) if (r.id == id) return r; var n = new ContactRel { id = id }; S.contactRel.Add(n); return n; }
        public string RelTier(string id) { var r = Rel(id); float score = (r.trust + r.respect) * 0.5f; return score >= 70f ? "close" : score >= 45f ? "friend" : score >= 25f ? "known" : "stranger"; }
        public bool TalkedToday(string id) => Rel(id).talkDay == S.day;
        public void BumpRel(string id, float trust, float respect) { var r = Rel(id); r.trust = Mathf.Clamp(r.trust + trust, 0f, 100f); r.respect = Mathf.Clamp(r.respect + respect, 0f, 100f); }

        public FamilyRel Family(string id) { foreach (var r in S.familyRel) if (r.id == id) return r; var n = new FamilyRel { id = id }; S.familyRel.Add(n); return n; }

        // ---- Yaba Tech ----
        public bool CompleteSemester()
        {
            if (S.collegeSemester < 0 || S.collegeGraduated) return false;
            S.collegeSemester++;
            GainSkill("business", 4); GainSkill("charisma", 1);
            if (S.collegeSemester >= data.school.semesters)
            {
                S.collegeGraduated = true; AddXp(30);
                Toast("Yaba Tech Diploma completed · IT Support Technician unlocked");
                Message("cafeguy", "Congratulations! Your Yaba Tech diploma don ready. Alagomeji Tech Hub dey hire IT Support Technicians.");
            }
            else { AddXp(10); Toast($"Yaba Tech semester {S.collegeSemester} of {data.school.semesters} completed"); }
            return true;
        }

        // Cash ↔ bank
        public bool Deposit(int n) { if (n <= 0 || S.cash < n) return false; S.cash -= n; S.bank += n; return true; }
        public bool Withdraw(int n) { if (n <= 0 || S.bank < n) return false; S.bank -= n; S.cash += n; return true; }

        // ---- look (src/data/characters.js LOOK) ----
        public Entities.Look Look
        {
            get
            {
                var l = Entities.Look.Default; l.Fabric = S.lookFabric;
                if (data.skins.Length > 0) l.Skin = Entities.Mannequin.Hex(data.skins[Mathf.Clamp(S.lookSkin, 0, data.skins.Length - 1)]);
                if (data.pants.Length > 0) l.Pants = Entities.Mannequin.Hex(data.pants[Mathf.Clamp(S.lookPants, 0, data.pants.Length - 1)]);
                string[] hair = { "#120b08", "#3a2214", "#6b3e1a", "#b0b0b0", "#d62828" };
                l.Hair = Entities.Mannequin.Hex(hair[Mathf.Clamp(S.lookHair, 0, hair.Length - 1)]);
                l.Cap = S.lookCap == 1 ? (Color?)Entities.Mannequin.Hex("#1f3a5f") : S.lookCap == 2 ? (Color?)Entities.Mannequin.Hex("#d62828") : null;
                l.Outfit = S.lookOutfit; l.BodyType = S.lookBody; l.HairStyle = S.lookHairStyle; l.FacialHair = S.lookFacialHair; l.Accessory = S.lookAccessory;
                return l;
            }
        }

        // ---- education ----
        public bool CompleteCourse(CourseData c)
        {
            if (c == null || HasCourse(c.id)) return false;
            S.courses.Add(c.id);
            if (c.skills != null) foreach (var sp in c.skills) GainSkill(sp.skill, sp.points);
            AddXp(20);
            var unlocked = new List<string>();
            foreach (var j in data.jobs) if (j.needCourse == c.id || (c.unlocks != null && System.Array.IndexOf(c.unlocks, j.id) >= 0)) if (!unlocked.Contains(j.title)) unlocked.Add(j.title);
            string extra = unlocked.Count > 0 ? $" Unlocks: {string.Join(", ", unlocked)}." : "";
            Toast($"{c.name} learned.{extra}");
            Message("cafeguy", $"{c.name} certificate ready.{extra}");
            return true;
        }

        public void PruneToasts() { Toasts.RemoveAll(t => t.until < Time.time); if (Toasts.Count > 4) Toasts.RemoveRange(0, Toasts.Count - 4); }
    }
}
