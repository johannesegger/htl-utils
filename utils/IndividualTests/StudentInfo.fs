module StudentInfo

open AAD
open Microsoft.Graph.Beta.Models
open Sokrates
open System
open System.IO
open System.Threading.Tasks

type StudentData = {
    Address: Address
    MailAddress: string
}

let getLookup tenantId clientId studentsGroupId sokratesReferenceDates =
    let sokratesApi = SokratesApi.FromEnvironment()
    let addressLookup =
        sokratesReferenceDates
        |> List.collect (fun v ->
            sokratesApi.FetchStudentAddresses (Some v) |> Async.RunSynchronously
            |> List.map (fun s -> s.StudentId, s.Address)
        )
        |> List.distinctBy fst
        |> Map.ofList
    let mailLookup =
        let aadConfig = AAD.Configuration.Config.fromEnvironment ()
        use graphClient = GraphServiceClientFactory.createWithDeviceCode aadConfig.OidcConfig [| "GroupMember.Read.All" |] "HtlUtils.IndividualTests"
        graphClient.Groups.[studentsGroupId].Members.GetAsync(fun config ->
            config.QueryParameters.Select <- [| "extension_0b429365529a4f1ea9337bdcd9346b84_htlvbSokratesId"; "mail" |]
        )
        |> graphClient.ReadAll<_, DirectoryObject>
        |> Async.RunSynchronously
        |> Linq.Enumerable.OfType<User>
        |> Seq.choose (fun v ->
            match v.AdditionalData.TryGetValue("extension_0b429365529a4f1ea9337bdcd9346b84_htlvbSokratesId") with
            | (true, sokratesId) -> Some (sokratesId :?> string, v.Mail)
            | (false, _) -> None
        )
        |> Map.ofSeq

    sokratesReferenceDates
    |> List.collect (fun sokratesReferenceDate ->
        sokratesApi.FetchStudents None (Some sokratesReferenceDate)
        |> Async.RunSynchronously
        |> List.map (fun student ->
            let studentData =
                match Map.tryFind student.Id addressLookup with
                | None -> Error $"Not found in Sokrates address list"
                | Some None -> Error $"No address"
                | Some (Some address) ->
                    let (SokratesId sokratesId) = student.Id
                    match Map.tryFind sokratesId mailLookup with
                    | None -> Error $"No mail address"
                    | Some mailAddress ->
                        Ok { Address = address; MailAddress = mailAddress }
            (student, studentData)
        )
    )
    |> List.distinctBy (fun (v, _) -> v.Id)