namespace AAD.Configuration

type PredefinedGroup =
    | Teachers of name: string
    | FormTeachers of name: string
    | FinalThesesMentors of name: string
    | ClassTeachers of classNameToGroupName: (string -> string)
    | ProfessionalGroup of name: string * subjects: string list
    | Students of string
    | ClassStudents of classNameToGroupName: (string -> string)

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
    PredefinedGroups: PredefinedGroup list
}
module Config =
    let fromEnvironment () =
        {
            OidcConfig = {
                AppId = Environment.getEnvVarOrFail "AAD_SERVICE_APP_ID"
                AppSecret = Environment.getEnvVarOrFail "AAD_SERVICE_APP_KEY"
                Instance = Environment.getEnvVarOrFail "AAD_INSTANCE"
                Domain = Environment.getEnvVarOrFail "AAD_DOMAIN"
                TenantId = Environment.getEnvVarOrFail "AAD_TENANT_ID"
            }
            PredefinedGroupPrefix = Environment.getEnvVarOrFail "AAD_PREDEFINED_GROUP_PREFIX"
            PredefinedGroups =
                Environment.getEnvVarOrFail "AAD_PREDEFINED_GROUPS"
                |> String.split ";"
                |> Seq.collect (fun row ->
                    let rowParts = String.split "," row
                    let groupId = Array.tryItem 0 rowParts |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of predefined groups settings: Can't get group id." row)
                    let groupName =
                        Array.tryItem 1 rowParts
                        |> Option.defaultWith (fun () -> failwithf "Error in row \"%s\" of predefined groups settings: Can't get group name." row)
                        |> sprintf "%s%s" (Environment.getEnvVarOrFail "AAD_PREDEFINED_GROUP_PREFIX")
                    match CIString groupId with
                    | CIString "Teachers" -> [ Teachers groupName ]
                    | CIString "FormTeachers" -> [ FormTeachers groupName ]
                    | CIString "FinalThesesMentors" -> [ FinalThesesMentors groupName ]
                    | CIString "ClassTeachers" -> [ ClassTeachers (fun className -> String.replace "<class>" className groupName) ]
                    | CIString "ProfessionalGroups" ->
                        Environment.getEnvVarOrFail "AAD_PROFESSIONAL_GROUPS_SUBJECTS"
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
                    | _ -> failwithf "Error in row \"%s\" of predefined groups settings: Unknown group id \"%s\"" row groupId
                )
                |> Seq.toList
        }
