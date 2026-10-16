using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>
    /// Anti-spam for emotes and quick chat (upgrade spec 7, 8): emote cooldown 3 s and max 10 per minute,
    /// quick chat 1 per 2 s. The host/server checks every request; clients use the same rules to grey out buttons.
    /// </summary>
    public sealed class SocialLimiter
    {
        public const float EmoteCooldown = 3f;
        public const int EmotesPerMinute = 10;
        public const float ChatCooldown = 2f;

        private readonly Queue<float> _emoteTimes = new Queue<float>();
        private float _lastEmote = -999f;
        private float _lastChat = -999f;

        public bool CanEmote(float time)
        {
            while (_emoteTimes.Count > 0 && time - _emoteTimes.Peek() >= 60f) _emoteTimes.Dequeue();
            return time - _lastEmote >= EmoteCooldown && _emoteTimes.Count < EmotesPerMinute;
        }

        public bool TryEmote(float time)
        {
            if (!CanEmote(time)) return false;
            _lastEmote = time;
            _emoteTimes.Enqueue(time);
            return true;
        }

        public bool CanChat(float time) => time - _lastChat >= ChatCooldown;

        public bool TryChat(float time)
        {
            if (!CanChat(time)) return false;
            _lastChat = time;
            return true;
        }

        public float EmoteCooldownLeft(float time) => KMath.Max(0f, EmoteCooldown - (time - _lastEmote));
    }

    /// <summary>The 8 preset quick chat messages (upgrade spec 8). No free text in v1.</summary>
    public static class QuickChat
    {
        public static readonly string[] Messages = { "Go up!", "Help!", "Attack him", "Defend me", "Crate here!", "Nice cut!", "Thanks", "GG" };
        public static readonly string[] Icons = { "up", "help", "attack", "defend", "crate", "cut", "thanks", "gg" };

        public static bool Valid(int id) => id >= 0 && id < Messages.Length;

        /// <summary>Who sees a message: team only in Team Battle, everyone otherwise.</summary>
        public static bool Visible(GameMode mode, int senderTeam, int viewerTeam) =>
            mode != GameMode.TeamBattle || senderTeam == viewerTeam;
    }

    /// <summary>Who may use voice chat (upgrade spec 8): party channel always, team channel in Team Battle, never strangers; under-13 off.</summary>
    public static class VoiceRules
    {
        public static bool Allowed(bool under13) => !under13;

        public static bool CanHear(GameMode mode, bool sameParty, bool sameTeam) =>
            sameParty || mode == GameMode.TeamBattle && sameTeam;
    }

    /// <summary>Training ground options (upgrade spec F8). Changed live from the training panel.</summary>
    [Serializable]
    public sealed class TrainingOptions
    {
        public bool overrideWind;
        public float windDirectionDeg;
        public float windSpeed = 6f;
        public bool gusts = true;
        /// <summary>-1 = normal cycle, else (int)RoundPhase forced.</summary>
        public int forcedPhase = -1;
        public BotDifficulty sparringDifficulty = BotDifficulty.Medium;
        public int sparringBots;
        public bool dummies = true;
    }
}
