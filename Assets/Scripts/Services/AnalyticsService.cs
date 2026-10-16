using System;
using System.Collections.Generic;
using UnityEngine;

namespace Kitefall.Services
{
    public interface IAnalyticsProvider
    {
        void Initialize(bool consent);
        void Track(string eventName, Dictionary<string, object> parameters);
    }

    public static class AnalyticsRegistry
    {
        public static Func<IAnalyticsProvider> Factory;
    }

    /// <summary>Analytics from day 1 (spec 14). Offline it only logs (dev builds).</summary>
    public sealed class AnalyticsService
    {
        private IAnalyticsProvider _provider;

        public void Initialize(bool consent)
        {
            _provider = AnalyticsRegistry.Factory?.Invoke();
            _provider?.Initialize(consent);
        }

        public void Track(string eventName, params (string key, object value)[] parameters)
        {
            var dict = new Dictionary<string, object>();
            foreach (var p in parameters) dict[p.key] = p.value;
            if (_provider != null) _provider.Track(eventName, dict);
            else if (Debug.isDebugBuild)
            {
                var parts = new List<string>();
                foreach (var kv in dict) parts.Add(kv.Key + "=" + kv.Value);
                Debug.Log($"[Analytics] {eventName} {string.Join(", ", parts)}");
            }
        }
    }
}
