using LoggerMonadPlayground;

namespace SmallShopBigAmbitions.Monads;

// Logger monad
public class OldLogger<T>(T value, ILogStrategy strategy, List<string> logs = null) : IMonad<T>
{
    public T Value { get; } = value;
    public List<string> Logs { get; } = logs ?? [];
    public ILogStrategy Strategy { get; } = strategy;

    public IMonad<U> Bind<U>(Func<T, IMonad<U>> func)
    {
        var result = func(Value) as OldLogger<U>;
        var combinedLogs = Logs.Concat(result.Logs).ToList();
        return new OldLogger<U>(result.Value, Strategy, combinedLogs);
    }

    public OldLogger<T> Log(string message)
    {
        Strategy.Log(message);
        Logs.Add(message);
        return this;
    }

    public void PrintLogs()
    {
        foreach (var log in Logs)
            Console.WriteLine(log);
    }
}

// Logging strategy interface
public interface ILogStrategy
{
    void Log(string message);
}

// Ops logging strategy
public class OpsLogStrategy : ILogStrategy
{
    public void Log(string message) => Console.WriteLine($"[Ops] {message}");
}

// Economy logging strategy
public class EconomyLogStrategy : ILogStrategy
{
    public void Log(string message) => Console.WriteLine($"[Economy] {message}");
}