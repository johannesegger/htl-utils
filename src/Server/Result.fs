module Result

open System

let ofOption e = function
    | Some o -> Ok o
    | None -> Error e

let toOption = function
    | Ok v -> Some v
    | Error _ -> None

let bindAsync fn = function
    | Ok v -> fn v
    | Error v -> async { return Error v }

let partition l =
    let folder t (a, b) =
        match t with
        | Ok item -> (item :: a), b
        | Error item -> a, (item :: b)

    List.foldBack folder l ([], [])

let sequence list =
    let folder item state =
        match item, state with
        | Ok v, Ok vs -> Ok (v :: vs)
        | Error e, Ok _ -> Error [ e ]
        | Ok v, Error es -> Error es
        | Error e, Error es -> Error (e :: es)
    List.foldBack folder (Seq.toList list) (Ok [])

// http://www.fssnip.net/7UJ/title/ResultBuilder-Computational-Expression
type ResultBuilder() =
    member __.Return(x) = Ok x

    member __.ReturnFrom(m: Result<_, _>) = m

    member __.Bind(m, f) = Result.bind f m
    member __.Bind((m, error): (Option<'T> * 'E), f) = m |> ofOption error |> Result.bind f

    member __.Zero() = None

    member __.Combine(m, f) = Result.bind f m

    member __.Delay(f: unit -> _) = f

    member __.Run(f) = f()

    member __.TryWith(m, h) =
        try __.ReturnFrom(m)
        with e -> h e

    member __.TryFinally(m, compensation) =
        try __.ReturnFrom(m)
        finally compensation()

    member __.Using(res:#IDisposable, body) =
        __.TryFinally(body res, fun () -> match res with null -> () | disp -> disp.Dispose())

    member __.While(guard, f) =
        if not (guard()) then Ok () else
        do f() |> ignore
        __.While(guard, f)

    member __.For(sequence:seq<_>, body) =
        __.Using(sequence.GetEnumerator(), fun enum -> __.While(enum.MoveNext, __.Delay(fun () -> body enum.Current)))

let result = ResultBuilder()
