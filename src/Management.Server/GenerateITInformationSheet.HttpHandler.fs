module GenerateITInformationSheet.HttpHandler

open Fue.Data
open Fue.Compiler
open GenerateITInformationSheet.Configuration
open GenerateITInformationSheet.DataTransferTypes
open GenerateITInformationSheet.Mapping
open Giraffe
open Markdig
open PuppeteerSharp
open PuppeteerSharp.Media
open System
open System.IO
open System.Text.RegularExpressions

let getUsers adConfig : HttpHandler =
    fun next ctx -> task {
        let teachers =
            AD.Core.getUsers |> Reader.run adConfig
            |> List.filter (fun user -> user.Type = AD.Domain.Teacher)
            |> List.map User.fromADDto
        return! Successful.OK teachers next ctx
    }

let private getFileName template (user: AD.Domain.ExistingUser) =
    init
    |> add "shortName" (let (AD.Domain.UserName userName) = user.Name in userName)
    |> add "firstName" user.FirstName
    |> add "lastName" user.LastName
    |> fromText template

let private replacePlaceholders template (user: AD.Domain.ExistingUser) =
    let (mailAddress, mailAliases) =
        let primaryAddress =
            user.ProxyAddresses
            |> List.tryPick (fun v -> if v.Protocol.IsPrimary then Some v.Address else None)
            |> Option.defaultValue user.UserPrincipalName
        let allAddresses =
            [
                yield!
                    user.ProxyAddresses
                    |> List.map (fun v -> v.Address)
                user.UserPrincipalName
            ]
        let aliases =
            allAddresses
            |> List.except [ primaryAddress ]
            |> List.map MailAddress.toString
        MailAddress.toString primaryAddress, aliases

    let template = Regex.Replace(template, "\r?\n?(?=<fs-template |</fs-template>)", "")
    init
    |> add "shortName" (let (AD.Domain.UserName userName) = user.Name in userName)
    |> add "firstName" user.FirstName
    |> add "lastName" user.LastName
    |> add "mailAddress" mailAddress
    |> add "aadUserName" (MailAddress.toString user.UserPrincipalName)
    |> add "firstAlias" (mailAliases |> List.tryHead |> Option.defaultValue "")
    |> add "secondAlias" (mailAliases |> List.tryItem 1 |> Option.defaultValue "")
    |> add "aliases" mailAliases
    |> add "hasAliases" (mailAliases |> List.isEmpty |> not)
    |> add "singleAlias" (mailAliases |> List.length = 1)
    |> add "twoAliases" (mailAliases |> List.length = 2)
    |> add "moreAliases" (mailAliases |> List.length > 2)
    |> add "first" List.head
    |> add "second" (List.item 1)
    |> add "isGreaterThan" (>)
    |> fromText template

let private markdownToHtml text =
    let pipeline = MarkdownPipelineBuilder().UseAdvancedExtensions().Build()
    Markdown.ToHtml(text, pipeline)

let private createFullHtmlDocument documentTemplate content =
    init
    |> add "content" content
    |> fromText documentTemplate

let private initializePuppeteer = lazy(
    let browserFetcher = Puppeteer.CreateBrowserFetcher(BrowserFetcherOptions(Path = Path.Combine(Path.GetTempPath(), "htl-utils", "puppeteer", ".local-chromium")))
    browserFetcher.DownloadAsync(BrowserFetcher.DefaultRevision) |> Async.AwaitTask |> Async.RunSynchronously
)

let private htmlToPdf header footer content =
    let browserRevisionInfo = initializePuppeteer.Force()
    let browser = Puppeteer.LaunchAsync(LaunchOptions(Headless = true, ExecutablePath = browserRevisionInfo.ExecutablePath)) |> Async.AwaitTask |> Async.RunSynchronously
    let page = browser.NewPageAsync() |> Async.AwaitTask |> Async.RunSynchronously
    page.SetContentAsync(content) |> Async.AwaitTask |> Async.RunSynchronously
    let pdfOptions =
        PdfOptions(
            DisplayHeaderFooter = true,
            HeaderTemplate = header,
            FooterTemplate = footer,
            Format = PaperFormat.A4,
            PrintBackground = true
        )
    use pdfStream = page.PdfStreamAsync(pdfOptions) |> Async.AwaitTask |> Async.RunSynchronously
    use stream = new MemoryStream()
    pdfStream.CopyToAsync(stream) |> Async.AwaitTask |> Async.RunSynchronously
    stream.ToArray()

let generateSheet adConfig config : HttpHandler =
    fun next ctx -> task {
        let! user = ctx.BindJsonAsync<User>()
        let adUser = AD.Core.getUser (AD.Domain.UserName user.ShortName) AD.Domain.Teacher |> Reader.run adConfig
        let result = {
            Title = getFileName config.FileNameTemplate adUser |> sprintf "%s.pdf"
            Content =
                let readFile path =
                    try
                        File.ReadAllText path
                    with :? IOException as e -> failwithf "Can't read %s: %s" path e.Message
                let headerTemplate = readFile config.HeaderTemplatePath
                let header = replacePlaceholders headerTemplate adUser
                let footerTemplate = readFile config.FooterTemplatePath
                let footer = replacePlaceholders footerTemplate adUser
                let contentTemplate = readFile config.ContentTemplatePath
                let documentTemplate = readFile config.DocumentTemplatePath
                replacePlaceholders contentTemplate adUser
                |> markdownToHtml
                |> createFullHtmlDocument documentTemplate
                |> htmlToPdf header footer
                |> Convert.ToBase64String
                |> Base64EncodedContent
        }
        return! Successful.OK result next ctx
    }
