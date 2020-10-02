[<AutoOpen>]
module Reader

type Reader<'environment, 'a> = Reader of ('environment -> 'a)

module Reader =
    let run env (Reader action) = action env
    let environment = Reader id
    let retn v = Reader (fun _ -> v)
    let bind fn reader =
        Reader (fun env ->
            let result = run env reader
            let reader' = fn result
            run env reader'
        )
    let map fn = bind (fn >> retn)
    let sequence list =
        Reader (fun env ->
            list
            |> List.map (run env)
        )
    let ignore r = map ignore r
type ReaderBuilder() =
    member _.Return v = Reader.retn v
    member this.Zero() = this.Return ()
    member _.Bind(x, f) = Reader.bind f x
    member _.ReturnFrom(m: Reader<_, _>) = m
    member _.Combine(a, b) = a |> Reader.bind (fun _ -> b)
    member _.Delay(fn) = Reader (fun env -> Reader.run env (fn()))
    member _.TryWith (m, errorFn) =
        Reader (fun env ->
            try Reader.run env m
            with e -> Reader.run env (errorFn e)
        )
    member _.TryFinally(m, finallyFn) =
        Reader (fun env ->
            try Reader.run env m
            finally finallyFn ()
        )
    member this.Using(res:#System.IDisposable, body) =
        this.TryFinally(body res, fun () -> res.Dispose())

let reader = ReaderBuilder()

module AsyncReader =
    let retn v = Reader.retn (Async.retn v)
    let bind fn reader =
        Reader (fun env -> async {
            let! value = Reader.run env reader
            return! Reader.run env (fn value)
        })
    let liftAsync (v: Async<_>) = Reader.retn v
    let liftReader v = Reader.map Async.retn v

type AsyncReaderBuilder() =
    member _.Return v = AsyncReader.retn v
    member this.Zero() = this.Return ()
    member _.Bind(x, f) = AsyncReader.bind f x
    member _.ReturnFrom(m: Reader<_, Async<_>>) = m
    member _.Combine(a, b) = a |> AsyncReader.bind (fun _ -> b)
    member _.Delay(fn: unit -> Reader<_, Async<_>>) = Reader (fun env -> Reader.run env (fn()))
    member _.TryWith (m, errorFn) =
        Reader (fun env -> async {
            try return! Reader.run env m
            with e -> return! Reader.run env (errorFn e)
        })
    member _.TryFinally(m, finallyFn) =
        Reader (fun env -> async {
            try return! Reader.run env m
            finally finallyFn ()
        })
    member this.Using(res:#System.IDisposable, body) =
        this.TryFinally(body res, fun () -> res.Dispose())

let asyncReader = AsyncReaderBuilder()


