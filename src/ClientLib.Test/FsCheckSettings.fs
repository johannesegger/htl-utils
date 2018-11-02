module FsCheckSettings

open Expecto
open FsCheck

type Arbs =
    static member strings () =
        Arb.Default.String()
        |> Arb.filter (fun s -> s <> null)
let config = { FsCheckConfig.defaultConfig with arbitrary = [typeof<Arbs>] }