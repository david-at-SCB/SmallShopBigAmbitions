namespace SmallShopBigAmbitions.HTTP;

public static class HttpTelemetryAttributes
{
    public static Seq<KeyValuePair<string, object>> FromFin<T>(Fin<T> fin, string requestUri)
    {
        return fin.Match<Seq<KeyValuePair<string, object>>>(
            Succ: _ => Seq(
                new KeyValuePair<string, object>("http.success", true),
                new KeyValuePair<string, object>("http.uri", requestUri),
                new KeyValuePair<string, object>("http.result_type", typeof(T).Name)
            ),
            Fail: error =>
            {
                var exType = error.Exception.Match(ex => ex.GetType().Name, () => "None");
                var exStack = error.Exception.Match(ex => ex.StackTrace ?? "None", () => "None");
                return Seq(
                    new KeyValuePair<string, object>("http.success", false),
                    new KeyValuePair<string, object>("http.uri", requestUri),
                    new KeyValuePair<string, object>("error.message", error.Message),
                    new KeyValuePair<string, object>("error.type", exType),
                    new KeyValuePair<string, object>("error.stacktrace", exStack)
                );
            }
        );
    }
}
