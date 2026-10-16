#if KITEFALL_IAP
using System;
using System.Collections.Generic;
using System.Linq;
using Kitefall.Core;
using Kitefall.Services;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Kitefall.Online
{
    /// <summary>
    /// Unity IAP v5 (StoreController). Gems are consumables; No-ads and Premium Pass are non-consumables.
    /// The receipt goes to the Cloud Code module ("grant_purchase") which validates before granting.
    /// Pending orders are only confirmed after the server granted them.
    /// </summary>
    public sealed class UnityIapProvider : IPurchaseProvider
    {
        private StoreController _store;
        private Action<bool, string> _pending;
        private readonly Dictionary<string, PendingOrder> _orders = new Dictionary<string, PendingOrder>();

        public string Name => "Unity IAP";

        public async void Initialize(IEnumerable<StoreProduct> products)
        {
            try
            {
                _store = UnityIAPServices.StoreController();
                _store.OnPurchasePending += OnPending;
                _store.OnPurchaseFailed += order => Finish(false, null);
                await _store.Connect();
                var defs = products.Select(p => new ProductDefinition(p.productId, p.gems > 0 ? ProductType.Consumable : ProductType.NonConsumable)).ToList();
                _store.FetchProducts(defs);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IAP] " + e.Message);
            }
        }

        public string LocalizedPrice(string productId)
        {
            Product p = _store?.GetProductById(productId);
            return p != null && p.metadata != null ? p.metadata.localizedPriceString : null;
        }

        public void Buy(string productId, Action<bool, string> done)
        {
            if (_store == null) { done?.Invoke(false, null); return; }
            _pending = done;
            _store.PurchaseProduct(productId);
        }

        private void OnPending(PendingOrder order)
        {
            // The server grants first; the order is confirmed in Complete(). An unconfirmed order is
            // delivered again by the store on the next launch, so a failed grant is retried.
            string id = order.CartOrdered.Items().First().Product.definition.id;
            _orders[id] = order;
            if (_pending == null) PurchaseRegistry.Unclaimed?.Invoke(id, order.Info.Receipt);
            else Finish(true, order.Info.Receipt);
        }

        public void Complete(string productId, bool granted)
        {
            if (!granted || !_orders.TryGetValue(productId, out PendingOrder order)) return;
            _orders.Remove(productId);
            _store.ConfirmPurchase(order);
        }

        private void Finish(bool ok, string receipt)
        {
            Action<bool, string> cb = _pending;
            _pending = null;
            cb?.Invoke(ok, receipt);
        }

        public void RestorePurchases(Action<bool> done)
        {
            if (_store == null) { done?.Invoke(false); return; }
            _store.RestoreTransactions((ok, error) => done?.Invoke(ok));
        }
    }
}
#endif
