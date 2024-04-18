[<AutoOpen>]
module SokratesExtensions

open Domain
open Sokrates
open System.Text.RegularExpressions

type SokratesApi with
    member this.GetParentGroups() = async {
        let allStudents = this.FetchStudents None None |> Async.RunSynchronously
        let students =
            allStudents
            |> List.filter (fun v -> not <| Regex.IsMatch(v.SchoolClass, @"^\d+(AVMB|ABMB)$"))
        let studentContacts = this.FetchStudentContactInfos (students |> List.map _.Id) None |> Async.RunSynchronously
        let studentContactsById =
            studentContacts
            |> List.map (fun v ->
                let contacts =
                    v.ContactAddresses
                    |> List.choose _.EMailAddress
                    |> List.filter (fun v -> v.Contains("@"))
                v.StudentId, contacts
            )
            |> Map.ofList
        return students
        |> List.map (fun student ->
            let mailAddresses = Map.tryFind student.Id studentContactsById |> Option.defaultValue []
            student, mailAddresses
        )
        |> List.groupBy (fst >> _.SchoolClass)
        |> List.map (fun (schoolClass, students) ->
            {
                GroupName = $"GrpEltern%s{schoolClass}"
                StudentsWithoutAddresses =
                    students
                    |> List.filter (snd >> List.isEmpty)
                    |> List.map (fst >> fun v -> $"%s{v.LastName} %s{v.FirstName1}")
                    |> List.sortBy PersonName
                StudentAddresses =
                    students
                    |> List.collect snd
                    |> List.distinct
            }
        )
    }