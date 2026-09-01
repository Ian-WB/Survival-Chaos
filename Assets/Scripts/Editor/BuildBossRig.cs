using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.EditorTools
{
    /// <summary>
    /// Authors the boss's firing rig and its three emplacements onto the Boss
    /// prefab, and wires the attacks that use them.
    ///
    /// A tool rather than hand-authoring because most of what it does is
    /// arithmetic on positions that already exist. The muzzles have to move into
    /// a frame that turns with the ship, which is a coordinate change applied to
    /// 32 transforms; the emplacements have to sit at the middle of the bank they
    /// feed, which is the average of that bank's muzzle heights; and the attacks
    /// have to name exactly the muzzles of one bank, which is a partition of the
    /// same 32. All three are things a person would do by eye and get slightly
    /// wrong, and slightly wrong here means shots leaving from inside the hull.
    ///
    /// Re-running is safe. It finds what it made last time and updates it, so it
    /// can be run again after the model changes.
    ///
    /// It fixes a real defect on the way through, and this is the tool that has
    /// to fix it because the fix is the coordinate change. EnemySpaceShip turns
    /// the NAVEBOSS model 90 degrees either way to face the direction of travel,
    /// and RingChase makes the boss reverse every time the player crosses its
    /// bearing - but all 32 muzzles were parented to the Boss root, which only
    /// ever faces the middle of the arena. So the ship turned and the guns did
    /// not, and half the time the boss fired out of its own tail, up to 87 world
    /// units from the barrels. A telegraphed attack is meaningless if the muzzle
    /// is not where the barrel is.
    /// </summary>
    public static class BuildBossRig
    {
        private const string PrefabPath = "Assets/Prefabs/Boss/Boss.prefab";
        private const string MaterialPath = "Assets/Prefabs/Boss/BossEmplacement.mat";
        private const string GlowSourcePath = "Assets/Art/Materials/VFX/PickupGlow.mat";
        private const string SparkPath = "Assets/Prefabs/VFX/Extracted/TinyExplosion.prefab";
        private const string BlastPath = "Assets/Prefabs/VFX/Extracted/EnergyExplosion.prefab";

        private const string WreckagePath = "Assets/Prefabs/Boss/BossWreckage.prefab";

        private const string RigName = "Muzzles";
        private const string OrphanName = "Laser Trigger";
        private const string GlowName = "Glow";
        private const string PlateBodyName = "Plate";

        /// <summary>
        /// The yaw the muzzles were authored at.
        ///
        /// EnemySpaceShip sets the model to +90 while the boss travels one way and
        /// -90 while it travels the other, and +90 is the one the prefab is saved
        /// in - so it is the orientation the pivots were placed against, and it is
        /// the one that puts them at the end of the ship the nose is at. Rotating
        /// the new rig to the same angle leaves every muzzle exactly where it is
        /// today; letting the rig turn is what mirrors them when the ship does.
        /// </summary>
        private const float AuthoredYaw = 90f;

        /// <summary>
        /// How far out along the ring an emplacement sits, in the boss's local
        /// units.
        ///
        /// The hull's own face is at 3.51 and the hull is a trigger that eats
        /// bullets, so anything inside it can never be shot. Mounting the pods
        /// proud of that face is what makes them reachable at all - the bullet
        /// meets the pod before it meets the armour. The boss turns to face
        /// whatever it is chasing, so this is the face the player sees.
        /// </summary>
        private const float PodOutboard = 3.95f;

        /// <summary>
        /// Pod radius in local units, so 7 world units across a playable band of
        /// 89.
        ///
        /// A target the player has to line up with rather than one they cannot
        /// miss. The widest shot upgrade spreads six bullets over 15 world units,
        /// so a centred volley lands about four of six on a pod and a volley aimed
        /// a body-length off lands none - which is the whole reason the
        /// emplacements sit at three different heights.
        /// </summary>
        private const float PodRadius = 0.7f;

        /// <summary>
        /// The visible plate of shed hull, in world units. Carried on a child so
        /// the object the collider sits on can stay at scale one.
        ///
        /// Authored in world units rather than the boss's, because a plate is not
        /// parented to the boss once it is off - it is spawned into the pool like
        /// a projectile and left where it was made.
        /// </summary>
        private static readonly Vector3 PlateSize = new Vector3(20f, 12f, 9f);

        /// <summary>
        /// The plate's hit radius, in world units - about what the boss's own pods
        /// are wide, which is not a coincidence: both are parts of this ship and
        /// both are things the player lines a shot up against.
        ///
        /// A sphere because a rotation cannot change one, so what the plate is
        /// worth hitting does not depend on where it happens to be in its tumble.
        /// That is worth having on its own, but it should not be mistaken for the
        /// fix to repeated contact damage - it was tried as that fix and made no
        /// difference, because the thing that turns is the player. The rule that
        /// one collision costs one hit lives in BossWreckage, where the reasoning
        /// and the measurements are.
        /// </summary>
        private const float PlateRadius = 8f;

        /// <summary>
        /// How fast the boss's shots settle onto the lane the player flies in.
        ///
        /// They have to settle onto it at all because a projectile orbits with
        /// RotateAround, which preserves the distance from the axis it was born
        /// at exactly and forever. The muzzles are spread across the width of a
        /// ship 30 units deep and sit anywhere from 131.6 to 150.2 from the axis,
        /// while the player is pinned to 137.2 and, with their own hitbox and the
        /// shot's, can only be touched between 133.9 and 140.5. Measured on the
        /// rig, 8 of the 32 muzzles were inside that band and 24 were not: three
        /// quarters of every volley was incapable of hitting anyone, which is why
        /// a curtain of twelve arrived as a wall of three.
        ///
        /// The alternative was moving the muzzles onto the lane, and that is the
        /// one thing the rig must not do - the whole point of it is that the shot
        /// leaves the barrel the artist modelled. So the shot leaves the barrel
        /// and then eases in, which is also what every ship in the arena does on
        /// its way to the ring.
        ///
        /// 5 puts the worst-placed muzzle's shot inside the band in 0.275
        /// seconds, by which time it has travelled about 22 degrees of arc and is
        /// clear of a hull 71 units wide. Fast enough to be dangerous while it
        /// still matters, slow enough to read as a shot curving in rather than as
        /// a muzzle in the wrong place.
        /// </summary>
        private const float RoundLaneResponse = 5f;

        /// <summary>
        /// One emplacement and the bank of muzzles it feeds.
        ///
        /// The muzzles are named rather than picked by position, because the
        /// prefab names them and a name survives an edit that a coordinate does
        /// not. The bank's height is not named: it is averaged from the muzzles at
        /// build time, so an emplacement cannot drift away from the guns it is
        /// supposed to belong to.
        /// </summary>
        private sealed class Bank
        {
            public string Name;
            public string Label;
            public int[] Muzzles;

            /// <summary>
            /// How far out to mount the pod. The prow bank is already the
            /// furthest-forward point of the ship and its guns stick out past the
            /// hull on their own, so its pod goes with them rather than back at
            /// the armour line.
            /// </summary>
            public float Outboard = PodOutboard;
        }

        /// <summary>
        /// The three banks, as the pivot numbering on the prefab has them.
        ///
        /// This partition is read off the model, not invented: the 32 muzzles
        /// cluster into three groups at three heights, and those heights land on
        /// the floor, the middle and the ceiling of the band the player can fly
        /// in. Nothing about the fight knew that before.
        /// </summary>
        private static readonly Bank[] Banks =
        {
            new Bank
            {
                Name = "Keel Emplacement",
                Label = "Keel",
                Muzzles = new[] { 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 },
            },
            new Bank
            {
                Name = "Prow Emplacement",
                Label = "Prow",
                Muzzles = new[] { 16, 29, 30, 31 },
                Outboard = 4.6f,
            },
            new Bank
            {
                Name = "Crown Emplacement",
                Label = "Crown",
                Muzzles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
            },
        };

        [MenuItem("Survival Chaos/Rebuild Boss Rig", priority = 55)]
        public static void Rebuild()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            if (root == null)
            {
                Debug.LogError("BuildBossRig found no boss prefab at " + PrefabPath);
                return;
            }

            try
            {
                Transform[] pivots = CollectPivots(root.transform);

                if (pivots == null)
                {
                    return;
                }

                DropOrphan(root.transform);
                PinAltitude(root);

                Transform rig = BuildRig(root.transform);
                MoveMuzzles(rig, pivots);

                Material skin = EmplacementMaterial();
                var pods = new Dictionary<string, BossWeakPoint>();

                foreach (Bank bank in Banks)
                {
                    pods[bank.Label] = BuildPod(root.transform, rig, bank, pivots, skin);
                }

                BuildWreckage(HullSkin(root.transform));

                WireAttacks(root, pivots, pods);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("BuildBossRig rebuilt " + PrefabPath + ": " + pivots.Length +
                          " muzzles on a mirroring rig, " + Banks.Length + " emplacements.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The 32 muzzles, indexed the way the prefab names them: "enemyShootPivot"
        /// is 0 and "enemyShootPivot (7)" is 7.
        ///
        /// Found wherever they currently sit, so this works both on a prefab that
        /// has never been through here and on one that has.
        /// </summary>
        private static Transform[] CollectPivots(Transform root)
        {
            var found = new Dictionary<int, Transform>();

            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
            {
                string name = candidate.name;

                if (!name.StartsWith("enemyShootPivot"))
                {
                    continue;
                }

                string tail = name.Substring("enemyShootPivot".Length).Trim();
                int index = 0;

                if (tail.Length > 0 &&
                    !int.TryParse(tail.Trim('(', ')'), out index))
                {
                    continue;
                }

                found[index] = candidate;
            }

            var pivots = new Transform[found.Count];

            for (int i = 0; i < pivots.Length; i++)
            {
                if (!found.TryGetValue(i, out pivots[i]))
                {
                    Debug.LogError("BuildBossRig found " + found.Count + " muzzles but none numbered " +
                                   i + ". The prefab's pivot names are not a contiguous run.");
                    return null;
                }
            }

            return pivots;
        }

        /// <summary>
        /// Stops the boss climbing to meet the player.
        ///
        /// The whole fight rests on the three banks covering three different
        /// parts of the playable band, and that is only true while the boss holds
        /// an altitude. Chasing the player's, which is what every other enemy
        /// does and what this one was set to do, drags the banks along with it -
        /// so the crown emplacement ends up parked 49 units above the player
        /// forever, which is 49 units above whatever height they climb to, and
        /// the fight loses a third of itself to a weak point that cannot be
        /// reached at all.
        ///
        /// Nothing is lost by it. The hull is 153 units tall against a band of
        /// 89, so it already spans every height the player can occupy: matching
        /// their altitude never changed whether the boss could be flown over,
        /// only where its guns were pointing. This is the one enemy in the game
        /// that is a wall rather than a chaser, and walls hold station.
        /// </summary>
        private static void PinAltitude(GameObject root)
        {
            EnemyMovement movement = root.GetComponent<EnemyMovement>();

            if (movement == null)
            {
                Debug.LogWarning("BuildBossRig found no EnemyMovement on the boss prefab.");
                return;
            }

            var so = new SerializedObject(movement);
            so.FindProperty("chaseRadiusFraction").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Removes the trigger volume left behind when the laser stopped being
        /// gated on one.
        ///
        /// It was added to switch the laser attack on when the player came near,
        /// and the script that read it has since been deleted - so what is on the
        /// prefab is a sphere carrying a broken MonoBehaviour reference, which
        /// Unity reports as a missing script on every load of the boss.
        /// </summary>
        private static void DropOrphan(Transform root)
        {
            Transform orphan = root.Find(OrphanName);

            if (orphan != null)
            {
                Object.DestroyImmediate(orphan.gameObject);
            }
        }

        /// <summary>
        /// The object every muzzle hangs off, turned to face the direction of
        /// travel by the same component that turns the model.
        ///
        /// Literally the same component - EnemySpaceShip, pointed at the same
        /// EnemyMovement. That is the point: the guns and the ship agree because
        /// they are answering one question with one piece of code, in the same
        /// frame, rather than because two copies of a rule were kept in step. The
        /// argument PlayerDash makes about the camera, on a smaller thing.
        /// </summary>
        private static Transform BuildRig(Transform root)
        {
            Transform rig = root.Find(RigName);

            if (rig == null)
            {
                var host = new GameObject(RigName);
                rig = host.transform;
                rig.SetParent(root, worldPositionStays: false);
            }

            rig.localPosition = Vector3.zero;
            rig.localRotation = Quaternion.Euler(0f, AuthoredYaw, 0f);
            rig.localScale = Vector3.one;

            EnemySpaceShip turn = rig.GetComponent<EnemySpaceShip>();

            if (turn == null)
            {
                turn = rig.gameObject.AddComponent<EnemySpaceShip>();
            }

            var so = new SerializedObject(turn);
            so.FindProperty("EnemyShip").objectReferenceValue = root.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            return rig;
        }

        /// <summary>
        /// Moves the muzzles onto the rig without moving them in space.
        ///
        /// worldPositionStays does the coordinate change: the rig is turned 90
        /// degrees from the root, so a muzzle at root-local (x, y, z) lands at rig
        /// local (-z, y, x), and every muzzle ends the operation exactly where it
        /// started. Which is the property worth having - this is a fix for where
        /// shots come from when the boss turns round, and it must not also be a
        /// change to where they come from when it does not.
        /// </summary>
        private static void MoveMuzzles(Transform rig, Transform[] pivots)
        {
            foreach (Transform pivot in pivots)
            {
                if (pivot.parent != rig)
                {
                    pivot.SetParent(rig, worldPositionStays: true);
                }
            }
        }

        /// <summary>
        /// The green the pods are lit in, copied from the pickup glow so it
        /// inherits a material setup already known to render correctly in this
        /// project rather than one assembled from scratch by a script.
        /// </summary>
        private static Material EmplacementMaterial()
        {
            Material skin = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            if (skin == null)
            {
                if (!AssetDatabase.CopyAsset(GlowSourcePath, MaterialPath))
                {
                    Debug.LogError("BuildBossRig could not copy " + GlowSourcePath);
                    return null;
                }

                skin = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            }

            // Bright enough to read against the lava the arena is lit by, which is
            // itself orange - so the one thing the player has to pick out of that
            // is as far from orange as the wheel goes.
            var green = new Color(0.05f, 1.6f, 0.35f);

            skin.SetColor("_UnlitColor", green);
            skin.SetColor("_EmissiveColor", green);
            EditorUtility.SetDirty(skin);

            return skin;
        }

        /// <summary>
        /// One emplacement: a trigger the size of a target, a pod to look at, and
        /// the component that ties the two to the bank of guns behind them.
        ///
        /// Its height is the middle of its own bank, taken from the muzzles rather
        /// than typed in, so the pod cannot drift away from the guns it stands
        /// for. That is what makes the fight's central promise true - that killing
        /// a bank means flying at that bank's height - without anybody having to
        /// maintain it.
        /// </summary>
        private static BossWeakPoint BuildPod(Transform root, Transform rig, Bank bank,
                                               Transform[] pivots, Material skin)
        {
            Transform pod = rig.Find(bank.Name);

            if (pod == null)
            {
                var host = new GameObject(bank.Name);
                pod = host.transform;
                pod.SetParent(rig, worldPositionStays: false);
            }

            float low = float.MaxValue;
            float high = float.MinValue;

            foreach (int muzzle in bank.Muzzles)
            {
                float height = pivots[muzzle].localPosition.y;
                low = Mathf.Min(low, height);
                high = Mathf.Max(high, height);
            }

            // The rig is turned 90 degrees from the root, so what reads as
            // "outboard along the ring" in the root's axes is the rig's +Z, and
            // "towards the middle of the arena" is the rig's -X.
            pod.localPosition = new Vector3(-Curvature(root, bank.Outboard),
                                            (low + high) * 0.5f,
                                            bank.Outboard);
            pod.localRotation = Quaternion.identity;
            pod.localScale = Vector3.one;

            SphereCollider target = pod.GetComponent<SphereCollider>();

            if (target == null)
            {
                target = pod.gameObject.AddComponent<SphereCollider>();
            }

            target.isTrigger = true;
            target.radius = PodRadius;
            target.center = Vector3.zero;

            BuildGlow(pod, skin);

            HitFlash flash = pod.GetComponent<HitFlash>();

            if (flash == null)
            {
                flash = pod.gameObject.AddComponent<HitFlash>();
            }

            // White rather than the hull's red. The pod is already green and the
            // flash has to read as a hit on something green, not as a hue change
            // that could be mistaken for the pod itself changing state.
            var flashProperties = new SerializedObject(flash);
            flashProperties.FindProperty("color").colorValue = new Color(1.6f, 1.6f, 1.6f);
            flashProperties.ApplyModifiedPropertiesWithoutUndo();

            BossWeakPoint weakPoint = pod.GetComponent<BossWeakPoint>();

            if (weakPoint == null)
            {
                weakPoint = pod.gameObject.AddComponent<BossWeakPoint>();
            }

            var properties = new SerializedObject(weakPoint);
            properties.FindProperty("label").stringValue = bank.Label;
            properties.FindProperty("healthPoints").intValue = 50;
            properties.FindProperty("hitEffect").objectReferenceValue = Load<GameObject>(SparkPath);
            properties.FindProperty("explosion").objectReferenceValue = Load<GameObject>(BlastPath);
            properties.FindProperty("glow").objectReferenceValue = pod.Find(GlowName);
            properties.FindProperty("telegraphScale").floatValue = 1.7f;

            // The same number the local position above was built from. The pod
            // re-applies it every frame against the direction the arena's middle
            // is really in, because the rig it hangs off mirrors and this must
            // not - see BossWeakPoint.LateUpdate.
            properties.FindProperty("curvature").floatValue = Curvature(root, bank.Outboard);
            properties.ApplyModifiedPropertiesWithoutUndo();

            return weakPoint;
        }

        /// <summary>
        /// How far back towards the arena's middle a pod has to sit to stay in the
        /// lane, given how far out along the ring it is mounted.
        ///
        /// The offset that puts a pod in front of the hull is measured along the
        /// tangent, and the arena is a circle: 39.5 units along the tangent from a
        /// point on a 137-unit ring leaves you 5.6 units outside the ring, not on
        /// it. Player bullets orbit at a fixed radius and never leave it, so those
        /// 5.6 units come straight off the target - a pod 7 units across loses
        /// most of its height to a miss that is sideways rather than vertical, and
        /// loses it invisibly, because from the camera the pod still looks like it
        /// is where the shots are going.
        ///
        /// Pulling it back by the sagitta puts the pod's middle back on the lane.
        /// It is a fraction of a local unit and it is the difference between a
        /// target with a 7-unit window and one with about 4.
        /// </summary>
        private static float Curvature(Transform root, float outboard)
        {
            float scale = Mathf.Abs(root.localScale.x);

            if (scale <= 0f)
            {
                return 0f;
            }

            float radius = ArenaGeometry.OrbitRadius / scale;
            return Mathf.Sqrt(radius * radius + outboard * outboard) - radius;
        }

        /// <summary>
        /// The visible half of a pod, kept on its own object so a telegraph can
        /// swell it without also swelling the collider and quietly making the pod
        /// easier to hit the moment it becomes dangerous.
        /// </summary>
        private static void BuildGlow(Transform pod, Material skin)
        {
            Transform glow = pod.Find(GlowName);

            if (glow == null)
            {
                var host = new GameObject(GlowName);
                glow = host.transform;
                glow.SetParent(pod, worldPositionStays: false);
            }

            glow.localPosition = Vector3.zero;
            glow.localRotation = Quaternion.identity;

            // A primitive sphere is a unit across, so twice the radius sizes it to
            // the collider it stands for.
            glow.localScale = Vector3.one * (PodRadius * 2f);

            MeshFilter filter = glow.GetComponent<MeshFilter>();

            if (filter == null)
            {
                filter = glow.gameObject.AddComponent<MeshFilter>();
            }

            filter.sharedMesh = PrimitiveMesh(PrimitiveType.Sphere);

            MeshRenderer renderer = glow.GetComponent<MeshRenderer>();

            if (renderer == null)
            {
                renderer = glow.gameObject.AddComponent<MeshRenderer>();
            }

            renderer.sharedMaterial = skin;

            // It is a light source, not a solid. A shadow off an unlit sphere
            // would be a black disc on the hull behind it.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// One of Unity's built-in meshes, borrowed off a primitive that is thrown
        /// away immediately. There is no public path to the built-in meshes, and a
        /// primitive also arrives with a collider the caller has to author itself.
        /// </summary>
        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject temporary = GameObject.CreatePrimitive(type);
            Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temporary);
            return mesh;
        }

        /// <summary>
        /// The material the hull is painted in, taken off the ship rather than
        /// authored.
        ///
        /// The wreckage is meant to be unmistakably part of this boss, and the one
        /// way to guarantee that is to give it the same skin - which lives inside
        /// NAVEBOSS.fbx and has no asset path of its own to name. Reading it back
        /// off the model also means a retexture of the ship carries to its debris
        /// without anyone remembering to.
        ///
        /// The pods are skipped rather than the first renderer taken, because by
        /// the time this runs a previous build's emplacements are already on the
        /// prefab and one of them may come first.
        /// </summary>
        private static Material HullSkin(Transform root)
        {
            foreach (Renderer part in root.GetComponentsInChildren<Renderer>(true))
            {
                if (part.GetComponentInParent<BossWeakPoint>() == null)
                {
                    return part.sharedMaterial;
                }
            }

            Debug.LogWarning("BuildBossRig found no hull renderer to take the wreckage skin from.");
            return null;
        }

        /// <summary>
        /// Removes the mesh and collider an older build of this tool left on the
        /// plate's root, so re-running converges on one shape rather than piling
        /// a new one on top of the old.
        /// </summary>
        private static void StripRootShape(GameObject plate)
        {
            Object.DestroyImmediate(plate.GetComponent<BoxCollider>());
            Object.DestroyImmediate(plate.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(plate.GetComponent<MeshFilter>());
        }

        /// <summary>
        /// The visible slab under a plate's root.
        ///
        /// Separate from the root so the root can stay at scale one for the
        /// collider's sake, and parented to it so the tumble turns both together -
        /// a mesh spinning inside a hitbox that stayed put would be the same lie
        /// in the other direction.
        /// </summary>
        private static void BuildPlateBody(Transform root, Material skin)
        {
            Transform body = root.Find(PlateBodyName);

            if (body == null)
            {
                var host = new GameObject(PlateBodyName);
                body = host.transform;
                body.SetParent(root, worldPositionStays: false);
            }

            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            body.localScale = PlateSize;

            MeshFilter filter = body.GetComponent<MeshFilter>();

            if (filter == null)
            {
                filter = body.gameObject.AddComponent<MeshFilter>();
            }

            filter.sharedMesh = PrimitiveMesh(PrimitiveType.Cube);

            MeshRenderer renderer = body.GetComponent<MeshRenderer>();

            if (renderer == null)
            {
                renderer = body.gameObject.AddComponent<MeshRenderer>();
            }

            if (skin != null)
            {
                renderer.sharedMaterial = skin;
            }
        }

        /// <summary>
        /// Authors the plate of hull the boss sheds during its second act.
        ///
        /// A prefab of its own rather than a child of the boss, because it has to
        /// outlive the moment it is made: it is spawned into the pool where the
        /// emplacement was and then left there while the ship cruises on. Tagged
        /// Boss so the player already treats it as part of the ship - contact
        /// costs a hit and does not consume it - which is the correct behaviour
        /// and needs no new branch over there.
        ///
        /// Built here rather than by hand for the same reason the pods are: the
        /// numbers that matter are measured against the arena, and the tool is
        /// where that reasoning is written down.
        /// </summary>
        private static void BuildWreckage(Material skin)
        {
            bool existed = AssetDatabase.LoadAssetAtPath<GameObject>(WreckagePath) != null;

            GameObject plate = existed
                ? PrefabUtility.LoadPrefabContents(WreckagePath)
                : new GameObject("Boss Wreckage");

            try
            {
                plate.tag = "Boss";

                // The root stays at one and the shape hangs off a child. A sphere
                // collider takes its radius from the largest component of its own
                // object's scale, so a root sized 20 by 12 by 9 would quietly turn
                // the radius below into twenty times itself. On separate objects
                // the radius is the radius.
                plate.transform.localScale = Vector3.one;

                // An earlier version of this tool put the mesh and a box collider
                // on the root. Re-running is supposed to be safe, and safe here
                // means the prefab ends up as this code describes rather than as
                // the union of every version that has ever built it - a leftover
                // box would still be a trigger tagged Boss, and would still charge
                // the player for touching a plate they were nowhere near.
                StripRootShape(plate);

                BuildPlateBody(plate.transform, skin);

                SphereCollider hit = plate.GetComponent<SphereCollider>();

                if (hit == null)
                {
                    hit = plate.AddComponent<SphereCollider>();
                }

                hit.isTrigger = true;
                hit.radius = PlateRadius;
                hit.center = Vector3.zero;

                Rigidbody body = plate.GetComponent<Rigidbody>();

                if (body == null)
                {
                    body = plate.AddComponent<Rigidbody>();
                }

                // Kinematic, like every other trigger in the game. Unity needs one
                // body in a trigger pair and the player has none, so without this
                // a plate would be scenery the player flies straight through.
                body.isKinematic = true;
                body.useGravity = false;

                BossWreckage wreckage = plate.GetComponent<BossWreckage>();

                if (wreckage == null)
                {
                    wreckage = plate.AddComponent<BossWreckage>();
                }

                var properties = new SerializedObject(wreckage);
                properties.FindProperty("healthPoints").intValue = 3;
                properties.FindProperty("lifeSeconds").floatValue = 16f;
                properties.FindProperty("hitEffect").objectReferenceValue = Load<GameObject>(SparkPath);
                properties.FindProperty("explosion").objectReferenceValue = Load<GameObject>(BlastPath);
                properties.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(plate, WreckagePath);
            }
            finally
            {
                if (existed)
                {
                    PrefabUtility.UnloadPrefabContents(plate);
                }
                else
                {
                    Object.DestroyImmediate(plate);
                }
            }
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                Debug.LogWarning("BuildBossRig found nothing at " + path);
            }

            return asset;
        }

        /// <summary>
        /// One authored attack. The fight, in five rows.
        /// </summary>
        private sealed class Volley
        {
            public string Label;
            public BossFirePattern Pattern;
            public BossPhaseMask Phases;

            /// <summary>The emplacement that silences it, or null for one nothing can.</summary>
            public string Bank;

            /// <summary>Muzzle numbers, or null for every muzzle on the ship.</summary>
            public int[] Muzzles;

            /// <summary>
            /// Which of the two identical projectile pairs to fire. They differ
            /// only in being separate prefabs, which gives the lance a pool of its
            /// own rather than competing with the volleys for one.
            /// </summary>
            public bool LanceRound;

            /// <summary>
            /// What this attack releases, when it is not one of the four rounds.
            ///
            /// The emitter asks an attack for a prefab per travel direction and
            /// does not care what comes back, so a pattern that sheds hull rather
            /// than firing at the player needs no new field on the attack itself -
            /// only a way of saying so here. Both directions get the same object,
            /// because a plate is not aimed.
            /// </summary>
            public string Round;

            public float InitialDelay;
            public float Interval;
            public float StepSeconds = 0.12f;
            public int OpenRows = 1;
            public float ChargeSeconds = 1.2f;
            public float BurstSeconds = 0.4f;
            public float BurstInterval = 0.06f;
            public float RamSpeedScale = 3f;
        }

        /// <summary>
        /// The fight.
        ///
        /// The three armoured attacks are one per bank, and each is densest in its
        /// own third of the playable band - so the emplacement that has to be shot
        /// to stop an attack sits in the middle of that attack's own fire. That is
        /// the risk and the reward, and it comes out of where the guns already are
        /// rather than out of a difficulty number.
        ///
        /// The cadences are set against a lap rather than against each other. A
        /// boss bullet goes round the ring in 4.5 seconds, so the curtain's 6
        /// brings the previous wall back just before the next one leaves, and the
        /// player spends most of the phase between two of them.
        /// </summary>
        private static readonly Volley[] Fight =
        {
            new Volley
            {
                Label = "Keel - Curtain",
                Pattern = BossFirePattern.Curtain,
                Phases = BossPhaseMask.Armoured,
                Bank = "Keel",
                Muzzles = new[] { 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28 },
                InitialDelay = 1.5f,
                Interval = 6f,
                OpenRows = 1,
            },
            new Volley
            {
                Label = "Crown - Rake",
                Pattern = BossFirePattern.Sequence,
                Phases = BossPhaseMask.Armoured,
                Bank = "Crown",
                Muzzles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
                InitialDelay = 0.5f,
                Interval = 4f,
                StepSeconds = 0.12f,
            },
            new Volley
            {
                Label = "Prow - Lance",
                Pattern = BossFirePattern.Lance,
                Phases = BossPhaseMask.Armoured,
                Bank = "Prow",
                Muzzles = new[] { 16, 29, 30, 31 },
                LanceRound = true,
                InitialDelay = 2.5f,
                Interval = 5f,
                ChargeSeconds = 1.2f,
                BurstSeconds = 0.4f,
                BurstInterval = 0.06f,
            },
            new Volley
            {
                Label = "Hull - Ram",
                Pattern = BossFirePattern.Ram,
                Phases = BossPhaseMask.Exposed,
                Muzzles = new int[0],
                InitialDelay = 1f,
                Interval = 7f,
                ChargeSeconds = 1f,
                BurstSeconds = 3f,
                RamSpeedScale = 3f,
            },
            new Volley
            {
                Label = "Hull - Wreckage",
                Pattern = BossFirePattern.Wreckage,
                Phases = BossPhaseMask.Exposed,
                Round = WreckagePath,
                Muzzles = new int[0],
                InitialDelay = 0.5f,

                // Life over interval is how many plates stand at once, and a third
                // of those sit at any one height. 16 over 1.8 holds about nine, so
                // three at the player's own altitude - which against a player
                // lapping the ring every 12.3 seconds is a forced move every four
                // seconds or so.
                Interval = 1.8f,
            },
            new Volley
            {
                Label = "Scuttle",
                Pattern = BossFirePattern.Simultaneous,
                Phases = BossPhaseMask.Scuttle,
                InitialDelay = 0f,
                Interval = 1.2f,
            },
        };

        private static void WireAttacks(GameObject root, Transform[] pivots,
                                        Dictionary<string, BossWeakPoint> pods)
        {
            BossEmitter emitter = root.GetComponent<BossEmitter>();

            if (emitter == null)
            {
                Debug.LogError("BuildBossRig found no BossEmitter on the boss prefab.");
                return;
            }

            GameObject[] rounds =
            {
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 3.prefab"),
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 4.prefab"),
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 5.prefab"),
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 6.prefab"),
            };

            TuneRounds(rounds);

            var so = new SerializedObject(emitter);

            so.FindProperty("hullSpark").objectReferenceValue = Load<GameObject>(SparkPath);
            so.FindProperty("scuttleThreshold").intValue = 30;
            so.FindProperty("phaseChangeSilence").floatValue = 2f;

            SerializedProperty attacks = so.FindProperty("attacks");
            attacks.arraySize = Fight.Length;

            for (int i = 0; i < Fight.Length; i++)
            {
                Volley volley = Fight[i];
                SerializedProperty entry = attacks.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("label").stringValue = volley.Label;
                entry.FindPropertyRelative("pattern").intValue = (int)volley.Pattern;
                entry.FindPropertyRelative("phases").intValue = (int)volley.Phases;

                BossWeakPoint pod = null;
                if (volley.Bank != null)
                {
                    pods.TryGetValue(volley.Bank, out pod);
                }

                entry.FindPropertyRelative("weakPoint").objectReferenceValue = pod;

                GameObject left;
                GameObject right;

                if (volley.Round != null)
                {
                    left = right = Load<GameObject>(volley.Round);
                }
                else
                {
                    int offset = volley.LanceRound ? 2 : 0;
                    left = rounds[offset];
                    right = rounds[offset + 1];
                }

                entry.FindPropertyRelative("projectileWhenLeft").objectReferenceValue = left;
                entry.FindPropertyRelative("projectileWhenRight").objectReferenceValue = right;

                SerializedProperty muzzles = entry.FindPropertyRelative("pivots");
                int[] chosen = volley.Muzzles ?? AllMuzzles(pivots.Length);
                muzzles.arraySize = chosen.Length;

                for (int m = 0; m < chosen.Length; m++)
                {
                    muzzles.GetArrayElementAtIndex(m).objectReferenceValue = pivots[chosen[m]];
                }

                entry.FindPropertyRelative("initialDelay").floatValue = volley.InitialDelay;
                entry.FindPropertyRelative("interval").floatValue = volley.Interval;
                entry.FindPropertyRelative("stepSeconds").floatValue = volley.StepSeconds;
                entry.FindPropertyRelative("openRows").intValue = volley.OpenRows;
                entry.FindPropertyRelative("chargeSeconds").floatValue = volley.ChargeSeconds;
                entry.FindPropertyRelative("burstSeconds").floatValue = volley.BurstSeconds;
                entry.FindPropertyRelative("burstInterval").floatValue = volley.BurstInterval;
                entry.FindPropertyRelative("ramSpeedScale").floatValue = volley.RamSpeedScale;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Sets the lane approach on the boss's own projectile prefabs.
        ///
        /// Here rather than by hand because it belongs with the muzzle placement
        /// it exists to compensate for - the number is only defensible next to
        /// the spread of the rig, and the two would drift apart the moment they
        /// lived in different files. The player's rounds are deliberately not
        /// touched: their gun is on the lane already, and easing their shots onto
        /// it would move every bullet they fire.
        /// </summary>
        private static void TuneRounds(GameObject[] rounds)
        {
            foreach (GameObject round in rounds)
            {
                if (round == null)
                {
                    continue;
                }

                if (!round.TryGetComponent(out ShootScript shot))
                {
                    Debug.LogWarning("BuildBossRig found no ShootScript on " + round.name);
                    continue;
                }

                var properties = new SerializedObject(shot);
                properties.FindProperty("laneResponse").floatValue = RoundLaneResponse;
                properties.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(round);
            }

            AssetDatabase.SaveAssets();
        }

        private static int[] AllMuzzles(int count)
        {
            var every = new int[count];

            for (int i = 0; i < count; i++)
            {
                every[i] = i;
            }

            return every;
        }
    }
}
