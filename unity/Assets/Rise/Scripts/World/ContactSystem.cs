using System.Collections.Generic;
using UnityEngine;
using Rise.Data;
using Rise.Entities;

namespace Rise.World
{
    // Story and job contacts stand outside their places as people: Baba K outside Ojuelegba Junction, Amaka at the
    // Iponri yard, Dayo at Ladipo… They carry a name label and turn to face you as you approach.
    public class ContactSystem : MonoBehaviour
    {
        public class Contact { public ContactData data; public Transform t; public Vector3 pos; public Quaternion rest; }

        readonly List<Contact> contacts = new List<Contact>();
        public IReadOnlyList<Contact> Contacts => contacts;
        public int FemaleContactCount { get; private set; }
        public int PoliticianContactCount { get; private set; }
        public int FraudsterContactCount { get; private set; }
        Transform player, arrow;
        string arrowFor;

        static readonly HashSet<string> FemaleIds = new HashSet<string>
        {
            "amaka", "nkechi", "mum", "sibling", "shrine_host", "makoko_fisher", "epe_fisher",
            "lekki_keeper", "oyingbo_trader", "agege_baker", "ojo_trader", "sisi", "seyi", "sopi",
            "janet", "olosho", "cbn_director", "efcc_commander", "efcc_ikeja_lead", "bisi_fashion",
            "zainab_banker", "adaeze_tech", "kemi_fitness", "titi_chef", "funke_lawyer",
            "blessing_nurse", "halima_mall", "ifunanya_vi", "senator_lagos_central"
        };

        public static readonly HashSet<string> PoliticianIds = new HashSet<string>
        {
            "presidential_envoy", "lagos_governor", "senator_lagos_central", "senator_lagos_west",
            "hon_rep_surulere", "lga_chairman_surulere"
        };

        public static readonly HashSet<string> FraudsterIds = new HashSet<string>
        {
            "fraudster_hk_boss", "fraudster_419_chief"
        };

        public static bool IsPoliticianContact(string id) => !string.IsNullOrEmpty(id) && PoliticianIds.Contains(id);
        public static bool IsFraudsterContact(string id) => !string.IsNullOrEmpty(id) && FraudsterIds.Contains(id);

        public static bool IsFemaleContact(string id, string name)
        {
            if (!string.IsNullOrEmpty(id) && FemaleIds.Contains(id)) return true;
            if (string.IsNullOrEmpty(name)) return false;
            return name.StartsWith("Mama ") || name.StartsWith("Aunty ") || name.StartsWith("Iya ") ||
                   name.StartsWith("Sisi ") || name.StartsWith("Hajiya ") || name.StartsWith("Lady ") ||
                   name.StartsWith("Matron ") || name.StartsWith("Chef ") || name.Contains("(Mrs.)");
        }

        public void Init(WorldData world, Transform playerTransform, Transform parent)
        {
            player = playerTransform;
            FemaleContactCount = 0;
            PoliticianContactCount = 0;
            FraudsterContactCount = 0;
            var root = new GameObject("Contacts").transform; root.SetParent(parent, false);
            var rng = new System.Random(11);
            foreach (var c in world.contacts)
            {
                if (!world.ContactSpot(c, out var pos)) continue;
                pos.y = world.DeckHeightAt(pos) + 0.3f;
                var go = new GameObject("Contact " + c.name);
                go.transform.SetParent(root, false);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0, 180f, 0);   // face the street (north, +z)
                var accent = Mannequin.Hex(c.color, "#ffc52f");
                Color top = Color.Lerp(accent, new Color(0.9f, 0.85f, 0.75f), 0.35f);
                Color pants = Mannequin.Hex(world.pants.Length > 0 ? world.pants[rng.Next(world.pants.Length)] : "#26312d");
                Color skin = Mannequin.Hex(world.skins.Length > 0 ? world.skins[rng.Next(world.skins.Length)] : "#8a5a3c");
                bool isFemale = IsFemaleContact(c.id, c.name);
                bool isPol = IsPoliticianContact(c.id);
                bool isFraud = IsFraudsterContact(c.id);
                if (isFemale) FemaleContactCount++;
                if (isPol) PoliticianContactCount++;
                if (isFraud) FraudsterContactCount++;
                bool gele = isFemale && (c.name.StartsWith("Mama ") || c.name.StartsWith("Aunty ") || c.name.StartsWith("Iya ") || c.name.StartsWith("Hajiya ") || c.id == "cbn_director" || c.id == "senator_lagos_central");
                Color? cap = gele ? (Color?)accent : isPol ? (Color?)new Color(0.85f, 0.68f, 0.18f) : (c.id == "babak" || c.id == "ikorodu_trader" ? (Color?)new Color(0.12f, 0.23f, 0.37f) : null);
                int fabric = isPol ? 5 : Mathf.Abs(c.id.GetHashCode()) % Mathf.Max(1, world.fabrics.Length);
                var look = new Look
                {
                    Fabric = fabric, Pants = pants, Skin = skin, Hair = Mannequin.Hex("#120b08"), Cap = cap,
                    Outfit = isFemale ? (Mathf.Abs(c.id.GetHashCode()) % 2 == 0 ? 9 : 10) : isPol ? 3 : isFraud ? 8 : 0,
                    BodyType = isPol ? 2 : isFemale ? (gele ? 1 : 0) : 1,
                    HairStyle = isFemale ? 3 : 0,
                    Accessory = isFraud ? 4 : cap.HasValue ? 1 : 0,
                    FacialHair = (!isFemale && (c.id == "babak" || isPol)) ? 1 : 0
                };
                foreach (var f in world.family) if (f.id == c.id) { look.Outfit = f.outfit; look.Fabric = f.fabric; look.BodyType = f.bodyType; look.HairStyle = f.hair; if (world.skins.Length > 0) look.Skin = Mannequin.Hex(world.skins[Mathf.Clamp(f.skin, 0, world.skins.Length - 1)]); if (world.pants.Length > 0) look.Pants = Mannequin.Hex(world.pants[Mathf.Clamp(f.pants, 0, world.pants.Length - 1)]); }
                bool useFullRig = CharacterRig.Available && (!GameBootstrap.LightWebGL || c.id == "babak" || c.id == "amaka" || c.id == "dayo" || c.id == "mum" || c.id == "sibling" || c.id == "peter" || c.id == "nkechi");
                if (useFullRig) CharacterRig.Build(go.transform, look, 0, isFemale ? 1.72f : 1.80f);
                else Mannequin.Build(go.transform, top, pants, skin, cap);
                Mannequin.Label(go.transform, c.name, accent);
                contacts.Add(new Contact { data = c, t = go.transform, pos = pos, rest = go.transform.rotation });
            }
        }

        static string CustomCharacterModelFor(string id, bool isFemale, bool isPol, bool isFraud)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (id == "presidential_envoy" || id == "lagos_governor") return "obama";
            if (isPol || id == "cbn_governor" || id == "bank_manager") return "executive_suit";
            if (id == "army_commander" || id == "navy_commander" || id == "barracks_qm") return "hecu_soldier";
            if (isFraud || id == "efcc_lead") return "commando_bill";
            if (id == "bisi_fashion" || id == "ifunanya_vi") return "pink_mini_dress";
            if (id == "halima_mall" || id == "sisi") return "bluevinyl";
            if (id == "kemi_fitness") return "runner";
            if (id == "coach") return "neymar_footballer";
            if (id == "teslim_scout" || id == "stadium_captain") return "zlatan_footballer";
            if (id == "surulere_striker") return "red_footballer";
            if (id == "campus_winger") return "lowpoly_footballer";
            if (id == "shrine_sax" || id == "afrobeat_king" || id == "dj_eko" || id.Contains("shrine") || id.Contains("music")) return "rastaman_singer";
            if (id == "seyi" || id == "sopi") return "tifa_vip";
            if (id == "adaeze_tech" || id == "cafeguy") return "casual_confidence";
            if (id == "bathopoly" || id == "yaba-painter") return "chambray_street";
            return null;
        }

        // The current objective's contact carries a bobbing yellow diamond pointer over their head.
        public void SetArrow(string contactId)
        {
            if (arrow == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Objective arrow";
                go.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
                go.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
                var m = new Material(ShaderLib.Lit) { color = new Color(1f, 0.77f, 0.18f) }; m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", new Color(1f, 0.77f, 0.18f) * 1.2f);
                go.GetComponent<MeshRenderer>().sharedMaterial = m; Destroy(go.GetComponent<Collider>());
                arrow = go.transform; arrow.SetParent(transform, false);
            }
            arrowFor = contactId;
            arrow.gameObject.SetActive(!string.IsNullOrEmpty(contactId));
        }

        public Contact Get(string id) { foreach (var c in contacts) if (c.data.id == id) return c; return null; }
        public void Move(string id, Vector3 pos) { var c = Get(id); if (c == null) return; c.pos = pos; c.t.position = pos; }

        public Contact Nearest(Vector3 pos, float maxDist)
        {
            Contact best = null; float bestD = maxDist * maxDist;
            foreach (var c in contacts) { float d = (c.pos - pos).sqrMagnitude; if (d < bestD) { bestD = d; best = c; } }
            return best;
        }

        void Update()
        {
            if (player == null) return;
            if (arrow != null && arrow.gameObject.activeSelf)
                foreach (var c in contacts) if (c.data.id == arrowFor) { arrow.position = c.pos + Vector3.up * (2.9f + Mathf.Sin(Time.time * 4f) * 0.15f); arrow.Rotate(Vector3.up, Time.deltaTime * 90f); break; }
            foreach (var c in contacts)
            {
                var to = player.position - c.pos; to.y = 0f;
                if (to.sqrMagnitude < 14f * 14f && to.sqrMagnitude > 0.01f)
                    c.t.rotation = Quaternion.Slerp(c.t.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 1f - Mathf.Exp(-5f * Time.deltaTime));
                else
                    c.t.rotation = Quaternion.Slerp(c.t.rotation, c.rest, 1f - Mathf.Exp(-2f * Time.deltaTime));
            }
        }
    }
}
