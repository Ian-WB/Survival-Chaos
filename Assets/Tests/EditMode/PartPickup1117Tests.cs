using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    /// <summary>
    /// Upgrade offers drawn as the ship parts they add, pulled onto the ship
    /// and clamped on (17 November 2026, build-brief-part-pickups.md).
    /// </summary>
    public class PartPickup1117Tests
    {
        private const string Skills = "Assets/Data/Skills/";
        private const string Parts = "Assets/Art/Models/Ships/Parts/";

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        [TestCase("ShotUpgrade", ShipUpgrade.Hydra)]
        [TestCase("AttackSpeed", ShipUpgrade.AttackSpeed)]
        [TestCase("MaxHealth", ShipUpgrade.MaxHealth)]
        [TestCase("MoveSpeed", ShipUpgrade.MoveSpeed)]
        [TestCase("DashRecovery", ShipUpgrade.DashRecovery)]
        [TestCase("SlowMo", ShipUpgrade.SlowMo)]
        [TestCase("Deflector", ShipUpgrade.Deflector)]
        [TestCase("Magnet", ShipUpgrade.Magnet)]
        public void EverySkillThatAddsAPart_IsThatPart(string skill, ShipUpgrade upgrade)
        {
            Assert.That(PartOffer.UpgradeOf(Load<SkillDefinition>(Skills + skill + ".asset"), out ShipUpgrade found), Is.True);
            Assert.That(found, Is.EqualTo(upgrade));
        }

        [Test]
        public void Heal_AddsNoPart()
        {
            Assert.That(PartOffer.UpgradeOf(Load<SkillDefinition>(Skills + "Heal.asset"), out _), Is.False);
            Assert.That(PartOffer.UpgradeOf(null, out _), Is.False);
        }

        [Test]
        public void TierDots_AreOneToThree()
        {
            Assert.That(PartOffer.DotsFor(0), Is.EqualTo(1));
            Assert.That(PartOffer.DotsFor(1), Is.EqualTo(1));
            Assert.That(PartOffer.DotsFor(2), Is.EqualTo(2));
            Assert.That(PartOffer.DotsFor(3), Is.EqualTo(3));
            Assert.That(PartOffer.DotsFor(6), Is.EqualTo(3));
            Assert.That(PartOffer.DotText(2), Is.EqualTo("••"));
        }

        [Test]
        public void PullReach_IsHalfAgainContact()
        {
            Assert.That(PartOffer.Reach(1.1f), Is.EqualTo(1.65f).Within(0.0001f));
            Assert.That(PartOffer.Reach(-1f), Is.EqualTo(0f));
        }

        [Test]
        public void Flight_StartsOnTheRingAndEndsOnTheMount()
        {
            Vector3 from = new Vector3(3f, 1f, 0f);
            Vector3 to = new Vector3(0f, 0f, 2f);
            Vector3 bow = Vector3.up;

            Assert.That(Vector3.Distance(PartOffer.Flight(from, to, bow, 0f), from), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(PartOffer.Flight(from, to, bow, 1f), to), Is.LessThan(0.0001f));
            Assert.That(PartOffer.Flight(from, to, bow, 0.5f).y, Is.GreaterThan((from.y + to.y) * 0.5f), "bows out");

            // Eased in: slow off the ring, fast onto the hull.
            Assert.That(PartOffer.Ease(0.5f), Is.LessThan(0.5f));
            Assert.That(PartOffer.Ease(1f), Is.EqualTo(1f));
        }

        [Test]
        public void SlowMo_ShowsItsFullHalo_OtherUpgradesTheNextStage()
        {
            Assert.That(PartOffer.HeroPick(ShipUpgrade.SlowMo, 1), Is.EqualTo(2));
            Assert.That(PartOffer.HeroPick(ShipUpgrade.SlowMo, 2), Is.EqualTo(2));
            Assert.That(PartOffer.HeroPick(ShipUpgrade.AttackSpeed, 4), Is.EqualTo(4));
        }

        [Test]
        public void EdgeArrow_CarriesDotsNotTheName_ForAPart()
        {
            string part = PickupLabelBoard.EdgeText("Attack Speed III", 3, left: true);
            Assert.That(part, Does.Not.Contain("Attack"));
            Assert.That(part, Does.StartWith("<  "));
            Assert.That(part, Does.Contain("#FFD140"));
            Assert.That(part, Does.Contain("•••"));

            Assert.That(PickupLabelBoard.EdgeText("Heal", 0, left: false), Is.EqualTo("Heal  >"));
        }

        [Test]
        public void APair_ShowsOneSide_ASinglePieceShowsWhole()
        {
            Mesh pair = Load<Mesh>(Parts + "ShipPart Hydra 2.asset");
            Mesh half = PartOffer.OneOfPair(pair);

            try
            {
                Assert.That(half, Is.Not.SameAs(pair));
                Assert.That(half.triangles.Length, Is.EqualTo(pair.triangles.Length / 2));
                Assert.That(half.bounds.min.z, Is.GreaterThanOrEqualTo(-0.002f), "one side of the centre line");
                Assert.That(half.bounds.size.z, Is.LessThan(pair.bounds.size.z * 0.6f), "its own bounds, not the pair's");
            }
            finally
            {
                Object.DestroyImmediate(half);
            }

            Mesh single = Load<Mesh>(Parts + "ShipPart MaxHealth 1.asset");
            Assert.That(PartOffer.OneOfPair(single), Is.SameAs(single));
        }

        [Test]
        public void PickupPrefab_HasThePartGlow()
        {
            var pickup = Load<GameObject>("Assets/Prefabs/Pickups/Pickup.prefab").GetComponent<Pickup>();
            var glow = new SerializedObject(pickup).FindProperty("partGlow").objectReferenceValue as Material;

            Assert.That(glow, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(glow), Is.EqualTo("Assets/Art/Materials/VFX/PartPickupGlow.mat"));
            Assert.That(glow.GetFloat("_SurfaceType"), Is.EqualTo(1f), "transparent");
            Assert.That(glow.GetFloat("_BlendMode"), Is.EqualTo(1f), "additive");
        }

        [Test]
        public void PickupPrefab_DrawsPartsBigEnoughToReadInAFight()
        {
            var pickup = Load<GameObject>("Assets/Prefabs/Pickups/Pickup.prefab").GetComponent<Pickup>();
            var so = new SerializedObject(pickup);

            Assert.That(so.FindProperty("partSize").floatValue, Is.EqualTo(3f).Within(0.0001f), "least, against the hull");
            Assert.That(so.FindProperty("partSizeMost").floatValue, Is.EqualTo(5.5f).Within(0.0001f), "most, against the hull");
            Assert.That(so.FindProperty("partRadius").floatValue, Is.EqualTo(0.9f).Within(0.0001f), "half-length on the ring");
        }

        // Ian, 17 Nov: the clamp should feel like the suit-up: line up, then slam.
        [Test]
        public void Pull_LinesUpOffTheMountThenSlamsHome()
        {
            var pickup = Load<GameObject>("Assets/Prefabs/Pickups/Pickup.prefab").GetComponent<Pickup>();
            var so = new SerializedObject(pickup);

            Assert.That(so.FindProperty("alignGap").floatValue, Is.GreaterThan(0f), "stops off the mount");
            Assert.That(so.FindProperty("alignSeconds").floatValue, Is.InRange(0.05f, 0.2f), "a beat to line up");
            Assert.That(so.FindProperty("slamSeconds").floatValue,
                Is.LessThan(so.FindProperty("alignSeconds").floatValue), "the slam is quicker than the line-up");
            Assert.That(so.FindProperty("pullSeconds").floatValue + so.FindProperty("alignSeconds").floatValue
                + so.FindProperty("slamSeconds").floatValue, Is.LessThan(0.6f), "the whole pull stays short");
        }

        [Test]
        public void PowerOn_GoesDarkFlaresThenRests()
        {
            Assert.That(PartPowerOn.Level(0f), Is.EqualTo(0f), "dark on the lock");
            Assert.That(PartPowerOn.Level(0.35f), Is.GreaterThan(1.5f), "flares past its resting glow");
            Assert.That(PartPowerOn.Level(2f), Is.EqualTo(1f), "settles to its resting glow");
        }

        [Test]
        public void NearestTag_NamesTheOfferOverWhatItDoes()
        {
            string tag = PickupLabelBoard.TagText("Max HP III", "+10 hull", Color.green);

            Assert.That(tag, Does.Contain("MAX HP III"));
            Assert.That(tag, Does.Contain("\n+10 hull"));
            Assert.That(PickupLabelBoard.TagText("Magnet", "", Color.white), Does.Not.Contain("\n"), "no empty second line");
        }

        [TestCase("AttackSpeed")]
        [TestCase("DashRecovery")]
        [TestCase("Deflector")]
        [TestCase("Magnet")]
        [TestCase("MaxHealth")]
        [TestCase("MoveSpeed")]
        [TestCase("ShotUpgrade")]
        [TestCase("SlowMo")]
        public void EveryPartOffer_HasAnEffectLineForItsTag(string skill)
        {
            var definition = Load<SkillDefinition>(Skills + skill + ".asset");
            Assert.That(definition.GetEffectLine(1), Is.Not.Empty);
        }

        [Test]
        public void ClampBurst_OneColourKeepsTheFade()
        {
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.cyan, 0f), new GradientColorKey(Color.blue, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });

            Gradient tinted = EffectTint.OneColour(fade, Color.magenta);

            Assert.That(tinted.Evaluate(0.5f).r, Is.EqualTo(1f).Within(0.001f));
            Assert.That(tinted.Evaluate(0.5f).g, Is.EqualTo(0f).Within(0.001f), "no cyan left");
            Assert.That(tinted.Evaluate(1f).a, Is.EqualTo(0f).Within(0.001f), "still fades out");
        }
    }
}
