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

        /// <summary>
        /// The keel's rounds, one per direction of travel like the rest.
        ///
        /// The keel bank fires out of a row of long vertical slots in the
        /// underside of the hull, and a slot is not a gun port. Discs read as
        /// something that shape would actually throw, and they cost nothing to
        /// tell apart from the crown's fire at a glance - which matters, because
        /// the two banks are the fight's way of saying that height is the thing
        /// you are being asked about.
        ///
        /// Sized on the prefab at 0.7 world units across, which is the same as a
        /// pod, and that number is not cosmetic. The keel's twelve muzzles are
        /// three rows of four, 1.265 units apart vertically; the player's ship is
        /// 0.409 tall. So the clear gap between two firing rows is 1.265 minus
        /// whatever the round is, and the round the curtain used to fire was 0.176
        /// tall - leaving 1.09 units between every row.
        ///
        /// Which means the curtain has never been a wall. FireCurtain's own
        /// summary describes a wall with one gap in it, stepping upward so it can
        /// be learned, and the player could in fact fly between any two rows at
        /// any time. At 0.7 the gap is 0.615 and the ship needs 0.409 of it, so the
        /// open row finally is the way through rather than one of four.
        ///
        /// That makes disc size a difficulty dial and not a look, and it is the
        /// first thing to lower if the curtain reads as unfair rather than as
        /// tight. Going the other way, anything past about 0.85 closes the gaps
        /// entirely and the attack stops being threadable at all.
        /// </summary>
        private const string DiscLeftPath = "Assets/Prefabs/Boss/boss_disc 1.prefab";

        private const string DiscRightPath = "Assets/Prefabs/Boss/boss_disc 2.prefab";

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
        /// Hit points per emplacement, and with them the length of the first act.
        ///
        /// This was 50, and 50 is why the fight played easy. The player's gun
        /// ends a run at six bullets a volley on a 0.15s floor - forty damage a
        /// second - so three pods at 50 were about six seconds of shooting. The
        /// three armoured attacks were on 4, 5 and 6 second intervals, so the act
        /// ended after roughly two cycles of each. The cadences never got to say
        /// anything.
        ///
        /// 150 buys about nineteen seconds at a normal late-run rate, which is
        /// six or seven cycles of each attack now that the intervals below have
        /// come down with it. Health and cadence are the two ways to buy the same
        /// thing here and they are not interchangeable: health decides how many
        /// cycles the player sees, cadence decides how hard each one is. This
        /// went to 200 first, and came back to 150 with the intervals tightened
        /// to match - the same pressure over less time, which reads as a fight
        /// rather than as a health bar.
        ///
        /// 100 since 8 September, on the first playtest verdict that was about
        /// length rather than difficulty: the fight felt good but ran a bit long.
        /// The cadences were called good, so they are not what moved - this is.
        ///
        /// The whole cut lands on this act rather than being spread across the
        /// three, and the reason is the shared pool. Every point of damage in the
        /// fight comes off the boss's one health pool, pods included, so three
        /// pods at 150 were 450 of the 900 - half the fight spent on one act, and
        /// the act you also have to fly between three heights to finish, so its
        /// share of the clock was larger than its share of the bar. At 100 it is
        /// 300, about thirteen seconds rather than nineteen.
        ///
        /// The other two acts were untouched at that point, which is what the
        /// matching 150 off Boss.asset bought: the exposed act kept its 360.
        ///
        /// 80 since the second verdict the same day, and that one was not about
        /// this act at all - the fight still ran long, and the fault was named as
        /// the total rather than as any one phase. So this cut is spread. The
        /// asset came down 750 to 600 and this came 100 to 80, which leaves the
        /// armoured act 240 and the exposed act the remaining 270.
        ///
        /// The exposed act is the one with a floor under it. It needs roughly a
        /// whole plate lifetime - about eleven seconds - before the steady state
        /// its wreckage cadence was tuned for exists at all. Measured against the
        /// roughly 23 points a second the earlier acts actually came down at, 270
        /// is about 11.7 seconds: still over the floor, but no longer comfortably
        /// over it. If a run reports that the wreckage never builds up, this is
        /// the reason, and the next cut should come out of the pods instead.
        /// </summary>
        private const int PodHealth = 80;

        /// <summary>
        /// Health remaining when every magazine still aboard goes off at once.
        ///
        /// Scaled with the rest rather than retuned. At 30 against a 300 point
        /// boss it was the last tenth of the fight, and 90 against 900 kept it
        /// exactly that.
        ///
        /// It stayed at 90 through both cuts on 8 September - 900 to 750, then
        /// 750 to 600 - so it is nearer a seventh of the bar now. That is
        /// deliberate, and it was reconsidered the second time rather than
        /// carried over unexamined. What matters about the last act is how long
        /// it lasts, not what fraction of the bar it is: scaling it to 72 would
        /// have taken well under a second off the one act nobody has asked to be
        /// shorter.
        /// </summary>
        private const int ScuttleThreshold = 90;

        /// <summary>
        /// The pod light's colour, range and output.
        ///
        /// Held apart from the material's own red: the sphere is what the player
        /// looks at and wants to clip white at its centre, while this is what
        /// lands on the plating and wants to stay a colour. So the light is the
        /// same hue at a sane display value rather than the material's HDR one.
        ///
        /// The range is set against the ship, not the arena. A pod is 14 units
        /// across on a hull tens of units long, and this is meant to wash the
        /// plating immediately around it - not to light the boss.
        /// </summary>
        private static readonly Color PodLightColor = new Color(1f, 0.13f, 0.05f);

        private const float PodLightRange = 60f;
        private const float PodLightLumens = 90f;

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
        private const float PlateRadius = 0.8f;

        /// <summary>
        /// How fast the boss's shots settle onto the lane the player flies in.
        ///
        /// They have to settle onto it at all because a projectile orbits with
        /// RotateAround, which preserves the distance from the axis it was born
        /// at exactly and forever. The muzzles are spread across the width of a
        /// ship 3 units deep and sit anywhere from 13.16 to 15.02 from the axis,
        /// while the player is pinned to 13.72 and, with their own hitbox and the
        /// shot's, can only be touched between 13.39 and 14.05. Measured on the
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
        /// Nothing is lost by it. The hull is 15.3 units tall against a band of
        /// 8.9, so it already spans every height the player can occupy: matching
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
        /// The red the pods are lit in, copied from the pickup glow so it
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

            // This was green until 2026-09-05, and why it was green is worth
            // keeping written down: the arena is lit by lava, so the one thing the
            // player has to pick out of all that orange was put as far from orange
            // as the wheel goes.
            //
            // It is red now because the whole of the boss's output - its rounds,
            // its lance and its pods - was brought into one warm family, and a
            // green target on an orange ship reads as a pickup rather than as the
            // ship's own weak spot. What replaces the hue separation is value and
            // saturation: this sits well above the lava's own light and is far
            // more saturated than the rock it lands on, and the pod light added in
            // BuildGlow throws it onto the hull so a pod reads as a source rather
            // than as a decal.
            //
            // The cost is real and known - a red pod against orange lava is a
            // harder read than a green one was. This is the first thing to
            // question if the armoured phase turns out to be hard to aim at.
            var red = new Color(2.2f, 0.09f, 0.04f);

            skin.SetColor("_UnlitColor", red);
            skin.SetColor("_EmissiveColor", red);
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
            properties.FindProperty("healthPoints").intValue = PodHealth;
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
        /// tangent, and the arena is a circle: 3.95 units along the tangent from a
        /// point on a 13.7-unit ring leaves you 0.56 units outside the ring, not on
        /// it. Player bullets orbit at a fixed radius and never leave it, so those
        /// 0.56 units come straight off the target - a pod 0.7 units across loses
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

            BuildPodLight(glow);
        }

        /// <summary>
        /// The light the pod throws onto the ship around it.
        ///
        /// An emissive sphere lights nothing: emission feeds the camera, not the
        /// lighting result, so without this the pod is a bright disc pasted on a
        /// hull that has no idea it is there. One real light makes the pod a
        /// source - the plating around it picks up its colour, and the pod stops
        /// reading as a decal and starts reading as something installed in the
        /// ship.
        ///
        /// Lives on the glow object rather than beside it so that
        /// BossWeakPoint.Show, which walks the glow's children to hide a wrecked
        /// pod, switches the light off with the sphere. A pod that went on
        /// lighting the hull after it was destroyed would say the gun behind it
        /// still worked.
        ///
        /// No shadows. Three of these, each a point light, would be eighteen
        /// cubemap faces a frame for a light whose whole job is a wash of colour
        /// on the plating a few units away - and the boss is the moment in the
        /// run when the frame has least to spare. It is set through
        /// HDAdditionalLightData rather than Light.shadows alone because HDRP
        /// reads its own copy of that flag.
        /// </summary>
        private static void BuildPodLight(Transform glow)
        {
            Light lamp = glow.GetComponent<Light>();

            if (lamp == null)
            {
                lamp = glow.gameObject.AddComponent<Light>();
            }

            lamp.type = LightType.Point;
            lamp.color = PodLightColor;
            lamp.range = PodLightRange;
            lamp.shadows = LightShadows.None;

            // The emitter is the sphere itself, so the falloff starts at the
            // sphere's own surface rather than at a point in the middle of it.
            // Taken off the rendered size rather than typed in, because the glow
            // is sized from PodRadius and the two must not drift apart. Held just
            // inside the sphere so the emitter cannot poke through into the hull,
            // which is what burns a hard bright patch into the plating.
            lamp.shapeRadius = Mathf.Max(0.5f, glow.lossyScale.x * 0.35f);

            var data = glow.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();

            if (data == null)
            {
                data = glow.gameObject.AddComponent<
                    UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            }

            // The unit first, then the value, rather than SetIntensity doing both.
            // SetIntensity converts out of whatever unit the light is currently
            // in, and a light component added a few lines above has not run its
            // own initialisation yet - so it is still in the point light's default
            // candela and the call quietly divides by 4*pi. Measured: asking for
            // 9000 through SetIntensity stored 716.2, which is 9000/4*pi exactly,
            // and the inspector then reads 716 lumens rather than the wrong-unit
            // 9000 that would at least have been visible.
            //
            // Assigning HDAdditionalLightData.intensity is not the mistake
            // LavaLightPlacer warns about; that one is Light.intensity, which
            // bypasses HDRP's unit handling entirely.
            data.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            data.intensity = PodLightLumens;
            data.EnableShadows(false);

            // The arena's fog is dense enough to carry a glow now, so the pod
            // gets one. At 1 it is physical: no multiplier propping up a light
            // that the fog is too thin to show, which is what made the old lava
            // lights read as fake.
            data.affectsVolumetric = true;
            data.volumetricDimmer = 1f;
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
            /// An explicit pair of rounds, one per direction of travel, for an
            /// attack that fires something other than the shared bullet.
            ///
            /// Separate from <see cref="Round"/> below, which is for things that
            /// are not aimed and so need only one prefab. A disc still orbits, so
            /// it still needs the two opposite angular speeds that every other
            /// round comes in.
            /// </summary>
            public string RoundLeft;

            public string RoundRight;

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

            /// <summary>
            /// Seconds between one muzzle and the next inside a single volley.
            ///
            /// Zero everywhere but the lance, and that is not an oversight. The
            /// curtain and the rake want their muzzles to go off together,
            /// because what they are is a wall arriving at once; a wall delivered
            /// in pieces is a different, weaker attack.
            /// </summary>
            public float MuzzleStagger;
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
        /// boss bullet goes round the ring in 4.5 seconds, and that is the number
        /// every interval here is reasoned from.
        ///
        /// Tightened on 2026-09-07, after the first full playthrough found both
        /// acts easy. The intervals were not the main fault - the acts were
        /// ending after about two cycles of each attack, which is what PodHealth
        /// addresses - but with the acts now long enough to express themselves
        /// these had more room in them than they need.
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

                // Discs, not bullets. The keel is the lowest of the three banks -
                // the floor of the playable band - and it fires from slots rather
                // than barrels. Giving it its own round means a glance at what is
                // coming tells the player which height it came from, which is the
                // single thing the armoured act is asking them to read.
                RoundLeft = DiscLeftPath,
                RoundRight = DiscRightPath,
                InitialDelay = 1.5f,

                // Under the lap, which is the change that matters. A boss bullet
                // takes 4.5s to come round, so at 3.4 the next wall leaves while
                // the last one is still a quarter of the ring from home: there are
                // now stretches with two walls on the ring at once, at different
                // gap rows. At 6 there was a second and a half of open ring between
                // them and that gap was where the act was won.
                //
                // This is the one interval here that changes what the attack is
                // rather than how often it happens, so it is the first to walk
                // back if the curtain stops being readable.
                Interval = 3.4f,

                // Still one row. This is the learnable part - two watched volleys
                // tell you where the third gap will be - and widening it would
                // trade the one thing the curtain teaches for difficulty that
                // Interval provides more honestly.
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

                // Sixteen muzzles at 0.12 is a 1.9s sweep, so 4 left two full
                // seconds of quiet after each one. 2.4 leaves half a second - the
                // rake is very nearly continuous, and the player is now behind it
                // rather than waiting for it.
                Interval = 2.4f,

                // Unchanged, deliberately. The step is how fast the rake crosses
                // the band, and it is what makes the attack readable rather than a
                // wall. Interval is the honest place to add pressure; this is the
                // attack's identity.
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

                // 2.6 was set against a burst that no longer exists. A 1.2s wind-up
                // and 0.6s of firing left eight tenths of a second of quiet, and
                // that was the floor: any less and the telegraph runs more often
                // than not, which turns a warning into background noise. The beam
                // that replaced the burst is instant to fire and then lives its two
                // seconds on its own, so the attack itself is shorter than it was
                // and the quiet is longer. The floor still holds.
                Interval = 2.6f,

                // The wind-up is untouched. It is the fight's one real telegraph,
                // it now makes a noise, and shortening it would make the warning
                // worth less at exactly the moment the fight leans on it harder.
                ChargeSeconds = 1.2f,

                // Zero, and none of the three is read any more: since 8 September
                // this attack is BossLanceBeam, one arc drawn along the ring, and
                // the rounds it used to fire are kept only as the thing the beam
                // takes its material and its direction from.
                //
                // They are left here rather than deleted because they say what the
                // attack was, and the measurement that ended it is the reason the
                // beam exists. The ring is 86.2 units around and a round crossed it
                // at 19.16 units a second, so rounds leaving 0.06s apart were 1.15
                // units apart while each was 1.286 units long - consecutive rounds
                // overlapped by 0.14 units before they had gone anywhere. The stream
                // was never a row of bullets waiting to be separated. It was
                // already a solid line, drawn the expensive way: forty objects,
                // forty colliders and forty lights for one continuous thing, into a
                // light cluster that holds 24 per cell and drops the rest in
                // silence.
                //
                // Welding them into four 3.3-unit rounds was tried first, on
                // 8 September, and rejected on sight - at that length they read as
                // sticks flying in formation rather than as a beam. 3.3 was not a
                // taste either: it is the longest a straight mesh can be on a
                // circle this size before it visibly stops following the lane. The
                // conclusion was that the shape wanted was never a projectile.
                BurstSeconds = 0f,
                BurstInterval = 0.06f,
                MuzzleStagger = 0f,
            },
            new Volley
            {
                Label = "Hull - Ram",
                Pattern = BossFirePattern.Ram,
                Phases = BossPhaseMask.Exposed,
                Muzzles = new int[0],
                InitialDelay = 1f,

                // 4.5, and it cannot usefully go lower. A ram is a 1s charge plus
                // a 3s pass, and Tick skips any attack whose running flag is still
                // set - so an interval under 4 does not fire more often, it just
                // stops meaning anything and the ram runs back to back. This
                // leaves half a second of hull between passes.
                //
                // The dash comes back in 1.2s, so the counterplay still answers
                // comfortably; what changed is that it is no longer idle for most
                // of the act.
                Interval = 4.5f,
                ChargeSeconds = 1f,
                BurstSeconds = 3f,

                // Untouched. The dash is the answer to this, and a faster ram is
                // not a harder version of the same question - it is a different
                // one, about whether the dash can catch it at all.
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

                // Life over interval is how many plates stand at once, and a
                // third of those sit at any one height.
                //
                // 16 over 1.8 held about nine - but only in a steady state that
                // takes a whole plate lifetime to build, and the act used to be
                // over in five seconds. The density this was tuned for had never
                // once been on screen.
                //
                // BossWreckage.lifeSeconds is 11 now so the steady state arrives
                // inside the act, and 11 over 0.7 holds about sixteen plates -
                // five of them at the player's own altitude, against a player
                // lapping the ring every 12.3 seconds. That is a forced move
                // roughly every two and a half seconds.
                Interval = 0.7f,
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

            // The first four are indexed by the offset below, so nothing may be
            // inserted before them. The discs ride along at the end purely so
            // TuneRounds reaches them - they are picked by name, not by index.
            GameObject[] rounds =
            {
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 3.prefab"),
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 4.prefab"),
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 5.prefab"),
                Load<GameObject>("Assets/Prefabs/Boss/boss_shoot 6.prefab"),
                Load<GameObject>(DiscLeftPath),
                Load<GameObject>(DiscRightPath),
            };

            TuneRounds(rounds);

            var so = new SerializedObject(emitter);

            so.FindProperty("hullSpark").objectReferenceValue = Load<GameObject>(SparkPath);
            so.FindProperty("scuttleThreshold").intValue = ScuttleThreshold;
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

                if (volley.RoundLeft != null)
                {
                    left = Load<GameObject>(volley.RoundLeft);
                    right = Load<GameObject>(volley.RoundRight);
                }
                else if (volley.Round != null)
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
                entry.FindPropertyRelative("muzzleStagger").floatValue = volley.MuzzleStagger;
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
