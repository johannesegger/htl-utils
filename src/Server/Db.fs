module Db

open System.Data
open System.Data.Common
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
