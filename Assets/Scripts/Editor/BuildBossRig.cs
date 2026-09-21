using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

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
    /// not, and half the time the boss fired out of its own tail, up to 8.7 world
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
        /// The muzzle glows borrow the ship's flare shader, and a glow mesh shaped
        /// like the dash-ready light's with one difference - see BuildTellMesh.
        /// </summary>
        private const string TellShaderPath = "Assets/Art/Shaders/ShipThruster.shadergraph";
        private const string TellMeshPath = "Assets/Art/Models/VFX/BossTellGlow.asset";
        private const string TellMaterialPath = "Assets/Art/Materials/VFX/BossMuzzleTell.mat";

        /// <summary>The ship's thruster plume, built by ShipThrusterBuilder; the torpedoes burn it too.</summary>
        private const string PlumeMeshPath = "Assets/Art/Models/VFX/ShipThrusterPlume.asset";

        /// <summary>
        /// The hue of the boss's own rounds, whose emission runs 3.0 : 0.7 : 0.02
        /// - so a glow on a muzzle is read as the fire it is about to become, and
        /// never as the player's green.
        /// </summary>
        private static readonly Vector3 TellColour = new Vector3(1.0f, 0.3f, 0.03f);

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
        /// Pod radius in local units, so 0.7 world units across a playable band of
        /// 8.9.
        ///
        /// A target the player has to line up with rather than one they cannot
        /// miss. The widest shot upgrade spreads six bullets over 1.5 world units,
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
        /// The range is set against the ship, not the arena. A pod is 1.4 units
        /// across on a hull a few units long, and this is meant to wash the
        /// plating immediately around it - not to light the boss.
        ///
        /// Range is the one light property the boss transform does not carry.
        /// A collider radius under a root at scale 1 is a local number; a light
        /// range is handed straight to HDRP in world units, so this divided by
        /// ten with the world while the pods around it did not.
        /// </summary>
        private static readonly Color PodLightColor = new Color(1f, 0.13f, 0.05f);

        private const float PodLightRange = 6f;
        private const float PodLightLumens = 90f;

        /// <summary>
        /// The visible plate of shed hull, in world units. Carried on a child so
        /// the object the collider sits on can stay at scale one.
        ///
        /// Authored in world units rather than the boss's, because a plate is not
        /// parented to the boss once it is off - it is spawned into the pool like
        /// a projectile and left where it was made.
        /// </summary>
        private static readonly Vector3 PlateSize = new Vector3(2f, 1.2f, 0.9f);

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
        /// shot's, can only be touched between about 13.48 and 13.93. (It was
        /// 13.39 to 14.05 until 11 September 2026, when the player's ship was
        /// halved; the band is mostly the ship.) Measured on the rig with the
        /// wider band, 8 of the 32 muzzles were inside it and 24 were not: three
        /// quarters of every volley was incapable of hitting anyone, which is why
        /// a curtain of twelve arrived as a wall of three.
        ///
        /// The alternative was moving the muzzles onto the lane, and that is the
        /// one thing the rig must not do - the whole point of it is that the shot
        /// leaves the barrel the artist modelled. So the shot leaves the barrel
        /// and then eases in, which is also what every ship in the arena does on
        /// its way to the ring.
        ///
        /// 5 put the worst-placed muzzle's shot inside the band in 0.275
        /// seconds, by which time it had travelled about 22 degrees of arc and was
        /// clear of a hull 7.1 units wide. Fast enough to be dangerous while it
        /// still matters, slow enough to read as a shot curving in rather than as
        /// a muzzle in the wrong place. Against the half-size ship's narrower
        /// band the same 5 takes about 0.36 seconds and 29 degrees; about 6.6
        /// would restore 0.275, if the fight turns out to want it back.
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
                SnapDiscMuzzles(root.transform, pivots);

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
        /// so the crown emplacement ends up parked 4.9 units above the player
        /// forever, which is 4.9 units above whatever height they climb to, and
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
        /// target with a 0.7-unit window and one with about 0.4.
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
            // candela and the call quietly divides by 4*pi. Measured again on
            // 6.6: asking for 9000 through SetIntensity stores 716.2, which is
            // 9000/4*pi exactly, and the inspector then reads 716 lumens rather
            // than the wrong-unit 9000 that would at least have been visible.
            //
            // These are Light's own properties now. HDAdditionalLightData.lightUnit
            // and .intensity were deprecated in 2023.3 and forward to these, which
            // is why the same two lines through the light read identically - 9000
            // in and 9000 stored, measured both ways.
            lamp.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            lamp.intensity = PodLightLumens;
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
                properties.FindProperty("lifeSeconds").floatValue = 11f;
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

            /// <summary>
            /// Seconds the muzzles glow before a Curtain or a Sequence fires. Zero
            /// for the lance and the ram, which carry their own charge.
            /// </summary>
            public float TellSeconds;

            /// <summary>World units across each muzzle's glow at full.</summary>
            public float TellSize = 0.5f;

            /// <summary>
            /// Units a second each round climbs or dives, bouncing off the band's
            /// floor and ceiling. Zero holds the height it was fired at, which is
            /// what every round did before 21 September 2026.
            /// </summary>
            public float ThrowSpeed;

            /// <summary>One muzzle per volley, the bank taken in turn.</summary>
            public bool SingleShot;

            /// <summary>
            /// Seconds each round steers for as a torpedo - its fuel. Zero is a
            /// plain round. The rest is how it flies; see TorpedoSteer.
            /// </summary>
            public float HomeSeconds;
            public float HomePerception = 1.6f;
            public float HomeSpeed = 7.5f;
            public float HomeLaunchSpeed = 3f;
            public float HomeSpinUp = 0.6f;
            public float HomeArmSeconds = 0.3f;
            public float HomeTurnRate = 90f;
            public float HomeCoastSeconds = 2f;
            public float HomeScale = 0.5f;
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
                //
                // Walked back to 8 by hand in the Inspector on 21 September 2026,
                // after the discs were thrown and fanned. Kept here so a full
                // rebuild does not undo it.
                Interval = 8f,

                // Still one row. This is the learnable part - two watched volleys
                // tell you where the third gap will be - and widening it would
                // trade the one thing the curtain teaches for difficulty that
                // Interval provides more honestly.
                OpenRows = 1,

                // The rows that are about to fire glow, and the dark one is the
                // gap, so the gap is on the hull before the wall leaves. Half a
                // second of warning against 3.4 of cadence, taken out of the
                // quiet between walls rather than added to it.
                TellSeconds = 0.6f,

                // The keel's rows are 1.2 apart, so its glows can be big enough
                // to hold their own beside the pods - which are what the eye is
                // drawn to on this hull - without two rows running together.
                TellSize = 0.9f,

                // Thrown, and bouncing off the floor and ceiling of the band: the
                // playtest's note was that discs and bullets merged into one lane,
                // and the answer asked for was discs thrown in different
                // directions that come off the game's bounds. Each row fans out,
                // steep up to steep down, the same every curtain. Thrown
                // as one wall first, a row's four discs still left as one stack,
                // because they share a height and ease onto one lane. The price is
                // the curtain's gap, which now holds only at the moment of
                // release before the fan spreads across it.
                //
                // One disc at a time. Released together, each pair of slots 0.27
                // apart put out one double disc. Set to 2s by hand in the
                // Inspector on 21 September 2026, from the 0.08 first tried: eight
                // discs then take 14s to leave, which is longer than Interval, so
                // the next curtain waits for the last disc - the running flag
                // holds it - and the attack is a steady drip of discs rather than
                // a wall.
                MuzzleStagger = 2f,

                // Not read by a curtain; only the lance used it. Set to 2 by hand
                // alongside the stagger above, and mirrored here so a full rebuild
                // leaves the prefab as it was found.
                BurstInterval = 2f,

                // 4 at the steep ends, about 17 degrees, against the player's
                // climb of 7, so any disc can be outflown vertically. The discs
                // go round at 40 degrees a second, 13 units a second at the
                // lane, so one crosses the 8.9-unit band in about 2.2s - four
                // bounces in the 9s a lap takes.
                ThrowSpeed = 4f,
            },
            new Volley
            {
                Label = "Crown - Rake",
                Pattern = BossFirePattern.Sequence,
                Phases = BossPhaseMask.Armoured,
                Bank = "Crown",
                Muzzles = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
                InitialDelay = 0.5f,

                // One torpedo every 3 seconds, at the player's call on 21 September
                // 2026 - first 5s, then 1, then 2 once the torpedoes steered and
                // had been played against (at one a second 13 of 18 hit and the
                // hull was gone in 46s; at 2s, 11 of 33 hit in 67s), then 3. The
                // crown had been a rake of sixteen every 2.4s; with each round
                // homing that was a wall of hunters, and a single torpedo is
                // something to watch coming and shake off. SingleShot takes the
                // sixteen muzzles in turn in the staircase's order, so a full
                // pass up the crown is 48 seconds and the next comes back down.
                // The half-second glow fits inside the interval, so the cadence
                // is the interval and not the warning.
                Interval = 3f,
                SingleShot = true,

                // Unchanged, deliberately. The step is how fast the rake crosses
                // the band, and it is what makes the attack readable rather than a
                // wall. Interval is the honest place to add pressure; this is the
                // attack's identity.
                StepSeconds = 0.12f,

                // Lights the bank in the order it will fire, so which end the
                // staircase starts from is readable before it does.
                TellSeconds = 0.5f,

                // The crown's rows are 0.40 to 0.52 apart, so its glows cannot be:
                // wider than the gap and four rows merge into one blob, and the
                // sweep that shows the staircase's direction is gone.
                TellSize = 0.42f,

                // Torpedoes that fly like game torpedoes, asked for on 21
                // September 2026 after the first ones - which slid after the
                // player's height at the old round's 80 degrees a second, 26
                // units a second at the lane - read as bullets with a lean.
                // These leave the muzzle at 3, run straight for 0.3s, spin up
                // over 0.6s to 7.5 - a touch over the player's 7, asked for
                // after 8 - and turn at 90 degrees a second: a turning circle
                // about 10 units across, wider than the band, so one turning
                // near an edge pulls out tight along it, and one that misses
                // swings round and comes back at you while its fuel lasts. Half
                // the round's size, hit box and all, with a plume out of the
                // tail that burns while the motor does.
                //
                // The delay is the dodge. A torpedo's idea of where the player
                // is catches up over about 0.6s (perception 1.6). Worked through
                // with the real hit boxes - the player's is 0.16 tall and a
                // half-size torpedo's 0.09, so a hit is an intercept: hold still
                // and it hits; dodge a couple of units a second and a half or
                // more early and it follows you in; move a unit or more in the
                // last second or so and it passes where you were, by 0.2 to 1;
                // run to the floor or ceiling and it follows you there. A
                // torpedo that hits goes off.
                //
                // 10s of fuel and 2s of coast, then it is gone - asked for the
                // same day as the 2s interval, after 4.5s and 1s had torpedoes
                // vanishing mid-chase. At one every 3s that is four in the air
                // at once, and a missed torpedo has time to come round again.
                HomeSeconds = 10f,
                HomePerception = 1.6f,
                HomeSpeed = 7.5f,
                HomeLaunchSpeed = 3f,
                HomeSpinUp = 0.6f,
                HomeArmSeconds = 0.3f,
                HomeTurnRate = 90f,
                HomeCoastSeconds = 2f,
                HomeScale = 0.5f,

                // Both banks go the same way round the ring: reversing the rake
                // was tried on 21 September 2026 and was not what the note meant.
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

                WriteTellAndRoute(entry, volley);
            }

            WriteTellAssets(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WriteTellAndRoute(SerializedProperty entry, Volley volley)
        {
            entry.FindPropertyRelative("tellSeconds").floatValue = volley.TellSeconds;
            entry.FindPropertyRelative("tellSize").floatValue = volley.TellSize;
            entry.FindPropertyRelative("throwSpeed").floatValue = volley.ThrowSpeed;
            entry.FindPropertyRelative("muzzleStagger").floatValue = volley.MuzzleStagger;
            entry.FindPropertyRelative("singleShot").boolValue = volley.SingleShot;

            // Only for a single-shot attack, where the interval is what the mode
            // means. Every other interval is left as the prefab has it, because
            // those get tuned by hand in the Inspector - the keel's was, to 8.
            if (volley.SingleShot)
            {
                entry.FindPropertyRelative("interval").floatValue = volley.Interval;
            }
            entry.FindPropertyRelative("homeSeconds").floatValue = volley.HomeSeconds;
            entry.FindPropertyRelative("homePerception").floatValue = volley.HomePerception;
            entry.FindPropertyRelative("homeSpeed").floatValue = volley.HomeSpeed;
            entry.FindPropertyRelative("homeLaunchSpeed").floatValue = volley.HomeLaunchSpeed;
            entry.FindPropertyRelative("homeSpinUp").floatValue = volley.HomeSpinUp;
            entry.FindPropertyRelative("homeArmSeconds").floatValue = volley.HomeArmSeconds;
            entry.FindPropertyRelative("homeTurnRate").floatValue = volley.HomeTurnRate;
            entry.FindPropertyRelative("homeCoastSeconds").floatValue = volley.HomeCoastSeconds;
            entry.FindPropertyRelative("homeScale").floatValue = volley.HomeScale;
        }

        private static void WriteTellAssets(SerializedObject emitter)
        {
            emitter.FindProperty("tellMesh").objectReferenceValue = BuildTellMesh();
            Material tell = BuildTellMaterial();
            emitter.FindProperty("tellMaterial").objectReferenceValue = tell;

            // The torpedoes' plume is the ship's own, in the tells' orange.
            emitter.FindProperty("exhaustMesh").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Mesh>(PlumeMeshPath);
            emitter.FindProperty("exhaustMaterial").objectReferenceValue = tell;
        }

        /// <summary>
        /// Writes only the tells and the routes onto the boss, from <see cref="Fight"/>.
        ///
        /// The full rebuild above opens the prefab with LoadPrefabContents, and
        /// that is the call that stalled the editor long enough for the GPU
        /// driver to reset it. These are plain fields on a component that
        /// already exists, so they go through a SerializedObject on the asset
        /// itself - and the values stay in the one table, so a full rebuild later
        /// writes the same numbers.
        /// </summary>
        [MenuItem("Survival Chaos/Apply Boss Tells and Routes", priority = 56)]
        public static void ApplyTellsAndRoutesFromMenu()
        {
            Debug.Log(ApplyTellsAndRoutes());
        }

        public static string ApplyTellsAndRoutes()
        {
            GameObject boss = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            BossEmitter emitter = boss != null ? boss.GetComponentInChildren<BossEmitter>(true) : null;

            if (emitter == null)
            {
                return "No BossEmitter in " + PrefabPath + ". Nothing was changed.";
            }

            Transform[] pivots = CollectPivots(boss.transform);
            string snapped = pivots != null ? SnapDiscMuzzles(boss.transform, pivots) : "No muzzles found to snap.";

            var so = new SerializedObject(emitter);
            SerializedProperty attacks = so.FindProperty("attacks");
            var log = new System.Text.StringBuilder();

            for (int i = 0; i < attacks.arraySize; i++)
            {
                SerializedProperty entry = attacks.GetArrayElementAtIndex(i);
                string label = entry.FindPropertyRelative("label").stringValue;
                Volley volley = System.Array.Find(Fight, v => v.Label == label);

                if (volley == null)
                {
                    log.AppendLine(label + ": not in the Fight table, left alone.");
                    continue;
                }

                WriteTellAndRoute(entry, volley);
                log.AppendLine(label + ": tell " + volley.TellSeconds + "s at " + volley.TellSize
                               + ", thrown at " + volley.ThrowSpeed + ", released " + volley.MuzzleStagger + "s apart"
                               + (volley.HomeSeconds > 0f
                                   ? ", torpedo at " + volley.HomeSpeed + " x" + volley.HomeScale + " turning " + volley.HomeTurnRate
                                     + " deg/s for " + volley.HomeSeconds + "s"
                                   : "")
                               + (volley.SingleShot ? ", one round every " + volley.Interval + "s" : ""));
            }

            WriteTellAssets(so);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(emitter);
            PrefabUtility.SavePrefabAsset(boss);
            AssetDatabase.SaveAssets();

            return log.ToString() + snapped;
        }

        /// <summary>
        /// The muzzles moved onto the hull: the keel's twelve, which fire the
        /// discs, and the crown's sixteen, which fire the torpedoes. The prow's
        /// four feed the lance beam, which is drawn from their average and was
        /// never the complaint.
        /// </summary>
        private static readonly int[] DiscMuzzles =
        {
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
        };

        /// <summary>How far in front of the hull a muzzle sits once snapped.</summary>
        private const float SlotClearance = 0.05f;

        /// <summary>
        /// Moves each disc muzzle back onto the slot it belongs to.
        ///
        /// The 32 pivots came over from BossScript where they were, and the
        /// keel's had always sat a full unit out in front of the hull - measured
        /// on 21 September 2026 at 0.98 to 1.45 clear of the slot panels they
        /// line up with. So a disc appeared in mid-air beside the ship rather than
        /// coming out of it. Each muzzle keeps its height and its place across the
        /// panel, and only its distance from the face changes: a ray from well
        /// outside the face, along the boss's -X, finds the slot, and the muzzle
        /// is put just in front of it.
        ///
        /// Measured off the hull mesh itself rather than a collider, so it works
        /// on the prefab asset without opening it. Re-running finds the same slot
        /// and changes nothing.
        /// </summary>
        private static string SnapDiscMuzzles(Transform root, Transform[] pivots)
        {
            Transform hull = root.Find("NAVEBOSS/Cube");
            MeshFilter filter = hull != null ? hull.GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;

            if (mesh == null)
            {
                return "No hull mesh at NAVEBOSS/Cube, so the disc muzzles were left where they were.";
            }

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Matrix4x4 hullToRoot = root.worldToLocalMatrix * hull.localToWorldMatrix;

            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = hullToRoot.MultiplyPoint3x4(vertices[i]);
            }

            var log = new System.Text.StringBuilder();
            int moved = 0;

            foreach (int index in DiscMuzzles)
            {
                Transform muzzle = index < pivots.Length ? pivots[index] : null;

                if (muzzle == null)
                {
                    continue;
                }

                Vector3 local = root.InverseTransformPoint(muzzle.position);
                Vector3 origin = new Vector3(local.x + 6f, local.y, local.z);

                if (!FirstHit(origin, Vector3.left, vertices, triangles, out float distance))
                {
                    log.AppendLine(muzzle.name + ": no slot behind it, left where it was.");
                    continue;
                }

                float face = origin.x - distance;
                Vector3 snapped = new Vector3(face + SlotClearance, local.y, local.z);

                if (Mathf.Abs(snapped.x - local.x) > 0.001f)
                {
                    muzzle.position = root.TransformPoint(snapped);
                    EditorUtility.SetDirty(muzzle);
                    moved++;
                }

                log.AppendLine(muzzle.name + ": slot at x " + face.ToString("F2") + ", was "
                               + (local.x - face).ToString("F2") + " in front of it.");
            }

            return "Muzzles moved onto the hull: " + moved + "\n" + log;
        }

        /// <summary>
        /// The nearest triangle a ray meets, by Moller-Trumbore. Both faces
        /// count, since which way the model's triangles wind is not something
        /// this should have to know.
        /// </summary>
        private static bool FirstHit(Vector3 origin, Vector3 direction, Vector3[] vertices, int[] triangles, out float nearest)
        {
            nearest = float.MaxValue;

            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 edge1 = vertices[triangles[t + 1]] - a;
                Vector3 edge2 = vertices[triangles[t + 2]] - a;

                Vector3 p = Vector3.Cross(direction, edge2);
                float det = Vector3.Dot(edge1, p);

                if (Mathf.Abs(det) < 1e-8f)
                {
                    continue;
                }

                float inverse = 1f / det;
                Vector3 s = origin - a;
                float u = Vector3.Dot(s, p) * inverse;

                if (u < 0f || u > 1f)
                {
                    continue;
                }

                Vector3 q = Vector3.Cross(s, edge1);
                float v = Vector3.Dot(direction, q) * inverse;

                if (v < 0f || u + v > 1f)
                {
                    continue;
                }

                float distance = Vector3.Dot(edge2, q) * inverse;

                if (distance > 0f && distance < nearest)
                {
                    nearest = distance;
                }
            }

            return nearest < float.MaxValue;
        }

        /// <summary>
        /// The muzzle glow's mesh: three fans a unit across, like the dash-ready
        /// light's, with U running 0 at the centre to 1 at the rim.
        ///
        /// What differs is V at the centre. The flare shader pushes a surface
        /// toward white where V sits at the middle of its ribbon, which is what
        /// gives the ready light its hot centre, and put here that made every
        /// muzzle glow a white four-point sparkle - seen in play on 21 September
        /// 2026 it read as the sparks off the hull, not as the boss's fire about
        /// to leave. Centred at 0.35 instead, the core is a fifth white at most
        /// and the glow reads as the orange the rounds are.
        /// </summary>
        private static Mesh BuildTellMesh()
        {
            const int sides = 12;
            const float centreV = 0.35f;

            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int plane = 0; plane < 3; plane++)
            {
                int centre = vertices.Count;
                vertices.Add(Vector3.zero);
                uvs.Add(new Vector2(0f, centreV));

                for (int i = 0; i < sides; i++)
                {
                    float angle = i / (float)sides * Mathf.PI * 2f;
                    float a = Mathf.Cos(angle) * 0.5f;
                    float b = Mathf.Sin(angle) * 0.5f;

                    vertices.Add(plane == 0 ? new Vector3(a, b, 0f)
                        : plane == 1 ? new Vector3(a, 0f, b)
                        : new Vector3(0f, a, b));

                    uvs.Add(new Vector2(1f, 0f));
                }

                for (int i = 0; i < sides; i++)
                {
                    triangles.Add(centre);
                    triangles.Add(centre + 1 + i);
                    triangles.Add(centre + 1 + (i + 1) % sides);
                }
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(TellMeshPath);
            bool isNew = mesh == null;

            if (isNew)
            {
                mesh = new Mesh();
            }
            else
            {
                mesh.Clear();
            }

            mesh.name = "BossTellGlow";
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, calculateBounds: false);
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);

            if (isNew)
            {
                AssetDatabase.CreateAsset(mesh, TellMeshPath);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        /// <summary>
        /// The muzzle glow's material, made the way the ship's flares make
        /// theirs - see ShipThrusterBuilder - with only the colour changed.
        /// Re-running updates it in place, so its GUID survives.
        /// </summary>
        private static Material BuildTellMaterial()
        {
            Shader shader = Load<Shader>(TellShaderPath);

            if (shader == null)
            {
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(TellMaterialPath);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, TellMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetVector("_FlameColor", TellColour);
            material.SetFloat("_FlameIntensity", 1f);
            material.SetFloat("_DoubleSidedEnable", 1f);
            material.SetFloat("_TransparentWritingMotionVec", 1f);
            material.SetFloat("_ExcludeFromTUAndAA", 0f);

            HDMaterial.ValidateMaterial(material);
            EditorUtility.SetDirty(material);
            return material;
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
