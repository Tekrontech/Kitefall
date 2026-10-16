namespace Kitefall.Core
{
    /// <summary>What the player is doing with the line this tick.</summary>
    public enum LineAction
    {
        /// <summary>Nothing pressed: tension drifts toward the idle target.</summary>
        Idle,
        /// <summary>PULL held (kheench): tension rises fast.</summary>
        Pull,
        /// <summary>Line is being released (dheel): tension falls, line out grows.</summary>
        Dheel,
        /// <summary>REEL held: line out shrinks. Cancels dheel.</summary>
        Reel
    }

    /// <summary>Tension bar zones (spec 9.2).</summary>
    public enum TensionZone
    {
        Safe,
        Strong,
        Danger
    }
}
