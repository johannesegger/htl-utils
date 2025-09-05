namespace IndividualTests.Server.Controllers

open FsToolkit.ErrorHandling
open iText.Kernel.Pdf
open iText.Kernel.Utils
open IndividualTests.Server
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open Microsoft.Graph
open PuppeteerSharp
open System
open System.Globalization
open System.IO
open System.Net.Mime
open System.Text.RegularExpressions

[<AutoOpen>]
module Letter =
    module Dto =
        type TimeOfDay = {
            Hours: float
        }

        type TestPart = {
            Type: string
            Begin: TimeOfDay option
            End: TimeOfDay option
            Time: TimeOfDay option
            Room: string option
        }

        type Teacher = {
            ShortName: string option
            LastName: string option
            FirstName: string option
            MailAddress: string option
        }

        type Student = {
            LastName: string option
            FirstName: string option
            ClassName: string option
            MailAddress: string option
            Gender: string option
            Address: {|
                Country: string
                Zip: string
                City: string
                Street: string
            |} option
        }

        type TestData = {
            Student: Student
            Subject: string option
            Teacher1: Teacher
            Teacher2: Teacher option
            Date: DateTime option
            PartWritten: TestPart option
            PartOral: TestPart option
            AdditionalData: {|
                ColumnName: string
                Value: string
            |} list
        }

        type GenerateLettersDto = {
            Tests: TestData list
            LetterText: string
        }

        type SendLettersDto = {
            Tests: TestData list
            LetterText: string
            MailSubject: string
            MailText: string
            OverwriteMailTo: string option
        }

    module Domain =
        type Teacher = {
            ShortName: string option
            LastName: string option
            FirstName: string option
            MailAddress: string option
        }

        module Teacher =
            let fromDto (v: Dto.Teacher) =
                {
                    ShortName = v.ShortName
                    LastName = v.LastName
                    FirstName = v.FirstName
                    MailAddress = v.MailAddress
                }

        type TestPart =
            | ExactTimeSpan of start: TimeSpan * ``end``: TimeSpan * room: string option
            | ExactTime of TimeSpan * room: string option
            | StartTime of TimeSpan * room: string option
            | Afterwards of room: string option
            | NoTime
        module TimeSpan =
            let fromTimeOfDay (v: Dto.TimeOfDay) = TimeSpan.FromHours v.Hours
        module TestPart =
            let private tryParseTimeSpan (text: string) =
                match DateTime.TryParse(text) with
                | (true, v) -> Some v.TimeOfDay
                | _ -> None
            let private tryParseExactTime text room =
                match tryParseTimeSpan text with
                | Some v -> ExactTime (v, room) |> Some
                | _ -> None
            let private tryParseExactTimeSpan start ``end`` room =
                match tryParseTimeSpan start, tryParseTimeSpan ``end`` with
                | Some start, Some ``end`` -> ExactTimeSpan (start, ``end``, room) |> Some
                | _ -> None
            let private tryParseStartTime (text: string) room =
                let m = Regex.Match(text, @"(Ab|ab) (?<time>\d\d:\d\d)")
                if m.Success then StartTime (TimeSpan.Parse(m.Groups.["time"].Value), room) |> Some
                else None
            let private tryParseAfterwards (text: string) room =
                if text = "im Anschluss" then Afterwards room |> Some
                else None
            let private tryParseNoTime (text: string) =
                if text = "" then Some NoTime
                else None
            let tryParse start ``end`` room =
                tryParseExactTimeSpan start ``end`` room
                |> Option.orElse (tryParseExactTime start room)
                |> Option.orElse (tryParseStartTime start room)
                |> Option.orElse (tryParseAfterwards start room)
                |> Option.orElse (tryParseNoTime start)
            let fromDto (v: Dto.TestPart) =
                if v.Type = "exact-time-span" then
                    match v.Begin, v.End with
                    | Some ``begin``, Some ``end`` -> Some (ExactTimeSpan (TimeSpan.fromTimeOfDay ``begin``, TimeSpan.fromTimeOfDay ``end``, v.Room))
                    | _ -> None
                // TODO match other types, not necessary at the moment
                else None

            let parseRoom v =
                if String.IsNullOrWhiteSpace v then None
                else Some v
            let tryGetStartTime = function
                | ExactTimeSpan (start, _, _) -> Some start
                | ExactTime (start, _) -> Some start
                | StartTime (start, _) -> Some start
                | Afterwards _ -> None
                | NoTime -> None
            let tryGetRoom = function
                | ExactTimeSpan (_, _, room) -> room
                | ExactTime (_, room) -> room
                | StartTime (_, room) -> room
                | Afterwards room -> room
                | NoTime -> None
            let toString = function
                | ExactTimeSpan (start, ``end``, room) ->
                    let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
                    sprintf "%s - %s%s" (start.ToString("hh\\:mm")) (``end``.ToString("hh\\:mm")) roomText
                | ExactTime (v, room) ->
                    let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
                    sprintf "%s%s" (v.ToString("hh\\:mm")) roomText
                | StartTime (v, room) ->
                    let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
                    sprintf "ab %s%s" (v.ToString("hh\\:mm")) roomText
                | Afterwards room ->
                    let roomText = room |> Option.map (fun v -> $" (%s{v})") |> Option.defaultValue ""
                    sprintf "anschließend%s" roomText
                | NoTime -> "-"

        type Gender = Male | Female
        module Gender =
            let tryParse v =
                if v = "m" then Some Male
                elif v = "f" then Some Female
                else None
            let getSalutation = function
                | Male -> "Herr"
                | Female -> "Frau"

        type Student = {
            LastName: string option
            FirstName: string option
            ClassName: string option
            MailAddress: string option
            Gender: Gender option
            Address: {|
                Country: string
                Zip: string
                City: string
                Street: string
            |} option
        }

        module Student =
            let fromDto (v: Dto.Student) =
                {
                    LastName = v.LastName
                    FirstName = v.FirstName
                    ClassName = v.ClassName
                    MailAddress = v.MailAddress
                    Gender = v.Gender |> Option.bind Gender.tryParse
                    Address = v.Address
                }

        type TestData = {
            Student: Student
            Subject: string option
            Teacher1: Teacher
            Teacher2: Teacher option
            Date: DateTime option
            PartWritten: TestPart option
            PartOral: TestPart option
            AdditionalData: {|
                ColumnName: string
                Value: string
            |} list
        }

        module TestData =
            let fromDto (test: Dto.TestData) =
                let teacher1 = Teacher.fromDto test.Teacher1
                let teacher2 = test.Teacher2 |> Option.map Teacher.fromDto
                {
                    Student = Student.fromDto test.Student
                    Subject = test.Subject
                    Teacher1 = teacher1
                    Teacher2 = teacher2
                    Date = test.Date
                    PartWritten = test.PartWritten |> Option.bind TestPart.fromDto
                    PartOral = test.PartOral |> Option.bind TestPart.fromDto
                    AdditionalData = test.AdditionalData
                }

            let toSortable v =
                (
                    v.Date,
                    (
                        v.PartWritten
                        |> Option.bind TestPart.tryGetStartTime
                        |> function
                        | Some v -> v
                        | None -> TimeSpan.MaxValue
                    ),
                    (
                        v.PartOral
                        |> Option.bind TestPart.tryGetStartTime
                        |> function
                        | Some v -> v
                        | None -> TimeSpan.MaxValue
                    ),
                    v.Student.ClassName,
                    v.Student.LastName,
                    v.Student.FirstName
                )

        module Date =
            let culture = CultureInfo.GetCultureInfo("de-AT")
            let toString (v: DateTime) = sprintf "%s, %s" (v.ToString("ddd", culture)) (v.ToString("d", culture))


        let private generateTestRow template additionalProperties (test: TestData) =
            let properties = [
                yield! test.AdditionalData |> Seq.map (fun v -> (v.ColumnName, v.Value))
                ("lastName", test.Student.LastName |> Option.defaultValue "-")
                ("firstName", test.Student.FirstName |> Option.defaultValue "-")
                ("class", test.Student.ClassName |> Option.defaultValue "-")
                ("subject", test.Subject |> Option.defaultValue "-")
                ("teacher1", test.Teacher1.ShortName |> Option.defaultValue "-")
                ("teacher2", match test.Teacher2 with | Some v -> v.ShortName |> Option.defaultValue "-" | None -> "-")
                ("date", test.Date |> Option.map Date.toString |> Option.defaultValue "-")
                ("partWritten", test.PartWritten |> Option.map TestPart.toString |> Option.defaultValue "-" )
                ("partOral", test.PartOral |> Option.map TestPart.toString |> Option.defaultValue "-")
                yield! additionalProperties
            ]
            (template, properties)
            ||> List.fold (fun s (k, v) -> String.replace (sprintf "{{%s}}" k) v s)

        let generateTeacherLetter letterTemplate testRowTemplate teacherShortName tests =
            tests
            |> List.groupBy (fun test -> test.Date)
            |> List.sortBy fst
            |> List.map (fun (date, tests) ->
                let testTableRows =
                    tests
                    |> List.sortBy TestData.toSortable
                    |> List.map (fun test ->
                        let additionalProperties = [
                            "row-class", (if test.Teacher1.ShortName = teacherShortName then "is-pruefer" else "is-beisitz")
                            "pruefer-reference", (if test.Teacher2 |> Option.bind _.ShortName = teacherShortName then sprintf "s. %s" (test.Teacher1.ShortName |> Option.defaultValue "Prüfer") else "&nbsp;")
                        ]
                        generateTestRow testRowTemplate additionalProperties test
                    )
                    |> String.concat ""
                letterTemplate
                |> String.replace "{{date}}" (date |> Option.map Date.toString |> Option.defaultValue "-")
                |> String.replace "{{teacherShortName}}" (teacherShortName |> Option.defaultValue "-")
                |> String.replace "{{testTableRows}}" testTableRows
            )
            |> String.concat ""

        let generateTeacherLetters (documentTemplate, letterTemplate, testRowTemplate) tests =
            let includeRoom = tests |> List.exists (fun v -> v.PartOral |> Option.bind TestPart.tryGetRoom |> Option.isSome)
            let styles = [
                if includeRoom then ".room-unavailable { display: none; }"
                else ".room-available { display: none; }"
            ]
            let teachers =
                tests
                |> List.collect (fun v -> [ v.Teacher1; yield! Option.toList v.Teacher2 ])
                |> List.distinctBy _.ShortName
                |> List.sort
            teachers
            |> List.map (fun teacher ->
                let teacherTests =
                    tests
                    |> List.filter (fun v -> teacher.ShortName = v.Teacher1.ShortName || (v.Teacher2 |> Option.exists (fun t -> t.ShortName = teacher.ShortName)))
                let teacherLetter = generateTeacherLetter letterTemplate testRowTemplate teacher.ShortName teacherTests
                let document =
                    documentTemplate
                    |> String.replace "{{header}}" $"""<style>%s{styles |> String.concat "\n"}</style>"""
                    |> String.replace "{{teacherShortName}}" (teacher.ShortName |> Option.defaultValue "-")
                    |> String.replace "{{content}}" teacherLetter
                (teacher, document)
            )

        let generateStudentLetter letterTemplate testRowTemplate student tests =
            let testTableRows =
                tests
                |> List.sortBy TestData.toSortable
                |> List.map (generateTestRow testRowTemplate [])
                |> String.concat ""
            letterTemplate
            |> String.replace "{{salutation}}" (student.Gender |> Option.map Gender.getSalutation |> Option.defaultValue "")
            |> String.replace "{{name}}" ([yield! Option.toList student.LastName; yield! Option.toList student.FirstName] |> String.concat " ")
            |> String.replace "{{lastName}}" (student.LastName |> Option.defaultValue "")
            |> String.replace "{{firstName}}" (student.FirstName |> Option.defaultValue "")
            |> String.replace "{{street}}" (student.Address |> Option.map _.Street |> Option.defaultValue "")
            |> String.replace "{{zipCode}}" (student.Address |> Option.map _.Zip |> Option.defaultValue "")
            |> String.replace "{{city}}" (student.Address |> Option.map _.City |> Option.defaultValue "")
            |> String.replace "{{testTableRows}}" testTableRows
            |> String.replace "{{testCountGroup}}" (if tests.Length = 1 then "single-test" else "multiple-tests")
            |> String.replace "{{date}}" (DateTime.Today.ToString("D", CultureInfo.GetCultureInfo("de-AT")))

        let generateStudentLetters (documentTemplate, letterTemplate, testRowTemplate) tests =
            let includeRoom = tests |> List.exists (fun v -> v.PartOral |> Option.bind TestPart.tryGetRoom |> Option.isSome)
            let styles = [
                if includeRoom then ".room-unavailable { display: none; }"
                else ".room-available { display: none; }"
            ]
            let students =
                tests
                |> List.map (fun v -> v.Student)
                |> List.distinct
                |> List.sortBy (fun v -> (v.ClassName, v.LastName, v.FirstName))
            students
            |> List.map (fun student ->
                let studentTests =
                    tests
                    |> List.filter (fun v -> v.Student = student)
                let styles = [
                    yield! styles
                    if studentTests.Length = 1 then ".test-count-multiple { display: none; }"
                    else ".test-count-single { display: none; }"
                    match student.Gender with
                    | Some Male -> ".gender-female, .gender-unknown { display: none; }"
                    | Some Female -> ".gender-male, .gender-unknown { display: none; }"
                    | None -> ".gender-male, .gender-female { display: none; }"
                ]
                let studentLetter = generateStudentLetter letterTemplate testRowTemplate student studentTests
                let document =
                    documentTemplate
                    |> String.replace "{{header}}" $"""<style>%s{styles |> String.concat "\n"}</style>"""
                    |> String.replace "{{content}}" studentLetter
                (student, document)
            )

        let startBrowser = async {
            let browserDownloadPath = Path.Combine(Path.GetTempPath(), "htlvb-individual-tests-browser")
            let browserFetcher = BrowserFetcher(BrowserFetcherOptions(Path = browserDownloadPath, Browser = SupportedBrowser.Chromium))
            let! downloadedBrowser = browserFetcher.DownloadAsync() |> Async.AwaitTask
            return! Puppeteer.LaunchAsync(LaunchOptions(Headless = true, Browser = downloadedBrowser.Browser, ExecutablePath = downloadedBrowser.GetExecutablePath())) |> Async.AwaitTask
        }

        let teacherLetterToPdf teacherShortName (htmlLetter: string) = async {
            use! browser = startBrowser
            let htmlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".html")
            File.WriteAllText(htmlFilePath, htmlLetter)
            use! page = browser.NewPageAsync() |> Async.AwaitTask
            let! _ = page.GoToAsync(Uri(htmlFilePath).AbsoluteUri) |> Async.AwaitTask
            return! page.PdfDataAsync(
                PdfOptions(
                    PrintBackground = true,
                    DisplayHeaderFooter = true,
                    Format = PuppeteerSharp.Media.PaperFormat.A4,
                    Landscape = true,
                    MarginOptions = PuppeteerSharp.Media.MarginOptions(
                        Top = "1cm",
                        Left = "1cm",
                        Right = "1cm",
                        Bottom = "1cm"
                    ),
                    HeaderTemplate = $"""<div style="font-family: 'Segoe UI Light', 'Segoe UI Variable Static Text Light'; width: 297mm; text-align: center; font-size: 12px">
                        <span>%s{Option.defaultValue "-" teacherShortName}</span>
                    </div>""",
                    FooterTemplate = $"""<div style="font-family: 'Segoe UI Light', 'Segoe UI Variable Static Text Light'; width: 297mm; text-align: center; font-size: 12px">
                        Seite <span class="pageNumber"></span>/<span class="totalPages"></span>
                    </div>"""
                )) |> Async.AwaitTask
        }

        let studentLetterToPdf (student: Student) (htmlLetter: string) = async {
            use! browser = startBrowser
            let htmlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".html")
            File.WriteAllText(htmlFilePath, htmlLetter)
            use! page = browser.NewPageAsync() |> Async.AwaitTask
            let! _ = page.GoToAsync(Uri(htmlFilePath).AbsoluteUri) |> Async.AwaitTask
            return! page.PdfDataAsync(
                PdfOptions(
                    PrintBackground = true,
                    DisplayHeaderFooter = true,
                    Format = PuppeteerSharp.Media.PaperFormat.A4,
                    MarginOptions = PuppeteerSharp.Media.MarginOptions(
                        Top = "0cm",
                        Left = "0cm",
                        Right = "0cm",
                        Bottom = "1cm"
                    ),
                    FooterTemplate = $"""<div style="font-family: 'Segoe UI Light', 'Segoe UI Variable Static Text Light'; width: 297mm; font-size: 12px">
                        <div style="margin-right: 1cm; text-align: right;"><span>%s{student.ClassName |> Option.defaultValue ""}</span></div>
                    </div>"""
                )) |> Async.AwaitTask
        }

        let combinePdfs docs =
            use stream = new MemoryStream()
            do
                use pdfWriter = new PdfWriter(stream)
                use targetDoc = new PdfDocument(pdfWriter)
                let merger = new PdfMerger(targetDoc)

                docs
                |> Seq.iter (fun (sourceDoc: byte[]) ->
                    use sourceStream = new MemoryStream(sourceDoc)
                    use reader = new PdfReader(sourceStream)
                    use sourceDoc = new PdfDocument(reader)
                    merger.Merge(sourceDoc, 1, sourceDoc.GetNumberOfPages()) |> ignore
                )
            stream.ToArray()

        let sendMail (graphClient: GraphServiceClient) mailToAddress subject content (pdfLetterName, pdfLetterContent) = async {
            let message = new Models.Message(
                ToRecipients =
                    Collections.Generic.List<_>([
                        Models.Recipient(
                            EmailAddress = Models.EmailAddress(Address = mailToAddress)
                        )
                    ]),
                Subject = subject,
                Body = new Models.ItemBody(
                    ContentType = Models.BodyType.Text,
                    Content = content
                ),
                Attachments = Collections.Generic.List<_>([
                    Models.FileAttachment(
                        Name = pdfLetterName,
                        ContentType = "application/pdf",
                        ContentBytes = pdfLetterContent
                    ) :> Models.Attachment
                ])
            )
            do! graphClient.Me.SendMail.PostAsync(Me.SendMail.SendMailPostRequestBody(Message = message)) |> Async.AwaitTask
        }

[<ApiController>]
[<Route("api/letter")>]
[<Authorize>]
type LetterController (graphClient: GraphServiceClient, config: IConfiguration, logger : ILogger<LetterController>) =
    inherit ControllerBase()

    [<HttpQuery>]
    [<Route("students")>]
    member this.GenerateStudentLetters ([<FromBody>]data: Dto.GenerateLettersDto) = async {
        let documentTemplate = File.ReadAllText config.["StudentLetterDocumentTemplatePath"]
        let contentTemplate = File.ReadAllText config.["StudentLetterContentTemplatePath"] |> String.replace "{{letterText}}" data.LetterText
        let testRowTemplate = File.ReadAllText config.["StudentLetterTestRowTemplatePath"]
        let tests = data.Tests |> List.map Domain.TestData.fromDto
        let! pdfLetters =
            Domain.generateStudentLetters (documentTemplate, contentTemplate, testRowTemplate) tests
            |> List.map (fun (student, htmlLetter) -> Domain.studentLetterToPdf student htmlLetter)
            |> Async.Sequential
        return this.File(Domain.combinePdfs pdfLetters, MediaTypeNames.Application.Pdf)
    }

    [<HttpPost>]
    [<Route("students")>]
    [<Authorize("SendLetters")>]
    member this.SendStudentLetters ([<FromBody>]data: Dto.SendLettersDto) = async {
        let documentTemplate = File.ReadAllText config.["StudentLetterDocumentTemplatePath"]
        let contentTemplate = File.ReadAllText config.["StudentLetterContentTemplatePath"] |> String.replace "{{letterText}}" data.LetterText
        let testRowTemplate = File.ReadAllText config.["StudentLetterTestRowTemplatePath"]
        let tests = data.Tests |> List.map Domain.TestData.fromDto
        let! sendResults =
            Domain.generateStudentLetters (documentTemplate, contentTemplate, testRowTemplate) tests
            |> List.map (fun (student, htmlLetter) -> async {
                match data.OverwriteMailTo |> Option.orElse student.MailAddress with
                | Some mailToAddress ->
                    let! pdfLetter = Domain.studentLetterToPdf student htmlLetter
                    try
                        let letterFileName =
                            match student.LastName, student.FirstName with
                            | Some studentLastName, Some studentFirstName -> $"Einteilung zu Wiederholungsprüfungen %s{studentFirstName} %s{studentLastName}.pdf"
                            | _, _ -> "Einteilung zu Wiederholungsprüfungen.pdf"
                        do! Domain.sendMail graphClient mailToAddress data.MailSubject data.MailText (letterFileName, pdfLetter)
                        return Ok ()
                    with e -> return Error {| Type = "sending-mail-failed"; StudentMailAddress = Some mailToAddress; Student = None |}
                | None -> return Error {| Type = "student-has-no-mail-address"; StudentMailAddress = None; Student = Some {| ClassName = student.ClassName; LastName = student.LastName; FirstName = student.FirstName |} |}
            })
            |> Async.Sequential
        match Array.sequenceResultA sendResults with
        | Ok _ -> return this.Ok() :> IActionResult
        | Error errors -> return this.StatusCode(StatusCodes.Status500InternalServerError, errors)
    }

    [<HttpQuery>]
    [<Route("teachers")>]
    member this.GenerateTeacherLetters ([<FromBody>]data: Dto.GenerateLettersDto) = async {
        let documentTemplate = File.ReadAllText config.["TeacherLetterDocumentTemplatePath"]
        let contentTemplate = File.ReadAllText config.["TeacherLetterContentTemplatePath"] |> String.replace "{{letterText}}" data.LetterText
        let testRowTemplate = File.ReadAllText config.["TeacherLetterTestRowTemplatePath"]
        let tests = data.Tests |> List.map Domain.TestData.fromDto
        let! pdfLetters =
            Domain.generateTeacherLetters (documentTemplate, contentTemplate, testRowTemplate) tests
            |> List.map (fun (teacher, htmlLetter) -> Domain.teacherLetterToPdf teacher.ShortName htmlLetter)
            |> Async.Sequential
        return this.File(Domain.combinePdfs pdfLetters, MediaTypeNames.Application.Pdf)
    }

    [<HttpPost>]
    [<Route("teachers")>]
    [<Authorize("SendLetters")>]
    member this.SendTeacherLetters ([<FromBody>]data: Dto.SendLettersDto) = async {
        let documentTemplate = File.ReadAllText config.["TeacherLetterDocumentTemplatePath"]
        let contentTemplate = File.ReadAllText config.["TeacherLetterContentTemplatePath"] |> String.replace "{{letterText}}" data.LetterText
        let testRowTemplate = File.ReadAllText config.["TeacherLetterTestRowTemplatePath"]
        let tests = data.Tests |> List.map Domain.TestData.fromDto
        let! sendResults =
            Domain.generateTeacherLetters (documentTemplate, contentTemplate, testRowTemplate) tests
            |> List.map (fun (teacher, htmlLetter) -> async {
                match data.OverwriteMailTo |> Option.orElse teacher.MailAddress with
                | Some mailToAddress ->
                    let! pdfLetter = Domain.teacherLetterToPdf teacher.ShortName htmlLetter
                    try
                        let letterFileName =
                            match teacher.ShortName with
                            | Some teacherShortName -> $"Einteilung zu Wiederholungsprüfungen %s{teacherShortName}.pdf"
                            | _ -> "Einteilung zu Wiederholungsprüfungen.pdf"
                        do! Domain.sendMail graphClient mailToAddress data.MailSubject data.MailText (letterFileName, pdfLetter)
                        return Ok ()
                    with e -> return Error {| Type = "sending-mail-failed"; TeacherMailAddress = Some mailToAddress; TeacherShortName = None |}
                | None -> return Error {| Type = "teacher-has-no-mail-address"; TeacherMailAddress = None; TeacherShortName = teacher.ShortName |}
            })
            |> Async.Sequential
        match Array.sequenceResultA sendResults with
        | Ok _ -> return this.Ok() :> IActionResult
        | Error errors -> return this.StatusCode(StatusCodes.Status500InternalServerError, errors)
    }
