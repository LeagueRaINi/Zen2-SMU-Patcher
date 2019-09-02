using System;
using System.Collections.Generic;
using System.Linq;

namespace SMUPNET.Utils
{
    // FIXME:
    // this class is partially broken it has problems with some patterns
    public static class BoyerMoore
    {
        private static int[] CreateBadMatchingsTable((byte, bool)[] parsedPattern)
        {
            var result = new int[256];
            var lastPatternByteIndex = parsedPattern.Length - 1;
            var mask = parsedPattern.Select(x => x.Item2).ToArray();

            var lastDiff = lastPatternByteIndex - Array.LastIndexOf(mask, false);
            var firstDiff = lastPatternByteIndex - Array.IndexOf(mask, false);

            var diff = firstDiff > lastDiff ? firstDiff : lastDiff;
            if (diff == 0) {
                diff = 1;
            }

            for (var i = 0; i < 256; i++) {
                result[i] = diff;
            }
            for (var i = 0; i < lastPatternByteIndex; i++) {
                result[parsedPattern[i].Item1 & 0xFF] = lastPatternByteIndex - i;
            }

            return result;
        }

        public static List<int> Search(byte[] haystack, string pattern)
        {
            var parsedPattern = pattern.Split(' ')
                .Select(
                    hex => hex.Contains('?')
                        ? (byte.MinValue, false)
                        : (Convert.ToByte(hex, 16), true))
                .ToArray();

            if (!parsedPattern.Any()) {
                throw new ArgumentException("Invalid pattern");
            }
            if (parsedPattern.Length > haystack.Length) {
                throw new ArgumentException("Haystack cannot be smaller than the pattern");
            }

            var result = new List<int>();
            var index = 0;
            var limit = haystack.Length - parsedPattern.Length;
            var badMatchingsTable = CreateBadMatchingsTable(parsedPattern);
            var lastPatternByteIndex = parsedPattern.Length - 1;
            while (index <= limit) {
                for (var i = lastPatternByteIndex; haystack[index + i] == parsedPattern[i].Item1 || !parsedPattern[i].Item2; --i) {
                    if (i == 0) {
                        result.Add(index);
                        break;
                    }
                }

                index += Math.Max(badMatchingsTable[haystack[index + lastPatternByteIndex] & 0xFF], 1);
            }

            return result;
        }
    }
}
