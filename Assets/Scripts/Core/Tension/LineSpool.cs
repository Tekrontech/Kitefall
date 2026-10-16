namespace Kitefall.Core
{
    /// <summary>
    /// Line out (L) on the charkhi – spec 9.1: "Release adds (release speed), Reel removes (reel speed)".
    /// Max line out is limited by charkhi capacity AND by the dor meters the player owns.
    /// (Consuming dor stock is server economy – milestone 11.)
    /// </summary>
    public sealed class LineSpool
    {
        public float LineOut { get; private set; }
        public float Capacity { get; }
        public float StockMeters { get; }
        public float MinLineOut { get; }
        public float ReelSpeed { get; }
        public float ReleaseSpeed { get; }

        public float MaxLineOut => KMath.Max(MinLineOut, KMath.Min(Capacity, StockMeters));
        public bool IsAtMax => LineOut >= MaxLineOut - 0.0001f;
        public bool IsAtMin => LineOut <= MinLineOut + 0.0001f;

        public LineSpool(float capacity, float reelSpeed, float releaseSpeed, float stockMeters, float minLineOut, float startLineOut)
        {
            Capacity = capacity;
            ReelSpeed = reelSpeed;
            ReleaseSpeed = releaseSpeed;
            StockMeters = stockMeters;
            MinLineOut = minLineOut;
            Reset(startLineOut);
        }

        public void Reset(float lineOut)
        {
            LineOut = KMath.Clamp(lineOut, MinLineOut, MaxLineOut);
        }

        public void Step(LineAction action, float dt)
        {
            if (dt <= 0f) return;
            if (action == LineAction.Dheel) LineOut += ReleaseSpeed * dt;
            else if (action == LineAction.Reel) LineOut -= ReelSpeed * dt;
            LineOut = KMath.Clamp(LineOut, MinLineOut, MaxLineOut);
        }
    }
}
