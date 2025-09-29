using System.Collections.Generic;
using System.Linq;
using LanguageExt;
using static LanguageExt.Prelude;

namespace SmallShopBigAmbitions.Models;

/// <summary>
/// Factory / enricher for <see cref="CartSnapshot"/> so data access can stay focused on persistence
/// and all business validation / derivations (Valid + Errors) are centralized.
/// </summary>
public static class CartSnapshotFactory
{
    /// <summary>
    /// Recompute validation flags and error list for an already materialized snapshot.
    /// Does NOT mutate the original instance; returns a new snapshot record via with-expression.
    /// </summary>
    public static CartSnapshot Enrich(CartSnapshot snapshot)
    {
        var errors = new List<string>();

        // Example rules (extend as needed)
        if (!snapshot.CartIsNotEmpty()) errors.Add("cart.empty");
        if (!snapshot.Cart.ValidTotal()) errors.Add("cart.total.invalid");
        if (snapshot.Cart.Currency.IsNone) errors.Add("cart.currency.missing");

        // You could add pricing / catalog consistency rules here (ensure products still exist, etc.)

        var valid = errors.Count == 0;
        return snapshot with { Valid = valid, Errors = errors.ToArray() };
    }
}
