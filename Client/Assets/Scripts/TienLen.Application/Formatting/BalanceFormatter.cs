using System;
using System.Globalization;

namespace TienLen.Application.Formatting
{
    /// <summary>
    /// Formats balances for compact UI display using k/M/B suffixes.
    /// </summary>
    public static class BalanceFormatter
    {
        private const long Thousand = 1000;
        private const long Million = 1_000_000;
        private const long Billion = 1_000_000_000;

        /// <summary>
        /// Formats a balance value into a short string (e.g. 10000 -> 10k, 1000000 -> 1M).
        /// </summary>
        /// <param name="value">The balance to format.</param>
        /// <returns>Short-form balance string.</returns>
        public static string FormatShort(long value)
        {
            var absValue = Math.Abs(value);

            if (absValue < Thousand)
            {
                return value.ToString("0", CultureInfo.InvariantCulture);
            }

            if (absValue < Million)
            {
                return FormatWithSuffix(value, Thousand, "k", Million, "M");
            }

            if (absValue < Billion)
            {
                return FormatWithSuffix(value, Million, "M", Billion, "B");
            }

            return FormatWithSuffix(value, Billion, "B", null, null);
        }

        private static string FormatWithSuffix(long value, long divisor, string suffix, long? nextDivisor, string nextSuffix)
        {
            var formatted = FormatScaled(value, divisor, suffix);

            if (nextDivisor.HasValue)
            {
                var scaled = value / (double)divisor;
                var rounded = Math.Round(scaled, 1, MidpointRounding.AwayFromZero);
                if (Math.Abs(rounded) >= 1000d)
                {
                    // If rounding pushes the value across the next suffix threshold, reformat at that scale.
                    formatted = FormatScaled(value, nextDivisor.Value, nextSuffix);
                }
            }

            return formatted;
        }

        private static string FormatScaled(long value, long divisor, string suffix)
        {
            var scaled = value / (double)divisor;
            var rounded = Math.Round(scaled, 1, MidpointRounding.AwayFromZero);
            return string.Concat(rounded.ToString("0.#", CultureInfo.InvariantCulture), suffix);
        }
    }
}
