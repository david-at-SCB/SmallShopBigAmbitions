namespace SmallShopBigAmbitions.Monads
{
    /// <summary>
    /// Extensions for composing <c><![CDATA[Fin<T>]]></c> inside <c>IO</c> without manual pattern matching.
    /// Keep validation results in the <c>Fin</c> world and only run effects when the pipeline executes.
    /// </summary>
    public static class FinIo
    {
        /// <summary>
        /// Lift a pure <c><![CDATA[Fin<T>]]></c> into <c><![CDATA[IO<Fin<T>>]]></c> so it can join an IO pipeline.
        /// </summary>
        public static IO<Fin<T>> ToIOFin<T>(this Fin<T> fin) =>
             IO.lift<Fin<T>>(() => fin);

        /// <summary>
        /// Execute steps sequentially, returning the first failure or the final success.
        /// Intended for ordered side-effecting unit operations (e.g. add N items).
        /// </summary>
        public static IO<Fin<Unit>> SequenceFailFast(this IEnumerable<IO<Fin<Unit>>> steps) =>
            steps.Aggregate(
                IO.pure(FinSucc(unit)),
                (acc, next) =>
                    acc.Bind(fin =>
                        fin.Match(
                            Succ: _ => next,
                            Fail: e => IO.pure(FinFail<Unit>(e))
                        )));

        /// <summary>
        /// Monadic bind for <c><![CDATA[IO<Fin<A>>]]></c>: on success apply <paramref name="f"/>, otherwise propagate the error.
        /// Removes the need to <c>Match</c> just to forward failures.
        /// </summary>
        public static IO<Fin<B>> BindFin<A, B>(this IO<Fin<A>> self, Func<A, IO<Fin<B>>> f) =>
            self.Bind(finA => finA.Match(
                Succ: a => f(a),
                Fail: e => IO.pure(FinFail<B>(e))));

        /// <summary>
        /// Map over the success value of <c><![CDATA[IO<Fin<A>>]]></c>. Failure is passed through unchanged.
        /// </summary>
        public static IO<Fin<B>> MapFin<A, B>(this IO<Fin<A>> self, Func<A, B> f) =>
            self.Map(finA => finA.Map(f));
    }
}