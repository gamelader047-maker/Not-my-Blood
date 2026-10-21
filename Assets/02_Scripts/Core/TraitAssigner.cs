using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using NotMyBlood.Player;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Logik zur regelkonformen Zufallsverteilung von Merkmalen und der Mörder-Rolle auf alle Spieler.
    /// Server-Autoritär konzipiert.
    /// </summary>
    public static class TraitAssigner
    {
        private const int MaxAttempts = 1000;

        /// <summary>
        /// Verteilt Merkmale und Mörder-Rolle nach den Regeln aus Kapitel 3 auf die Spielerliste.
        /// </summary>
        public static bool Assign(List<PlayerIdentity> players)
        {
            if (players == null || players.Count < 3)
            {
                Debug.LogWarning("[TraitAssigner] Zu wenige Spieler zum Vergeben von Merkmalen (min. 3).");
                return false;
            }

            int count = players.Count;

            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (TryGenerateTraits(count, out List<PlayerTraits> traitsList))
                {
                    // Zufälligen Mörder bestimmen
                    int murdererIndex = UnityEngine.Random.Range(0, count);

                    for (int i = 0; i < count; i++)
                    {
                        string defaultName = players[i].PlayerName;
                        if (string.IsNullOrEmpty(defaultName))
                        {
                            defaultName = $"Spieler_{i + 1}";
                        }

                        bool isMurderer = (i == murdererIndex);
                        players[i].SetIdentity(defaultName, traitsList[i], isMurderer);
                    }

                    return true;
                }
            }

            Debug.LogError("[TraitAssigner] Konnte nach 1000 Versuchen keine gültige Merkmalsverteilung erzeugen.");
            return false;
        }

        private static bool TryGenerateTraits(int count, out List<PlayerTraits> generated)
        {
            generated = new List<PlayerTraits>();

            // Regel 2: 3-5 Spieler = pro Merkmal nur 2 der 3 Werte; 6 Spieler = alle 3 Werte.
            List<BloodType> allowedBlood = SelectAllowedValues<BloodType>(count);
            List<ShoeProfile> allowedShoe = SelectAllowedValues<ShoeProfile>(count);
            List<ClothingColor> allowedColor = SelectAllowedValues<ClothingColor>(count);

            HashSet<PlayerTraits> uniqueCombinations = new HashSet<PlayerTraits>();

            for (int i = 0; i < count; i++)
            {
                BloodType b = allowedBlood[UnityEngine.Random.Range(0, allowedBlood.Count)];
                ShoeProfile s = allowedShoe[UnityEngine.Random.Range(0, allowedShoe.Count)];
                ClothingColor c = allowedColor[UnityEngine.Random.Range(0, allowedColor.Count)];

                PlayerTraits trait = new PlayerTraits(b, s, c);

                // Regel 1: keine zwei Spieler mit derselben Kombination
                if (uniqueCombinations.Contains(trait))
                {
                    return false;
                }

                uniqueCombinations.Add(trait);
                generated.Add(trait);
            }

            // Regel 3: ab 4 Spielern muss jeder verwendete Wert bei mindestens 2 Spielern vorkommen
            if (count >= 4)
            {
                if (!ValidateMinimumOccurrences(generated, allowedBlood, allowedShoe, allowedColor))
                {
                    return false;
                }
            }

            return true;
        }

        private static List<T> SelectAllowedValues<T>(int playerCount) where T : Enum
        {
            T[] allValues = (T[])Enum.GetValues(typeof(T));
            // Alle Werte sind gültig – auch der erste Enum-Wert (Blutgruppe 0, Kreise, Rot)!
            List<T> validValues = allValues.ToList();

            if (playerCount >= 6)
            {
                return validValues;
            }

            // Bei 3-5 Spielern zufällig 2 der 3 Werte auswählen
            while (validValues.Count > 2)
            {
                validValues.RemoveAt(UnityEngine.Random.Range(0, validValues.Count));
            }

            return validValues;
        }

        private static bool ValidateMinimumOccurrences(List<PlayerTraits> traits, List<BloodType> allowedBlood, List<ShoeProfile> allowedShoe, List<ClothingColor> allowedColor)
        {
            foreach (var b in allowedBlood)
            {
                if (traits.Count(t => t.blood == b) < 2) return false;
            }

            foreach (var s in allowedShoe)
            {
                if (traits.Count(t => t.shoe == s) < 2) return false;
            }

            foreach (var c in allowedColor)
            {
                if (traits.Count(t => t.color == c) < 2) return false;
            }

            return true;
        }
    }
}