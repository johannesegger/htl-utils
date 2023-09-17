[<AutoOpen>]
module AD.Test.Utils

open AD.Ldap
open Expecto
open System
open System.Threading.Tasks

module Async =
    let toAsyncDisposable (wf: Async<unit>) =
        { new IAsyncDisposable with member _.DisposeAsync() = wf |> Async.StartAsTask |> ValueTask }

module AsyncDisposable =
    let combine list =
        async {
            for disposable: IAsyncDisposable in list do
                do! disposable.DisposeAsync().AsTask() |> Async.AwaitTask
        }
        |> Async.toAsyncDisposable

    let fromDisposable (d: IDisposable) =
        { new IAsyncDisposable with member _.DisposeAsync() = d.Dispose(); ValueTask.CompletedTask }

let createNodeAndParents (ldap: Ldap) node nodeType properties = async {
    let! createdNodes = ldap.CreateNodeAndParents(node, nodeType, properties)
    return
        createdNodes
        |> List.rev
        |> List.map (fun node -> async { do! ldap.DeleteNode(node) } |> Async.toAsyncDisposable)
        |> AsyncDisposable.combine
}

let private testCaseTaskBuilder (fn: string -> Async<unit> -> Test) name (taskFn: unit -> Task) =
    fn name (async { do! taskFn () |> Async.AwaitTask })
let testCaseTask = testCaseTaskBuilder testCaseAsync
let ftestCaseTask = testCaseTaskBuilder ftestCaseAsync
let ptestCaseTask = testCaseTaskBuilder ptestCaseAsync
