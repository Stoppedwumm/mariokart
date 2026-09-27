using UnityEngine;

namespace KartRacer
{
    /// <summary>Floating, rainbow-cycling "?" box. Grants a random item and respawns after a few seconds.</summary>
    public class ItemBox : MonoBehaviour
    {
        const float RespawnTime = 3f;

        GameObject visual;
        Renderer boxRenderer;
        float hiddenTimer;
        float phase;

        public static ItemBox Create(Transform parent, Vector3 position, Texture2D texture)
        {
            var go = new GameObject("ItemBox");
            go.transform.SetParent(parent, false);
            go.transform.position = position;

            var box = go.AddComponent<ItemBox>();
            box.visual = Factory.Part(PrimitiveType.Cube, go.transform, Vector3.zero, Vector3.one * 1.4f,
                Factory.Mat(texture, Vector2.one, 0.9f));
            box.boxRenderer = box.visual.GetComponent<Renderer>();
            box.phase = Random.value;
            return box;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (hiddenTimer > 0f)
            {
                hiddenTimer -= dt;
                if (hiddenTimer <= 0f) visual.SetActive(true);
                else
                {
                    // Grow back in during the last half second.
                    float s = Mathf.Clamp01(1f - hiddenTimer / 0.5f);
                    visual.transform.localScale = Vector3.one * 1.4f * s;
                    visual.SetActive(s > 0f);
                }
                return;
            }

            visual.transform.localScale = Vector3.one * 1.4f;
            visual.transform.localPosition = new Vector3(0f, Mathf.Sin((Time.time + phase) * 3f) * 0.2f, 0f);
            visual.transform.Rotate(new Vector3(20f, 60f, 10f) * dt);
            Factory.SetColor(boxRenderer, Color.HSVToRGB((Time.time * 0.3f + phase) % 1f, 0.55f, 1f));

            RaceManager race = RaceManager.Instance;
            if (race == null) return;
            foreach (KartController k in race.Karts)
            {
                if ((k.transform.position + Vector3.up * 0.6f - transform.position).sqrMagnitude > 2.3f * 2.3f)
                    continue;
                if (k.Item == ItemType.None)
                    k.GiveItem(Items.Roll(k.Progress.Place, race.Karts.Count));
                hiddenTimer = RespawnTime;
                visual.SetActive(false);
                break;
            }
        }
    }
}
