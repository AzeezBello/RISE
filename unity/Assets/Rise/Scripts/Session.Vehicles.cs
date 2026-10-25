using UnityEngine;
using Rise.Data;
using Rise.Vehicles;
using Rise.World;

namespace Rise
{
    // Dayo's dealership at Ladipo: vehicle sales, protection weapon, owned vehicles that persist where you
    // parked them, and refuelling at the Mobil filling station.
    public partial class Session
    {
        public const int WeaponPrice = 18000;

        void OpenDealerMenu(ContactSystem.Contact dayo)
        {
            var m = new Menu { Title = dayo.data.name, Subtitle = dayo.data.role, Line = "Bring the motor, I go check am. Or buy one — papers dey inside." };
            foreach (var d in Data.dealer)
            {
                var cls = Data.VehicleClass(d.type); if (cls == null) continue; var deal = d;
                m.Options.Add(new MenuOption { Label = $"{cls.name} · ₦{d.price:N0}", Action = () => BuyVehicle(deal.type) });
            }
            bool hasWeapon = State.Has("weapon");
            m.Options.Add(new MenuOption { Label = hasWeapon ? "Protection weapon · carried" : $"Protection weapon · ₦{WeaponPrice:N0}", Enabled = !hasWeapon, Action = () => BuyProtectionWeapon() });
            foreach (var wId in new[] { "koboko", "baton", "cutlass" })
            {
                var spec = FindWeaponSpec(wId);
                if (!spec.HasValue) continue;
                var w = spec.Value; string id = w.Id;
                bool owned = State.Has(id); bool eq = State.S.equippedWeapon == id;
                m.Options.Add(new MenuOption { Label = owned ? $"{w.Name} · {(eq ? "equipped" : "equip")}" : $"{w.Name} · ₦{w.Price:N0} ({w.Desc})", Enabled = !eq, Action = () => BuyFightWeapon(id) });
            }
            AddActionOpsToMenu(m, dayo.data.id);
            m.Options.Add(new MenuOption
            {
                Label = "NFS Underground Tuning & Mod Garage (Engine · Turbo · NOS · Aero · Neon · Paint)",
                Action = () => OpenNfsGarageMenu(Vehicle)
            });
            foreach (var v in Parked)
            {
                if (!v.Owned || string.IsNullOrEmpty(v.OwnedId) || (v.transform.position - dayo.pos).magnitude > 30f) continue;
                OwnedVehicle o = null; foreach (var ov in State.S.vehicles) if (ov.id == v.OwnedId) o = ov;
                if (o == null) continue; var vv = v; var oo = o;
                m.Options.Add(new MenuOption { Label = $"My {v.Class.name} · service, insure, NFS garage", Action = () => OpenVehicleMenu(vv, oo) });
            }
            m.Options.Add(new MenuOption { Label = "Not now", Action = null });
            OpenMenu(m);
        }

        public bool BuyProtectionWeapon()
        {
            if (State.Has("weapon")) { State.Toast("You already have a weapon"); return false; }
            if (!State.Pay(WeaponPrice)) { State.Toast("Not enough money"); return false; }
            State.AddItem("weapon");
            if (string.IsNullOrEmpty(State.S.equippedWeapon)) EquipWeapon("weapon");
            State.Toast("Weapon added to your inventory");
            return true;
        }

        public VehicleController BuyVehicle(string type)
        {
            var cls = Data.VehicleClass(type); if (cls == null) return null;
            int price = 0; foreach (var d in Data.dealer) if (d.type == type) price = d.price;
            if (!State.Pay(price)) { State.Toast($"You need ₦{price:N0} across cash and bank"); return null; }
            // Delivered to your gate if you have a home, else to the Ladipo forecourt.
            Vector3 at; float yaw = 180f;
            var home = State.Home;
            if (home != null) { at = home.Door + new Vector3(6f, 0f, 3f); }
            else if (Data.TryPlace("ladipo", out var ladipo)) { at = ladipo.World() + new Vector3(12f, 0f, 14f + State.S.vehicles.Count * 5f); }
            else at = Player.transform.position + Player.transform.right * 4f;
            var owned = new OwnedVehicle { id = System.DateTime.UtcNow.Ticks.ToString("x"), type = type, x = at.x, y = at.y, z = at.z, yaw = yaw };
            State.S.vehicles.Add(owned);
            var vc = SpawnOwned(owned);
            State.AddRep("business", 2);
            State.Toast($"{cls.name} is yours — parked at {(home != null ? "your gate" : "Ladipo")}");
            State.Message("dayo", $"{cls.name} don ready. Papers dey inside. Bring am back when e need service.");
            return vc;
        }

        public VehicleController SpawnOwned(OwnedVehicle o)
        {
            var cls = Data.VehicleClass(o.type) ?? Data.VehicleClass("car");
            var go = VehicleBodies.Build(cls, transform, new System.Random(o.id.GetHashCode()), out var wheels);
            var pos = new Vector3(o.x, 0f, o.z); pos.y = Data.DeckHeightAt(pos) + 0.12f;
            go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0f, o.yaw, 0f);
            var vc = go.AddComponent<VehicleController>(); vc.Setup(cls, wheels); vc.Owned = true; vc.OwnedId = o.id; vc.Condition = o.cond; vc.Insured = o.insured;
            vc.Place(pos, Quaternion.Euler(0f, o.yaw, 0f));
            vc.ApplyOwnedCustomizations(o, Data);
            Parked.Add(vc);
            return vc;
        }

        public void SpawnOwnedFromSave() { foreach (var o in State.S.vehicles) SpawnOwned(o); }

        // Owned vehicles stay where you parked them between sessions.
        void SaveVehicles()
        {
            foreach (var v in Parked)
            {
                if (!v.Owned || string.IsNullOrEmpty(v.OwnedId)) continue;
                foreach (var o in State.S.vehicles) if (o.id == v.OwnedId) { var p = v.transform.position; o.x = p.x; o.y = p.y; o.z = p.z; o.yaw = v.transform.eulerAngles.y; o.cond = v.Condition; }
            }
        }

        bool NearFuel(Vector3 pos) => Data.TryPlace("fuel", out var f) && (f.World() - pos).magnitude < 16f;

        void Refuel()
        {
            if (State.S.fuel >= 99f) { State.Toast("Tank is full"); return; }
            if (!State.Pay(Data.meta.refuel)) { State.Toast("Not enough money to refuel"); return; }
            State.S.fuel = 100f; State.Toast($"Refuelled · ₦{Data.meta.refuel:N0}");
        }

        public bool EmergencyRefuel(int cost = 4000)
        {
            if (State.S.fuel >= 99f) { State.Toast("Fuel tank is already full"); return false; }
            if (!State.Pay(cost)) { State.Toast($"Need ₦{cost:N0} for emergency roadside jerrycan refuel"); return false; }
            State.S.fuel = 100f;
            State.Toast($"Emergency jerrycan refuelled tank to 100% · -₦{cost:N0}");
            return true;
        }

        // ---- Need for Speed (NFS) Underground Tuning, Performance, Aero & Paint Garage ----
        GameObject nfsBayRoot;
        Transform nfsTurntable, nfsShowcaseCar;
        readonly System.Collections.Generic.List<Material> nfsBayMaterials = new System.Collections.Generic.List<Material>();

        public bool InNfsGarageBay => nfsBayRoot != null;

        (VehicleController vc, OwnedVehicle ov) ResolveGarageVehicle(VehicleController preferred = null, OwnedVehicle preferredOwned = null)
        {
            VehicleController vc = preferred != null ? preferred : Vehicle;
            if (vc == null)
            {
                float bestD = float.MaxValue;
                Vector3 p = Player != null ? Player.transform.position : Vector3.zero;
                foreach (var parked in Parked)
                {
                    if (parked == null || !parked.Owned) continue;
                    float d = (parked.transform.position - p).sqrMagnitude;
                    if (d < bestD) { bestD = d; vc = parked; }
                }
                if (vc == null && Parked.Count > 0) vc = Parked[0];
            }
            if (vc == null) return (null, null);

            OwnedVehicle ov = preferredOwned;
            if (ov == null && !string.IsNullOrEmpty(vc.OwnedId))
            {
                foreach (var item in State.S.vehicles)
                    if (item.id == vc.OwnedId) { ov = item; break; }
            }
            if (ov == null)
            {
                // Register vehicle into player's garage record so custom upgrades persist
                var pos = vc.transform.position;
                ov = new OwnedVehicle
                {
                    id = string.IsNullOrEmpty(vc.OwnedId) ? System.DateTime.UtcNow.Ticks.ToString("x") : vc.OwnedId,
                    type = vc.Class != null ? vc.Class.id : "car",
                    cond = vc.Condition,
                    x = pos.x, y = pos.y, z = pos.z, yaw = vc.transform.eulerAngles.y,
                    insured = vc.Insured,
                    engineStage = vc.EngineStage,
                    turboStage = vc.TurboStage,
                    suspensionStage = vc.SuspensionStage,
                    nosStage = vc.NosStage,
                    spoiler = vc.Spoiler,
                    skirtKit = vc.SkirtKit,
                    roofScoop = vc.RoofScoop,
                    rims = vc.Rims,
                    neonColor = vc.NeonColor,
                    paintFinish = vc.PaintFinish
                };
                vc.Owned = true;
                vc.OwnedId = ov.id;
                State.S.vehicles.Add(ov);
            }
            return (vc, ov);
        }

        void SyncGarageShowcase(VehicleController vc, OwnedVehicle ov)
        {
            if (vc == null || ov == null) return;
            vc.ApplyOwnedCustomizations(ov, Data);
            if (vc.HasNos && NosCharge < 100f) NosCharge = 100f;
            if (nfsTurntable != null && vc.Class != null)
            {
                if (nfsShowcaseCar != null) Destroy(nfsShowcaseCar.gameObject);
                var go = VehicleBodies.Build(vc.Class, nfsTurntable, new System.Random(ov.id.GetHashCode()), out var wheels);
                go.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                go.transform.localRotation = Quaternion.identity;
                var previewVc = go.AddComponent<VehicleController>();
                previewVc.Setup(vc.Class, wheels);
                previewVc.ApplyOwnedCustomizations(ov, Data);
                nfsShowcaseCar = go.transform;
            }
        }

        public void EnterNfsGarageBay(VehicleController preferred = null, OwnedVehicle preferredOwned = null)
        {
            var (vc, ov) = ResolveGarageVehicle(preferred, preferredOwned);
            if (vc == null) { State.Toast("Bring or buy a vehicle first to enter the NFS Tuning Bay"); return; }

            if (nfsBayRoot == null)
            {
                Vector3 bayCenter = new Vector3(0f, 220f, 0f);
                nfsBayRoot = new GameObject("NFS_Underground_Garage_Bay");
                var lit = ShaderLib.Lit;
                var unlit = ShaderLib.Unlit;
                Material Mat(Color c, float gloss = 0.7f, float met = 0.3f)
                {
                    var m = new Material(lit) { color = c };
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    m.SetFloat("_Smoothness", gloss);
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", met);
                    nfsBayMaterials.Add(m);
                    return m;
                }
                Material Neon(Color c)
                {
                    var m = new Material(unlit) { color = c };
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    nfsBayMaterials.Add(m);
                    return m;
                }
                void BoxPart(string n, Vector3 lp, Vector3 sc, Material m)
                {
                    var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    g.name = n; g.transform.SetParent(nfsBayRoot.transform, false);
                    g.transform.position = bayCenter + lp; g.transform.localScale = sc;
                    g.GetComponent<MeshRenderer>().sharedMaterial = m;
                    Destroy(g.GetComponent<Collider>());
                }

                BoxPart("GarageFloor", new Vector3(0f, -0.5f, 0f), new Vector3(22f, 1f, 22f), Mat(new Color(0.06f, 0.08f, 0.11f), 0.92f, 0.55f));
                BoxPart("GarageCeiling", new Vector3(0f, 6.2f, 0f), new Vector3(22f, 0.3f, 22f), Mat(new Color(0.04f, 0.05f, 0.07f), 0.3f, 0.2f));
                BoxPart("BackWall", new Vector3(0f, 3f, 9.5f), new Vector3(22f, 6.5f, 0.4f), Mat(new Color(0.09f, 0.11f, 0.15f), 0.4f));
                BoxPart("LeftWall", new Vector3(-9.5f, 3f, 0f), new Vector3(0.4f, 6.5f, 22f), Mat(new Color(0.09f, 0.11f, 0.15f), 0.4f));
                BoxPart("RightWall", new Vector3(9.5f, 3f, 0f), new Vector3(0.4f, 6.5f, 22f), Mat(new Color(0.09f, 0.11f, 0.15f), 0.4f));

                // Neon ceiling & wall light tubes (Cyan + Magenta + Gold NFS Underground vibe)
                BoxPart("NeonBeamCyanL", new Vector3(-3.8f, 5.8f, 0f), new Vector3(0.16f, 0.10f, 14f), Neon(new Color(0f, 0.95f, 1f)));
                BoxPart("NeonBeamCyanR", new Vector3(3.8f, 5.8f, 0f), new Vector3(0.16f, 0.10f, 14f), Neon(new Color(0f, 0.95f, 1f)));
                BoxPart("NeonWallPink", new Vector3(0f, 4.2f, 9.2f), new Vector3(14f, 0.16f, 0.10f), Neon(new Color(1f, 0.15f, 0.65f)));
                BoxPart("NeonWallGold", new Vector3(0f, 2.2f, 9.2f), new Vector3(10f, 0.12f, 0.10f), Neon(new Color(1f, 0.78f, 0.15f)));

                // Rotating Turntable Podium
                var ttGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ttGo.name = "TurntablePodium";
                ttGo.transform.SetParent(nfsBayRoot.transform, false);
                ttGo.transform.position = bayCenter + new Vector3(0f, 0.08f, 1.8f);
                ttGo.transform.localScale = new Vector3(6.6f, 0.08f, 6.6f);
                ttGo.GetComponent<MeshRenderer>().sharedMaterial = Mat(new Color(0.12f, 0.15f, 0.20f), 0.95f, 0.75f);
                Destroy(ttGo.GetComponent<Collider>());

                var rimGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rimGo.name = "TurntableRim";
                rimGo.transform.SetParent(nfsBayRoot.transform, false);
                rimGo.transform.position = bayCenter + new Vector3(0f, 0.03f, 1.8f);
                rimGo.transform.localScale = new Vector3(6.9f, 0.03f, 6.9f);
                rimGo.GetComponent<MeshRenderer>().sharedMaterial = Neon(new Color(0f, 0.92f, 1f));
                Destroy(rimGo.GetComponent<Collider>());

                nfsTurntable = new GameObject("TurntablePivot").transform;
                nfsTurntable.SetParent(nfsBayRoot.transform, false);
                nfsTurntable.position = bayCenter + new Vector3(0f, 0.16f, 1.8f);

                var l1 = new GameObject("KeyLight").AddComponent<Light>();
                l1.transform.SetParent(nfsBayRoot.transform, false);
                l1.transform.position = bayCenter + new Vector3(0f, 5.2f, -1.5f);
                l1.type = LightType.Point; l1.range = 24f; l1.intensity = 3.2f; l1.color = new Color(0.92f, 0.96f, 1f);

                var l2 = new GameObject("RimLight").AddComponent<Light>();
                l2.transform.SetParent(nfsBayRoot.transform, false);
                l2.transform.position = bayCenter + new Vector3(4.5f, 2.8f, 4.5f);
                l2.type = LightType.Point; l2.range = 18f; l2.intensity = 2.0f; l2.color = new Color(0f, 0.85f, 1f);

                // 3D Garage Workshop Props
                Entities.CharacterRig.InstantiateGlb("Props/wood_furniture", "GarageWorkBench", bayCenter + new Vector3(-7.4f, 0.02f, 6.2f), new Vector3(2.4f, 1.3f, 1.4f), nfsBayRoot.transform, 90f, false);
                Entities.CharacterRig.InstantiateGlb("Props/air_conditioner", "GarageBayAC", bayCenter + new Vector3(0f, 4.8f, 9.1f), new Vector3(1.3f, 0.6f, 0.45f), nfsBayRoot.transform, 180f, false);
                Entities.CharacterRig.InstantiateGlb("Props/newspaper_stack", "TuningCatalogs", bayCenter + new Vector3(-7.1f, 0.95f, 6.2f), new Vector3(0.55f, 0.35f, 0.55f), nfsBayRoot.transform, 15f, false);
            }

            SyncGarageShowcase(vc, ov);
            if (Camera != null && nfsTurntable != null)
            {
                Camera.Target = nfsTurntable;
                Camera.Yaw = 28f;
                Camera.Pitch = 14f;
                Camera.Distance = 6.2f;
                Camera.Snap();
            }
            OpenNfsGarageMenu(vc, ov);
        }

        public void LeaveNfsGarageBay()
        {
            if (nfsBayRoot != null)
            {
                Destroy(nfsBayRoot);
                nfsBayRoot = null;
                nfsTurntable = null;
                nfsShowcaseCar = null;
            }
            for (int i = 0; i < nfsBayMaterials.Count; i++)
                if (nfsBayMaterials[i] != null) Destroy(nfsBayMaterials[i]);
            nfsBayMaterials.Clear();
            if (Camera != null)
            {
                Camera.SetDriving(Vehicle != null ? Vehicle.transform : null);
                Camera.Snap();
            }
        }

        public void UpdateNfsGarageBay(float dt)
        {
            if (nfsTurntable == null) return;
            nfsTurntable.Rotate(Vector3.up, 24f * dt, Space.World);
        }

        public void OpenNfsGarageMenu(VehicleController preferred = null, OwnedVehicle preferredOwned = null)
        {
            var (vc, ov) = ResolveGarageVehicle(preferred, preferredOwned);
            if (vc == null || ov == null)
            {
                State.Toast("You need a vehicle to use the NFS Tuning Garage");
                return;
            }
            SyncGarageShowcase(vc, ov);

            float topKmh = vc.Class.max * (1f + ov.engineStage * 0.12f) * (1f + ov.turboStage * 0.06f) * 3.6f;
            float nosKmh = vc.HasNos ? vc.Class.boost * (1f + ov.engineStage * 0.12f) * (1f + ov.turboStage * 0.10f) * 3.6f : topKmh;
            string nosBadge = vc.IsPremiumRacing
                ? $"Factory Twin-NOS + Stage {ov.nosStage}"
                : (ov.nosStage > 0 ? $"Custom Stage {ov.nosStage} NOS Fitted" : "No NOS (Standard Vehicle)");

            var m = new Menu
            {
                Title = $"NFS Underground Garage · {vc.Class.name}",
                Subtitle = $"Top Speed {topKmh:0} km/h (NOS {nosKmh:0} km/h) · {nosBadge} · Condition {vc.Condition:0}%",
                Line = "Upgrade Engine, Twin-Turbo, Coilover Suspension & Brembo Brakes, install a Custom NOS Nitrous Oxide System, or fit 3D Spoilers, Widebody Skirts, Neon Underglow & Custom Paint Finishes."
            };

            if (!InNfsGarageBay)
            {
                m.Options.Add(new MenuOption
                {
                    Label = "★ Enter 3D NFS Underground Turntable Showcase Bay",
                    Action = () => EnterNfsGarageBay(vc, ov)
                });
            }
            else
            {
                m.Options.Add(new MenuOption
                {
                    Label = "← Exit 3D Turntable Bay back to Street",
                    Action = () => { LeaveNfsGarageBay(); OpenNfsGarageMenu(vc, ov); }
                });
            }

            m.Options.Add(new MenuOption
            {
                Label = $"1. Performance Tuning (Engine Stg {ov.engineStage}/3 · Turbo Stg {ov.turboStage}/3 · Suspension Stg {ov.suspensionStage}/3 · NOS Stg {ov.nosStage}/3)",
                Action = () => OpenNfsPerformanceMenu(vc, ov)
            });
            m.Options.Add(new MenuOption
            {
                Label = $"2. Body, Aero & Rims (Spoiler {ov.spoiler}/3 · Widebody Skirts {ov.skirtKit}/2 · Roof Scoop {ov.roofScoop}/2 · Rims {ov.rims}/5)",
                Action = () => OpenNfsAeroMenu(vc, ov)
            });
            m.Options.Add(new MenuOption
            {
                Label = $"3. Paint Booth, Finishes & 3D Neon Underglow (Finish #{ov.paintFinish + 1} · {(ov.neonColor >= 0 ? $"Neon #{ov.neonColor + 1}" : "No Neon")})",
                Action = () => OpenNfsPaintMenu(vc, ov)
            });

            if (vc.Condition < 100f || State.S.fuel < 99f)
            {
                m.Options.Add(new MenuOption
                {
                    Label = "Full Dyno Overhaul & Race Fuel Fill · ₦6,000 (100% Condition & 100% Fuel)",
                    Action = () =>
                    {
                        if (!State.Pay(6000)) { State.Toast("Need ₦6,000 for full overhaul"); return; }
                        vc.Condition = 100f; ov.cond = 100f; State.S.fuel = 100f;
                        State.Toast($"{vc.Class.name} restored to 100% condition & full tank!");
                        OpenNfsGarageMenu(vc, ov);
                    }
                });
            }

            m.Options.Add(new MenuOption
            {
                Label = "Done / Drive Out",
                Action = () => { if (InNfsGarageBay) LeaveNfsGarageBay(); }
            });
            OpenMenu(m);
        }

        void OpenNfsPerformanceMenu(VehicleController vc, OwnedVehicle ov)
        {
            var m = new Menu
            {
                Title = $"NFS Dyno & Performance · {vc.Class.name}",
                Subtitle = $"Engine Stg {ov.engineStage}/3 · Turbo Stg {ov.turboStage}/3 · Suspension Stg {ov.suspensionStage}/3 · NOS Stg {ov.nosStage}/3",
                Line = "Only Premium Racing Cars come with factory NOS — install a Custom NOS Kit here to unlock [SHIFT] Twin-Flame Nitrous on any build!"
            };

            int[] engPrices = { 25000, 55000, 95000 };
            if (ov.engineStage < 3)
            {
                int next = ov.engineStage + 1, cost = engPrices[ov.engineStage];
                m.Options.Add(new MenuOption
                {
                    Label = $"Install Stage {next} Forged Engine & ECU Remap (+12% Top Speed & Accel) · ₦{cost:N0}",
                    Action = () =>
                    {
                        if (!State.Pay(cost)) { State.Toast($"Need ₦{cost:N0}"); OpenNfsPerformanceMenu(vc, ov); return; }
                        ov.engineStage = next; SyncGarageShowcase(vc, ov);
                        State.Toast($"Installed Stage {next} Engine & ECU Remap!");
                        OpenNfsPerformanceMenu(vc, ov);
                    }
                });
            }
            else m.Options.Add(new MenuOption { Label = "Stage 3 Race Engine & ECU Remap · MAXED ✓", Enabled = false });

            int[] turboPrices = { 30000, 65000, 110000 };
            if (ov.turboStage < 3)
            {
                int next = ov.turboStage + 1, cost = turboPrices[ov.turboStage];
                m.Options.Add(new MenuOption
                {
                    Label = $"Install Stage {next} Twin-Scroll Turbocharger (+15% Accel & Boost) · ₦{cost:N0}",
                    Action = () =>
                    {
                        if (!State.Pay(cost)) { State.Toast($"Need ₦{cost:N0}"); OpenNfsPerformanceMenu(vc, ov); return; }
                        ov.turboStage = next; SyncGarageShowcase(vc, ov);
                        State.Toast($"Installed Stage {next} Twin-Scroll Turbocharger!");
                        OpenNfsPerformanceMenu(vc, ov);
                    }
                });
            }
            else m.Options.Add(new MenuOption { Label = "Stage 3 Twin-Scroll Turbocharger · MAXED ✓", Enabled = false });

            int[] suspPrices = { 20000, 45000, 80000 };
            if (ov.suspensionStage < 3)
            {
                int next = ov.suspensionStage + 1, cost = suspPrices[ov.suspensionStage];
                m.Options.Add(new MenuOption
                {
                    Label = $"Install Stage {next} Track Coilovers & Brembo Brakes (+15% Grip & Braking) · ₦{cost:N0}",
                    Action = () =>
                    {
                        if (!State.Pay(cost)) { State.Toast($"Need ₦{cost:N0}"); OpenNfsPerformanceMenu(vc, ov); return; }
                        ov.suspensionStage = next; SyncGarageShowcase(vc, ov);
                        State.Toast($"Installed Stage {next} Track Coilovers & Brembo Brakes!");
                        OpenNfsPerformanceMenu(vc, ov);
                    }
                });
            }
            else m.Options.Add(new MenuOption { Label = "Stage 3 Track Coilovers & Brembo Brakes · MAXED ✓", Enabled = false });

            int[] nosPrices = { 35000, 70000, 120000 };
            if (ov.nosStage < 3)
            {
                int next = ov.nosStage + 1, cost = nosPrices[ov.nosStage];
                m.Options.Add(new MenuOption
                {
                    Label = $"Install Stage {next} NOS Nitrous Oxide System (Unlocks [SHIFT] NOS Boost + Twin Exhausts) · ₦{cost:N0}",
                    Action = () =>
                    {
                        if (!State.Pay(cost)) { State.Toast($"Need ₦{cost:N0}"); OpenNfsPerformanceMenu(vc, ov); return; }
                        ov.nosStage = next; NosCharge = 100f; SyncGarageShowcase(vc, ov);
                        State.Toast($"Installed Stage {next} NOS Nitrous Oxide System! Press [SHIFT] while driving for Nitrous!");
                        OpenNfsPerformanceMenu(vc, ov);
                    }
                });
            }
            else m.Options.Add(new MenuOption { Label = "Stage 3 Direct-Port NOS Nitrous System · MAXED ✓", Enabled = false });

            m.Options.Add(new MenuOption { Label = "Back to NFS Garage", Action = () => OpenNfsGarageMenu(vc, ov) });
            OpenMenu(m);
        }

        void OpenNfsAeroMenu(VehicleController vc, OwnedVehicle ov)
        {
            string[] spoilerNames = { "Stock (None)", "Street Lip Spoiler", "Carbon GT Track Wing", "Time-Attack Dual-Deck Aero Wing" };
            string[] skirtNames = { "Stock Body", "Street Carbon Splitter & Side Skirts", "Widebody Aero Kit + Rear Diffuser + Twin Titanium Exhausts" };
            string[] scoopNames = { "None", "Rally Roof Air Scoop", "Twin-Vortex Carbon Roof Scoop" };
            string[] rimNames = { "Factory Alloy", "24K Gold BBS Mesh", "Forged Carbon Black", "Chrome Deep-Dish", "Track Anodized Red", "Nismo Cyber Blue" };

            var m = new Menu
            {
                Title = $"NFS Body, Aero & Rims · {vc.Class.name}",
                Subtitle = $"Wing: {spoilerNames[Mathf.Clamp(ov.spoiler, 0, 3)]} · Body: {skirtNames[Mathf.Clamp(ov.skirtKit, 0, 2)]} · Rims: {rimNames[Mathf.Clamp(ov.rims, 0, 5)]}",
                Line = "All aero parts, spoilers, splitters, exhausts, roof scoops, and custom rims attach immediately in 3D."
            };

            for (int sIdx = 0; sIdx < spoilerNames.Length; sIdx++)
            {
                int captured = sIdx;
                int cost = sIdx == 0 ? 0 : sIdx * 15000;
                bool active = ov.spoiler == sIdx;
                m.Options.Add(new MenuOption
                {
                    Label = active ? $"Spoiler: {spoilerNames[sIdx]} · INSTALLED ✓" : $"Fit {spoilerNames[sIdx]} · ₦{cost:N0}",
                    Enabled = !active,
                    Action = () =>
                    {
                        if (cost > 0 && !State.Pay(cost)) { State.Toast($"Need ₦{cost:N0}"); OpenNfsAeroMenu(vc, ov); return; }
                        ov.spoiler = captured; SyncGarageShowcase(vc, ov);
                        State.Toast($"Fitted {spoilerNames[captured]}");
                        OpenNfsAeroMenu(vc, ov);
                    }
                });
            }

            for (int kIdx = 0; kIdx < skirtNames.Length; kIdx++)
            {
                int captured = kIdx;
                int cost = kIdx == 0 ? 0 : kIdx * 22000;
                bool active = ov.skirtKit == kIdx;
                m.Options.Add(new MenuOption
                {
                    Label = active ? $"Body Kit: {skirtNames[kIdx]} · INSTALLED ✓" : $"Fit {skirtNames[kIdx]} · ₦{cost:N0}",
                    Enabled = !active,
                    Action = () =>
                    {
                        if (cost > 0 && !State.Pay(cost)) { State.Toast($"Need ₦{cost:N0}"); OpenNfsAeroMenu(vc, ov); return; }
                        ov.skirtKit = captured; SyncGarageShowcase(vc, ov);
                        State.Toast($"Fitted {skirtNames[captured]}");
                        OpenNfsAeroMenu(vc, ov);
                    }
                });
            }

            int nextScoop = (ov.roofScoop + 1) % scoopNames.Length;
            m.Options.Add(new MenuOption
            {
                Label = $"Cycle Roof Scoop → {scoopNames[nextScoop]} · ₦12,000",
                Action = () =>
                {
                    if (nextScoop > 0 && !State.Pay(12000)) { State.Toast("Need ₦12,000"); OpenNfsAeroMenu(vc, ov); return; }
                    ov.roofScoop = nextScoop; SyncGarageShowcase(vc, ov);
                    State.Toast($"Roof Scoop: {scoopNames[nextScoop]}");
                    OpenNfsAeroMenu(vc, ov);
                }
            });

            int nextRim = (ov.rims + 1) % rimNames.Length;
            m.Options.Add(new MenuOption
            {
                Label = $"Cycle Custom Rims → {rimNames[nextRim]} · ₦18,000",
                Action = () =>
                {
                    if (nextRim > 0 && !State.Pay(18000)) { State.Toast("Need ₦18,000"); OpenNfsAeroMenu(vc, ov); return; }
                    ov.rims = nextRim; SyncGarageShowcase(vc, ov);
                    State.Toast($"Fitted {rimNames[nextRim]} Rims");
                    OpenNfsAeroMenu(vc, ov);
                }
            });

            m.Options.Add(new MenuOption { Label = "Back to NFS Garage", Action = () => OpenNfsGarageMenu(vc, ov) });
            OpenMenu(m);
        }

        void OpenNfsPaintMenu(VehicleController vc, OwnedVehicle ov)
        {
            string[] finishNames = { "Factory Gloss", "Metallic Flake", "Pearlescent Candy", "Stealth Matte", "24K Gold / Chrome Mirror" };
            (string name, string hex)[] swatches =
            {
                ("Rosso Corsa Ferrari Red", "#d91b1b"),
                ("Eko Midnight Obsidian", "#0d1117"),
                ("Lagos Danfo Gold", "#f29b0a"),
                ("Nismo Bayside Blue", "#1d4ed8"),
                ("Emerald Naija Green", "#15803d"),
                ("Tokyo Drift Purple", "#6d28d9"),
                ("Miami Vice Cyan", "#06b6d4"),
                ("24K Champagne Gold", "#d4af37"),
                ("Arctic Pearl White", "#f1f5f9"),
                ("Sunset Burnt Orange", "#ea580c"),
            };
            string[] neonNames = { "Cyber Cyan", "Tokyo Magenta", "Nitrous Lime", "Lagos Gold Amber", "Ultra Violet", "Diablo Red" };

            var m = new Menu
            {
                Title = $"NFS Paint & Neon Booth · {vc.Class.name}",
                Subtitle = $"Finish: {finishNames[Mathf.Clamp(ov.paintFinish, 0, finishNames.Length - 1)]} · Neon: {(ov.neonColor >= 0 && ov.neonColor < neonNames.Length ? neonNames[ov.neonColor] : "Off")}",
                Line = "Choose a custom paint finish, respray exterior body panels (windows & tires stay untouched), or fit 3D Neon Underglow."
            };

            int nextFinish = (ov.paintFinish + 1) % finishNames.Length;
            m.Options.Add(new MenuOption
            {
                Label = $"Cycle Paint Finish → {finishNames[nextFinish]} · ₦15,000",
                Action = () =>
                {
                    if (!State.Pay(15000)) { State.Toast("Need ₦15,000"); OpenNfsPaintMenu(vc, ov); return; }
                    ov.paintFinish = nextFinish;
                    if (string.IsNullOrEmpty(ov.customHex)) ov.customHex = "#d91b1b";
                    SyncGarageShowcase(vc, ov);
                    State.Toast($"Applied {finishNames[nextFinish]} finish!");
                    OpenNfsPaintMenu(vc, ov);
                }
            });

            foreach (var sw in swatches)
            {
                var captured = sw;
                bool active = ov.customHex == sw.hex;
                m.Options.Add(new MenuOption
                {
                    Label = active ? $"Paint: {sw.name} ({sw.hex}) · CURRENT ✓" : $"Respray {sw.name} · ₦12,000",
                    Enabled = !active,
                    Action = () =>
                    {
                        if (!State.Pay(12000)) { State.Toast("Need ₦12,000 for custom respray"); OpenNfsPaintMenu(vc, ov); return; }
                        ov.customHex = captured.hex;
                        SyncGarageShowcase(vc, ov);
                        State.Toast($"Resprayed in {captured.name}!");
                        OpenNfsPaintMenu(vc, ov);
                    }
                });
            }

            for (int nIdx = 0; nIdx < neonNames.Length; nIdx++)
            {
                int captured = nIdx;
                bool active = ov.neonColor == nIdx;
                m.Options.Add(new MenuOption
                {
                    Label = active ? $"3D Neon Underglow: {neonNames[nIdx]} · FITTED ✓" : $"Fit {neonNames[nIdx]} 3D Neon Underglow · ₦20,000",
                    Enabled = !active,
                    Action = () =>
                    {
                        if (!State.Pay(20000)) { State.Toast("Need ₦20,000 for Neon Underglow"); OpenNfsPaintMenu(vc, ov); return; }
                        ov.neonColor = captured;
                        SyncGarageShowcase(vc, ov);
                        State.Toast($"Installed {neonNames[captured]} 3D Neon Underglow!");
                        OpenNfsPaintMenu(vc, ov);
                    }
                });
            }

            if (ov.neonColor >= 0)
            {
                m.Options.Add(new MenuOption
                {
                    Label = "Remove Neon Underglow",
                    Action = () => { ov.neonColor = -1; SyncGarageShowcase(vc, ov); OpenNfsPaintMenu(vc, ov); }
                });
            }

            m.Options.Add(new MenuOption { Label = "Back to NFS Garage", Action = () => OpenNfsGarageMenu(vc, ov) });
            OpenMenu(m);
        }
    }
}
