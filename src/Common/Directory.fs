module Directory

open System.IO

let deleteWithRetry path =
    let rec fn attempts = async {
        try
            Directory.Delete(path, true)
        with
            | :? IOException when attempts > 0 ->
                do! Async.Sleep 500
                do! fn (attempts - 1)
            | e -> raise e
    }
    fn 10
