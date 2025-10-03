//namespace SmallShopBigAmbitions.Monads.TraceableTransformer;

//using System.Diagnostics;
//using LanguageExt;
//using LanguageExt.Common;
//using static LanguageExt.Prelude;

//public enum ErrKind { Transient, Permanent, Auth, Validation, Canceled, Unknown }

//public static class FinActivityExtensions
//{
//    public static TraceableT<Fin<T>> WithFinStatus<T>(this TraceableT<Fin<T>> t) =>
//        t with
//        {
//            Annotate = (activity, fin) =>
//            {
//                if (fin.IsSucc)
//                {
//                    activity.SetStatus(ActivityStatusCode.Ok);
//                    return;
//                }

//                var error = fin.Match(_ => default(Error), e => e);

//                var kind = Classify(error);
//                activity.SetStatus(ActivityStatusCode.Error, error.Message);
//                activity.SetTag("app.err.kind", kind.ToString());
//                if (!string.IsNullOrWhiteSpace(error.Message))
//                    activity.SetTag("app.err.message", error.Message);

//                if (error.Exception is not null)
//                {
//                    var ex = error.Exception;
//                    activity.AddEvent(new ActivityEvent("exception",
//                        tags: new ActivityTagsCollection
//                        {
//                            ["exception.type"] = ex.GetType().FullName ?? ex.GetType().Name,
//                            ["exception.message"] = ex.Message,
//                            ["exception.stacktrace"] = ex.StackTrace ?? ""
//                        }));
//                }
//            }
//        };

//    public static ErrKind Classify(Error e)
//    {
//        if (e.Exception is OperationCanceledException || e.Exception is TaskCanceledException)
//            return ErrKind.Canceled;

//        // Customize to your domain: exception-based classification is a good default.
//        return e.Exception switch
//        {
//            TimeoutException => ErrKind.Transient,
//            System.Net.Http.HttpRequestException => ErrKind.Transient,
//            System.Net.Sockets.SocketException => ErrKind.Transient,
//            UnauthorizedAccessException => ErrKind.Auth,
//            ArgumentException or ValidationException => ErrKind.Validation,
//            _ => ErrKind.Unknown
//        };
//    }
//}

//// (Optional) A domain ValidationException if you have one:
//public sealed class ValidationException : Exception
//{
//    public ValidationException(string message) : base(message) { }
//}