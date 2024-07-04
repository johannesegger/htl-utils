module StudentInfo

open Azure.Identity
open Microsoft.Graph
open Microsoft.Graph.Models
open Sokrates
open System
open System.IO
open System.Threading.Tasks
open Azure.Core

type StudentData = {
    Address: Address
    MailAddress: string
}

module private MSGraph =
    open Microsoft.Kiota.Abstractions.Serialization

    let readAll<'a, 'b when 'a: (new: unit -> 'a) and 'a :> IParsable and 'a :> IAdditionalDataHolder> (
        graphClient: GraphServiceClient)
        (query: Task<'a>) = async {
        let result = Collections.Generic.List<_>()
        let! firstResponse = query |> Async.AwaitTask
        let iterator =
            PageIterator<'b, 'a>
                .CreatePageIterator(
                    graphClient,
                    firstResponse,
                    (fun item ->
                        result.Add(item)
                        true // continue iteration
                    ),
                    (fun r -> r)
                )
        do! iterator.IterateAsync() |> Async.AwaitTask
        return result
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
        let scopes = [| "GroupMember.Read.All" |]
        let deviceCodeCredential =
            let opts = DeviceCodeCredentialOptions (
                ClientId = clientId,
                TenantId = tenantId,
                TokenCachePersistenceOptions = TokenCachePersistenceOptions(Name = "HtlUtils.IndividualTests"),
                DeviceCodeCallback = (fun code ct ->
                    printWarning $"%s{code.Message}"
                    Task.CompletedTask
                )
            )
            let authTokenPath = Path.Combine(Path.GetTempPath(), $"%s{opts.TokenCachePersistenceOptions.Name}.token")
            if File.Exists(authTokenPath) then
                use fileStream = File.OpenRead(authTokenPath)
                opts.AuthenticationRecord <- AuthenticationRecord.DeserializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
                DeviceCodeCredential(opts)
            else
                let deviceCodeCredential = DeviceCodeCredential(opts)
                let authenticationRecord = deviceCodeCredential.AuthenticateAsync(TokenRequestContext(scopes)) |> Async.AwaitTask |> Async.RunSynchronously
                use fileStream = File.OpenWrite(authTokenPath)
                authenticationRecord.SerializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
                deviceCodeCredential

        use graphClient = new GraphServiceClient(deviceCodeCredential, scopes)
        graphClient.Groups.[studentsGroupId].Members.GetAsync(fun config ->
            config.QueryParameters.Select <- [| "department"; "givenName"; "mail"; "surname" |]
        )
        |> MSGraph.readAll<_, DirectoryObject> graphClient
        |> Async.RunSynchronously
        |> Linq.Enumerable.OfType<User>
        |> Seq.map (fun v -> ((String.toLower v.Department, String.toLower v.Surname, String.toLower v.GivenName), v.Mail))
        |> Map.ofSeq

    sokratesReferenceDates
    |> List.collect (fun sokratesReferenceDate ->
        sokratesApi.FetchStudents None (Some sokratesReferenceDate)
        |> Async.RunSynchronously
        |> List.map (fun student ->
            let studentData =
                match Map.tryFind student.Id addressLookup with
                | None -> Error $"Student %s{student.LastName} %s{student.FirstName1} (%s{student.SchoolClass}) not found in address list"
                | Some None -> Error $"Student %s{student.LastName} %s{student.FirstName1} (%s{student.SchoolClass}) doesn't have an address"
                | Some (Some address) ->
                    match Map.tryFind (student.SchoolClass.ToLower(), student.LastName.ToLower(), student.FirstName1.ToLower()) mailLookup with
                    | None -> Error $"Student %s{student.LastName} %s{student.FirstName1} (%s{student.SchoolClass}) doesn't have a mail address"
                    | Some mailAddress ->
                        Ok { Address = address; MailAddress = mailAddress }
            (student, studentData)
        )
    )
    |> List.distinctBy (fun (v, _) -> v.Id)