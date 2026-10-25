using UnityEngine;
using Rise.Data;
using Rise.World;

namespace Rise
{
    // Street encounters: the agbero levy (pay or risk heat; a pet dog or a home with security keeps them off), the
    // night hustlers outside the clubs (hookup, pickpockets), Mama Nkechi's dog, and tenants, upgrades, staff and
    // vehicle customisation menus.
    public partial class Session
    {
        [System.NonSerialized] public StreetLife Street;
        StreetLife.Agbero targetNight; (Transform t, string name)? targetHustler;

        public void ForceStreetUpdate() => UpdateStreet();

        void UpdateStreet()
        {
            if (Street == null || Vehicle != null || ActiveMenu != null || Working != null) return;
            Street.mature = State.S.mature;
            if (State.S.pet && !Street.HasPet) Street.AdoptDog();
            var pos = Player.transform.position;
            var a = Street.NearestAgbero(pos, 3.6f);
            bool guarded = State.S.pet || State.HomeHas("security");
            if (a != null && a.cool <= 0f && !guarded) { a.cool = 150f; OpenAgberoMenu(a); }
        }

        public struct FightWeaponSpec
        {
            public string Id, Name, Desc;
            public int Price, Power;
            public bool IsGun;
            public float Range;
        }

        public static readonly FightWeaponSpec[] FightWeapons =
        {
            new FightWeaponSpec { Id = "plank",    Name = "2x4 Nail Plank",     Price = 4000,   Power = 15, IsGun = false, Range = 2.8f, Desc = "Timber board with nails" },
            new FightWeaponSpec { Id = "pepper",   Name = "Pepper Spray",       Price = 5000,   Power = 18, IsGun = false, Range = 3.2f, Desc = "Blinds attackers fast" },
            new FightWeaponSpec { Id = "koboko",   Name = "Koboko Horsewhip",   Price = 6000,   Power = 22, IsGun = false, Range = 3.0f, Desc = "Braided leather whip" },
            new FightWeaponSpec { Id = "knuckles", Name = "Brass Knuckles",     Price = 8000,   Power = 25, IsGun = false, Range = 2.4f, Desc = "Close-range metal knuckles" },
            new FightWeaponSpec { Id = "baton",    Name = "Iron Rod",           Price = 12000,  Power = 30, IsGun = false, Range = 2.8f, Desc = "Heavy Ladipo steel pipe" },
            new FightWeaponSpec { Id = "cutlass",  Name = "Cutlass",            Price = 18000,  Power = 40, IsGun = false, Range = 3.0f, Desc = "Sharp bush machete" },
            new FightWeaponSpec { Id = "pistol",   Name = "9mm Beretta Pistol",      Price = 45000,  Power = 55,  IsGun = true,  Range = 42f,  Desc = "Semi-auto 9mm sidearm" },
            new FightWeaponSpec { Id = "shotgun",  Name = "Pump-Action Shotgun",     Price = 85000,  Power = 80,  IsGun = true,  Range = 28f,  Desc = "12-gauge close-range blast" },
            new FightWeaponSpec { Id = "smg",      Name = "Navy MP5 Tactical SMG",   Price = 95000,  Power = 70,  IsGun = true,  Range = 48f,  Desc = "Suppressed Navy SBS submachine gun" },
            new FightWeaponSpec { Id = "rifle",    Name = "Tactical AK-47 Rifle",    Price = 140000, Power = 100, IsGun = true,  Range = 65f,  Desc = "Automatic 7.62mm assault rifle" },
            new FightWeaponSpec { Id = "sniper",   Name = "M24 Marksman Sniper Rifle",Price = 180000,Power = 140, IsGun = true,  Range = 95f,  Desc = "Long-range bolt-action precision rifle" },
            new FightWeaponSpec { Id = "lmg",      Name = "PKM Heavy Machine Gun",   Price = 210000, Power = 115, IsGun = true,  Range = 75f,  Desc = "Belt-fed squad support machine gun" },
        };

        public static FightWeaponSpec? FindWeaponSpec(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var w in FightWeapons) if (w.Id == id) return w;
            return null;
        }

        public bool HasWeapon()
        {
            if (State.Has("weapon")) return true;
            foreach (var w in FightWeapons) if (State.Has(w.Id)) return true;
            return false;
        }

        public bool HasGun()
        {
            foreach (var w in FightWeapons) if (w.IsGun && State.Has(w.Id)) return true;
            return false;
        }

        public string BestGunId()
        {
            string eq = State.S.equippedWeapon;
            var eqSpec = FindWeaponSpec(eq);
            if (eqSpec.HasValue && eqSpec.Value.IsGun && State.Has(eq)) return eq;
            string best = ""; int bestP = -1;
            foreach (var w in FightWeapons)
                if (w.IsGun && State.Has(w.Id) && w.Power > bestP) { bestP = w.Power; best = w.Id; }
            return best;
        }

        public bool IsGunEquipped
        {
            get
            {
                var spec = FindWeaponSpec(BestWeaponId());
                return spec.HasValue && spec.Value.IsGun;
            }
        }

        public float WeaponRange()
        {
            var spec = FindWeaponSpec(BestWeaponId());
            return spec.HasValue ? spec.Value.Range : 2.8f;
        }

        public bool BuyAmmo(int rounds = 30, int price = 6000)
        {
            if (!State.Pay(price)) { State.Toast("Not enough money for ammunition"); return false; }
            State.S.ammo += rounds;
            State.Toast($"+{rounds} rounds of ammunition ({State.S.ammo} total)");
            return true;
        }

        public bool BuyTacticalArmor(int price = 15000)
        {
            if (State.S.armor >= 100f) { State.Toast("Kevlar Tactical Vest is already at 100% armor"); return false; }
            if (!State.Pay(price)) { State.Toast("Not enough money for Kevlar Tactical Vest"); return false; }
            State.S.armor = 100f;
            State.Toast("Kevlar Tactical Vest equipped (100% Body Armor)");
            return true;
        }

        public string BestWeaponId()
        {
            string eq = State.S.equippedWeapon;
            if (eq == "none") return "";
            if (!string.IsNullOrEmpty(eq) && State.Has(eq)) return eq;
            string best = ""; int bestP = -1;
            foreach (var w in FightWeapons)
                if (State.Has(w.Id) && w.Power > bestP) { bestP = w.Power; best = w.Id; }
            if (!string.IsNullOrEmpty(best)) return best;
            return State.Has("weapon") ? "weapon" : "";
        }

        public int WeaponPower()
        {
            string id = BestWeaponId();
            if (string.IsNullOrEmpty(id)) return 0;
            var spec = FindWeaponSpec(id);
            return spec.HasValue ? spec.Value.Power : 30;
        }

        public string EquippedWeaponName
        {
            get
            {
                string id = BestWeaponId();
                if (string.IsNullOrEmpty(id)) return "Bare Fists";
                var spec = FindWeaponSpec(id);
                return spec.HasValue ? spec.Value.Name : Data.ItemName(id);
            }
        }

        public void EquipWeapon(string id)
        {
            State.S.equippedWeapon = id ?? "";
            Player?.SetWeapon(BestWeaponId());
        }

        // Cycle through owned fight weapons and bare hands (Q / LB / ITEM button).
        public string CycleWeapon()
        {
            var owned = new System.Collections.Generic.List<string> { "none" };
            foreach (var w in FightWeapons) if (State.Has(w.Id)) owned.Add(w.Id);
            if (owned.Count == 1 && State.Has("weapon")) owned.Add("weapon");
            if (owned.Count <= 1)
            {
                State.Toast("No fight weapons in inventory — pick one up (G / E) or buy at Ladipo / vendors");
                return "";
            }
            string cur = BestWeaponId();
            if (string.IsNullOrEmpty(cur)) cur = "none";
            int idx = owned.IndexOf(cur);
            string next = owned[(idx + 1) % owned.Count];
            EquipWeapon(next);
            State.Toast(next == "none" ? "Holstered weapon · Bare Fists" : $"Equipped {EquippedWeaponName} (power {WeaponPower()})");
            return next == "none" ? "" : next;
        }

        // Pick up a 3D street item (G / E / D-pad Down / ITEM button).
        public bool PickupGroundItem(StreetLife.GroundItem g)
        {
            if (g == null || !g.Active) return false;
            g.respawn = 180f;
            if (g.t != null) g.t.gameObject.SetActive(false);
            if (g.cash > 0)
            {
                State.Earn(g.cash);
                State.Toast($"Picked up {g.label} (+₦{g.cash:N0})");
                return true;
            }
            if (g.id == "ammo_box")
            {
                State.S.ammo += 24;
                State.Toast($"Picked up {g.label} (+24 rounds · {State.S.ammo} ammo)");
                return true;
            }
            State.AddItem(g.id);
            var spec = FindWeaponSpec(g.id);
            if (spec.HasValue)
            {
                if (spec.Value.IsGun) State.S.ammo += 18;
                EquipWeapon(g.id);
                State.Toast($"Picked up and equipped {g.label} (power {WeaponPower()})");
            }
            else
            {
                State.Toast($"Picked up {g.label}");
            }
            return true;
        }

        // G / ITEM button: pick up nearest street item if within reach, otherwise cycle equipped weapon.
        public bool TryPickupOrCycleItem()
        {
            if (Street != null && Player != null)
            {
                var g = Street.NearestGroundItem(Player.transform.position, 2.8f);
                if (g != null) return PickupGroundItem(g);
            }
            CycleWeapon();
            return false;
        }

        float lastSwingToast;

        // Direct on-foot attack or gunshot (F / Left Click / RB / FIGHT button).
        public bool PerformStreetAttack()
        {
            if (Player == null || Vehicle != null) return false;
            if (IsGunEquipped) return FireGun();
            if (ActiveEnemies.Count > 0 &&NearestEnemy(Player.transform.position, 45f) != null)
            {
                if (HasGun())
                {
                    EquipWeapon(BestGunId());
                    return FireGun();
                }
                State.Toast("They have firearms! You need a gun (Pistol, Shotgun, or AK-47) to shoot back!");
                return false;
            }
            var pos = Player.transform.position;
            var agbero = Street != null ? Street.NearestAgbero(pos, 3.2f) : null;
            var ped = agbero == null && Pedestrians != null ? Pedestrians.Nearest(pos, 2.8f) : null;
            if (agbero != null && agbero.t != null)
            {
                Vector3 toTarget = agbero.t.position - pos; toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                    Player.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }
            else if (ped != null)
            {
                Vector3 toTarget = ped.position - pos; toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                    Player.transform.rotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            Player.SwingWeapon();
            UI.SoundSystem.I?.PlayKick(0.55f);
            State.S.stamina = Mathf.Max(0f, State.S.stamina - 3f);
            State.GainSkill("strength", 0.08f);

            // 1. Nearby agbero
            if (agbero != null)
            {
                bool won = FightAgbero(agbero);
                if (won && Street != null && Street.NearestGroundItem(agbero.pos, 2.5f) == null)
                    Street.DropItem(agbero.pos + new Vector3(0.8f, 0f, -0.4f), "koboko", "Koboko Horsewhip", 0);
                return true;
            }

            // 2. Nearby stopped/slow traffic vehicle
            var tv = Traffic != null ? Traffic.Nearest(pos, 3.2f) : null;
            if (tv != null && tv.speed < 3.5f)
            {
                return TryHijackTrafficVehicle(tv);
            }

            // 3. Nearby pedestrian
            if (ped != null)
            {
                Pedestrians.Knock(ped);
                if (Street != null && Street.NearestGroundItem(ped.position, 2.5f) == null)
                    Street.DropItem(ped.position, "cash", "Dropped Naira", 500);
                State.AddRep("street", 1);
                State.AddRep("public", -2);
                if (Law != null && Law.PoliceWithin(pos, 45f)) State.AddHeat(1, "Street assault near police");
                State.Toast($"Struck pedestrian with {EquippedWeaponName} — dropped Naira on the pavement");
                return true;
            }

            if (Time.time - lastSwingToast > 1.8f)
            {
                lastSwingToast = Time.time;
                string moveName = Player.ComboStep == 0 ? "Right Cross" : Player.ComboStep == 1 ? "Left Hook" : "Street Kick";
                State.Toast(!string.IsNullOrEmpty(BestWeaponId()) ? $"Swung {EquippedWeaponName}" : $"Threw {moveName} (press Q to equip a weapon, G to pick up items)");
            }
            return true;
        }

        public bool BuyFightWeapon(string id)
        {
            var spec = FindWeaponSpec(id);
            if (!spec.HasValue) return false;
            var w = spec.Value;
            if (State.Has(w.Id))
            {
                EquipWeapon(w.Id);
                State.Toast($"Equipped {w.Name}");
                return true;
            }
            if (!State.Pay(w.Price)) { State.Toast("Not enough money"); return false; }
            State.AddItem(w.Id);
            if (w.IsGun) State.S.ammo += 36;
            EquipWeapon(w.Id);
            State.Toast(w.IsGun ? $"{w.Name} bought and equipped (+36 ammo)" : $"{w.Name} bought and equipped");
            return true;
        }

        public bool FightAgbero(StreetLife.Agbero a)
        {
            Player?.SwingWeapon();
            UI.SoundSystem.I?.PlayKick(0.6f);
            if (a != null && Player != null) a.TriggerFight(Player.transform.position);
            int power = WeaponPower();
            float score = power + State.S.skills.strength * 0.6f;
            if (a != null) a.cool = 120f;
            if (HasWeapon() || score >= 18f)
            {
                int dmg = Mathf.Max(0, 12 - power / 3);
                State.S.health = Mathf.Max(20f, State.S.health - dmg);
                State.S.stamina = Mathf.Max(10f, State.S.stamina - 12f);
                int loot = 500 + (power >= 25 ? 1000 : 500);
                State.Earn(loot);
                State.GainSkill("strength", 1.5f);
                State.AddRep("street", 3);
                State.AddXp(6);
                if (BestWeaponId() == "cutlass" && Law != null && Law.PoliceWithin(Focus.position, 45f))
                    State.AddHeat(1, "Brandished a cutlass near police");
                State.Toast($"You scatter the agberos with {EquippedWeaponName}! +₦{loot:N0} · Street +3");
                return true;
            }
            State.S.health = Mathf.Max(15f, State.S.health - 18f);
            State.S.stamina = Mathf.Max(5f, State.S.stamina - 20f);
            State.GainSkill("strength", 1f);
            State.Toast("Rough street scrap with bare fists (-18 health) — get a fight weapon!");
            return false;
        }

        void OpenAgberoMenu(StreetLife.Agbero a)
        {
            string where = a.stop.name.Replace(" Bus Stop", "").Replace(" Bus Terminal", "");
            var m = new Menu { Title = "Agbero", Subtitle = a.stop.name, Line = $"Oga! Owo da? This na our {where} junction. Drop ₦500 for the boys." };
            m.Options.Add(new MenuOption { Label = "Pay ₦500", Action = () => { if (State.S.cash >= 500) { State.S.cash -= 500; State.Toast("Correct guy. Waka free."); } else State.Toast("No cash — they let it slide this time"); } });
            m.Options.Add(new MenuOption { Label = "I no get.", Action = () => { if (Random.value < 0.5f) State.AddHeat(1, "The boys remember your face"); else State.Toast("They hiss and let you pass"); } });
            m.Options.Add(new MenuOption { Label = HasWeapon() ? $"Fight them off ({EquippedWeaponName})" : "Fight them off (bare fists)", Action = () => FightAgbero(a) });
            OpenMenu(m);
        }

        void OpenHustlerMenu((Transform t, string name) n)
        {
            var m = new Menu { Title = n.name, Subtitle = "Night hustler", Line = $"{n.name}: Fine boy, you dey find hook up? ₦{Data.hookupPrice:N0} make we go inside." };
            m.Options.Add(new MenuOption { Label = $"Hook up ₦{Data.hookupPrice:N0}", Action = () =>
            {
                if (!State.Pay(Data.hookupPrice)) { State.Toast("Not enough money"); return; }
                Clock.Advance(1f); State.S.stamina = 100f; State.AddRep("social", 2);
                if (Random.value < 0.25f && !State.HomeHas("security")) { int loss = Mathf.Min(State.S.cash, 10000); State.S.cash -= loss; State.Toast($"Pocket picked · −₦{loss:N0}"); }
                else State.Toast("Night well spent");
            } });
            m.Options.Add(new MenuOption { Label = "Not tonight", Action = null });
            OpenMenu(m);
        }

        // Mama Nkechi sells the dog that keeps agberos away.
        void AddDogOption(Menu m, ContactSystem.Contact c)
        {
            if (c.data.id != "nkechi" || State.S.pet) return;
            m.Options.Add(new MenuOption { Label = $"Adopt the dog · ₦{Data.petPrice:N0}", Action = () => { if (!State.Pay(Data.petPrice)) { State.Toast("Not enough money"); return; } State.S.pet = true; Street?.AdoptDog(); State.Toast("You now have a dog. Agberos will keep their distance."); } });
        }

        // ---- tenants and upgrades (Agent Kunle, the home) ----
        void AddPropertyOptions(Menu m, PropertyData p)
        {
            if (!State.Owns(p)) return;
            if (State.S.let.Contains(p.id)) m.Options.Add(new MenuOption { Label = "Give the tenants notice", Action = () => State.TakeBack(p) });
            else if (State.S.home != p.id) m.Options.Add(new MenuOption { Label = $"Let to tenants · ₦{Mathf.RoundToInt(p.rent * Data.tenantShare):N0} a day", Action = () => State.LetOut(p) });
            var u = State.Upgrades(p.id);
            if (!string.IsNullOrEmpty(u.complaint)) m.Options.Add(new MenuOption { Label = $"Fix: {u.complaint} · ₦15,000", Action = () => State.FixComplaint(p) });
        }

        void AddUpgradeOptions(Menu m, PropertyData p)
        {
            if (!State.Owns(p)) return;
            foreach (var up in Data.upgrades) { var u = up; bool has = State.HasUpgrade(p.id, up.id); m.Options.Add(new MenuOption { Label = has ? $"{up.name} · installed" : $"{up.name} · ₦{up.price:N0} — {up.desc}", Enabled = !has, Action = () => State.BuyUpgrade(p, u) }); }
        }

        // ---- business staff ----
        void AddStaffOptions(Menu m, BusinessData b)
        {
            var o = State.Biz(b.id); if (o == null) return;
            m.Options.Add(new MenuOption { Label = o.staff.Count >= Data.maxStaff ? $"Staff: {string.Join(", ", o.staff)} (full)" : $"Hire staff · ₦{Data.staffCost:N0} (+{Mathf.RoundToInt(Data.staffBonus * 100)}% income){(o.staff.Count > 0 ? " · " + string.Join(", ", o.staff) : "")}", Enabled = o.staff.Count < Data.maxStaff, Action = () => State.HireStaff(b) });
            for (int i = 0; i < o.staff.Count; i++)
            {
                int idx = i; string name = o.staff[i];
                string shift = GameState.ShiftLabel(State.StaffShift(o, i));
                bool onDuty = State.StaffIsOnDuty(o, i, Clock.Clock);
                m.Options.Add(new MenuOption { Label = $"{name} · {shift}{(onDuty ? " · ON SHIFT" : " · off duty")} (cycle shift)", Action = () => State.CycleStaffShift(b, idx) });
            }
            if (State.RivalOpen) m.Options.Add(new MenuOption { Label = $"{Data.rivalName} dey undercut you — low prices keep your customers", Enabled = false });
        }

        // ---- vehicle customisation at Dayo's ----
        void OpenVehicleMenu(Vehicles.VehicleController v, OwnedVehicle o)
        {
            int price = 0; foreach (var d in Data.dealer) if (d.type == o.type) price = d.price; if (price == 0) price = 1000000;
            var m = new Menu { Title = "Dayo", Subtitle = $"{v.Class.name} · condition {v.Condition:0}%{(o.insured ? " · insured" : "")}{(v.HasNos ? " · NOS FITTED" : "")}", Line = "Wetin we dey do?" };
            m.Options.Add(new MenuOption { Label = "NFS Underground Tuning & Mod Garage (Engine · Turbo · NOS · Aero · Neon · Paint)", Action = () => OpenNfsGarageMenu(v, o) });
            if (v.Condition < 100f) { int c = Mathf.RoundToInt((100f - v.Condition) * 500f * (o.insured ? 0.5f : 1f)); m.Options.Add(new MenuOption { Label = $"Service · ₦{c:N0}{(o.insured ? " (insured)" : "")}", Action = () => { if (!State.Pay(c)) { State.Toast("Not enough money"); return; } v.Condition = 100f; o.cond = 100f; State.Toast("Serviced"); } }); }
            if (!o.insured) { int c = Mathf.RoundToInt(price * 0.08f); m.Options.Add(new MenuOption { Label = $"Insure · ₦{c:N0}", Action = () => { if (!State.Pay(c)) { State.Toast("Not enough money"); return; } o.insured = true; v.Insured = true; State.Toast("Insured: half-price service, half crash damage"); } }); }
            if (Data.liveries.Length > 0) m.Options.Add(new MenuOption { Label = "Livery · ₦20,000", Action = () => { if (!State.Pay(20000)) { State.Toast("Not enough money"); return; } o.livery = (o.livery + 1) % Data.liveries.Length; v.ApplyLivery(Data.liveries[o.livery], o.paintFinish); State.Toast("Fresh paint"); } });
            if ((o.type == "danfo" || o.type == "korope") && Data.slogans.Length > 0) m.Options.Add(new MenuOption { Label = "Slogan · ₦5,000", Action = () => { if (!State.Pay(5000)) { State.Toast("Not enough money"); return; } o.slogan = (o.slogan + 1) % Data.slogans.Length; v.ApplySlogan(Data.slogans[o.slogan]); State.Toast("“" + Data.slogans[o.slogan] + "”"); } });
            m.Options.Add(new MenuOption { Label = "Back", Action = () => { var dayo = Contacts.Get("dayo"); if (dayo != null) OpenDealerMenu(dayo); } });
            OpenMenu(m);
        }
    }
}
