module Db

open System
open System.Data
open System.Data.Common
open System.IO
open MySql.Data.MySqlClient

let private execute connectionString fn = async {
    use connection = new MySqlConnection(connectionString)
    do! connection.OpenAsync() |> Async.AwaitTask
    return! fn connection
}

let private readAll (reader: DbDataReader) (mapEntry: IDataRecord -> 'a) : Async<'a list> = async {
    let rec readAll' acc = async {
        let! cont = reader.ReadAsync() |> Async.AwaitTask
        if cont
        then return! readAll' ((mapEntry reader) :: acc)
        else return acc
    }
    let! entries = readAll' []
    return List.rev entries
}

let getClassList connectionString = async {
    let fn (connection: MySqlConnection) = async {
        use command = connection.CreateCommand()
        command.CommandText <- "SELECT DISTINCT(SchoolClass) FROM pupil ORDER BY SchoolClass"
        use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask
        return! readAll reader (fun record -> record.GetString 0)
    }
    return! execute connectionString fn
}

let getStudents connectionString className = async {
    let fn (connection: MySqlConnection) = async {
        use command = connection.CreateCommand()
        command.CommandText <- "SELECT LastName, FirstName1 FROM pupil WHERE SchoolClass = @className ORDER BY LastName, FirstName1"
        command.Parameters.AddWithValue("className", className) |> ignore
        use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask
        return! readAll reader (fun record -> record.GetString 0, record.GetString 1)
    }
    return! execute connectionString fn
}

type ContactKind =
    | Mobile of string
    | Email of string
    | Home of string

let getContacts connectionString = async {
    let normalizePhone (number: string) =
        number
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("/", "")
            .Replace("(", "")
            .Replace(")", "")

    let fn (connection: MySqlConnection) = async {
        use command = connection.CreateCommand()
        command.CommandText <- "SELECT Llogin, Raum, Telefon FROM telefonliste"
        use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask
        return! readAll reader (fun record ->
            record.GetString 0,
            record.GetString 1,
            record.GetString 2)
    }
    let! contacts = execute connectionString fn

    return
        contacts
        |> Seq.groupBy (fun (login, _, _) -> login)
        |> Seq.map (fun (key, items) ->
            key,
            items
            |> Seq.choose(fun (_, raum, telefon) ->
                match raum with
                | "E-Mail" -> Email telefon |> Some
                | "Festnetz" -> normalizePhone telefon |> Home |> Some
                | "Mobil" -> normalizePhone telefon |> Mobile |> Some
                | _ -> None
            )
            |> Seq.toList
        )
        |> Map.ofSeq
}

type Teacher = {
    ShortName: string
    FirstName: string
    LastName: string
    SocialInsuranceNumber: string option
}

let getTeachers connectionString = async {
    let fn (connection: MySqlConnection) = async {
        use command = connection.CreateCommand()
        command.CommandText <- "SELECT Llogin, Lvorname, Lname, Svnr FROM lehrer WHERE Ausgeschieden IS NULL AND Lname <> \"0\" ORDER BY Lname, Lvorname"
        use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask
        return! readAll reader (fun record ->
            let shortName = record.GetString 0
            let firstName = record.GetString 1
            let lastName = record.GetString 2
            let svnr = if reader.IsDBNull 3 then None else record.GetString 3 |> Some
            {
                ShortName = shortName
                FirstName = firstName
                LastName = lastName
                SocialInsuranceNumber = svnr
           })
    }
    return! execute connectionString fn
}
