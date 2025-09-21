open FsExcel
open Sokrates
open System.Text.RegularExpressions
open ClosedXML.Excel

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

type TableRow = {
    ``Klasse``: string
    ``Name``: string
    ``Freitag: Aktivitäten``: string
    ``Freitag: Lehrer*innenkürzel``: string
    ``Samstag: Aktivitäten``: string
    ``Samstag: Lehrer*innenkürzel``: string
}
module TableRow =
    let create student =
        {
            ``Klasse`` = student.SchoolClass
            ``Name`` = $"{student.LastName} {student.FirstName1}";
            ``Freitag: Aktivitäten`` = ""
            ``Freitag: Lehrer*innenkürzel`` = ""
            ``Samstag: Aktivitäten`` = ""
            ``Samstag: Lehrer*innenkürzel`` = ""
        }


let sokratesApi = SokratesApi.FromEnvironment()
async {
    let! students = sokratesApi.FetchStudents None None
    let groups =
        students
        |> List.filter (fun v -> not <| Regex.IsMatch(v.SchoolClass, @"^(\d\wBMB|\d\wVMB)$"))
        |> List.sortBy (fun v -> (PersonName v.LastName, PersonName v.FirstName1))
        |> List.groupBy (fun v -> v.SchoolClass)
        |> List.sortBy (fst >> classNameToSortIndex)

    [
        for (group, students) in groups do
            Worksheet group
            Table [
                TableItems (
                    students |> List.map TableRow.create
                )
            ]
            Go (RC (1, 3))
            Cell [ BackgroundColor (XLColor.FromArgb(0, 117, 32, 68)) ]
            Cell [ BackgroundColor (XLColor.FromArgb(0, 117, 32, 68)) ]
            Cell [ BackgroundColor (XLColor.FromArgb(0, 130, 102, 5)) ]
            Cell [ BackgroundColor (XLColor.FromArgb(0, 130, 102, 5)) ]

            AutoFit AllCols
    ]
    |> Render.AsFile "TdoT 2526 Einteilung Schülerinnen.xlsx"
}
|> Async.RunSynchronously