using BeMyArms.M2;
using NUnit.Framework;

namespace BeMyArms.M2.Tests
{
    /// <summary>Connection-to-role binding, token reconnect and the double-role edge.</summary>
    public class M2RoleRegistryTests
    {
        [Test]
        public void AssignsRequestedRoles_AndRejectsUnauthorized()
        {
            var reg = new M2RoleRegistry();
            Assert.AreEqual(M2RoleRegistry.RoleP1, reg.Assign(1, "", M2RoleRegistry.RoleP1, out _));
            Assert.AreEqual(M2RoleRegistry.RoleP2, reg.Assign(2, "", M2RoleRegistry.RoleP2, out _));

            Assert.IsTrue(reg.HasRole(1, M2RoleRegistry.RoleP1));
            Assert.IsFalse(reg.HasRole(1, M2RoleRegistry.RoleP2), "A P1 connection must not be authorized for P2 input.");
            Assert.IsFalse(reg.HasRole(2, M2RoleRegistry.RoleP1));
        }

        [Test]
        public void SecondClientRequestingTakenRole_GetsTheFreeRole()
        {
            var reg = new M2RoleRegistry();
            reg.Assign(1, "", M2RoleRegistry.RoleP1, out _);
            Assert.AreEqual(M2RoleRegistry.RoleP2, reg.Assign(2, "", M2RoleRegistry.RoleP1, out _));
        }

        [Test]
        public void TokenReconnect_RestoresRole_AfterDisconnect()
        {
            var reg = new M2RoleRegistry();
            reg.Assign(1, "p1", M2RoleRegistry.RoleP1, out _);
            reg.Release(1);

            Assert.AreEqual(M2RoleRegistry.RoleP1, reg.Assign(9, "p1", M2RoleRegistry.RoleNone, out ulong displaced));
            Assert.AreEqual(ulong.MaxValue, displaced);
        }

        [Test]
        public void DoubleRole_ReconnectingBeforeTimeout_DisplacesOldConnection()
        {
            var reg = new M2RoleRegistry();
            reg.Assign(1, "p1", M2RoleRegistry.RoleP1, out _);

            // Same token reconnects while client 1 still holds P1: role is reclaimed, old is displaced.
            Assert.AreEqual(M2RoleRegistry.RoleP1, reg.Assign(7, "p1", M2RoleRegistry.RoleP1, out ulong displaced));
            Assert.AreEqual(1ul, displaced, "The server must be told to drop the stale owner.");
            Assert.IsTrue(reg.HasRole(7, M2RoleRegistry.RoleP1));
            Assert.IsFalse(reg.HasRole(1, M2RoleRegistry.RoleP1));
        }

        [Test]
        public void ThirdClient_WithBothRolesTaken_IsSpectator()
        {
            var reg = new M2RoleRegistry();
            reg.Assign(1, "", M2RoleRegistry.RoleP1, out _);
            reg.Assign(2, "", M2RoleRegistry.RoleP2, out _);
            Assert.AreEqual(M2RoleRegistry.RoleNone, reg.Assign(3, "", M2RoleRegistry.RoleNone, out _));
        }
    }
}
