using System;
using System.Collections.Generic;

namespace Kitefall.Core
{
    /// <summary>Steps of the launch ritual (patang charhana, upgrade spec 11.1).</summary>
    public enum PrepStep { PickKite, TieKanni, TieLine, Toss, Tunke, Climb, Untangle, Flying }

    public enum PrepGrade { Perfect, Good, Off }

    public enum PrepEventType { StepStarted, KanniTap, KanniTied, TossPerfect, TossGood, TossMiss, TugGood, TugBad, Snagged, Untangled, Finished }

    public struct PrepEvent
    {
        public PrepEventType Type;
        public PrepStep Step;
        public PrepGrade Grade;
        public float Time;
    }

    /// <summary>All prep numbers (data, Remote Config). Defaults from upgrade spec 11.</summary>
    [Serializable]
    public class PrepSettings
    {
        public float pickTime = 2f;
        public float shortPickTime = 1f;
        public float tieLineTime = 1.5f;
        public float climbTime = 1.5f;

        // Kanni timing ring
        public float kanniPeriod = 1.6f;
        public float kanniGoldenHalf = 0.07f;
        public float kanniGoodHalf = 0.18f;
        public int kanniTaps = 2;
        public float kanniTimeout = 7f;

        // Toss (chhudaai)
        public float tossPerfectMin = 1.2f;
        public float tossPerfectMax = 1.6f;
        public float tossGoodMin = 0.9f;
        public float tossGoodMax = 2.0f;
        public float tossOverhold = 2.6f;
        public float tossMissPenalty = 3f;
        public float tossPerfectBonus = 10f;
        public float tossGoodBonus = 5f;
        public float helperPerfectBonus = 2f;
        public float tossTimeout = 7f;

        // Tunke (climb tugs)
        public int tunkeCountFull = 4;
        public int tunkeCountShort = 3;
        public float tunkePeriod = 1.1f;
        public float tunkeWindowHalf = 0.17f;
        public float tugGoodClimb = 4f;
        public float tugBadClimb = -2f;
        public float tunkeTimeout = 7f;

        // Untangle (cable snag)
        public int untangleSwipes = 3;
        public float untangleTimeout = 5f;
        public float snagChanceOnMiss = 0.5f;
        public float snagChanceOnBadTug = 0.2f;

        // Results
        public float heightCap = 20f;
        public float driftGoodDegPerSec = 0.5f;
        public float driftOffDegPerSec = 1.5f;
        public float driftOffDuration = 20f;
        public float fullCap = 25f;
        public float shortCap = 12f;
    }

    /// <summary>
    /// The launch ritual state machine (upgrade spec 11.7). Pure C#; the host/server runs one per player and evaluates
    /// every timing window from timestamped PULL presses, so results are identical on host and client.
    /// Full: PickKite → TieKanni → TieLine → Toss → Tunke → Climb → Flying (≤ 25 s).
    /// Short: PickKite → Toss → Tunke → Climb → Flying (≤ 12 s, kanni already tied).
    /// </summary>
    public sealed class LaunchPrepController
    {
        public PrepSettings Settings { get; }
        public bool Full { get; }
        public PrepStep Step { get; private set; }
        public float StepStartTime { get; private set; }
        public float StartTime { get; }
        public float Cap => Full ? Settings.fullCap : Settings.shortCap;
        public bool Done => Step == PrepStep.Flying;
        public bool AutoPrep { get; set; }
        /// <summary>A teammate is helping (upgrade spec 11.3).</summary>
        public bool HelperPresent { get; set; }
        public bool CablesNearby { get; set; }
        /// <summary>Sheltered roof: the toss never fails (a miss counts as Good).</summary>
        public bool SafeLaunch { get; set; }

        public PrepGrade KanniGrade { get; private set; } = PrepGrade.Good;
        public PrepGrade TossGrade { get; private set; } = PrepGrade.Good;
        public int KanniTapsDone { get; private set; }
        public int TugsDone { get; private set; }
        public int TugsGood { get; private set; }
        public int TossRetries { get; private set; }
        public int SwipesDone { get; private set; }
        public float TossHold { get; private set; }
        public bool HelperTossPerfect { get; private set; }
        /// <summary>Time until which the step is blocked (missed toss dip animation).</summary>
        public float BlockedUntil { get; private set; }
        public float FinishTime { get; private set; } = -1f;

        /// <summary>Start height bonus (toss + tugs + helper), capped at +20 m.</summary>
        public float HeightBonus => KMath.Clamp(_tossBonus + _tugBonus + _helperBonus, -6f, Settings.heightCap);
        public float DriftDegPerSec => KanniGrade == PrepGrade.Perfect ? 0f : KanniGrade == PrepGrade.Good ? Settings.driftGoodDegPerSec : Settings.driftOffDegPerSec;
        public float DriftDuration => KanniGrade == PrepGrade.Off ? Settings.driftOffDuration : KanniGrade == PrepGrade.Good ? 999f : 0f;
        /// <summary>Good kanni drift stops as soon as the player steers once.</summary>
        public bool DriftUntilSteer => KanniGrade == PrepGrade.Good;

        public int TunkeCount => Full ? Settings.tunkeCountFull : Settings.tunkeCountShort;
        public List<PrepEvent> Events { get; } = new List<PrepEvent>();

        private readonly DeterministicRandom _rng;
        private bool _prevPull;
        private float _prevSteerX;
        private float _tossBonus, _tugBonus, _helperBonus;
        private PrepStep _resumeAfterUntangle;
        private bool _botMode;
        private float _botPerfect, _botGood;
        private float _botNextAction;

        public LaunchPrepController(PrepSettings settings, bool full, float startTime, int seed, PrepGrade previousKanni = PrepGrade.Good)
        {
            Settings = settings ?? new PrepSettings();
            Full = full;
            StartTime = startTime;
            _rng = new DeterministicRandom(seed * 613 + 29);
            if (!full) KanniGrade = previousKanni;
            Enter(PrepStep.PickKite, startTime);
        }

        /// <summary>Bot: rolls results by difficulty instead of reading inputs (Easy mostly Good/Off, Hard mostly Perfect).</summary>
        public void SetBot(BotDifficulty difficulty)
        {
            _botMode = true;
            switch (difficulty)
            {
                case BotDifficulty.Hard: _botPerfect = 0.7f; _botGood = 0.27f; break;
                case BotDifficulty.Medium: _botPerfect = 0.35f; _botGood = 0.5f; break;
                default: _botPerfect = 0.1f; _botGood = 0.55f; break;
            }
        }

        // ---- Timing helpers (the UI draws exactly these) ---------------------------

        /// <summary>Kanni needle position 0–1 (golden arc is centred on 0.5).</summary>
        public float NeedleAt(float time)
        {
            float t = (time - StepStartTime) / Settings.kanniPeriod;
            float f = t - (float)Math.Floor(t);
            return f < 0.5f ? f * 2f : 2f - f * 2f;
        }

        /// <summary>Tunke nose phase 0–1: 0.25 = nose straight up (the moment to tug).</summary>
        public float NosePhaseAt(float time)
        {
            float t = (time - StepStartTime) / Settings.tunkePeriod;
            return t - (float)Math.Floor(t);
        }

        public float StepTimeLeft(float time)
        {
            switch (Step)
            {
                case PrepStep.PickKite: return KMath.Max(0f, StepStartTime + (Full ? Settings.pickTime : Settings.shortPickTime) - time);
                case PrepStep.TieLine: return KMath.Max(0f, StepStartTime + Settings.tieLineTime - time);
                case PrepStep.Climb: return KMath.Max(0f, StepStartTime + Settings.climbTime - time);
                default: return 0f;
            }
        }

        public float TotalTimeLeft(float time) => KMath.Max(0f, StartTime + Cap - time);

        // ---- Update ---------------------------------------------------------------

        /// <summary>Advances the ritual. <paramref name="pullHeld"/> and <paramref name="steerX"/> come from the player's input this tick.</summary>
        public void Update(float time, bool pullHeld, float steerX, float dt)
        {
            Events.Clear();
            if (Done) return;
            if (time >= StartTime + Cap - 1e-4f) { AutoComplete(time); return; }

            bool press = pullHeld && !_prevPull;
            bool release = !pullHeld && _prevPull;
            _prevPull = pullHeld;
            bool swipe = KMath.Abs(steerX) > 0.6f && (KMath.Abs(_prevSteerX) <= 0.6f || Math.Sign(steerX) != Math.Sign(_prevSteerX));
            _prevSteerX = steerX;

            if (AutoPrep || _botMode) { StepAuto(time); return; }
            if (time < BlockedUntil) return;

            switch (Step)
            {
                case PrepStep.PickKite:
                    if (StepTimeLeft(time) <= 0f) Enter(Full ? PrepStep.TieKanni : PrepStep.Toss, time);
                    break;
                case PrepStep.TieKanni:
                    if (press)
                    {
                        float off = KMath.Abs(NeedleAt(time) - 0.5f);
                        PrepGrade g = off <= Settings.kanniGoldenHalf ? PrepGrade.Perfect : off <= Settings.kanniGoodHalf ? PrepGrade.Good : PrepGrade.Off;
                        _kanniSum += (int)g;
                        KanniTapsDone++;
                        Raise(PrepEventType.KanniTap, time, g);
                        if (KanniTapsDone >= Settings.kanniTaps) FinishKanni(time);
                    }
                    else if (time - StepStartTime >= Settings.kanniTimeout)
                    {
                        while (KanniTapsDone < Settings.kanniTaps) { _kanniSum += (int)PrepGrade.Good; KanniTapsDone++; }
                        FinishKanni(time);
                    }
                    break;
                case PrepStep.TieLine:
                    if (StepTimeLeft(time) <= 0f) Enter(PrepStep.Toss, time);
                    break;
                case PrepStep.Toss:
                    if (pullHeld) TossHold += dt;
                    if (release || TossHold >= Settings.tossOverhold) ResolveToss(time, TossHold);
                    else if (TossHold <= 0f && time - StepStartTime >= Settings.tossTimeout) ResolveToss(time, (Settings.tossGoodMin + Settings.tossPerfectMin) * 0.5f);
                    break;
                case PrepStep.Tunke:
                    if (press)
                    {
                        float phase = NosePhaseAt(time);
                        bool good = KMath.Abs(phase - 0.25f) <= Settings.tunkeWindowHalf;
                        Tug(time, good);
                    }
                    else if (time - StepStartTime >= Settings.tunkeTimeout)
                    {
                        Enter(PrepStep.Climb, time);
                    }
                    break;
                case PrepStep.Untangle:
                    if (swipe) SwipesDone++;
                    if (SwipesDone >= Settings.untangleSwipes || time - StepStartTime >= Settings.untangleTimeout)
                    {
                        Raise(PrepEventType.Untangled, time);
                        Enter(_resumeAfterUntangle, time);
                    }
                    break;
                case PrepStep.Climb:
                    if (StepTimeLeft(time) <= 0f) Finish(time);
                    break;
            }
        }

        private int _kanniSum;

        private void FinishKanni(float time)
        {
            float avg = _kanniSum / (float)KMath.Max(1, KanniTapsDone);
            KanniGrade = avg <= 0.25f ? PrepGrade.Perfect : avg <= 1.25f ? PrepGrade.Good : PrepGrade.Off;
            Raise(PrepEventType.KanniTied, time, KanniGrade);
            Enter(PrepStep.TieLine, time);
        }

        private void ResolveToss(float time, float hold)
        {
            TossHold = 0f;
            if (hold >= Settings.tossPerfectMin && hold <= Settings.tossPerfectMax)
            {
                TossGrade = PrepGrade.Perfect;
                _tossBonus = Settings.tossPerfectBonus;
                if (HelperPresent) { _helperBonus = Settings.helperPerfectBonus; HelperTossPerfect = true; }
                Raise(PrepEventType.TossPerfect, time, PrepGrade.Perfect);
                Enter(PrepStep.Tunke, time);
            }
            else if (hold >= Settings.tossGoodMin && hold <= Settings.tossGoodMax)
            {
                TossGrade = PrepGrade.Good;
                _tossBonus = Settings.tossGoodBonus;
                Raise(PrepEventType.TossGood, time, PrepGrade.Good);
                Enter(PrepStep.Tunke, time);
            }
            else if (SafeLaunch)
            {
                TossGrade = PrepGrade.Good;
                _tossBonus = Settings.tossGoodBonus;
                Raise(PrepEventType.TossGood, time, PrepGrade.Good);
                Enter(PrepStep.Tunke, time);
            }
            else
            {
                // Missed toss: the kite dips, bumps the parapet and flutters back – retry (no durability loss).
                TossGrade = PrepGrade.Off;
                TossRetries++;
                Raise(PrepEventType.TossMiss, time, PrepGrade.Off);
                BlockedUntil = time + Settings.tossMissPenalty;
                StepStartTime = BlockedUntil;
                if (CablesNearby && _rng.Chance(Settings.snagChanceOnMiss)) Snag(time, PrepStep.Toss);
            }
        }

        private void Tug(float time, bool good)
        {
            TugsDone++;
            if (good) { TugsGood++; _tugBonus += Settings.tugGoodClimb; Raise(PrepEventType.TugGood, time, PrepGrade.Good); }
            else
            {
                _tugBonus += Settings.tugBadClimb;
                Raise(PrepEventType.TugBad, time, PrepGrade.Off);
                if (CablesNearby && _rng.Chance(Settings.snagChanceOnBadTug)) { Snag(time, TugsDone >= TunkeCount ? PrepStep.Climb : PrepStep.Tunke); return; }
            }
            if (TugsDone >= TunkeCount) Enter(PrepStep.Climb, time);
        }

        private void Snag(float time, PrepStep resume)
        {
            _resumeAfterUntangle = resume;
            SwipesDone = 0;
            Raise(PrepEventType.Snagged, time);
            Enter(PrepStep.Untangle, KMath.Max(time, BlockedUntil));
            BlockedUntil = 0f;
        }

        /// <summary>Auto-prep / bots: the full animation plays with rolled (auto-prep: Good) results at a natural pace.</summary>
        private void StepAuto(float time)
        {
            if (time < _botNextAction) return;
            switch (Step)
            {
                case PrepStep.PickKite:
                    if (StepTimeLeft(time) <= 0f) Enter(Full ? PrepStep.TieKanni : PrepStep.Toss, time);
                    break;
                case PrepStep.TieKanni:
                    if (time - StepStartTime < 2.4f) return;
                    KanniTapsDone = Settings.kanniTaps;
                    KanniGrade = Roll();
                    Raise(PrepEventType.KanniTied, time, KanniGrade);
                    Enter(PrepStep.TieLine, time);
                    break;
                case PrepStep.TieLine:
                    if (StepTimeLeft(time) <= 0f) Enter(PrepStep.Toss, time);
                    break;
                case PrepStep.Toss:
                    if (time - StepStartTime < 1.4f) return;
                    PrepGrade toss = Roll();
                    ResolveToss(time, toss == PrepGrade.Perfect ? 1.4f : toss == PrepGrade.Good ? 1.0f : 0.4f);
                    if (Step == PrepStep.Toss) _botNextAction = BlockedUntil;
                    break;
                case PrepStep.Tunke:
                    if (time - StepStartTime < TugsDone * Settings.tunkePeriod + 0.9f) return;
                    Tug(time, AutoPrep && !_botMode ? TugsDone % 2 == 0 : Roll() != PrepGrade.Off); // auto-prep: "Good" overall, never a perfect climb
                    break;
                case PrepStep.Untangle:
                    if (time - StepStartTime < 2.5f) return;
                    SwipesDone = Settings.untangleSwipes;
                    Raise(PrepEventType.Untangled, time);
                    Enter(_resumeAfterUntangle, time);
                    break;
                case PrepStep.Climb:
                    if (StepTimeLeft(time) <= 0f) Finish(time);
                    break;
            }
        }

        private PrepGrade Roll()
        {
            if (AutoPrep && !_botMode) return PrepGrade.Good;
            float r = _rng.NextFloat();
            return r < _botPerfect ? PrepGrade.Perfect : r < _botPerfect + _botGood ? PrepGrade.Good : PrepGrade.Off;
        }

        /// <summary>Cap reached: unfinished steps complete with "Good" results.</summary>
        public void AutoComplete(float time)
        {
            if (Done) return;
            if (Full && KanniTapsDone < Settings.kanniTaps && (Step == PrepStep.PickKite || Step == PrepStep.TieKanni)) KanniGrade = PrepGrade.Good;
            if (Step <= PrepStep.Toss || Step == PrepStep.Untangle && _resumeAfterUntangle == PrepStep.Toss)
            {
                TossGrade = PrepGrade.Good;
                _tossBonus = Settings.tossGoodBonus;
            }
            Finish(time);
        }

        private void Finish(float time)
        {
            FinishTime = time;
            Enter(PrepStep.Flying, time);
            Raise(PrepEventType.Finished, time);
        }

        private void Enter(PrepStep step, float time)
        {
            Step = step;
            StepStartTime = time;
            Raise(PrepEventType.StepStarted, time);
        }

        private void Raise(PrepEventType type, float time, PrepGrade grade = PrepGrade.Good) =>
            Events.Add(new PrepEvent { Type = type, Step = Step, Grade = grade, Time = time });
    }
}
