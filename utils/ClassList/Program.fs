open Sokrates
open System

let listClasses (sokratesApi: SokratesApi) =
    printfn "Classes:"
    sokratesApi.FetchClasses(None)
    |> Async.RunSynchronously
    |> List.sort
    |> List.iter (printfn "  * %s")

[<EntryPoint>]
let main argv =
    let sokratesApi = SokratesApi.FromEnvironment()

    let className =
        argv
        |> Array.tryItem 0
        |> Option.defaultWith (fun () ->
            listClasses sokratesApi
            printf "Class: "
            Console.ReadLine()
        )
    let students = sokratesApi.FetchStudents (Some className) None |> Async.RunSynchronously
    students
    |> List.sortBy (fun student -> student.LastName, student.FirstName1, student.FirstName2)
    |> List.iter (fun student ->
        let firstName =
            match student.FirstName2 with
            | Some v -> $"%s{student.FirstName1} %s{v}"
            | None -> student.FirstName1
        printfn $"%s{student.LastName} %s{firstName}"
    )
    0
