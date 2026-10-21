using System;
using UnityEngine;

namespace NotMyBlood.Core
{
    /// <summary>Welches Merkmal eine Spur verrät (Konzept 1.1: pro Spur genau EIN Merkmal).</summary>
    public enum ClueCategory { Blut, Schuh, Faser }

    /// <summary>Blutgruppen laut Konzept 1.1. "Null" wird im UI als "0" angezeigt.</summary>
    public enum BloodType { Null, A, B }

    public enum ShoeProfile { Kreise, Zickzack, Glatt }

    public enum ClothingColor { Rot, Schwarz, Grau }

    /// <summary>
    /// Eine notierte Spur. Es zählt nur das Wert-Feld, das zur Kategorie passt
    /// (Blut → bloodType, Schuh → shoeProfile, Faser → clothingColor).
    /// </summary>
    [Serializable]
    public struct ClueData
    {
        public string clueName;          // leer = Standardname ("Blutspur", ...)
        public string description;       // leer = Standardtext
        public string location;          // Fundort, z. B. "Küche"
        public ClueCategory category;
        public BloodType bloodType;
        public ShoeProfile shoeProfile;
        public ClothingColor clothingColor;
        public float foundTime;          // Sekunden seit Spielstart (später: Rundentimer)

        public string DisplayName => string.IsNullOrWhiteSpace(clueName) ? DefaultName(category) : clueName;

        public string DescriptionText => string.IsNullOrWhiteSpace(description) ? DefaultDescription(category) : description;

        public string LocationText => string.IsNullOrWhiteSpace(location) ? "Unbekannt" : location;

        /// <summary>Der Wert als Text, z. B. "A", "0", "Zickzack", "Schwarz".</summary>
        public string ValueText => category switch
        {
            ClueCategory.Blut => BloodTypeText(bloodType),
            ClueCategory.Schuh => shoeProfile.ToString(),
            _ => clothingColor.ToString(),
        };

        /// <summary>Name des Merkmals, z. B. "Blutgruppe".</summary>
        public string TraitLabel => TraitName(category);

        /// <summary>Fundzeit als mm:ss.</summary>
        public string FoundTimeText
        {
            get
            {
                int t = Mathf.Max(0, Mathf.FloorToInt(foundTime));
                return $"{t / 60:00}:{t % 60:00}";
            }
        }

        public static string BloodTypeText(BloodType type) => type == BloodType.Null ? "0" : type.ToString();

        public static string DefaultName(ClueCategory category) => category switch
        {
            ClueCategory.Blut => "Blutspur",
            ClueCategory.Schuh => "Schuhabdruck",
            _ => "Textilfaser",
        };

        public static string TraitName(ClueCategory category) => category switch
        {
            ClueCategory.Blut => "Blutgruppe",
            ClueCategory.Schuh => "Schuhprofil",
            _ => "Kleidungsfarbe",
        };

        public static string DefaultDescription(ClueCategory category) => category switch
        {
            ClueCategory.Blut => "Eine Blutspur. Die Blutgruppe lässt sich bestimmen.",
            ClueCategory.Schuh => "Ein Schuhabdruck. Das Sohlenprofil ist gut zu erkennen.",
            _ => "Eine Textilfaser. Die Farbe passt zu einem Kleidungsstück.",
        };
    }
}
