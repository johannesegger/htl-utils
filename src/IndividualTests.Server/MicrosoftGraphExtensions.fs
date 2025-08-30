[<AutoOpen>]
module MicrosoftGraphExtensions

open System
open System.Threading.Channels
open System.Threading.Tasks
open Microsoft.Graph

type GraphServiceClient with
    member this.ReadAll(query: Task<'TCollectionPage>) =
        let channel = Channel.CreateUnbounded<'TEntity>()

        Task.Run(fun () ->
            try
                PageIterator<'TEntity, 'TCollectionPage>
                    .CreatePageIterator(
                        this,
                        query.Result,
                        Func<'TEntity, Task<bool>>(fun item -> task {
                            do! channel.Writer.WriteAsync(item)
                            return true // continue iteration
                        })
                    )
                    .IterateAsync()
                    .Wait()
                channel.Writer.Complete()
            with ex ->
                channel.Writer.Complete(ex)
        ) |> ignore

        channel.Reader.ReadAllAsync()
