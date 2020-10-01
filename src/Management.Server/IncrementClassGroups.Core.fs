module IncrementClassGroups.Core

open IncrementClassGroups.Configuration
open IncrementClassGroups.DataTransferTypes
open System

let modifications classGroups = reader {
    let! config = Reader.environment
    return
        classGroups
        |> Seq.choose (fun groupName ->
            config.MaxClassLevels
            |> List.indexed
            |> List.choose (fun (index, classMaxLevelSetting) ->
                let m = classMaxLevelSetting.Pattern.Match(groupName)
                if m.Success then
                    let classLevel =
                        m.Value
                        |> tryDo Int32.TryParse
                        |> Option.defaultWith (fun () -> failwithf "Pattern \"%O\" doesn't match class level of \"%s\" as number" classMaxLevelSetting.Pattern groupName)
                    if classLevel < classMaxLevelSetting.MaxLevel then
                        let newName = classMaxLevelSetting.Pattern.Replace(groupName, string (classLevel + 1))
                        Some ((index, classMaxLevelSetting.Title), classLevel, ChangeClassGroupName (groupName, newName))
                    else
                        Some ((index, classMaxLevelSetting.Title), classLevel, DeleteClassGroup groupName)
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
}
