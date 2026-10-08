using BeMyArms.Core;
using BeMyArms.Match;
using NUnit.Framework;

namespace BeMyArms.Client.Tests
{
    public class CombatFeedbackTests
    {
        [Test]
        public void FeedbackDistinguishesOwnRoleFromPartnerContribution()
        {
            var e = new DamageEvent { Kind = DamageKind.Weapon, Region = HitboxRegion.Region.Head };
            Assert.AreEqual("HEAD HIT", CombatFeedback.ConfirmationText(e, 1));
            Assert.AreEqual("PARTNER HEAD HIT", CombatFeedback.ConfirmationText(e, 0));
            e.Region = HitboxRegion.Region.Body;
            Assert.AreEqual("", CombatFeedback.ConfirmationText(e, 1));
            Assert.AreEqual("PARTNER HIT", CombatFeedback.ConfirmationText(e, 0));
            e.Kind = DamageKind.Kick;
            Assert.AreEqual("KICK HIT", CombatFeedback.ConfirmationText(e, 0));
            Assert.AreEqual("PARTNER KICK", CombatFeedback.ConfirmationText(e, 1));
            e.Killed = true;
            StringAssert.Contains("ELIMINATION", CombatFeedback.ConfirmationText(e, 1));
        }
    }
}
