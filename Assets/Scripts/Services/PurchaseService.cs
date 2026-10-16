using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kitefall.Core;
using UnityEngine;

namespace Kitefall.Services
{
    /// <summary>Store purchases behind one interface (Unity IAP lives in Kitefall.Online).</summary>
    public interface IPurchaseProvider
    {
        string Name { get; }
        void Initialize(IEnumerable<StoreProduct> products);
        string LocalizedPrice(string productId);
        /// <summary>Buys; callback gets (success, receipt) – the receipt is validated by the server.</summary>
        void Buy(string productId, Action<bool, string> done);
        void RestorePurchases(Action<bool> done);
        /// <summary>Called after the server answered: confirm (granted) or leave pending for a retry.</summary>
        void Complete(string productId, bool granted);
    }

    /// <summary>Editor stand-in: purchases succeed instantly with a fake receipt. Never used in a store build with IAP.</summary>
    public sealed class DebugPurchaseProvider : IPurchaseProvider
    {
        public string Name => "Test store (no real money)";
        public void Initialize(IEnumerable<StoreProduct> products) { }
        public string LocalizedPrice(string productId) => Catalog.Product(productId)?.displayPrice ?? "";
        public void Buy(string productId, Action<bool, string> done) => done?.Invoke(true, "debug-receipt-" + productId);
        public void RestorePurchases(Action<bool> done) => done?.Invoke(true);
        public void Complete(string productId, bool granted) { }
    }

    public static class PurchaseRegistry
    {
        public static Func<IPurchaseProvider> Factory;
        /// <summary>A store delivered a purchase nobody is waiting for (app restarted mid-purchase).</summary>
        public static Action<string, string> Unclaimed;
    }

    public sealed class PurchaseService
    {
        private readonly IBackendService _backend;
        public IPurchaseProvider Provider { get; private set; }

        public PurchaseService(IBackendService backend) => _backend = backend;

        public void Initialize()
        {
            PurchaseRegistry.Unclaimed = async (productId, receipt) =>
            {
                OpResult r = await _backend.RunAsync(EconomyCommands.GrantPurchase, productId, receipt ?? "");
                Provider.Complete(productId, r.Ok);
            };
            Provider = PurchaseRegistry.Factory?.Invoke() ?? new DebugPurchaseProvider();
            Provider.Initialize(Catalog.Products);
        }

        public string Price(string productId)
        {
            string p = Provider?.LocalizedPrice(productId);
            return string.IsNullOrEmpty(p) ? Catalog.Product(productId)?.displayPrice : p;
        }

        public Task<OpResult> BuyAsync(string productId)
        {
            var tcs = new TaskCompletionSource<OpResult>();
            Provider.Buy(productId, async (ok, receipt) =>
            {
                if (!ok) { tcs.TrySetResult(OpResult.Fail("Purchase cancelled.")); return; }
                // The online backend validates the receipt before granting (grant_purchase).
                OpResult r = await _backend.RunAsync(EconomyCommands.GrantPurchase, productId, receipt ?? "");
                Provider.Complete(productId, r.Ok);
                tcs.TrySetResult(r);
            });
            return tcs.Task;
        }
    }
}
