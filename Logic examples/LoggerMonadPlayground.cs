using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoggerMonadPlayground
{
    // Monad interface
    public interface IMonad<T>
    {
        IMonad<U> Bind<U>(Func<T, IMonad<U>> func);
        T Value { get; }
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

    // Logger monad
    public class Logger<T> : IMonad<T>
    {
        public T Value { get; }
        public List<string> Logs { get; }
        public ILogStrategy Strategy { get; }

        public Logger(T value, ILogStrategy strategy, List<string> logs = null!)
        {
            Value = value;
            Strategy = strategy;
            Logs = logs ?? [];
        }

        public IMonad<U> Bind<U>(Func<T, IMonad<U>> func)
        {
            var result = func(Value) as Logger<U>;
            var combinedLogs = Logs.Concat(result?.Logs ?? []).ToList();
            return new Logger<U>(result!.Value, Strategy, combinedLogs);
        }

        public Logger<T> Log(string message)
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

    // Mock services
    public class CustomerService
    {
        public string GetCustomerInfo(int customerId) => $"Customer-{customerId}";
    }

    public class ProductService
    {
        public string GetProductInfo(string customerInfo) => $"Product-for-{customerInfo}";
    }

    public class OrderService
    {
        public string ProcessOrder(string productInfo) => $"Order-confirmed-for-{productInfo}";
    }

    // Service locator delegate
    public delegate T ServiceLocator<T>();

    // Main program
    class Program
    {
        static void RunExample()
        {
            var customerService = new CustomerService();
            var productService = new ProductService();
            var orderService = new OrderService();

            // Service locators
            ServiceLocator<CustomerService> customerLocator = () => customerService;
            ServiceLocator<ProductService> productLocator = () => productService;
            ServiceLocator<OrderService> orderLocator = () => orderService;

            // Choose logging strategy
            ILogStrategy opsLogger = new OpsLogStrategy();
            ILogStrategy economyLogger = new EconomyLogStrategy();

            // Start monadic chain
            var logger = new Logger<int>(123, opsLogger).Log("Starting order process for customer 123");

            var result = logger
                .Bind(id => new Logger<string>(customerLocator().GetCustomerInfo(id), opsLogger).Log("Fetched customer info"))
                .Bind(customer => new Logger<string>(productLocator().GetProductInfo(customer), opsLogger).Log("Fetched product info"))
                .Bind(product => new Logger<string>(orderLocator().ProcessOrder(product), economyLogger).Log("Processed order"));

            Console.WriteLine($">>> Final Result: {result.Value}");
            Console.WriteLine(">>> Logs:");
            // Fix for CS8602: Dereference of a possibly null reference.
            (result as Logger<string>)?.PrintLogs();
        }
    }
}
