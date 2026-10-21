using System;
using UnityEngine;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Repräsentiert die optischen und forensischen Merkmale eines Spielers.
    /// </summary>
    [Serializable]
    public struct PlayerTraits : IEquatable<PlayerTraits>
    {
        public BloodType blood;
        public ShoeProfile shoe;
        public ClothingColor color;

        public PlayerTraits(BloodType blood, ShoeProfile shoe, ClothingColor color)
        {
            this.blood = blood;
            this.shoe = shoe;
            this.color = color;
        }

        public bool Equals(PlayerTraits other)
        {
            return blood == other.blood && shoe == other.shoe && color == other.color;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerTraits other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)blood, (int)shoe, (int)color);
        }

        public static bool operator ==(PlayerTraits left, PlayerTraits right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerTraits left, PlayerTraits right)
        {
            return !left.Equals(right);
        }
    }
}