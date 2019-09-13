module Sokrates

open System.IO

type SokratesId = SokratesId of string

type Teacher = {
    Id: SokratesId
    ShortName: string
    FirstName: string
    LastName: string
}

type private SokratesTeacher = FSharp.Data.JsonProvider<"data\\sokrates\\teachers.json">

let getTeachers (stream: Stream) = async {
    use reader = new StreamReader(stream)
    let! content = reader.ReadToEndAsync() |> Async.AwaitTask
    return
        SokratesTeacher.Parse(content)
        |> Seq.map (fun entry -> { Id = SokratesId entry.Id; ShortName = entry.ShortName; FirstName = entry.FirstName; LastName = entry.LastName })
        |> Seq.toList
}