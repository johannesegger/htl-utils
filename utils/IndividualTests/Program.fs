module Program

open PuppeteerSharp
open System
open System.Globalization
open System.IO
open iText.Kernel.Pdf
open iText.IO.Source
open iText.Kernel.Utils

let getFullTests tests students =
    let studentLookup =
        students
        |> List.map (fun (s: Sokrates.Student, d) ->
            ((s.SchoolClass.ToLower(), s.LastName.ToLower(), s.FirstName1.ToLower()), (s, d))
        )
        |> Map.ofList
    tests
    |> List.choose (fun (t: TestData.Test) ->
        let studentId = (t.Student.Class.ToLower(), t.Student.LastName.ToLower(), t.Student.FirstName.ToLower())
        match Map.tryFind studentId studentLookup with
        | Some (student, Ok (studentData: StudentInfo.StudentData)) ->
            let result: Letter.FullTest = {
                Test = t
                Student = {
                    Data = student
                    Address = studentData.Address
                    MailAddress = studentData.MailAddress
                }
            }
            Some result
        | Some (_, Error e) ->
            printWarning $"Student data lookup error: %s{TestData.Student.toString t.Student}: %s{e}"
            None
        | None ->
            printWarning $"Student data not found: %s{TestData.Student.toString t.Student}"
            None
    )

let teacherLetterToPdf (browser: IBrowser) teacherShortName htmlLetter = task {
    let htmlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".html")
    File.WriteAllText(htmlFilePath, htmlLetter)
    use! page = browser.NewPageAsync()
    let! _ = page.GoToAsync(Uri(htmlFilePath).AbsoluteUri)
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
            HeaderTemplate = $"""<div style="font-family: &quot;Segoe UI Light&quot;; width: 297mm; text-align: center; font-size: 12px">
                <span>%s{teacherShortName}</span>
            </div>""",
            FooterTemplate = $"""<div style="font-family: &quot;Segoe UI Light&quot;; width: 297mm; text-align: center; font-size: 12px">
                Seite <span class="pageNumber"></span>/<span class="totalPages"></span>
            </div>"""
        ));
}

let studentLetterToPdf (browser: IBrowser) (student: Letter.Student) htmlLetter = task {
    let htmlFilePath = Path.ChangeExtension(Path.GetTempFileName(), ".html")
    File.WriteAllText(htmlFilePath, htmlLetter)
    use! page = browser.NewPageAsync()
    let! _ = page.GoToAsync(Uri(htmlFilePath).AbsoluteUri)
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
            FooterTemplate = $"""<div style="font-family: &quot;Segoe UI Light&quot;; width: 297mm; font-size: 12px">
                <div style="margin-right: 1cm; text-align: right;"><span>%s{student.Data.SchoolClass}</span></div>
            </div>"""
        ));
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

let generateLetters fullTests includeRoom = task {
    let browserFetcher = BrowserFetcher()
    let! _ = browserFetcher.DownloadAsync()
    let! browser = Puppeteer.LaunchAsync(LaunchOptions(Headless = true))
    
    let combinedLetterTargetDir = "./out"

    let targetDir = "./out/teachers"
    Directory.CreateDirectory(targetDir) |> ignore
    let! teacherLetters =
        Letter.generateTeacherLetters fullTests includeRoom
        |> List.map (fun (teacherShortName, htmlLetter) -> async {
            let! pdfLetter = teacherLetterToPdf browser teacherShortName htmlLetter |> Async.AwaitTask
            return (teacherShortName, pdfLetter)
        })
        |> Async.Sequential
    teacherLetters |> Seq.iter (fun (teacherShortName, pdfLetter) ->
        File.WriteAllBytes(System.IO.Path.Combine(targetDir, $"%s{teacherShortName}.pdf"), pdfLetter)
    )
    File.WriteAllBytes(System.IO.Path.Combine(combinedLetterTargetDir, $"Lehrer.pdf"), teacherLetters |> Seq.map snd |> combinePdfs)

    let targetDir = "./out/students"
    Directory.CreateDirectory(targetDir) |> ignore
    let! studentLetters =
        Letter.generateStudentLetters fullTests includeRoom
        |> List.map (fun (student, htmlLetter) -> async {
            let! pdfLetter = studentLetterToPdf browser student htmlLetter |> Async.AwaitTask
            return (student, pdfLetter)
        })
        |> Async.Sequential
    studentLetters |> Seq.iter (fun (student, pdfLetter) ->
        let fileName = $"%s{student.Data.SchoolClass} %s{student.Data.LastName} %s{student.Data.FirstName1} - %s{let (Sokrates.SokratesId v) = student.Data.Id in v}.pdf"
        File.WriteAllBytes(System.IO.Path.Combine(targetDir, fileName), pdfLetter)
    )
    File.WriteAllBytes(System.IO.Path.Combine(combinedLetterTargetDir, $"Schüler.pdf"), studentLetters |> Seq.map snd |> combinePdfs)
}

let run tenantId clientId studentsGroupId sokratesReferenceDates testFilePath includeRoom = task {
    let students = StudentInfo.getLookup tenantId clientId studentsGroupId sokratesReferenceDates
    let tests = TestData.load testFilePath includeRoom
    match TestData.getProblems tests with
    | [] -> printfn "No problems found"
    | v ->
        printWarning $"%d{v.Length} problems found:"
        v |> List.iter (fun v -> printWarning $"* %s{TestData.Problem.toString v}")
    let fullTests = getFullTests tests students
    do! generateLetters fullTests includeRoom
}

[<EntryPoint>]
let main argv =
    match argv with
    | [| tenantId; clientId; studentsGroupId; sokratesReferenceDates; testFilePath; includeRoom |] ->
        let referenceDates = sokratesReferenceDates.Split(',') |> Seq.map (fun v -> DateTime.Parse(v, CultureInfo.InvariantCulture)) |> Seq.toList
        run tenantId clientId studentsGroupId referenceDates testFilePath (includeRoom = "--include-room")
        |> Async.AwaitTask
        |> Async.RunSynchronously
        0
    | _ ->
        printfn $"Usage: dotnet run -- <tenantId> <clientId>"
        -1
