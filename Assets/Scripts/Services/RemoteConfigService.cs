using System;
using System.Threading.Tasks;
using Kitefall.Data;
using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>Fetches a JSON override for BalanceConfig (and later per-item balance). Online provider in Kitefall.Online.</summary>
    public interface IRemoteConfigProvider
    {
        Task<string> FetchBalanceJsonAsync();
    }

    public static class RemoteConfigRegistry
    {
        public static Func<IRemoteConfigProvider> Factory;
    }

    public sealed class RemoteConfigService
    {
        public bool Applied { get; private set; }

        /// <summary>Overwrites the in-memory BalanceConfig with the server JSON (never the asset on disk).</summary>
        public async Task ApplyAsync(BalanceConfig balance)
        {
            IRemoteConfigProvider provider = RemoteConfigRegistry.Factory?.Invoke();
            if (provider == null || balance == null) return;
            try
            {
                string json = await provider.FetchBalanceJsonAsync();
                if (string.IsNullOrEmpty(json)) return;
                JsonUtility.FromJsonOverwrite(json, balance);
                Applied = true;
                Debug.Log("[RemoteConfig] Balance overrides applied.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RemoteConfig] Using built-in balance. " + e.Message);
            }
        }
    }
}
