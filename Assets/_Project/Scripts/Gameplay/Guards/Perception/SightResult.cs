namespace EvidenceRun.Gameplay.Guards.Perception
{
    public readonly struct SightResult
    {
        public readonly bool Visible;
        public readonly float SqrDistance;

        public SightResult(bool visible, float sqrDistance)
        {
            Visible = visible;
            SqrDistance = sqrDistance;
        }
    }
}