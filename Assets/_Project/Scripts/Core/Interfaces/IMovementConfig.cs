namespace EvidenceRun.Core
{
    public interface IMovementConfig
    {
        float MoveSpeed { get; }
        float Acceleration { get; }
        float Deceleration { get; }
        float CrouchSpeedMultiplier { get; }
    }
}