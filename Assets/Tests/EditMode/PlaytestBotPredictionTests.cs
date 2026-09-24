using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SurvivalChaos.Tests
{
    public class PlaytestBotPredictionTests
    {
        // The tests assembly deliberately does not depend on the editor tools assembly.
        // These checks exercise its pure predictor without constructing a live pilot.
        // Looked up by assembly-qualified name: walking AppDomain.GetAssemblies can
        // turn up assemblies Unity has unloaded, which is what warning UAC0005 says.
        private static Type Pilot =>
            Type.GetType("SurvivalChaos.EditorTools.SurvivalPlaytestBot+Pilot, SurvivalChaos.Editor")
            ?? throw new InvalidOperationException(
                "SurvivalPlaytestBot.Pilot is not in the SurvivalChaos.Editor assembly any more.");

        private static Vector3[] Route(Vector3 origin, Vector3 centre, Vector2 heading, float duration,
            float floor = -100, float ceiling = 100, Vector2 request = default)
        {
            return (Vector3[])Pilot.GetMethod("PredictRoute", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { origin, centre, Vector2.zero, request,
                    2f, 2f, 10f, floor, ceiling, heading, duration, 4f, 2f });
        }

        private static float PickupMiss(Vector3[] route, Vector3 pickup)
        {
            return (float)Pilot.GetMethod("PickupMiss", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { route, pickup });
        }

        [Test]
        public void PickupMiss_FlyingThroughAPickup_BeatsStoppingShortOfIt()
        {
            // The pickup a quarter of the way along the walk: close enough that the
            // walk ends further from it than standing still is, which is the case
            // that parked the pilot 2 units short of upgrades on 24 Sep.
            Vector3 origin = new Vector3(10, 0, 0);
            var walk = Route(origin, Vector3.zero, Vector2.zero, 0, request: Vector2.right);
            var stand = Route(origin, Vector3.zero, Vector2.zero, 0);
            Vector3 pickup = walk[walk.Length / 4];

            Assert.That(Vector3.Distance(walk.Last(), pickup), Is.GreaterThan(Vector3.Distance(origin, pickup)));
            Assert.That(PickupMiss(walk, pickup), Is.LessThan(PickupMiss(stand, pickup)));
        }

        [Test]
        public void DashEndingBetweenSteps_DoesNotGainExtraBoost()
        {
            // Synthetic fixture: 2 units/s * 4 boost * .25 seconds = 2 units of arc.
            var points = Route(new Vector3(10,0,0), Vector3.zero, Vector2.right, .25f);
            Vector3 expected = Quaternion.Euler(0,-.2f*Mathf.Rad2Deg,0)*Vector3.right*10;
            Assert.That(Vector3.Distance(points.Last(),expected), Is.LessThan(.001f));
        }

        [Test]
        public void CommittedDash_CannotBeSteeredDuringItsWindow()
        {
            var a = Route(new Vector3(10,0,0), Vector3.zero, Vector2.right, .25f, request: Vector2.up);
            var b = Route(new Vector3(10,0,0), Vector3.zero, Vector2.right, .25f, request: Vector2.down);
            Assert.That(Vector3.Distance(a[9],b[9]), Is.LessThan(.0001f));
            Assert.That(a.Last().y, Is.GreaterThan(b.Last().y));
        }

        [Test]
        public void DiagonalDash_UsesSeparateClimbBoostAndClampsBand()
        {
            var points = Route(new Vector3(10,0,0),Vector3.zero,new Vector2(1,1).normalized,.25f,ceiling:.5f);
            Assert.That(points.Max(p=>p.y),Is.EqualTo(.5f).Within(.0001f));
            Vector3 last=points.Last(); last.y=0;
            float travelled = Mathf.Atan2(last.z,last.x)*10;
            Assert.That(travelled,Is.EqualTo(2/Mathf.Sqrt(2)).Within(.001f));
        }

        [Test]
        public void OffsetArena_PreservesRadiusAndHeightAtRest()
        {
            Vector3 centre=new Vector3(4,0,5), origin=new Vector3(14,2,5);
            var points=Route(origin,centre,Vector2.zero,0);
            Assert.That(points.All(p=>Vector3.Distance(p,origin)<.0001f),Is.True);
        }

        [Test]
        public void BeamSegmentDistance_IncludesInteriorAndEnds()
        {
            var method=Pilot.GetMethod("SegmentDistance",BindingFlags.NonPublic|BindingFlags.Static);
            float Inside(Vector3 p) => (float)method.Invoke(null,new object[]{p,Vector3.zero,new Vector3(10,0,0)});
            Assert.That(Inside(new Vector3(5,2,0)),Is.EqualTo(2).Within(.0001f));
            Assert.That(Inside(new Vector3(12,0,0)),Is.EqualTo(2).Within(.0001f));
        }

        [Test]
        public void HomedHeight_ClosesOnTheTarget_AtTheEnemysResponse()
        {
            var method=Pilot.GetMethod("HomedHeight",BindingFlags.NonPublic|BindingFlags.Static);
            float At(float elapsed) => (float)method.Invoke(null,new object[]{10f,6f,2f,elapsed});
            Assert.That(At(0),Is.EqualTo(10).Within(.0001f));
            // Enemy 2's response of 2 closes 63% of the gap in half a second.
            Assert.That(At(.5f),Is.EqualTo(6+4*Mathf.Exp(-1)).Within(.0001f));
            Assert.That(At(10),Is.EqualTo(6).Within(.001f));
        }

        // The game's own chase, EnemyMovement's ShipMotion.Approach, run in fine steps
        // toward a player whose height is given at each moment.
        private static float Simulated(float start, Func<float,float> player, float rate, float seconds)
        {
            const float dt=.0005f;
            float height=start;
            for(float t=0;t<seconds;t+=dt) height=ShipMotion.Approach(height,player(t+dt),rate,dt);
            return height;
        }

        [Test]
        public void ChaseAlong_FollowsAPlayerWhoClimbs_NotTheTopOfTheClimb()
        {
            var method=Pilot.GetMethod("ChaseAlong",BindingFlags.NonPublic|BindingFlags.Static);
            // Held climb from 0 at 2 units a second: the fixture's climb, for the whole horizon.
            var route=Route(new Vector3(10,0,0),Vector3.zero,Vector2.zero,0,request:Vector2.up);
            Func<int,Vector3> beside=i=>route[i];
            var heights=(float[])method.Invoke(null,new object[]{0f,2f,100f,route,beside});
            float seconds=route.Length*.02f;
            float expected=Simulated(0,t=>route[Mathf.Clamp(Mathf.CeilToInt(t/.02f)-1,0,route.Length-1)].y,2f,seconds);
            Assert.That(heights.Last(),Is.EqualTo(expected).Within(.02f));
            // Worked out in one go toward the route's last height, as it was before:
            // the chaser is forecast well above where it really is.
            var closed=Pilot.GetMethod("HomedHeight",BindingFlags.NonPublic|BindingFlags.Static);
            float oneGo=(float)closed.Invoke(null,new object[]{0f,route.Last().y,2f,seconds});
            Assume.That(oneGo-expected,Is.GreaterThan(.5f));
        }

        [Test]
        public void ChaseAlong_HoldsItsHeight_OutOfRange()
        {
            var method=Pilot.GetMethod("ChaseAlong",BindingFlags.NonPublic|BindingFlags.Static);
            var route=Route(new Vector3(10,0,0),Vector3.zero,Vector2.zero,0,request:Vector2.up);
            Func<int,Vector3> farAway=i=>new Vector3(-10,0,0);
            var heights=(float[])method.Invoke(null,new object[]{3f,2f,5f,route,farAway});
            Assert.That(heights.All(h=>h==3f),Is.True);
        }

        [Test]
        public void CaughtUp_FollowsWhereThePlayerWentDuringTheDelay()
        {
            var method=Pilot.GetMethod("CaughtUp",BindingFlags.NonPublic|BindingFlags.Static);
            // Seen at 5 with the player at 5; the player has since dropped to 2 over a
            // third of a second. The chaser follows the drop, not a player at 2 all along.
            float caught=(float)method.Invoke(null,new object[]{5f,5f,2f,2f,.33f});
            float expected=Simulated(5,t=>Mathf.Lerp(5,2,t/.33f),2f,.33f);
            Assert.That(caught,Is.EqualTo(expected).Within(.02f));
            Assert.That((float)method.Invoke(null,new object[]{5f,5f,2f,2f,0f}),Is.EqualTo(5f));
        }

        [Test]
        public void LineOfFire_IsAheadOfTheGunOnly_AtItsHeight_AndNear()
        {
            var method=Pilot.GetMethod("InLineOfFire",BindingFlags.NonPublic|BindingFlags.Static);
            bool In(float direction,float angle,float height) => (bool)method.Invoke(null,new object[]{0f,8f,direction,angle,height});
            Assert.That(In(1,20,8),Is.True);
            Assert.That(In(-1,20,8),Is.False, "behind the gun");
            Assert.That(In(-1,-20,8),Is.True);
            Assert.That(In(1,20,8.5f),Is.False, "off its height");
            Assert.That(In(1,80,8),Is.False, "far enough to see the rounds coming");
            Assert.That(In(1,350,8),Is.False, "wraps the short way round");
        }

        [Test]
        public void HeightMiss_RewardsPassingThrough_AndHoldingBeatsOvershooting()
        {
            var method=Pilot.GetMethod("HeightMiss",BindingFlags.NonPublic|BindingFlags.Static);
            const float target=7.47f, above=target+.6f;
            Vector3[] Held(float from, Vector2 request) =>
                Route(new Vector3(10,from,0),Vector3.zero,Vector2.zero,0,request:request);
            float Miss(Vector3[] route) => (float)method.Invoke(null,new object[]{route,target});
            float EndMiss(Vector3[] route) => Mathf.Abs(route.Last().y-target);
            // The 22 Sep Prow, at this fixture's climb: a held descent ends further
            // past the height than staying put misses it. Scored on the end, the
            // pilot stays where it is and shoots over the target.
            Assume.That(EndMiss(Held(above,Vector2.down)),Is.GreaterThan(EndMiss(Held(above,Vector2.zero))));
            Assert.That(Miss(Held(above,Vector2.down)),Is.LessThan(Miss(Held(above,Vector2.zero))));
            Assert.That(Miss(Held(target,Vector2.zero)),Is.LessThan(Miss(Held(target,Vector2.down))),
                "once there, it holds the height");
        }
    }
}
