using UnityEngine;

namespace SurvivalChaos
{
    /// <summary>
    /// The rules behind an upgrade offer drawn as the ship part it adds (17
    /// November 2026): which part a skill is, how many tier dots it carries,
    /// how close the ship must come before the part is pulled in, and the
    /// path it flies onto its mount. Kept apart from the pickup so the tests
    /// can hold them without a scene.
    /// </summary>
    public static class PartOffer
    {
        /// <summary>The most dots under a part. Picks past the third show three.</summary>
        public const int MostDots = 3;

        /// <summary>
        /// How far past contact the pull starts, as a multiple of the contact
        /// distance. Close enough that a dodge past one offer does not grab
        /// it; the Magnet upgrade does not widen it.
        /// </summary>
        public const float ReachOverContact = 1.5f;

        /// <summary>The ship upgrade a skill bolts on, if it bolts one on. Heal adds nothing.</summary>
        public static bool UpgradeOf(SkillDefinition skill, out ShipUpgrade upgrade)
        {
            switch (skill)
            {
                case ShotUpgradeSkill _: upgrade = ShipUpgrade.Hydra; return true;
                case AttackSpeedSkill _: upgrade = ShipUpgrade.AttackSpeed; return true;
                case MaxHealthSkill _: upgrade = ShipUpgrade.MaxHealth; return true;
                case MoveSpeedSkill _: upgrade = ShipUpgrade.MoveSpeed; return true;
                case DashRecoverySkill _: upgrade = ShipUpgrade.DashRecovery; return true;
                case SlowMoSkill _: upgrade = ShipUpgrade.SlowMo; return true;
                case DeflectorSkill _: upgrade = ShipUpgrade.Deflector; return true;
                case MagnetSkill _: upgrade = ShipUpgrade.Magnet; return true;
                default: upgrade = default; return false;
            }
        }

        /// <summary>
        /// The stage an offer is drawn as, for the pick it would fit. The next
        /// stage, except where that stage does not read alone at play size and
        /// the upgrade has one that does: then that one, its hero piece. Only the
        /// look changes: the flight still lands where the next stage goes on.
        /// </summary>
        public static int HeroPick(ShipUpgrade upgrade, int pick)
        {
            // Slow Mo's first stage is a thin flat ring that reads as a line
            // seen edge on; its second is the ring with the rest of the halo.
            if (upgrade == ShipUpgrade.SlowMo)
            {
                return Mathf.Max(pick, 2);
            }

            return pick;
        }

        private static readonly System.Collections.Generic.Dictionary<Mesh, Mesh> Halves =
            new System.Collections.Generic.Dictionary<Mesh, Mesh>();

        /// <summary>
        /// One of a pair: a stage whose pieces lie apart on either side of the
        /// ship's centre line (no triangle crosses it) is cut to the pieces on
        /// one side. Shown alone on the ring, a pair read as two loose things
        /// with a gap between. A single piece comes back as it is.
        /// </summary>
        public static Mesh OneOfPair(Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable)
            {
                return mesh;
            }

            if (Halves.TryGetValue(mesh, out Mesh half) && half != null)
            {
                return half;
            }

            Vector3[] vertices = mesh.vertices;
            float middle = mesh.bounds.center.z;
            const float Slack = 0.002f;
            bool apart = true;
            var kept = new System.Collections.Generic.List<int>[mesh.subMeshCount];

            for (int s = 0; s < mesh.subMeshCount && apart; s++)
            {
                int[] triangles = mesh.GetTriangles(s);
                kept[s] = new System.Collections.Generic.List<int>(triangles.Length / 2);

                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    float a = vertices[triangles[t]].z - middle;
                    float b = vertices[triangles[t + 1]].z - middle;
                    float c = vertices[triangles[t + 2]].z - middle;

                    if (Mathf.Min(a, Mathf.Min(b, c)) < -Slack && Mathf.Max(a, Mathf.Max(b, c)) > Slack)
                    {
                        apart = false;
                        break;
                    }

                    if (a + b + c > 0f)
                    {
                        kept[s].Add(triangles[t]);
                        kept[s].Add(triangles[t + 1]);
                        kept[s].Add(triangles[t + 2]);
                    }
                }
            }

            if (!apart)
            {
                Halves[mesh] = mesh;
                return mesh;
            }

            half = Object.Instantiate(mesh);
            half.name = mesh.name + " (one of pair)";

            // From the triangles kept: the cut mesh still holds the other
            // side's vertices, and bounds over all of them are the pair's.
            var bounds = new Bounds();
            bool first = true;

            for (int s = 0; s < kept.Length; s++)
            {
                half.SetTriangles(kept[s], s, false);

                foreach (int index in kept[s])
                {
                    if (first)
                    {
                        bounds = new Bounds(vertices[index], Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        bounds.Encapsulate(vertices[index]);
                    }
                }
            }

            half.bounds = bounds;
            Halves[mesh] = half;
            return half;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            Halves.Clear();
        }

        /// <summary>Dots under a part for the pick it is: one to three.</summary>
        public static int DotsFor(int pick)
        {
            return Mathf.Clamp(pick, 1, MostDots);
        }

        /// <summary>The dots as text, for the edge arrow.</summary>
        public static string DotText(int pick)
        {
            return new string('•', DotsFor(pick));
        }

        /// <summary>Centre-to-centre distance at which the pull starts.</summary>
        public static float Reach(float contact)
        {
            return Mathf.Max(0f, contact) * ReachOverContact;
        }

        /// <summary>
        /// How far along its flight a part is, eased in: it leaves the ring
        /// gently and hits the hull fast, which is what makes it a clamp.
        /// </summary>
        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        /// <summary>
        /// A point on the flight: a curve from where the part broke off to the
        /// mount, bowed out by <paramref name="bow"/> at its middle. The mount
        /// is read every frame, so the curve homes on a moving ship.
        /// </summary>
        public static Vector3 Flight(Vector3 from, Vector3 to, Vector3 bow, float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 control = (from + to) * 0.5f + bow;
            float u = 1f - t;
            return u * u * from + 2f * u * t * control + t * t * to;
        }
    }
}
