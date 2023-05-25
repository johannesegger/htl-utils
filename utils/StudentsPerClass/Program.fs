open Sokrates
open System
open Untis
open System.Text.RegularExpressions

let getDepartmentSortIndex department =
    if department = CIString "HMBT" then 1
    elif department = CIString "HME" then 2
    elif department = CIString "HGTI" then 3
    elif department = CIString "FMBM" then 4
    elif department = CIString "HWII" then 5
    elif department = CIString "HWIM" || department = CIString "HWIE" then 6
    else 7

let classNameToSortIndex (className: string) =
    let classLevel = className[0..1]
    let parallelClassLetter = className.[1..2]
    let department = CIString className.[2..]
    let departmentSortIndex = getDepartmentSortIndex department
    (departmentSortIndex, classLevel, parallelClassLetter)

let mutable excelRow = 3

let sokratesApi = SokratesApi.FromEnvironment()
let untisExport = UntisExport.FromEnvironment()
let students = sokratesApi.FetchStudents None None |> Async.RunSynchronously
students
|> List.filter (fun v -> not <| Regex.IsMatch(v.SchoolClass, @"^\d+ABMB$|^5[A-Z]H|^4[A-Z]F"))
|> List.groupBy (fun student -> student.SchoolClass)
|> List.sortBy (fun (className, _) -> classNameToSortIndex className)
|> List.groupBy (fun (className, _) -> getDepartmentSortIndex (CIString className.[2..]))
|> List.map (fun (_, list) ->
    list
    |> List.map (fun (className, students) ->
        let males = students |> List.filter (fun v -> v.Gender = Male) |> List.length
        let females = students |> List.filter (fun v -> v.Gender = Female) |> List.length
        let formTeacher =
            untisExport.GetTeachingData()
            |> List.tryPick (function
                | FormTeacher (SchoolClass c, TeacherShortName teacherName) when c = className -> Some teacherName
                | _ -> None
            )
            |> Option.defaultWith (fun () -> failwith $"Can't find form teacher of \"%s{className}\"")
        (className, males, females, formTeacher)
    )
)
|> List.iter (fun list ->
    list
    |> List.iter (fun (className, males, females, formTeacher) ->
        printfn $"%s{className}\t%d{males}\t%d{females}\t=SUM($B%d{excelRow}:$C%d{excelRow})\t%s{formTeacher}\t\t\t=$D%d{excelRow}-$G%d{excelRow}"
        excelRow <- excelRow + 1
    )
    printfn ""
    excelRow <- excelRow + 1
)
