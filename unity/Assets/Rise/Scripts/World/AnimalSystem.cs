using System.Collections.Generic;
using UnityEngine;
using Rise.Data;
using Rise.Entities;

namespace Rise.World
{
    // Goats around the markets, chickens near compounds, stray dogs and cats.
    // The dog is the rigged GLB with its walk and idle clips; the cat is the static GLB; goats and chickens are
    // primitives. Each wanders a small patch around its home spot.
    public class AnimalSystem : MonoBehaviour
    {
        class Animal { public Transform t; public Vector3 home, target; public float speed, pause; public Animation anim; public string kind; }
        readonly List<Animal> animals = new List<Animal>();
        public int Count => animals.Count;
        public int Dogs { get; private set; }
        WorldData data;

        public void Init(WorldData world, Transform parent)
        {
            data = world;
            var root = new GameObject("Animals").transform; root.SetParent(parent, false);
            var rng = new System.Random(17);
            var dog = Resources.Load<GameObject>("Models/Animals/dog"); var cat = Resources.Load<GameObject>("Models/Animals/cat");
            var dogWalk = Resources.Load<AnimationClip>("Characters/dog_walk"); var dogIdle = Resources.Load<AnimationClip>("Characters/dog_idle");
            foreach (var a in data.animals)
            {
                var go = new GameObject(a.kind); go.transform.SetParent(root, false);
                var pos = a.World();
                var roadOn = data.RoadAt(pos, 2.5f);
                if (roadOn != null)
                    pos = roadOn.Point(roadOn.Along(pos), roadOn.k + (roadOn.width * 0.5f + 4.5f));
                pos.y = data.DeckHeightAt(pos);
                go.transform.position = pos;
                var an = new Animal { t = go.transform, home = pos, target = pos, kind = a.kind, speed = a.kind == "chicken" ? 0.6f : a.kind == "goat" ? 0.9f : 1.6f, pause = (float)rng.NextDouble() * 3f };
                if (a.kind == "dog" && dog != null) { Model(go.transform, dog, 0.62f); Dogs++; if (dogWalk != null) { var anim = go.GetComponentInChildren<Animation>(); if (anim == null) anim = go.AddComponent<Animation>(); anim.AddClip(dogWalk, "walk"); if (dogIdle != null) anim.AddClip(dogIdle, "idle"); anim.wrapMode = WrapMode.Loop; anim.playAutomatically = false; an.anim = anim; } }
                else if (a.kind == "cat" && cat != null) Model(go.transform, cat, 0.35f);
                else Primitive(go.transform, a.kind);
                animals.Add(an);
            }
            // Stray cats and goats by the markets.
            foreach (var l in data.landmarks) if (l.kind == "market" && animals.Count < data.animals.Length + 6)
            {
                if (cat != null)
                {
                    var go = new GameObject("cat"); go.transform.SetParent(root, false);
                    var pos = l.World() + new Vector3(6f, 0f, 9f); pos.y = data.DeckHeightAt(pos); go.transform.position = pos;
                    Model(go.transform, cat, 0.35f);
                    animals.Add(new Animal { t = go.transform, home = pos, target = pos, kind = "cat", speed = 1.1f });
                }
                {
                    var ggo = new GameObject("goat"); ggo.transform.SetParent(root, false);
                    var gpos = l.World() + new Vector3(-7f, 0f, 10f); gpos.y = data.DeckHeightAt(gpos); ggo.transform.position = gpos;
                    Primitive(ggo.transform, "goat");
                    animals.Add(new Animal { t = ggo.transform, home = gpos, target = gpos, kind = "goat", speed = 0.85f });
                }
            }
        }

        static void Model(Transform parent, GameObject prefab, float height)
        {
            var m = Instantiate(prefab, parent, false); m.name = "Model";
            CharacterRig.RemapGlbMaterialsToLit(m, 0.25f);
            var b = CharacterRig.Bounds(m); float k = b.size.y > 0.01f ? height / b.size.y : 1f;
            m.transform.localScale = Vector3.one * k;
            b = CharacterRig.Bounds(m);
            m.transform.localPosition = new Vector3(-(b.center.x - parent.position.x), -(b.min.y - parent.position.y), -(b.center.z - parent.position.z));
            foreach (var c in m.GetComponentsInChildren<Collider>()) Destroy(c);
        }

        static void Primitive(Transform parent, string kind)
        {
            Color body = kind == "goat" ? new Color(0.85f, 0.82f, 0.75f) : new Color(0.88f, 0.58f, 0.26f);
            var b = GameObject.CreatePrimitive(PrimitiveType.Capsule); b.transform.SetParent(parent, false); Destroy(b.GetComponent<Collider>());
            if (kind == "goat")
            {
                b.transform.localPosition = new Vector3(0, 0.45f, 0); b.transform.localRotation = Quaternion.Euler(90, 0, 0); b.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
                b.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(body);
                var h = GameObject.CreatePrimitive(PrimitiveType.Sphere); h.transform.SetParent(parent, false); Destroy(h.GetComponent<Collider>());
                h.transform.localPosition = new Vector3(0, 0.62f, 0.45f); h.transform.localScale = Vector3.one * 0.22f;
                h.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(body * 0.8f);
                foreach (var x in new[] { -0.12f, 0.12f }) foreach (var z in new[] { -0.3f, 0.3f })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); leg.transform.SetParent(parent, false); Destroy(leg.GetComponent<Collider>());
                    leg.transform.localPosition = new Vector3(x, 0.15f, z); leg.transform.localScale = new Vector3(0.06f, 0.15f, 0.06f);
                    leg.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(body * 0.7f);
                }
            }
            else
            {
                b.transform.localPosition = new Vector3(0f, 0.16f, 0f); b.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); b.transform.localScale = new Vector3(0.18f, 0.15f, 0.16f);
                b.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(body);
                var h = GameObject.CreatePrimitive(PrimitiveType.Sphere); h.transform.SetParent(parent, false); Destroy(h.GetComponent<Collider>());
                h.transform.localPosition = new Vector3(0f, 0.25f, 0.12f); h.transform.localScale = Vector3.one * 0.11f;
                h.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(body * 0.95f);
                var comb = GameObject.CreatePrimitive(PrimitiveType.Cube); comb.transform.SetParent(parent, false); Destroy(comb.GetComponent<Collider>());
                comb.transform.localPosition = new Vector3(0f, 0.31f, 0.12f); comb.transform.localScale = new Vector3(0.025f, 0.045f, 0.07f);
                comb.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(new Color(0.82f, 0.18f, 0.14f));
                var beak = GameObject.CreatePrimitive(PrimitiveType.Cube); beak.transform.SetParent(parent, false); Destroy(beak.GetComponent<Collider>());
                beak.transform.localPosition = new Vector3(0f, 0.24f, 0.19f); beak.transform.localScale = new Vector3(0.03f, 0.025f, 0.05f);
                beak.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(new Color(0.95f, 0.76f, 0.18f));
                foreach (var x in new[] { -0.05f, 0.05f })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); leg.transform.SetParent(parent, false); Destroy(leg.GetComponent<Collider>());
                    leg.transform.localPosition = new Vector3(x, 0.05f, 0f); leg.transform.localScale = new Vector3(0.022f, 0.05f, 0.022f);
                    leg.GetComponent<MeshRenderer>().sharedMaterial = Mannequin.Mat(new Color(0.90f, 0.68f, 0.18f));
                }
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var a in animals)
            {
                if (a.pause > 0f) { a.pause -= dt; a.anim?.Play("idle"); continue; }
                var to = a.target - a.t.position; to.y = 0f;
                if (to.magnitude < 0.3f)
                {
                    a.pause = Random.Range(1f, 5f);
                    var r = Random.insideUnitCircle * (a.kind == "chicken" ? 3.5f : 7f);
                    a.target = a.home + new Vector3(r.x, 0f, r.y);
                    if (data.InWater(a.target) || data.RoadAt(a.target, 2.5f) != null) a.target = a.home;
                    continue;
                }
                if (a.anim != null && !a.anim.IsPlaying("walk")) a.anim.CrossFade("walk", 0.2f);
                var nextPos = a.t.position + to.normalized * a.speed * dt;
                nextPos.y = data.DeckHeightAt(nextPos);
                a.t.position = nextPos;
                a.t.rotation = Quaternion.Slerp(a.t.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 1f - Mathf.Exp(-6f * dt));
            }
        }
    }
}
