using System.Diagnostics;

namespace SmallShopBigAmbitions.Monads;

public static class ActivityTagExtensions
{

    public static IO<Fin<T>> WithAttributes<T>(this IO<Fin<T>> io, params (string Key, object? Value)[] attrs) =>
            io.TapSucc(_ =>
            {
                var span = Activity.Current;
                if (span is null) return;
                foreach (var (k, v) in attrs) span.SetTag(k, v);
            });

}
