using UnityEngine;

public sealed class NightShiftAudioController : MonoBehaviour
{
    private const int SampleRate = 22050;

    private AudioSource oneShotSource;
    private AudioSource ambientSource;
    private AudioClip doorCloseClip;
    private AudioClip doorOpenClip;
    private AudioClip switchClip;
    private AudioClip monitorClip;
    private AudioClip cameraClip;
    private AudioClip footstepClip;
    private AudioClip impactClip;
    private AudioClip powerOutClip;
    private AudioClip jumpscareClip;
    private AudioClip ambientClip;
    private uint noiseState = 0x92D68CA2u;

    private void Awake()
    {
        oneShotSource = gameObject.AddComponent<AudioSource>();
        oneShotSource.playOnAwake = false;
        oneShotSource.spatialBlend = 0f;

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.loop = true;
        ambientSource.spatialBlend = 0f;
        ambientSource.volume = 0.16f;

        BuildClips();
        StartNightAmbience();
    }

    public void StartNightAmbience()
    {
        if (ambientSource == null || ambientClip == null)
            return;

        ambientSource.clip = ambientClip;
        if (!ambientSource.isPlaying)
            ambientSource.Play();
    }

    public void PlayDoor(bool closed) => Play(closed ? doorCloseClip : doorOpenClip, 0.72f);
    public void PlaySwitch() => Play(switchClip, 0.55f);
    public void PlayMonitor() => Play(monitorClip, 0.42f);
    public void PlayCameraSwitch() => Play(cameraClip, 0.38f);
    public void PlayEnemyMove() => Play(footstepClip, 0.45f);
    public void PlayDoorImpact() => Play(impactClip, 0.9f);

    public void PlayPowerOut()
    {
        if (ambientSource != null)
            ambientSource.Stop();
        Play(powerOutClip, 0.9f);
    }

    public void PlayJumpscare()
    {
        if (ambientSource != null)
            ambientSource.Stop();
        Play(jumpscareClip, 1f);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (oneShotSource != null && clip != null)
            oneShotSource.PlayOneShot(clip, volume);
    }

    private void BuildClips()
    {
        doorCloseClip = CreateTone("Door Close", 0.46f, 78f, 34f, 0.45f, 0.28f);
        doorOpenClip = CreateTone("Door Open", 0.36f, 42f, 96f, 0.32f, 0.18f);
        switchClip = CreateTone("Switch", 0.08f, 820f, 310f, 0.22f, 0.08f);
        monitorClip = CreateNoise("Monitor", 0.18f, 0.2f, 0.08f);
        cameraClip = CreateNoise("Camera Switch", 0.11f, 0.16f, 0.05f);
        footstepClip = CreateTone("Footstep", 0.34f, 52f, 31f, 0.42f, 0.24f);
        impactClip = CreateTone("Door Impact", 0.62f, 45f, 24f, 0.72f, 0.42f);
        powerOutClip = CreateTone("Power Down", 1.35f, 160f, 22f, 0.38f, 0.18f);
        jumpscareClip = CreateNoise("Jumpscare", 0.9f, 0.88f, 0.35f);
        ambientClip = CreateAmbient();
    }

    private AudioClip CreateTone(string name, float duration, float startFrequency, float endFrequency, float volume, float noiseAmount)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)Mathf.Max(1, sampleCount - 1);
            float frequency = Mathf.Lerp(startFrequency, endFrequency, progress);
            phase += frequency / SampleRate;
            float envelope = Mathf.Sin(progress * Mathf.PI);
            float tone = Mathf.Sin(phase * Mathf.PI * 2f);
            samples[i] = (tone * (1f - noiseAmount) + NextNoise() * noiseAmount) * envelope * volume;
        }

        return CreateClip(name, samples);
    }

    private AudioClip CreateNoise(string name, float duration, float volume, float tailVolume)
    {
        int sampleCount = Mathf.Max(1, Mathf.RoundToInt(duration * SampleRate));
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)Mathf.Max(1, sampleCount - 1);
            float envelope = Mathf.Lerp(volume, tailVolume, progress) * Mathf.Sin(progress * Mathf.PI);
            samples[i] = NextNoise() * envelope;
        }

        return CreateClip(name, samples);
    }

    private AudioClip CreateAmbient()
    {
        int sampleCount = SampleRate * 2;
        float[] samples = new float[sampleCount];
        float phase = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            phase += 48f / SampleRate;
            float hum = Mathf.Sin(phase * Mathf.PI * 2f) * 0.12f;
            float noise = NextNoise() * 0.035f;
            samples[i] = hum + noise;
        }

        return CreateClip("Office Ambience", samples);
    }

    private static AudioClip CreateClip(string name, float[] samples)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private float NextNoise()
    {
        noiseState = noiseState * 1664525u + 1013904223u;
        return ((noiseState >> 8) / 8388607.5f) - 1f;
    }
}
