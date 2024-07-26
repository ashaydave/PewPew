using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Audio;

[RequireComponent(typeof(AudioSource))]
public class GunshotSound : MonoBehaviour
{
    private float[] noiseBuffer;
    private int sampleRate;
    private float phase;
    private float decayTime = 0.05f;
    private float chirpDuration = 0.05f;
    private float chirpStartFreq = 10000f;
    private float chirpEndFreq = 100f;
    private bool triggerGunshot = false;
    private int sampleIndex = 0;

    private float[] bandpassFrequencies = { 200f, 300f, 400f, 500f, 2500f };
    private float[] bandpassBandwidths = { 50f, 50f, 50f, 50f, 50f };

    void Start()
    {
        sampleRate = AudioSettings.outputSampleRate;
        noiseBuffer = new float[sampleRate];
        GenerateNoise();
        AudioSource audioSource = GetComponent<AudioSource>();
        audioSource.spatialize = true;
        audioSource.spatialBlend = 1.0f;

        AudioMixer audioMixer = Resources.Load<AudioMixer>("Master");
        if (audioMixer != null)
        {
            audioSource.outputAudioMixerGroup = audioMixer.FindMatchingGroups("Gunshot")[0];
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            sampleIndex = 0;
            phase = 0f;
            triggerGunshot = true;
        }
    }

    void GenerateNoise()
    {
        for (int i = 0; i < noiseBuffer.Length; i++)
        {
            noiseBuffer[i] = Random.Range(-1f, 1f);
        }
    }

    float GetExcitationNoiseSample(int sampleIndex)
    {
        float time = sampleIndex / (float)sampleRate;
        float envelope = Mathf.Exp(-time / decayTime); // 50ms decay
        return envelope * noiseBuffer[sampleIndex % noiseBuffer.Length];
    }

    float ApplyResonanceFilter(float sample)
    {
        float output = 0f;
        foreach (float frequency in bandpassFrequencies)
        {
            float omega = 2 * Mathf.PI * frequency / sampleRate;
            float alpha = Mathf.Sin(omega) / (2 * Mathf.Sqrt(2));
            float cosOmega = Mathf.Cos(omega);
            float a0 = 1 + alpha;
            float a1 = -2 * cosOmega;
            float a2 = 1 - alpha;
            float b0 = alpha;
            float b1 = 0;
            float b2 = -alpha;

            float filteredSample = (b0 / a0) * sample + (b1 / a0) * 0 + (b2 / a0) * 0
                                 - (a1 / a0) * 0 - (a2 / a0) * 0;

            output += filteredSample;
        }
        return output;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!triggerGunshot) return;

        for (int i = 0; i < data.Length; i += channels)
        {
            float sample = 0f;

            // Chirp
            float time = sampleIndex / (float)sampleRate;
            if (time <= chirpDuration)
            {
                float envelope = Mathf.Exp(-time / decayTime);
                float frequency = Mathf.Lerp(chirpStartFreq, chirpEndFreq, time / chirpDuration);
                sample += envelope * Mathf.Cos(2 * Mathf.PI * frequency * phase / sampleRate);
            }

            // Excitation Noise
            sample += GetExcitationNoiseSample(sampleIndex);

            // Body Resonance
            sample = ApplyResonanceFilter(sample);

            phase++;
            sampleIndex++;

            for (int j = 0; j < channels; j++)
            {
                data[i + j] = sample;
            }
        }

        // Stop triggering after processing enough samples for a short burst
        if (sampleIndex >= sampleRate * chirpDuration)
        {
            triggerGunshot = false;
        }
    }
}

