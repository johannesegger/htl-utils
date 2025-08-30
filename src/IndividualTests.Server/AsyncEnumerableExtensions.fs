module AsyncEnumerable

open System.Collections.Generic

let toList (items: IAsyncEnumerable<'T>) =
    let rec fn (enumerator: IAsyncEnumerator<'T>) = task {
        let! moveNext = enumerator.MoveNextAsync()
        if moveNext then
            let item = enumerator.Current
            let! next = fn enumerator
            return item :: next
        else return []
    }
    async {
        return! task {
            use enumerator = items.GetAsyncEnumerator()
            return! fn enumerator
        }
        |> Async.AwaitTask
    }