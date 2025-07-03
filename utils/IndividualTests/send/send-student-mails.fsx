#r "nuget:Azure.Identity"
#r "nuget:FSharp.Data"
#r "nuget:Microsoft.Graph"

open Azure.Core
open Azure.Identity
open FSharp.Data
open Microsoft.Graph
open System
open System.IO
open Microsoft.Graph.Me.SendMail
open Microsoft.Graph.Models

[<Literal>]
let samplePath = __SOURCE_DIRECTORY__ + "\\students.csv"
type MailAddresses = CsvProvider<samplePath>
let mailAddresses = MailAddresses.GetSample()

let args = Environment.GetCommandLineArgs()[2..]

let scopes = [| "Mail.Send.Shared" |]
let deviceCodeCredential =
    let opts = DeviceCodeCredentialOptions (
        TenantId = args.[0],
        ClientId = args.[1],
        TokenCachePersistenceOptions = TokenCachePersistenceOptions(Name = "HtlUtils.IndividualTests"),
        DeviceCodeCallback = (fun code ct ->
            printfn $"%s{code.Message}"
            Threading.Tasks.Task.CompletedTask
        )
    )
    let authTokenPath = Path.Combine(Path.GetTempPath(), $"%s{opts.TokenCachePersistenceOptions.Name}.token")
    if File.Exists(authTokenPath) then
        use fileStream = File.OpenRead(authTokenPath)
        opts.AuthenticationRecord <- AuthenticationRecord.DeserializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
        DeviceCodeCredential(opts)
    else
        let deviceCodeCredential = DeviceCodeCredential(opts)
        let authenticationRecord = deviceCodeCredential.AuthenticateAsync(TokenRequestContext(scopes)) |> Async.AwaitTask |> Async.RunSynchronously
        use fileStream = File.OpenWrite(authTokenPath)
        authenticationRecord.SerializeAsync(fileStream) |> Async.AwaitTask |> Async.RunSynchronously
        deviceCodeCredential

do
    use graphClient = new GraphServiceClient(deviceCodeCredential, scopes)
    Directory.GetFiles(@".\out\students")
    // |> Seq.head
    |> Seq.iter (fun file ->
        let fullName = Path.GetFileNameWithoutExtension(file)
        let student =
            mailAddresses.Rows
            |> Seq.tryFind (fun e -> $"{e.ClassName} {e.LastName} {e.FirstName} - {e.SokratesId}".Equals(fullName, StringComparison.InvariantCultureIgnoreCase))
        match student with
        | Some student ->
            let message = new Message(
                ToRecipients =
                    Collections.Generic.List<_>([
                        Recipient(
                            EmailAddress = EmailAddress(Address = student.Mail)
                        )
                    ]),
                From = Recipient(EmailAddress = EmailAddress(Address = "office@htlvb.at")),
                Subject = "Einteilung zu Wiederholungsprüfungen",
                Body = new ItemBody(
                    ContentType = BodyType.Text,
                    Content = $"""Liebe Schülerinnen und Schüler,

im Anhang findet ihr die Einteilung zu euren Wiederholungsprüfungen.

Viel Erfolg dabei und eine schöne letzte Schulwoche.
"""
                ),
                Attachments = Collections.Generic.List<_>([
                    FileAttachment(
                        Name = sprintf "Einteilung zu Wiederholungsprüfungen %s %s%s" student.FirstName student.LastName (Path.GetExtension(file)),
                        ContentType = "application/pdf",
                        ContentBytes = File.ReadAllBytes(file)
                    ) :> Attachment
                ])
            )
            try
                printfn "Sending mail to %s (%s)" fullName student.Mail
                graphClient.Me.SendMail.PostAsync(SendMailPostRequestBody(Message = message)) |> Async.AwaitTask |> Async.RunSynchronously
            with e -> eprintfn "Error while sending mail to %s: %s" fullName e.Message
        | None -> printfn "WARNING: Mail address of %s couldn't be found" fullName
    )