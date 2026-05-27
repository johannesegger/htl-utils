open AD.Core
open ADModifications.HttpHandler
open ADModifications.Mapping
open Sokrates

async {
    let adApi = ADApi.FromEnvironment()

    let sokratesTeachers = Teachers.fromCsvExport "/home/jegger/Downloads/sokrates-teachers.csv"
    let sokratesStudents = Students.fromCsvExport "/home/jegger/Downloads/sokrates-students.csv"
    let! adUsers = adApi.GetUsers() |> Async.StartChild
    let! uniqueUserAttributes = adApi.GetAllUniqueUserProperties() |> Async.StartChild

    // let! sokratesTeachers = sokratesTeachers |> Async.map (List.sortBy (fun v -> v.LastName, v.FirstName))
    // let! sokratesStudents = sokratesStudents |> Async.map (List.sortBy (fun v -> v.SchoolClass, v.LastName, v.FirstName1))
    let! adUsers = adUsers |> Async.map (List.sortBy (fun v -> v.Type, v.LastName, v.FirstName))
    let! uniqueUserAttributes = uniqueUserAttributes |> Async.map UniqueUserAttributes.fromADDto

    let modifications = ADModifications.HttpHandler.modifications sokratesTeachers sokratesStudents adUsers uniqueUserAttributes
    printfn "%A" modifications
    // let! modificationResults =
    //     modifications
    //     |> List.map DirectoryModification.toADDto
    //     |> adApi.ApplyDirectoryModifications
    // match modificationResults with
    // | Ok () -> printfn "Modifications applied"
    // | Error list ->
    //     printfn "Some modifications couldn't be applied:"
    //     list
    //     |> List.iter (fun e ->
    //         printfn $"* %s{e}"
    //         System.Console.ForegroundColor <- System.ConsoleColor.Green
    //         printfn "===================="
    //         System.Console.ResetColor()
    //     )
} |> Async.RunSynchronously