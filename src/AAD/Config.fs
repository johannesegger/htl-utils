namespace AAD.Configuration

type PredefinedGroup =
    | Teachers of name: string
    | FormTeachers of name: string
    | FinalThesesMentors of name: string
    | ClassTeachers of classNameToGroupName: (string -> string)
    | ProfessionalGroup of name: string * subjects: string list
    | Students of string
    | ClassStudents of classNameToGroupName: (string -> string)

type Config = {
    GraphClientId: string
    GraphClientSecret: string
    GraphAuthority: string
    GraphTenantId: string
    GlobalAdminRoleId: string
    TeacherGroupId: string
    PredefinedGroupPrefix: string
    PredefinedGroups: PredefinedGroup list
}
module Config =
    let fromEnvironment () =
        {
            GraphClientId = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_CLIENT_ID"
            GraphClientSecret = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_APP_KEY"
            GraphAuthority = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_AUTHORITY"
            GraphTenantId = Environment.getEnvVarOrFail "AAD_MICROSOFT_GRAPH_TENANT_ID"
            GlobalAdminRoleId = Environment.getEnvVarOrFail "AAD_GLOBAL_ADMIN_ROLE_ID"
            TeacherGroupId = Environment.getEnvVarOrFail "AAD_TEACHER_GROUP_ID"
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
