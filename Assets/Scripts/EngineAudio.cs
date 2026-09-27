using UnityEngine;

namespace KartRacer
{
    /// <summary>Procedural engine drone for the player's kart; pitch follows speed.</summary>
    [RequireComponent(typeof(AudioSource))]
    public class EngineAudio : MonoBehaviour
    {
        KartController kart;
        int sampleRate;
        double phase;
        float volume;
        volatile float frequency = 50f;
        volatile float targetVolume;

        void Awake()
        {
            kart = GetComponent<KartController>();
            sampleRate = AudioSettings.outputSampleRate;
            AudioSource source = GetComponent<AudioSource>();
            source.playOnAwake = true;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0.2f;
            source.Play();
        }

        void Update()
        {
            float s = Mathf.Clamp01(Mathf.Abs(kart.ForwardSpeed) / (kart.maxSpeed * KartController.BoostFactor));
            frequency = 45f + s * 150f + (kart.IsBoosting ? 25f : 0f);
            targetVolume = Time.timeScale > 0f ? 0.35f + 0.65f * s : 0f;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            if (sampleRate <= 0) return;
            float f = frequency, tv = targetVolume;
            for (int i = 0; i < data.Length; i += channels)
            {
                volume += (tv - volume) * 0.0005f;
                phase += f / sampleRate;
                if (phase >= 1.0) phase -= 1.0;
                float saw = (float)(phase * 2.0 - 1.0);
                float square = phase < 0.5 ? 0.5f : -0.5f;
                float sub = Mathf.Sin((float)(phase * Mathf.PI)) * 0.5f;
                float v = (saw * 0.35f + square * 0.25f + sub) * volume * 0.5f;
                for (int c = 0; c < channels; c++) data[i + c] = v;
            }
        }
    }
}
