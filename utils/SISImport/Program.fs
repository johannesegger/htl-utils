open AD.Core
open Dapper
open MySql.Data.MySqlClient
open Sokrates
open System
open ClosedXML.Excel

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

let syncStudents (sokratesApi: SokratesApi) (adApi: ADApi) = async {
    use connection = new MySqlConnection(connectionString)
    let! sisStudents = connection.QueryAsync<Pupil>("SELECT * FROM pupil") |> Async.AwaitTask |> Async.map Seq.toList
    let! adUsers = adApi.GetUsers ()
    let! sokratesStudents = sokratesApi.FetchStudents None None

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

    do!
        sokratesStudents
        |> List.map (fun sokratesStudent ->
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
                connection.ExecuteAsync(
                    "UPDATE pupil SET firstName1=@FirstName1, firstName2=@FirstName2, lastName=@LastName, schoolClass=@SchoolClass, dateOfBirth=@DateOfBirth, accountName=@AccountName, accountCreatedAt=@AccountCreatedAt WHERE sokratesID=@SokratesId",
                    update
                )
                |> Async.AwaitTask
                |> Async.Ignore
            | None ->
                printfn "Create %s %s (%s)" sokratesStudent.LastName sokratesStudent.FirstName1 sokratesStudent.SchoolClass
                connection.ExecuteAsync(
                    "INSERT INTO pupil (sokratesID, accountName, accountCreatedAt, firstName1, firstName2, lastName, schoolClass, dateOfBirth) VALUES (@SokratesId, @AccountName, @AccountCreatedAt, @FirstName1, @FirstName2, @LastName, @SchoolClass, @DateOfBirth)",
                    update
                )
                |> Async.AwaitTask
                |> Async.Ignore
        )
        |> Async.Sequential
        |> Async.Ignore

    do!
        sisStudents
        |> List.map (fun sisStudent -> async {
            match Map.tryFind sisStudent.SokratesId sokratesStudentsBySokratesId with
            | None ->
                printfn "Delete %s %s (%s)" sisStudent.LastName sisStudent.FirstName1 sisStudent.SchoolClass
                do!
                    connection.ExecuteAsync(
                        "DELETE FROM pupil WHERE sokratesId=@SokratesId",
                        {| SokratesId = sisStudent.SokratesId |}
                    )
                    |> Async.AwaitTask
                    |> Async.Ignore
            | Some _ -> return ()
        })
        |> Async.Sequential
        |> Async.Ignore
}

let syncStudentAddresses (sokratesApi: SokratesApi) = async {
    let! addresses = sokratesApi.FetchStudentAddresses None
    use connection = new MySqlConnection(connectionString)
    do! connection.OpenAsync() |> Async.AwaitTask
    let! dbTransaction = connection.BeginTransactionAsync() |> Async.AwaitTask
    do! connection.ExecuteAsync("DELETE FROM address WHERE addrType='Wohnadresse'") |> Async.AwaitTask |> Async.Ignore
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
    do! connection.ExecuteAsync("INSERT INTO address (addrType, personID, plz, city, street, phone1, phone2,  country, fromDate, fromSpecified, tillDate, tillSpecified, updateDate, updateDateSpecified) VALUES (@AddressType, @PersonId, @Zip, @City, @Street, @Phone1, @Phone2, @Country, @From, @FromSpecified, @Till, @TillSpecified, @UpdateDate, @UpdateDateSpecified)", updates) |> Async.AwaitTask |> Async.Ignore
    do! dbTransaction.CommitAsync() |> Async.AwaitTask
}

let syncStudentContactInfos (sokratesApi: SokratesApi) = async {
    use connection = new MySqlConnection(connectionString)
    let! studentIds = connection.QueryAsync<string>("SELECT DISTINCT personID FROM pupil") |> Async.AwaitTask |> Async.map (Seq.map SokratesId >> Seq.toList)

    let! contactInfos = sokratesApi.FetchStudentContactInfos studentIds None
    use connection = new MySqlConnection(connectionString)
    do! connection.OpenAsync() |> Async.AwaitTask
    let dbTransaction = connection.BeginTransaction()
    do! connection.ExecuteAsync("DELETE FROM address WHERE addrType<>'Wohnadresse'") |> Async.AwaitTask |> Async.Ignore
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
    do! connection.ExecuteAsync("INSERT INTO address (addrType, personID, plz, city, street, phone1, phone2,  country, fromDate, fromSpecified, tillDate, tillSpecified, updateDate, updateDateSpecified) VALUES (@AddressType, @PersonId, @Zip, @City, @Street, @Phone1, @Phone2, @Country, @From, @FromSpecified, @Till, @TillSpecified, @UpdateDate, @UpdateDateSpecified)", updates) |> Async.AwaitTask |> Async.Ignore
    do! dbTransaction.CommitAsync() |> Async.AwaitTask
}

[<CLIMutable>]
type ExistingPhoneNumber = {
    ShortName: string
    PhoneType: string
    PhoneNumber: string
    RowId: string
}

type PhoneNumberModification =
    | AddPhoneNumber of {| ShortName: string; PhoneType: string; PhoneNumber: string |}
    | RemoveTeacher of ExistingPhoneNumber
    | RemovePhoneNumber of ExistingPhoneNumber

// module PhoneNumber =
//     // TODO this is just an approximation
//     let format = function
//         | Mobile number when number.Length = 13 -> number.[0..3] + " " + number.[4..6] + " " + number.[7..9] + " " + number.[10..12]
//         | Mobile number when number.Length = 12 -> number.[0..3] + " " + number.[4..6] + " " + number.[7..9] + " " + number.[10..11]
//         | Mobile number when number.Length = 11 -> number.[0..3] + " " + number.[4..6] + " " + number.[7..8] + " " + number.[9..10]
//         | Mobile number -> number
//         | Home number when number.StartsWith("0720") && number.Length = 10 -> number.[0..3] + " " + number.[4..6] + " " + number.[7..9]
//         | Home number when number.Length = 10 -> number.[0..4] + " " + number.[5..7] + " " + number.[8..9]
//         | Home number when number.Length = 9 -> number.[0..4] + " " + number.[5..8]
//         | Home number -> number

// TODO doesn't work very well because both Sokrates and AD are not always up-to-date
// let syncTeacherPhoneNumbers (sokratesApi: SokratesApi) (adApi: ADApi) = async {
//     let! sokratesTeachers = sokratesApi.FetchTeachers
//     let! adTeachers = adApi.GetUsers() |> Async.map (List.filter (fun v -> v.Type = AD.Domain.Teacher))
//     let activeTeachers =
//         sokratesTeachers
//         |> List.filter (fun v -> adTeachers |> List.exists (fun t -> t.Name = AD.Domain.UserName v.ShortName))
//         |> List.sortBy (fun v -> v.ShortName)
//     use connection = new MySqlConnection(connectionString)
//     let! existingPhoneNumbers = connection.QueryAsync<ExistingPhoneNumber>("SELECT Llogin as ShortName, Raum as PhoneType, Telefon as PhoneNumber, nr as RowId FROM telefonliste WHERE quelle = 'LehrerDB 2022'") |> Async.AwaitTask |> Async.map Seq.toList

//     let addModifications =
//         activeTeachers
//         |> List.collect (fun teacher ->
//             teacher.Phones
//             |> List.choose (fun phone ->
//                 let phoneType =
//                     match phone with
//                     | Mobile _ -> "Mobil"
//                     | Home _ -> "Festnetz"
//                 let phoneNumber = PhoneNumber.format phone
//                 let existingPhoneNumber =
//                     existingPhoneNumbers
//                     |> List.tryFind (fun v -> v.PhoneType = phoneType && v.PhoneNumber = phoneNumber)
//                 match existingPhoneNumber with
//                 | Some _ -> None
//                 | None -> Some (AddPhoneNumber {| ShortName = teacher.ShortName; PhoneType = phoneType; PhoneNumber = phoneNumber |})
//             )
//         )
//     let removeModifications =
//         existingPhoneNumbers
//         |> List.choose (fun existingPhoneNumber ->
//             let teacher = activeTeachers |> List.tryFind (fun v -> v.ShortName = existingPhoneNumber.ShortName)
//             match teacher with
//             | Some teacher ->
//                 if teacher.Phones |> List.exists (PhoneNumber.format >> ((=) existingPhoneNumber.PhoneNumber)) |> not then Some (RemovePhoneNumber existingPhoneNumber)
//                 else None
//             | None -> Some (RemoveTeacher existingPhoneNumber)
//         )

//     List.concat [ addModifications; removeModifications ]
//     |> List.sortBy (function
//         | AddPhoneNumber v -> (v.ShortName, 1)
//         | RemoveTeacher v -> (v.ShortName, 2)
//         | RemovePhoneNumber v -> (v.ShortName, 3)
//     )
//     |> List.iter (function
//         | AddPhoneNumber v ->
//             Console.ForegroundColor <- ConsoleColor.Green
//             printfn $"Add phone number of %s{v.ShortName}: %s{v.PhoneType} %s{v.PhoneNumber}"
//             Console.ResetColor()
//         | RemoveTeacher v ->
//             Console.ForegroundColor <- ConsoleColor.Red
//             printfn $"Remove phone number of removed teacher %s{v.ShortName}: %s{v.PhoneType} %s{v.PhoneNumber}"
//             Console.ResetColor()
//         | RemovePhoneNumber v ->
//             Console.ForegroundColor <- ConsoleColor.Yellow
//             printfn $"Remove phone number of %s{v.ShortName}: %s{v.PhoneType} %s{v.PhoneNumber}"
//             Console.ResetColor()
//     )
// }

let syncTeacherPhoneNumbers (excelDocPath: string) = async {
    let rows =
        use doc = new XLWorkbook(excelDocPath)
        let worksheet = doc.Worksheets.Worksheet(1)
        worksheet.Rows()
        |> Seq.skip 1
        |> Seq.cast<IXLRow>
        |> Seq.map (fun row ->
            {| ShortName = row.Cell("A").GetString(); PhoneNumber = row.Cell("H").GetString() |}
        )
        |> Seq.filter (fun v -> not <| String.IsNullOrWhiteSpace v.PhoneNumber)
        |> Seq.toList
    use connection = new MySqlConnection(connectionString)
    let! existingPhoneNumbers = connection.QueryAsync<ExistingPhoneNumber>("SELECT Llogin as ShortName, Raum as PhoneType, Telefon as PhoneNumber, nr as RowId FROM telefonliste WHERE quelle LIKE 'LehrerDB %'") |> Async.AwaitTask |> Async.map Seq.toList

    let addModifications =
        rows
        |> List.choose (fun row ->
            let phoneType = "Mobil"
            let phoneNumber = row.PhoneNumber
            let existingPhoneNumber =
                existingPhoneNumbers
                |> List.tryFind (fun v -> v.PhoneType = phoneType && v.PhoneNumber = phoneNumber)
            match existingPhoneNumber with
            | Some _ -> None
            | None -> Some (AddPhoneNumber {| ShortName = row.ShortName; PhoneType = phoneType; PhoneNumber = phoneNumber |})
        )
    let removeModifications =
        existingPhoneNumbers
        |> List.filter (fun v -> v.PhoneType = "Mobil")
        |> List.choose (fun existingPhoneNumber ->
            let row = rows |> List.tryFind (fun v -> v.ShortName = existingPhoneNumber.ShortName)
            match row with
            | Some row ->
                if row.PhoneNumber <> existingPhoneNumber.PhoneNumber then Some (RemovePhoneNumber existingPhoneNumber)
                else None
            | None -> Some (RemoveTeacher existingPhoneNumber)
        )

    do!
        List.concat [ addModifications; removeModifications ]
        |> List.sortBy (function
            | AddPhoneNumber v -> (v.ShortName, 1)
            | RemoveTeacher v -> (v.ShortName, 2)
            | RemovePhoneNumber v -> (v.ShortName, 3)
        )
        |> List.map (fun modification -> async {
            match modification with
            | AddPhoneNumber v ->
                Console.ForegroundColor <- ConsoleColor.Green
                printfn $"Add phone number of %s{v.ShortName}: %s{v.PhoneType} %s{v.PhoneNumber}"
                Console.ResetColor()
                do! connection.ExecuteAsync("INSERT INTO telefonliste (Llogin, Raum, Telefon, quelle) VALUES(@ShortName, @PhoneType, @PhoneNumber, 'LehrerDB 2023')", v) |> Async.AwaitTask |> Async.Ignore
            | RemoveTeacher v ->
                Console.ForegroundColor <- ConsoleColor.Red
                printfn $"Remove phone number of removed teacher %s{v.ShortName}: %s{v.PhoneType} %s{v.PhoneNumber}"
                Console.ResetColor()
                do! connection.ExecuteAsync("DELETE FROM telefonliste WHERE nr = @RowId", v) |> Async.AwaitTask |> Async.Ignore
            | RemovePhoneNumber v ->
                Console.ForegroundColor <- ConsoleColor.Yellow
                printfn $"Remove phone number of %s{v.ShortName}: %s{v.PhoneType} %s{v.PhoneNumber}"
                Console.ResetColor()
                do! connection.ExecuteAsync("DELETE FROM telefonliste WHERE nr = @RowId", v) |> Async.AwaitTask |> Async.Ignore
        })
        |> Async.Sequential
        |> Async.Ignore
}

[<EntryPoint>]
let main argv =
    let sokratesApi = SokratesApi.FromEnvironment()
    use adApi = ADApi.FromEnvironment()

    printfn "== Syncing students"
    syncStudents sokratesApi adApi |> Async.RunSynchronously

    printfn "== Syncing student addresses"
    syncStudentAddresses sokratesApi |> Async.RunSynchronously

    printfn "== Syncing student contact infos"
    syncStudentContactInfos sokratesApi |> Async.RunSynchronously

    printfn "== Syncing teacher phone numbers"
    // syncTeacherPhoneNumbers sokratesApi adApi |> Async.RunSynchronously
    syncTeacherPhoneNumbers @"utils\SISImport\Lehrerliste klein 23-24.xlsx" |> Async.RunSynchronously
    0
