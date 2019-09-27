module FinalTheses

open Expecto

let private mentorsData = """Benutzername;TitelVor;Vorname;Nachname;Email;Typ;Status
betreuer_123456_78;DI;Alice1 ; Henderson; alice.henderson@htlvb.at ;betreuer;Inaktiv
betreuer_123456_78;DI;Alice2 ; Henderson; alice.henderson@htlvb.at ;betreuer;Aktiv
av_123456_78;DI;Alice3 ; Henderson; alice.henderson@htlvb.at ;av;Aktiv
"""

let tests = testList "FinalTheses" [
    testCase "Get final theses mentors" <| fun () ->
        let mentors = FinalTheses.getMentors (FinalTheses.Mentors.ParseRows mentorsData)
        Expect.equal mentors [ { FirstName = "Alice2"; LastName = "Henderson"; MailAddress = "alice.henderson@htlvb.at" } ] "Mentors don't match"
]
