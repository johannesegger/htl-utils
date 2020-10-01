module FinalTheses.Core

open FinalTheses.Configuration
open FinalTheses.Domain
open FSharp.Data
open System.IO

[<Literal>]
let private MentorsDataPath = __SOURCE_DIRECTORY__ + "/data/mentors.csv"
type private Mentors = CsvProvider<MentorsDataPath, Separators = ";">

let getMentors = reader {
    let! config = Reader.environment
    return
        config.MentorsFilePath
        |> Mentors.Load
        |> fun v -> v.Rows
        |> Seq.filter (fun row -> CIString row.Typ = CIString "Betreuer" && CIString row.Status = CIString "Aktiv")
        |> Seq.map (fun row -> { FirstName = row.Vorname.Trim(); LastName = row.Nachname.Trim(); MailAddress = row.Email.Trim() })
        |> Seq.toList
}
