namespace IncrementClassGroups.Configuration

open System
open System.Text.RegularExpressions

type MaxClassLevel = {
    Title: string
    Pattern: Regex
    MaxLevel: int
}

type Config = {
    MaxClassLevels: MaxClassLevel list
}
module Config =
    let fromEnvironment () =
        {
            MaxClassLevels =
                Environment.getEnvVarOrFail "MGMT_MAX_CLASS_LEVELS"
                |> String.split ";"
                |> Seq.map (fun row ->
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
                    { Title = title; Pattern = pattern; MaxLevel = maxLevel }
                )
                |> Seq.toList
        }
