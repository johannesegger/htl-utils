namespace IncrementClassGroups.Configuration

open System.Text.RegularExpressions

type IncrementStrategy =
    | Increment
    | Rename of string
    | Delete
module IncrementStrategy =
    let parse v =
        if CIString v = CIString "+" then Increment
        elif CIString v = CIString "*" then Delete
        else Rename v

type IncrementRule = {
    Pattern: Regex
    Strategy: IncrementStrategy
}

type IncrementRuleGroup = {
    Title: string
    Rules: IncrementRule list
}

type Config = {
    IncrementRuleGroups: IncrementRuleGroup list
}
module Config =
    let fromEnvironment () =
        {
            IncrementRuleGroups =
                Environment.getEnvVarOrFail "MGMT_INCREMENT_CLASS_LEVEL_RULES"
                |> String.split ";"
                |> Seq.map (fun row ->
                    let parts = row.Split ","
                    let title =
                        Array.tryItem 0 parts
                        |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class level rules: Can't get title" row)
                    let rules =
                        parts
                        |> Seq.skip 1
                        |> Seq.chunkBySize 2
                        |> Seq.map (fun ruleParts ->
                            let pattern =
                                Array.tryItem 0 ruleParts
                                |> Option.bind (fun pattern -> try Some (Regex pattern) with _ -> None)
                                |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class level rules: Can't parse regex pattern" row)
                            let strategy =
                                Array.tryItem 1 ruleParts
                                |> Option.map IncrementStrategy.parse
                                |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of class level rules: Can't parse increment strategy" row)
                            { Pattern = pattern; Strategy = strategy }
                        )
                        |> Seq.toList
                    { Title = title; Rules = rules }
                )
                |> Seq.toList
        }
