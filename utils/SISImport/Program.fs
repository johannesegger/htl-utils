open Dapper
open MySql.Data.MySqlClient
open Sokrates
open System

let private getEnvVarOrFail name =
    let value = Environment.GetEnvironmentVariable name
    if isNull value
    then failwithf "Environment variable \"%s\" not set" name
    else value

let connectionString = Environment.getEnvVarOrFail "SISIMPORT_SIS_DB_CONNECTION_STRING"

[<CLIMutable>]
type Pupil = {
    SokratesId: string
    AccountName: string
    AccountCreatedAt: Nullable<DateTime>
    FirstName1: string
    FirstName2: string
    LastName: string
    SchoolClass: string
    DateOfBirth: DateTime
}

[<CLIMutable>]
type Address = {
    PersonId: string
    AddressType: string
    Zip: string
    City: string
    Street: string
    Phone1: string
    Phone2: string
    Country: string
    From: DateTime
    FromSpecified: int
    Till: DateTime
    TillSpecified: int
    UpdateDate: DateTime
    UpdateDateSpecified: int
}

let syncStudents (sokratesApi: SokratesApi) =
    use connection = new MySqlConnection(connectionString)
    let sisStudents = connection.Query<Pupil>("SELECT * FROM pupil") |> Seq.toList
    let adConfig = AD.Configuration.Config.fromEnvironment ()
    let adUsers = AD.Core.getUsers |> Reader.run adConfig
    let sokratesStudents = sokratesApi.FetchStudents None None |> Async.RunSynchronously

    let sisStudentsBySokratesId =
        sisStudents
        |> List.map (fun student -> student.SokratesId, student)
        |> Map.ofList
    let adUsersBySokratesId =
        adUsers
        |> List.choose (fun adUser ->
            match adUser.SokratesId with
            | Some (AD.Domain.SokratesId sokratesId) -> Some (sokratesId, adUser)
            | None -> None
        )
        |> Map.ofList
    let sokratesStudentsBySokratesId =
        sokratesStudents
        |> List.map (fun student -> let (SokratesId sokratesId) = student.Id in sokratesId, student)
        |> Map.ofList

    sokratesStudents
    |> List.iter (fun sokratesStudent ->
        let (SokratesId sokratesId) = sokratesStudent.Id
        let (accountName, accountCreatedAt) =
            Map.tryFind sokratesId adUsersBySokratesId
            |> Option.map (fun adUser -> let (AD.Domain.UserName userName) = adUser.Name in userName, Nullable(adUser.CreatedAt))
            |> Option.defaultValue (null, Nullable<DateTime>())
        let update =
            {
                FirstName1 = sokratesStudent.FirstName1
                FirstName2 = sokratesStudent.FirstName2 |> Option.toObj
                LastName = sokratesStudent.LastName
                SchoolClass = sokratesStudent.SchoolClass
                DateOfBirth = sokratesStudent.DateOfBirth
                AccountName = accountName
                AccountCreatedAt = accountCreatedAt
                SokratesId = sokratesId
            }
        match Map.tryFind sokratesId sisStudentsBySokratesId with
        | Some _ ->
            printfn "Update %s %s (%s)" sokratesStudent.LastName sokratesStudent.FirstName1 sokratesStudent.SchoolClass
            connection.Execute(
                "UPDATE pupil SET firstName1=@FirstName1, firstName2=@FirstName2, lastName=@LastName, schoolClass=@SchoolClass, dateOfBirth=@DateOfBirth, accountName=@AccountName, accountCreatedAt=@AccountCreatedAt WHERE sokratesID=@SokratesId",
                update
            )
            |> ignore
        | None ->
            printfn "Create %s %s (%s)" sokratesStudent.LastName sokratesStudent.FirstName1 sokratesStudent.SchoolClass
            connection.Execute(
                "INSERT INTO pupil (sokratesID, accountName, accountCreatedAt, firstName1, firstName2, lastName, schoolClass, dateOfBirth) VALUES (@SokratesId, @AccountName, @AccountCreatedAt, @FirstName1, @FirstName2, @LastName, @SchoolClass, @DateOfBirth)",
                update
            )
            |> ignore
    )

    sisStudents
    |> List.iter (fun sisStudent ->
        match Map.tryFind sisStudent.SokratesId sokratesStudentsBySokratesId with
        | None ->
            printfn "Delete %s %s (%s)" sisStudent.LastName sisStudent.FirstName1 sisStudent.SchoolClass
            connection.Execute(
                "DELETE FROM pupil WHERE sokratesId=@SokratesId",
                {| SokratesId = sisStudent.SokratesId |}
            )
            |> ignore
        | Some _ -> ()
    )

let syncStudentAddresses (sokratesApi: SokratesApi) =
    let addresses = sokratesApi.FetchStudentAddresses None |> Async.RunSynchronously
    use connection = new MySqlConnection(connectionString)
    connection.Open()
    let dbTransaction = connection.BeginTransaction()
    connection.Execute("DELETE FROM address WHERE addrType='Wohnadresse'") |> ignore
    let updates =
        addresses
        |> List.map (fun address ->
            {
                PersonId = (let (SokratesId studentId) = address.StudentId in studentId)
                AddressType = "Wohnadresse"
                Zip = address.Address |> Option.map (fun address -> address.Zip) |> Option.toObj
                City = address.Address |> Option.map (fun address -> address.City) |> Option.toObj
                Street = address.Address |> Option.map (fun address -> address.Street) |> Option.toObj
                Phone1 = address.Phone1 |> Option.toObj
                Phone2 = address.Phone2 |> Option.toObj
                Country = address.Address |> Option.map (fun address -> address.Country) |> Option.toObj
                From = address.From |> Option.map (fun d -> d.DateTime) |> Option.defaultValue DateTime.MinValue
                FromSpecified = match address.From with | Some _ -> 1 | None -> 0
                Till = address.Till |> Option.map (fun d -> d.DateTime) |> Option.defaultValue DateTime.MinValue
                TillSpecified = match address.Till with | Some _ -> 1 | None -> 0
                UpdateDate = address.UpdateDate |> Option.map (fun d -> d.DateTime) |> Option.defaultValue DateTime.MinValue
                UpdateDateSpecified = match address.UpdateDate with | Some _ -> 1 | None -> 0
            }
        )
    connection.Execute("INSERT INTO address (addrType, personID, plz, city, street, phone1, phone2,  country, fromDate, fromSpecified, tillDate, tillSpecified, updateDate, updateDateSpecified) VALUES (@AddressType, @PersonId, @Zip, @City, @Street, @Phone1, @Phone2, @Country, @From, @FromSpecified, @Till, @TillSpecified, @UpdateDate, @UpdateDateSpecified)", updates) |> ignore
    dbTransaction.Commit()

let syncStudentContactInfos (sokratesApi: SokratesApi) =
    use connection = new MySqlConnection(connectionString)
    let studentIds = connection.Query<string>("SELECT DISTINCT personID FROM pupil") |> Seq.map SokratesId |> Seq.toList

    let contactInfos = sokratesApi.FetchStudentContactInfos studentIds None |> Async.RunSynchronously
    use connection = new MySqlConnection(connectionString)
    connection.Open()
    let dbTransaction = connection.BeginTransaction()
    connection.Execute("DELETE FROM address WHERE addrType<>'Wohnadresse'") |> ignore
    let updates =
        contactInfos
        |> List.collect (fun contactInfo -> contactInfo.ContactAddresses |> List.map (fun address -> contactInfo.StudentId, address))
        |> List.map (fun (SokratesId studentId, address) ->
            {
                PersonId = studentId
                AddressType = address.Type
                Zip = address.Address |> Option.map (fun address -> address.Zip) |> Option.toObj
                City = address.Address |> Option.map (fun address -> address.City) |> Option.toObj
                Street = address.Address |> Option.map (fun address -> address.Street) |> Option.toObj
                Phone1 = address.Phones |> List.tryItem 0 |> Option.toObj
                Phone2 = address.Phones |> List.tryItem 1 |> Option.toObj
                Country = address.Address |> Option.map (fun address -> address.Country) |> Option.toObj
                From = address.From |> Option.map (fun d -> d.DateTime) |> Option.defaultValue DateTime.MinValue
                FromSpecified = match address.From with | Some _ -> 1 | None -> 0
                Till = address.Till |> Option.map (fun d -> d.DateTime) |> Option.defaultValue DateTime.MinValue
                TillSpecified = match address.Till with | Some _ -> 1 | None -> 0
                UpdateDate = address.UpdateDate |> Option.map (fun d -> d.DateTime) |> Option.defaultValue DateTime.MinValue
                UpdateDateSpecified = match address.UpdateDate with | Some _ -> 1 | None -> 0
            }
        )
    connection.Execute("INSERT INTO address (addrType, personID, plz, city, street, phone1, phone2,  country, fromDate, fromSpecified, tillDate, tillSpecified, updateDate, updateDateSpecified) VALUES (@AddressType, @PersonId, @Zip, @City, @Street, @Phone1, @Phone2, @Country, @From, @FromSpecified, @Till, @TillSpecified, @UpdateDate, @UpdateDateSpecified)", updates) |> ignore
    dbTransaction.Commit()

[<EntryPoint>]
let main argv =
    let sokratesConfig = {
        WebServiceUrl = getEnvVarOrFail "SOKRATES_URL"
        UserName = getEnvVarOrFail "SOKRATES_USER_NAME"
        Password = getEnvVarOrFail "SOKRATES_PASSWORD"
        SchoolId = getEnvVarOrFail "SOKRATES_SCHOOL_ID"
        ClientCertificatePath = getEnvVarOrFail "SOKRATES_CLIENT_CERTIFICATE_PATH"
        ClientCertificatePassphrase = getEnvVarOrFail "SOKRATES_CLIENT_CERTIFICATE_PASSPHRASE"
    }
    let sokratesApi = SokratesApi(sokratesConfig)

    printfn "== Syncing students"
    syncStudents sokratesApi

    printfn "== Syncing student addresses"
    syncStudentAddresses sokratesApi

    printfn "== Syncing student contact infos"
    syncStudentContactInfos sokratesApi
    0
