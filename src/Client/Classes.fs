module Classes

open System
open System.Text.RegularExpressions

let groupAndSort (classes: string list) =
    let makeSortable (c: string) =
        Option.ofObj c
        |> Option.bind (fun c ->
            let m = Regex.Match(c, @"^(\d)(\w)(\w)([^\W_]+)(?:_\w+)?$")
            if m.Success then
                Some (Int32.Parse(m.Groups.[1].Value), m.Groups.[2].Value, m.Groups.[3].Value, m.Groups.[4].Value)
            else None
        )
        |> Option.map (fun (level, parallelClassName, schoolType, department) ->
            let schoolTypeKey =
                [ ("H", 1); ("F", 2); ("B", 3) ]
                |> Map.ofList
                |> Map.tryFind (schoolType.ToUpper())
                |> Option.defaultValue 9999
            let departmentKey =
                [ ("WII", 1); ("WIM", 2); ("ME", 3); ("GTI", 4); ("MBT", 5); ("MBM", 6); ("MBA", 7); ("MIS", 8) ]
                |> Map.ofList
                |> Map.tryFind (department.ToUpper())
                |> Option.defaultValue 9999
            (level, schoolTypeKey, departmentKey, parallelClassName)
        )
        |> Option.defaultValue (9999, 9999, 9999, "Z")
    classes
    |> List.groupBy (fun v -> v.[0])
    |> List.sortBy fst
    |> List.map (snd >> List.sortBy makeSortable)