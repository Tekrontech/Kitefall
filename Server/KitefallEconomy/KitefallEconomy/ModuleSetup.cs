using Microsoft.Extensions.DependencyInjection;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace Kitefall.Server
{
    /// <summary>Registers the UGS API client for constructor / parameter injection.</summary>
    public sealed class ModuleSetup : ICloudCodeSetup
    {
        public void Setup(ICloudCodeConfig config)
        {
            // Newer CloudCode.Apis versions offer config.AddGameApiClient(); this form works on 0.0.x.
#pragma warning disable CS0618
            config.Dependencies.AddSingleton(GameApiClient.Create());
#pragma warning restore CS0618
        }
    }
}
