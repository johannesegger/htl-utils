namespace AAD.Configuration

open Microsoft.Extensions.Configuration
open System.Text.RegularExpressions

type PredefinedGroup =
    | Teachers of name: string
    | FormTeachers of name: string
    | FinalThesesMentors of name: string
    | ClassTeachers of classNameToGroupName: (string -> string)
    | ProfessionalGroup of name: string * subjects: string list
    | Students of string
    | ClassStudents of classNameToGroupName: (string -> string)
    | FemaleStudents of name: string

type OidcConfig = {
    AppId: string
    AppSecret: string
    Instance: string
    Domain: string
    TenantId: string
}

type Config = {
    OidcConfig: OidcConfig
    PredefinedGroupPrefix: string
    ManuallyManagedGroupsPattern: Regex option
    PredefinedGroups: PredefinedGroup list
}
module Config =
    type AADConfig() =
        member val ClientId = "" with get, set
        member val ClientSecret = "" with get, set
        member val Instance = "" with get, set
        member val Domain = "" with get, set
        member val TenantId = "" with get, set
        member val PredefinedGroupPrefix = "" with get, set
        member val ManuallyManagedGroupsPattern : string option = None with get, set
        member val PredefinedGroups = "" with get, set
        member val ProfessionalGroupsSubjects = "" with get, set
        member x.Build() : Config = {
            OidcConfig = {
                AppId = x.ClientId
                AppSecret = x.ClientSecret
                Instance = x.Instance
                Domain = x.Domain
                TenantId = x.TenantId
            }
            PredefinedGroupPrefix = x.PredefinedGroupPrefix
            ManuallyManagedGroupsPattern =
                x.ManuallyManagedGroupsPattern
                |> Option.map Regex
            PredefinedGroups =
                x.PredefinedGroups
                |> String.split ";"
                |> Seq.collect (fun row ->
                    let rowParts = String.split "," row
                    let groupId = Array.tryItem 0 rowParts |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of predefined groups settings: Can't get group id." row)
                    let groupName =
                        Array.tryItem 1 rowParts
                        |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of predefined groups settings: Can't get group name." row)
                        |> sprintf "%s%s" x.PredefinedGroupPrefix
                    match CIString groupId with
                    | CIString "Teachers" -> [ Teachers groupName ]
                    | CIString "FormTeachers" -> [ FormTeachers groupName ]
                    | CIString "FinalThesesMentors" -> [ FinalThesesMentors groupName ]
                    | CIString "ClassTeachers" -> [ ClassTeachers (fun className -> String.replace "<class>" className groupName) ]
                    | CIString "ProfessionalGroups" ->
                        x.ProfessionalGroupsSubjects
                        |> String.split ";"
                        |> Seq.map (fun row ->
                            let (rawGroupName, subjectString) =
                                String.trySplitAt "-" row
                                |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of professional groups subjects settings: Can't find separator between group name and subjects" row)
                            let fullGroupName = String.replace "<subject>" rawGroupName groupName
                            let subjects =
                                subjectString
                                |> String.split ","
                                |> List.ofArray
                            ProfessionalGroup (fullGroupName, subjects)
                        )
                        |> Seq.toList
                    | CIString "Students" -> [ Students groupName ]
                    | CIString "ClassStudents" -> [ ClassStudents (fun className -> String.replace "<class>" className groupName) ]
                    | CIString "FemaleStudents" -> [ FemaleStudents groupName ]
                    | _ -> failwithf "Error in row \"%s\" of predefined groups settings: Unknown group id \"%s\"" row groupId
                )
                |> Seq.toList
        }
    let fromEnvironment () =
        let config = ConfigurationBuilder().AddEnvironmentVariables().AddUserSecrets("htl-utils").Build()
        ConfigurationBinder.Get<AADConfig>(config.GetSection("AAD")).Build()
