namespace Managementv2.Server

open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks

type OperationExecutionGate() =
    let semaphores = ConcurrentDictionary<string, SemaphoreSlim>()

    member _.RunExclusive(operationName: string, action: unit -> Task<'T>, cancellationToken: CancellationToken) : Task<'T> =
        task {
            let semaphore = semaphores.GetOrAdd(operationName, fun _ -> new SemaphoreSlim(1, 1))
            do! semaphore.WaitAsync cancellationToken

            try
                return! action ()
            finally
                semaphore.Release() |> ignore
        }
