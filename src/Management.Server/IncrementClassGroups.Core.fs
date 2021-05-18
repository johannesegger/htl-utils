module IncrementClassGroups.Core

open IncrementClassGroups.Configuration
open IncrementClassGroups.DataTransferTypes
open System

let modifications classGroups = reader {
    let! config = Reader.environment
    return
        classGroups
        |> Seq.choose (fun groupName ->
            config.IncrementRuleGroups
            |> List.indexed
            |> List.choose (fun (index, ruleGroup) ->
                ruleGroup.Rules
                |> List.tryPick (fun rule ->
                    let m = rule.Pattern.Match(groupName)
                    if m.Success then
                        match rule.Strategy with
                        | Increment ->
                            let classLevel =
                                m.Value
                                |> tryDo Int32.TryParse
                                |> Option.defaultWith (fun () -> failwithf "Pattern \"%O\" doesn't match class level of \"%s\" as number" rule.Pattern groupName)
                            let newName = rule.Pattern.Replace(groupName, string (classLevel + 1))
                            Some ((index, ruleGroup.Title), (0, classLevel), ChangeClassGroupName (groupName, newName))
                        | Rename v ->
                            let newName = rule.Pattern.Replace(groupName, v)
                            Some ((index, ruleGroup.Title), (1, 0), ChangeClassGroupName (groupName, newName))
                        | Delete ->
                            Some ((index, ruleGroup.Title), (2, 0), DeleteClassGroup groupName)
                    else None
                )
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
