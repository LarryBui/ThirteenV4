using NUnit.Framework;
using TienLen.Application.Formatting;

namespace TienLen.Application.Tests
{
    public sealed class BalanceFormatterTests
    {
        [TestCase(0, "0")]
        [TestCase(999, "999")]
        [TestCase(1000, "1k")]
        [TestCase(10_000, "10k")]
        [TestCase(12_500, "12.5k")]
        [TestCase(1_000_000, "1M")]
        [TestCase(1_200_000, "1.2M")]
        [TestCase(2_000_000_000, "2B")]
        [TestCase(-12_500, "-12.5k")]
        public void FormatShort_UsesCompactSuffix(long value, string expected)
        {
            var result = BalanceFormatter.FormatShort(value);

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void FormatShort_RoundsUpToNextSuffixWhenNeeded()
        {
            var result = BalanceFormatter.FormatShort(999_500);

            Assert.That(result, Is.EqualTo("1M"));
        }
    }
}