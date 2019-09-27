module FinalTheses

open FSharp.Data

[<Literal>]
let MentorsDataPath = __SOURCE_DIRECTORY__ + "/data/final-theses/mentors.csv"
type Mentors = CsvProvider<MentorsDataPath, ";">

type Mentor = {
    FirstName: string
    LastName: string
    MailAddress: string
}

let getMentors (mentors: Mentors.Row array) =
    mentors
    |> Seq.filter (fun row -> String.equalsCaseInsensitive row.Typ "Betreuer" && String.equalsCaseInsensitive row.Status "Aktiv")
    |> Seq.map (fun row -> { FirstName = row.Vorname.Trim(); LastName = row.Nachname.Trim(); MailAddress = row.Email.Trim() })
    |> Seq.toList
