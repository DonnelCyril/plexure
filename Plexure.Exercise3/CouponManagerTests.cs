using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Plexure.Exercise3.sut;

namespace Plexure.Exercise3
{
    [TestClass]
    public class CouponManagerTests
    {

        [DataRow(true, true, true, DisplayName = "Throws exception when all ILogger and ICouponProvider is null")]
        [DataRow(true, false, true, DisplayName = "Throws exception when ILogger is null")]
        [DataRow(false, true, true, DisplayName = "Throws exception when ICouponProvider is null")]
        [DataRow(false, false, false, DisplayName = "Can create a CouponManager when all parameters are provided")]
        [DataTestMethod]
        public async Task Arguments_for_the_CouponManager_class_are_validated(bool loggerIsNull, bool couponProviderIsNull, bool exceptionExpected)
        {
            var mockLogger = loggerIsNull ? null : Mock.Of<ILogger>();
            var mockProvider = couponProviderIsNull ? null : Mock.Of<ICouponProvider>();
            if (exceptionExpected)
            {
                Assert.ThrowsException<ArgumentNullException>(() => new CouponManager(mockLogger, mockProvider));
                return;
            }
            var couponManager = new CouponManager(mockLogger, mockProvider);
            Assert.IsNotNull(couponManager);
        }

        [TestMethod]
        public async Task Arguments_are_validated_for_CanRedeemCoupon_method()
        {
            var mockLogger = Mock.Of<ILogger>();
            var mockProvider = Mock.Of<ICouponProvider>();
            var sut = new CouponManager(mockLogger, mockProvider);
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => sut.CanRedeemCoupon(Guid.NewGuid(), Guid.NewGuid(), null));
        }

        [TestMethod]
        public async Task Invalid_couponId_throws_a_KeyNotFound_exception()
        {
            var mockProvider = new Mock<ICouponProvider>();
            mockProvider.Setup(p => p.Retrieve(It.IsAny<Guid>())).ReturnsAsync(() => null);
            var sut = CreateSut(mockProvider.Object);
            var emptyEvaluators = Enumerable.Empty<Func<Coupon, Guid, bool>>();
            await Assert.ThrowsExceptionAsync<KeyNotFoundException>(() =>
                sut.CanRedeemCoupon(Guid.NewGuid(), Guid.NewGuid(), emptyEvaluators));
        }

        [TestMethod]
        public async Task Can_redeem_a_coupon_if_there_are_no_evaluators()
        {
            var sut = CreateSut();
            var emptyEvaluators = Enumerable.Empty<Func<Coupon, Guid, bool>>();
            var canRedeemCoupon = await sut.CanRedeemCoupon(Guid.NewGuid(), Guid.NewGuid(), emptyEvaluators);
            Assert.IsTrue(canRedeemCoupon);
        }

        [TestMethod]
        public async Task Cannot_redeem_a_coupon_if_it_is_invalid_across_all_evaluators()
        {
            var sut = CreateSut();
            var evaluators = new List<Func<Coupon, Guid, bool>>()
            {
                (coupon, id) => false,
                (coupon, id) => false
            };
            var canRedeemCoupon = await sut.CanRedeemCoupon(Guid.NewGuid(), Guid.NewGuid(), evaluators);
            Assert.IsFalse(canRedeemCoupon);
        }

        [TestMethod]
        public async Task Can_redeem_a_coupon_if_it_valid_by_atleast_one_evaluator()
        {
            var sut = CreateSut();
            var evaluators = new List<Func<Coupon, Guid, bool>>()
            {
                (coupon, id) => false,
                (coupon, id) => true,
                (coupon, id) => false
            };
            var canRedeemCoupon = await sut.CanRedeemCoupon(Guid.NewGuid(), Guid.NewGuid(), evaluators);
            Assert.IsTrue(canRedeemCoupon);
        }

        private static CouponManager CreateSut(ICouponProvider couponProvider = null)
        {
            var mockLogger = Mock.Of<ILogger>();
            return new CouponManager(mockLogger, couponProvider ?? GetCouponProvider());

            static ICouponProvider GetCouponProvider()
            {
                var mockProvider = new Mock<ICouponProvider>();
                mockProvider.Setup(p => p.Retrieve(It.IsAny<Guid>())).ReturnsAsync(() => new Coupon());
                return mockProvider.Object;
            }
        }

    }
}
