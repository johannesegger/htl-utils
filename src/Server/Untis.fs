module Untis

open FSharp.Data
open System

[<Literal>]
let TeachingDataPath = __SOURCE_DIRECTORY__ + "/data/Untis/GPU002.TXT"
type TeachingData = CsvProvider<TeachingDataPath, Schema=",,,,Class,Teacher,Subject">

let getClassesWithTeachers (teachingData: TeachingData.Row array) =
    teachingData
    |> Seq.groupBy (fun row -> Class.tryParse row.Class)
    |> Seq.choose (function
        | Some ``class``, rows ->
            Some (``class``, rows |> Seq.map (fun row -> row.Teacher) |> Set.ofSeq)
        | None, _ -> None
    )
    |> Set.ofSeq

// TODO support multiple class teachers? or classes without a class teacher?
let getClassTeachers (teachingData: TeachingData.Row array) =
    teachingData
    |> Seq.filter (fun row -> String.equalsCaseInsensitive row.Subject "ORD")
    |> Seq.choose (fun row ->
        match Class.tryParse row.Class with
        | Some ``class`` -> Some (``class``, row.Teacher)
        | None -> None
    )
    |> Map.ofSeq
