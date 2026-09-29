using System;
using UnityEngine;

namespace DublinRetrofit
{
    // Maps SEAI BER labels (A1 ... G) to colours and to a rank for comparisons.
    // Static because it is pure lookup data with no state; the colours follow a
    // green-to-red diverging scale so "better" and "worse" read at a glance.
    public static class BerPalette
    {
        // Every BER label in order from best to worst. Rank = index in this array.
        public static readonly string[] Labels =
        {
            "A1", "A2", "A3", "B1", "B2", "B3", "C1", "C2", "C3",
            "D1", "D2", "E1", "E2", "F", "G",
        };

        // Legend groups: one colour per letter, as in the brief.
        public static readonly string[] Letters = { "A", "B", "C", "D", "E", "F", "G" };

        static readonly Color[] LetterColors =
        {
            Hex("#1a9850"), // A
            Hex("#91cf60"), // B
            Hex("#fee08b"), // C
            Hex("#fdae61"), // D
            Hex("#f46d43"), // E
            Hex("#d73027"), // F
            Hex("#a50026"), // G
        };

        // Neutral grey for buildings with no BER (sheds, garages), so they read as "no data"
        // rather than as good or bad.
        public static readonly Color NotRated = new Color(0.72f, 0.72f, 0.72f);

        // The colour depends only on the letter, so "D1" and "D2" share a colour.
        public static Color ColorFor(string label)
        {
            int letter = LetterIndex(label);
            return letter >= 0 ? LetterColors[letter] : NotRated;
        }

        public static Color ColorForLetter(int letterIndex) => LetterColors[letterIndex];

        public static int LetterIndex(string label) =>
            string.IsNullOrEmpty(label) ? -1 : Array.IndexOf(Letters, label.Substring(0, 1));

        public static int Rank(string label) => Array.IndexOf(Labels, label);

        // "D or worse" is the usual policy threshold for poorly performing homes.
        public static bool IsDOrWorse(string label) => Rank(label) >= Rank("D1");

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
