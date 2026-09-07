namespace Managementv2.Server

open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks

type OperationExecutionGate() =
    let semaphores = ConcurrentDictionary<string * int, SemaphoreSlim>()

    member _.Run(operationName: string, maxConcurrency: int, action: unit -> Task<'T>, cancellationToken: CancellationToken) : Task<'T> =
        task {
            let semaphore =
                semaphores.GetOrAdd(
                    (operationName, maxConcurrency),
                    fun _ -> new SemaphoreSlim(maxConcurrency, maxConcurrency))
            do! semaphore.WaitAsync cancellationToken

            try
                return! action ()
            finally
                semaphore.Release() |> ignore
        }
