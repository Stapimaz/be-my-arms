using BeMyArms.Networking;
using NUnit.Framework;

namespace BeMyArms.Networking.Tests
{
    /// <summary>Connection-to-role binding, token reconnect and the double-role edge.</summary>
    public class RoleRegistryTests
    {
        [Test]
        public void AssignsRequestedRoles_AndRejectsUnauthorized()
        {
            var reg = new BodyRoleRegistry();
            Assert.AreEqual(BodyRoleRegistry.RoleP1, reg.Assign(1, "", BodyRoleRegistry.RoleP1, out _));
            Assert.AreEqual(BodyRoleRegistry.RoleP2, reg.Assign(2, "", BodyRoleRegistry.RoleP2, out _));

            Assert.IsTrue(reg.HasRole(1, BodyRoleRegistry.RoleP1));
            Assert.IsFalse(reg.HasRole(1, BodyRoleRegistry.RoleP2), "A P1 connection must not be authorized for P2 input.");
            Assert.IsFalse(reg.HasRole(2, BodyRoleRegistry.RoleP1));
        }

        [Test]
        public void SecondClientRequestingTakenRole_GetsTheFreeRole()
        {
            var reg = new BodyRoleRegistry();
            reg.Assign(1, "", BodyRoleRegistry.RoleP1, out _);
            Assert.AreEqual(BodyRoleRegistry.RoleP2, reg.Assign(2, "", BodyRoleRegistry.RoleP1, out _));
        }

        [Test]
        public void TokenReconnect_RestoresRole_AfterDisconnect()
        {
            var reg = new BodyRoleRegistry();
            reg.Assign(1, "p1", BodyRoleRegistry.RoleP1, out _);
            reg.Release(1);

            Assert.AreEqual(BodyRoleRegistry.RoleP1, reg.Assign(9, "p1", BodyRoleRegistry.RoleNone, out ulong displaced));
            Assert.AreEqual(ulong.MaxValue, displaced);
        }

        [Test]
        public void DoubleRole_ReconnectingBeforeTimeout_DisplacesOldConnection()
        {
            var reg = new BodyRoleRegistry();
            reg.Assign(1, "p1", BodyRoleRegistry.RoleP1, out _);

            // Same token reconnects while client 1 still holds P1: role is reclaimed, old is displaced.
            Assert.AreEqual(BodyRoleRegistry.RoleP1, reg.Assign(7, "p1", BodyRoleRegistry.RoleP1, out ulong displaced));
            Assert.AreEqual(1ul, displaced, "The server must be told to drop the stale owner.");
            Assert.IsTrue(reg.HasRole(7, BodyRoleRegistry.RoleP1));
            Assert.IsFalse(reg.HasRole(1, BodyRoleRegistry.RoleP1));
        }

        [Test]
        public void ThirdClient_WithBothRolesTaken_IsSpectator()
        {
            var reg = new BodyRoleRegistry();
            reg.Assign(1, "", BodyRoleRegistry.RoleP1, out _);
            reg.Assign(2, "", BodyRoleRegistry.RoleP2, out _);
            Assert.AreEqual(BodyRoleRegistry.RoleNone, reg.Assign(3, "", BodyRoleRegistry.RoleNone, out _));
        }

        [Test]
        public void DisconnectedRole_GivenToBot_HasExactlyOneOwner()
        {
            var reg = new BodyRoleRegistry();
            reg.Assign(1, "p1", BodyRoleRegistry.RoleP1, out _);

            byte role = reg.Release(1);
            Assert.AreEqual(BodyRoleRegistry.RoleP1, role);
            reg.SetBot(role);

            Assert.IsTrue(reg.IsBot(BodyRoleRegistry.RoleP1));
            Assert.IsTrue(reg.HasSingleOwner(BodyRoleRegistry.RoleP1));
            Assert.IsFalse(reg.HasRole(1, BodyRoleRegistry.RoleP1), "The stale connection must not own the role.");
        }

        [Test]
        public void ReconnectingHuman_AtomicallyReplacesBot_NoDoubleOwner()
        {
            var reg = new BodyRoleRegistry();
            reg.Assign(1, "p1", BodyRoleRegistry.RoleP1, out _);
            reg.Release(1);
            reg.SetBot(BodyRoleRegistry.RoleP1);

            // Same token reconnects: human takes the role, bot is cleared.
            Assert.AreEqual(BodyRoleRegistry.RoleP1, reg.Assign(9, "p1", BodyRoleRegistry.RoleNone, out _));
            Assert.IsFalse(reg.IsBot(BodyRoleRegistry.RoleP1), "The bot must relinquish the role on human reconnect.");
            Assert.IsTrue(reg.HasRole(9, BodyRoleRegistry.RoleP1));
            Assert.IsFalse(reg.HasRole(1, BodyRoleRegistry.RoleP1));
            Assert.IsTrue(reg.HasSingleOwner(BodyRoleRegistry.RoleP1));
        }
    }
}
