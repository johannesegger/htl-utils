open System

[<EntryPoint>]
let main argv =
    let className =
        argv
        |> Array.tryItem 0
        |> Option.defaultWith (fun () ->
            printf "Class: "
            Console.ReadLine()
        )
    let sokratesConfig = Sokrates.Configuration.Config.fromEnvironment ()
    let students = Sokrates.Core.getStudents (Some className) None |> Reader.run sokratesConfig |> Async.RunSynchronously
    students
    |> List.sortBy (fun student -> student.LastName, student.FirstName1, student.FirstName2)
    |> List.iter (fun student ->
        printfn "%s %s %s"
            student.LastName
            student.FirstName1
            (Option.defaultValue "" student.FirstName2)
    )
    0
