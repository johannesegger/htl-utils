namespace ManageGuestAccounts.Server.Controllers

open AD.Core
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open System
open System.IO

module DataTransfer =
    type CreateGuestAccountsRequestBody = {
        Group: string
        Count: int
        WLANOnly: bool
        Notes: string option
    }

    let createdAccount (account: AD.Domain.NewGuestAccount) =
        {|
            UserName = let (AD.Domain.UserName userName) = account.UserName in userName
            Password = account.Password
            Notes = account.Notes
        |}

    let createdAccountWithResult (account, result) =
        match result with
        | Ok () -> {| Account = createdAccount account; Errors = [] |}
        | Error e -> {| Account = createdAccount account; Errors = e |}

    let createdAccountsWithResults (group: string) accounts (pdf: byte[]) =
        {|
            Group = group
            Accounts = accounts |> List.map createdAccountWithResult
            Pdf = pdf
        |}

    let existingAccount (account: AD.Domain.ExistingGuestAccount) =
        {|
            Name = let (AD.Domain.UserName name) = account.Name in name
            CreatedAt = account.CreatedAt
            WLANOnly = account.WLANOnly
            Notes = account.Notes
        |}

    let existingAccountGroup (group: string, accounts) =
        {|
            Group = group
            Accounts = accounts |> List.map existingAccount
        |}

    let existingAccountGroups = List.map existingAccountGroup

    let removedAccount (account: AD.Domain.ExistingGuestAccount, result) =
        {|
            UserName = let (AD.Domain.UserName name) = account.Name in name
            Error =
                match result with
                | Ok () -> None
                | Error (errors: string list) -> Some errors
        |}

    let removedAccounts = List.map removedAccount

module Domain =
    type CreateGuestAccountsData = {
        Group: string
        Count: int
        WLANOnly: bool
        Notes: string option
    }

module Parse =
    open System.Text.RegularExpressions

    let notes v =
        if String.IsNullOrEmpty v then None
        else Some v

    let createGuestAccountsData (v: DataTransfer.CreateGuestAccountsRequestBody) : Result<Domain.CreateGuestAccountsData, _> =
        if not <| Regex.IsMatch(v.Group, "^[a-z0-9]{1,8}$") then Error "InvalidGroupName"
        elif v.Count < 1 then Error "InvalidSize"
        else Ok { Group = v.Group; Count = v.Count; WLANOnly = v.WLANOnly; Notes = v.Notes |> Option.bind notes }

module Html =
    open PuppeteerSharp

    type BrowserFactory(logger: ILogger<BrowserFactory>) =
        member _.LaunchBrowser() = task {
            if Environment.getEnvVar "DOTNET_RUNNING_IN_CONTAINER" = "true" then
                logger.LogInformation("Launching browser in container environment")
                let browserPath =
                    Directory.GetDirectories("/chromium", "linux-*")
                    |> Seq.tryPick(fun v ->
                        let path = Path.Combine(v, "chrome-linux/chrome")
                        if File.Exists path then Some path
                        else None
                    )
                    |> function
                    | Some v ->
                        logger.LogInformation("Browser path: {BrowserPath}", v)
                        v
                    | None -> failwith "Browser not found: /chromium/linux-*/chrome-linux/chrome doesn't exist"

                return! LaunchOptions(
                    Args = [| "--no-sandbox" |], // Required to run it in Docker as root
                    Headless = true,
                    Browser = SupportedBrowser.Chromium,
                    ExecutablePath = browserPath
                )
                |> Puppeteer.LaunchAsync
            else
                logger.LogInformation("Launching browser in normal environment")
                let browserDownloadPath = Path.Combine(Path.GetTempPath(), "htlutils-manage-guest-accounts-browser")
                let browserFetcher = BrowserFetcher(BrowserFetcherOptions(Path = browserDownloadPath, Browser = SupportedBrowser.Chromium))
                let! downloadedBrowser = browserFetcher.DownloadAsync()
                return!
                    LaunchOptions(
                        Headless = true,
                        Browser = downloadedBrowser.Browser,
                        ExecutablePath = downloadedBrowser.GetExecutablePath()
                    )
                    |> Puppeteer.LaunchAsync
        }

    let convertToPdf (browserFactory: BrowserFactory) (headerTemplate, footerTemplate) (html: string) = task {
        let tempFilePath = Path.GetTempFileName() |> fun v -> Path.ChangeExtension(v, ".html")
        File.WriteAllText(tempFilePath, html)
        use __ = { new IDisposable with member _.Dispose() = File.Delete tempFilePath }

        use! browser = browserFactory.LaunchBrowser()
        let! page = browser.NewPageAsync()
        let! response = page.GoToAsync(Uri(tempFilePath).AbsoluteUri)
        return! page.PdfDataAsync(PdfOptions(
            DisplayHeaderFooter = true,
            HeaderTemplate = headerTemplate,
            FooterTemplate = footerTemplate,
            Format = Media.PaperFormat.A4,
            MarginOptions = Media.MarginOptions(
                Bottom = "2cm",
                Left = "2cm",
                Right = "2cm",
                Top = "2cm"
            )
        ))
    }

module NewGuestAccounts =
    open Fue.Compiler
    open Fue.Data
    open System.Globalization

    let private timeProvider =
        { new TimeProvider() with
            member _.LocalTimeZone with get (): TimeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna")
        }

    let private culture = CultureInfo.GetCultureInfo("de-AT")

    let createPdf (browserFactory: Html.BrowserFactory) htmlTemplate (group: string) (accounts: AD.Domain.NewGuestAccount list) = async {
        let logoBase64 = File.ReadAllBytes("logo.svg") |> Convert.ToBase64String
        let headerTemplate =
            $"""<div style="width: 297mm; margin: 0 1cm; font-size: 12px; font-variant-caps: small-caps; display: flex; align-items: center; justify-content: space-between">
                <span style="flex: 1 1 0; text-align: left;">{group.ToUpper()} Gäste-Accounts</span>
                <span style="flex: 1 1 0; text-align: center;"></span>
                <span style="flex: 1 1 0; text-align: right;"><img src="data:image/svg+xml;base64,{logoBase64}" style="height: 40px" /></span>
            </div>"""
        let footerTemplate =
            $"""<div style="width: 297mm; margin: 0.5cm 1cm; font-size: 12px; font-variant-caps: small-caps; display: flex; align-items: center; justify-content: space-between">
                <span style="flex: 1 1 0; text-align: left;">{timeProvider.GetLocalNow().ToString("f", culture)}</span>
                <span style="flex: 1 1 0; text-align: center;"></span>
                <span style="flex: 1 1 0; text-align: right;">Seite <span class="pageNumber"></span>/<span class="totalPages"></span></span>
            </div>"""
        let html =
            init
            |> add "group" (group.ToUpper())
            |> add "accounts" [
                for account in accounts do
                    let (AD.Domain.UserName userName) = account.UserName
                    {| userName = userName; password = account.Password; notes = account.Notes |}
            ]
            |> fromText htmlTemplate
        return! Html.convertToPdf browserFactory (headerTemplate, footerTemplate) html |> Async.AwaitTask
    }

[<ApiController>]
[<Route("api/guest-accounts")>]
[<Authorize("ManageGuestAccounts")>]
type GuestAccountController (ad: ADApi, browserFactory: Html.BrowserFactory, config: IConfiguration, logger : ILogger<GuestAccountController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member _.GetGuestAccounts() = async {
        let! guestAccounts = ad.GetGuestAccounts()
        return
            guestAccounts
            |> List.sortByDescending (fun (group, accounts) ->
                accounts
                |> List.map _.CreatedAt
                |> List.max
            )
            |> DataTransfer.existingAccountGroups
    }

    [<HttpPost>]
    member this.CreateGuestAccounts([<FromBody>]data: DataTransfer.CreateGuestAccountsRequestBody) = async {
        match Parse.createGuestAccountsData data with
        | Ok data ->
            // let! accounts = ad.CreateGuestAccounts(data.Group, data.Count, data.WLANOnly, data.Notes)
            let accounts = [
                {
                    AD.Domain.NewGuestAccount.UserName = AD.Domain.UserName "htlgast.eggj-01"
                    AD.Domain.NewGuestAccount.Password = "12345"
                    AD.Domain.NewGuestAccount.Notes = Some "bla blub"
                }, Ok()
            ]
            let htmlTemplate =
                config.GetValue<string>("NewGuestAccountsHtmlTemplateFilePath")
                |> File.ReadAllText
            let! pdf = NewGuestAccounts.createPdf browserFactory htmlTemplate data.Group (accounts |> List.map fst)
            return this.Ok(DataTransfer.createdAccountsWithResults data.Group accounts pdf) :> IActionResult
        | Error e ->
            return this.BadRequest(e)
    }

    [<HttpDelete(template = "{group}")>]
    member _.RemoveGuestAccounts(group: string) = async {
        let! result = ad.RemoveGuestAccounts group
        return DataTransfer.removedAccounts result
    }

[<ApiController>]
[<Route("api/test-pdf-generation")>]
type TestPdfGenerationController (browserFactory: Html.BrowserFactory, logger : ILogger<TestPdfGenerationController>) =
    inherit ControllerBase()

    [<HttpGet>]
    member _.GetSamplePdf() = async {
        let! pdfContent = Html.convertToPdf browserFactory ("", "") "<h1>Yay. That works.<h1>" |> Async.AwaitTask
        return FileContentResult(pdfContent, Net.Mime.MediaTypeNames.Application.Pdf)
    }
