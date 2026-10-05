namespace EvidenceRun.Core
{
    public interface INoiseListener
    {
        void OnNoise(in NoiseEvent noiseEvent);
    }
}