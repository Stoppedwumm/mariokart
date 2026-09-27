using UnityEngine;

namespace KartRacer
{
    public enum ItemType
    {
        None,
        Mushroom,
        TripleMushroom,
        Banana,
        GreenShell,
        RedShell,
        Star,
    }

    public static class Items
    {
        public static readonly ItemType[] All =
        {
            ItemType.Mushroom, ItemType.TripleMushroom, ItemType.Banana,
            ItemType.GreenShell, ItemType.RedShell, ItemType.Star,
        };

        public static string Name(ItemType t)
        {
            switch (t)
            {
                case ItemType.Mushroom: return "Mushroom";
                case ItemType.TripleMushroom: return "Triple Mushroom";
                case ItemType.Banana: return "Banana";
                case ItemType.GreenShell: return "Green Shell";
                case ItemType.RedShell: return "Red Shell";
                case ItemType.Star: return "Star";
                default: return "";
            }
        }

        public static Color Color(ItemType t)
        {
            switch (t)
            {
                case ItemType.Mushroom:
                case ItemType.TripleMushroom: return new Color(0.9f, 0.2f, 0.15f);
                case ItemType.Banana: return new Color(1f, 0.9f, 0.2f);
                case ItemType.GreenShell: return new Color(0.2f, 0.8f, 0.25f);
                case ItemType.RedShell: return new Color(0.95f, 0.15f, 0.2f);
                case ItemType.Star: return new Color(1f, 0.85f, 0.1f);
                default: return UnityEngine.Color.gray;
            }
        }

        /// <summary>
        /// Position-weighted item roll: leaders mostly get defensive items,
        /// karts at the back get boosts, homing shells and stars.
        /// </summary>
        public static ItemType Roll(int place, int racers)
        {
            float t = racers > 1 ? (place - 1f) / (racers - 1f) : 0f;
            float[] w =
            {
                Mathf.Lerp(25f, 18f, t), // Mushroom
                Mathf.Lerp(0f, 22f, t),  // Triple mushroom
                Mathf.Lerp(35f, 4f, t),  // Banana
                Mathf.Lerp(30f, 10f, t), // Green shell
                Mathf.Lerp(4f, 26f, t),  // Red shell
                Mathf.Lerp(0f, 14f, t),  // Star
            };
            float total = 0f;
            foreach (float x in w) total += x;
            float r = Random.value * total;
            for (int i = 0; i < w.Length; i++)
            {
                if (r < w[i]) return All[i];
                r -= w[i];
            }
            return ItemType.Mushroom;
        }
    }
}
