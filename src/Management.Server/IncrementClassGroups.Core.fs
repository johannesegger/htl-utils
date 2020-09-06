module IncrementClassGroups.Core

open IncrementClassGroups.DataTransferTypes
open System
open System.Text.RegularExpressions

let modifications classGroups =
    let classLevels =
        Environment.getEnvVarOrFail "MGMT_CLASS_LEVELS"
        |> String.split ";"
        |> Seq.mapi (fun index row ->
            let parts = row.Split ","
            let title =
                Array.tryItem 0 parts
                |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class levels setting: Can't get title" row)
            let pattern =
                Array.tryItem 1 parts
                |> Option.bind (fun pattern -> try Some (Regex pattern) with _ -> None)
                |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class levels setting: Can't parse regex pattern" row)
            let maxLevel =
                Array.tryItem 2 parts
                |> Option.bind (tryDo Int32.TryParse)
                |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class levels setting: Can't parse max level" row)
            (index, title, pattern, maxLevel)
        )
        |> Seq.toList

    classGroups
    |> Seq.choose (fun groupName ->
        classLevels
        |> List.choose (fun (index, title, pattern, maxLevel) ->
            let m = pattern.Match(groupName)
            if m.Success then
                let classLevel =
                    m.Value
                    |> tryDo Int32.TryParse
                    |> Option.defaultWith (fun () -> failwithf "Pattern \"%O\" doesn't match class level of \"%s\" as number" pattern groupName)
                if classLevel < maxLevel then
                    let newName = pattern.Replace(groupName, string (classLevel + 1))
                    Some ((index, title), classLevel, ChangeClassGroupName (groupName, newName))
                else
                    Some ((index, title), classLevel, DeleteClassGroup groupName)
            else None
        )
        |> function
        | [] -> None
        | [ x ] -> Some x
        | _ -> failwithf "Class \"%s\" was matched by multiple patterns" groupName
    )
    |> Seq.groupBy(fun (group, _, _) -> group)
    |> Seq.sortBy fst
    |> Seq.map (fun ((_, title), modifications) ->
        {
            Title = title
            Modifications =
                modifications
                |> Seq.sortByDescending (fun (_, classLevel, _) -> classLevel)
                |> Seq.map (fun (_, _, modification) -> modification)
                |> Seq.toList
        }
    )
    |> Seq.toList
