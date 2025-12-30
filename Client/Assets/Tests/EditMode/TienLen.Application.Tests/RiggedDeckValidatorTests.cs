using System.Collections.Generic;
using NUnit.Framework;
using TienLen.Application;

namespace TienLen.Application.Tests
{
    /// <summary>
    /// Tests for rigged deck request validation.
    /// </summary>
    public sealed class RiggedDeckValidatorTests
    {
        [Test]
        public void TryValidate_WhenRequestIsValid_ReturnsTrue()
        {
            var request = new RiggedDeckRequestDto(
                "match-1",
                new List<RiggedHandDto>
                {
                    new RiggedHandDto(0, new List<RiggedCardDto> { new RiggedCardDto(12, 0) })
                });

            var result = RiggedDeckValidator.TryValidate(request, out var error);

            Assert.IsTrue(result);
            Assert.IsNull(error);
        }

        [Test]
        public void TryValidate_WhenSeatOutOfRange_ReturnsFalse()
        {
            var request = new RiggedDeckRequestDto(
                "match-1",
                new List<RiggedHandDto>
                {
                    new RiggedHandDto(4, new List<RiggedCardDto>())
                });

            var result = RiggedDeckValidator.TryValidate(request, out var error);

            Assert.IsFalse(result);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryValidate_WhenDuplicateCard_ReturnsFalse()
        {
            var hands = new List<RiggedHandDto>
            {
                new RiggedHandDto(0, new List<RiggedCardDto> { new RiggedCardDto(12, 0) }),
                new RiggedHandDto(1, new List<RiggedCardDto> { new RiggedCardDto(12, 0) })
            };
            var request = new RiggedDeckRequestDto("match-1", hands);

            var result = RiggedDeckValidator.TryValidate(request, out var error);

            Assert.IsFalse(result);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void TryValidate_WhenHandTextsProvided_ReturnsTrue()
        {
            var request = new RiggedDeckRequestDto(
                "match-1",
                new List<RiggedHandDto>(),
                new List<RiggedHandTextDto>
                {
                    new RiggedHandTextDto(0, "3H, 3D")
                });

            var result = RiggedDeckValidator.TryValidate(request, out var error);

            Assert.IsTrue(result);
            Assert.IsNull(error);
        }

        [Test]
        public void TryValidate_WhenNoHandsProvided_ReturnsFalse()
        {
            var request = new RiggedDeckRequestDto("match-1", new List<RiggedHandDto>(), new List<RiggedHandTextDto>());

            var result = RiggedDeckValidator.TryValidate(request, out var error);

            Assert.IsFalse(result);
            Assert.IsNotEmpty(error);
        }
    }
}
