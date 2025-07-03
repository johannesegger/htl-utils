#r "nuget:Azure.Identity"
#r "nuget:Microsoft.Graph"

open Azure.Core
open Azure.Identity
open Microsoft.Graph
open System
open System.IO
open Microsoft.Graph.Me.SendMail
open Microsoft.Graph.Models

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
    Directory.GetFiles("../out/teachers")
    // |> Seq.filter (fun v -> Path.GetFileNameWithoutExtension v = "EGGJ")
    |> Seq.iter (fun file ->
        let name = Path.GetFileNameWithoutExtension(file)
        let message = new Message(
            ToRecipients =
                Collections.Generic.List<_>([
                    Recipient(
                        EmailAddress = EmailAddress(Address = $"%s{name}@htlvb.at")
                    )
                ]),
            From = Recipient(EmailAddress = EmailAddress(Address = "office@htlvb.at")),
            ReplyTo = Collections.Generic.List<_>([
                Recipient(
                    EmailAddress = EmailAddress(Address = $"STAL@htlvb.at")
                )
            ]),
            Subject = "Wiederholungsprüfungen",
            Body = new ItemBody(
                ContentType = BodyType.Text,
                Content = $"""Liebe Kolleginnen und Kollegen,

im Anhang findet ihr die finale Einteilung zu euren Wiederholungsprüfungen.
Die Raumeinteilung erfolgt zu einem späteren Zeitpunkt.

Schöne Ferien!
"""
            ),
            Attachments = Collections.Generic.List<_>([
                FileAttachment(
                    Name = sprintf "Einteilung zu Wiederholungsprüfungen %s%s" name (Path.GetExtension(file)),
                    ContentType = "application/pdf",
                    ContentBytes = File.ReadAllBytes(file)
                ) :> Attachment
            ])
        )
        try
            printfn "Sending mail to %s" name
            graphClient.Me.SendMail.PostAsync(SendMailPostRequestBody(Message = message)) |> Async.AwaitTask |> Async.RunSynchronously
        with e -> eprintfn "Error while sending mail to %s: %s" name e.Message
    )