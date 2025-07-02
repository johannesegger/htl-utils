#r "nuget: FSharp.Data"

open FSharp.Data
open System
open System.IO
open System.Net
open System.Net.Mail

[<Literal>]
let samplePath = __SOURCE_DIRECTORY__ + "\\students.csv"
type MailAddresses = CsvProvider<samplePath>
let mailAddresses = MailAddresses.GetSample()

Directory.GetFiles(@".\out\students\pdf")
|> Seq.iter (fun file ->
    let fullName = Path.GetFileNameWithoutExtension(file)
    let student =
        mailAddresses.Rows
        |> Seq.tryFind (fun e -> $"{e.LastName} {e.FirstName} - {e.SokratesId}".Equals(fullName, StringComparison.InvariantCultureIgnoreCase))
    match student with
    | Some student ->
        use message = new MailMessage("office@htlvb.at", student.Mail)
        message.Subject <- "Einteilung zu Wiederholungsprüfungen"
        let body = $"""Liebe Schülerinnen und Schüler,

im Anhang findet ihr die Einteilung zu euren Wiederholungsprüfungen inkl. Raumzuteilung.

Viel Erfolg dabei und eine schöne letzte Ferienwoche.
"""
        message.Body <- body
        use stream = File.OpenRead(file)
        use attachment = new Attachment(stream, sprintf "Einteilung zu Wiederholungsprüfungen %s %s%s" student.FirstName student.LastName (Path.GetExtension(file)), "application/pdf")
        message.Attachments.Add(attachment)
        use smtpClient = new SmtpClient("smtp.office365.com", 587)
        smtpClient.Credentials <- NetworkCredential("office@htlvb.at", "htlVB417427")
        smtpClient.EnableSsl <- true

        try
            printfn "Sending mail to %s (%s)" fullName student.Mail
            smtpClient.Send(message)
        with e -> eprintfn "Error while sending mail to %s: %s" fullName e.Message
    | None -> printfn "WARNING: Mail address of %s couldn't be found" fullName
)