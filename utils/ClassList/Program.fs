open Sokrates
open System

let private getEnvVarOrFail name =
    let value = Environment.GetEnvironmentVariable name
    if isNull value
    then failwithf "Environment variable \"%s\" not set" name
    else value

[<EntryPoint>]
let main argv =
    let className =
        argv
        |> Array.tryItem 0
        |> Option.defaultWith (fun () ->
            printf "Class: "
            Console.ReadLine()
        )
    let sokratesApi = SokratesApi.FromEnvironment()
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
