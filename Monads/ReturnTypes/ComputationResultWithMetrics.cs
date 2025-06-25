namespace SmallShopBigAmbitions.Monads.ReturnTypes;

public record ComputationResultWithMetrics<T>(
    T? Value,               // The result value (if successful)
    bool IsSuccess,         // Indicates success or failure
    string? Error,          // Error message (if failed)
    long ExecutionTime,     // Execution time in milliseconds
    string Message          // Preformatted message summarizing the result
);