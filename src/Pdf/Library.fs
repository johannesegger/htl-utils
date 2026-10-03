module Pdf

open Microsoft.Extensions.Logging
open PuppeteerSharp
open System
open System.IO

type PrintMargin = {
    Top: string
    Right: string
    Bottom: string
    Left: string
}
module PrintMargin =
    let all value = {
        Top = value
        Right = value
        Bottom = value
        Left = value
    }

type PrintOrientation = Portrait | Landscape

type PrintSettings = {
    HeaderTemplate: string
    FooterTemplate: string
    Margin: PrintMargin
    Orientation: PrintOrientation
}

type PdfPrinter(browser: IBrowser) =
    member _.Print printSettings (html: string) = task {
        let tempFilePath = Path.GetTempFileName() |> fun v -> Path.ChangeExtension(v, ".html")
        File.WriteAllText(tempFilePath, html)
        use __ = { new IDisposable with member _.Dispose() = File.Delete tempFilePath }

        let! page = browser.NewPageAsync()
        let! _ = page.GoToAsync(Uri(tempFilePath).AbsoluteUri)
        return! page.PdfDataAsync(PdfOptions(
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = printSettings.HeaderTemplate,
            FooterTemplate = printSettings.FooterTemplate,
            Format = Media.PaperFormat.A4,
            Landscape = printSettings.Orientation.IsLandscape,
            MarginOptions = Media.MarginOptions(
                Bottom = printSettings.Margin.Bottom,
                Left = printSettings.Margin.Left,
                Right = printSettings.Margin.Right,
                Top = printSettings.Margin.Top
            )
        ))
    }
    interface IDisposable with
        member _.Dispose () = browser.Dispose()

type PdfPrinterFactory(logger: ILogger<PdfPrinterFactory>) =
    member _.LaunchPrinter() = async {
        if Environment.GetEnvironmentVariable "DOTNET_RUNNING_IN_CONTAINER" = "true" then
            logger.LogInformation "Launching browser in container environment"
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

            let! browser =
                LaunchOptions(
                    Args = [| "--no-sandbox" |], // Required to run it in Docker as root
                    Headless = true,
                    Browser = SupportedBrowser.Chromium,
                    ExecutablePath = browserPath
                )
                |> Puppeteer.LaunchAsync
                |> Async.AwaitTask
            return new PdfPrinter(browser)
        else
            logger.LogInformation "Launching browser in normal environment"
            let browserDownloadPath = Path.Combine(Path.GetTempPath(), "htlutils-browser")
            let browserFetcher = BrowserFetcher(BrowserFetcherOptions(Path = browserDownloadPath, Browser = SupportedBrowser.Chromium))
            let! downloadedBrowser = browserFetcher.DownloadAsync() |> Async.AwaitTask
            let! browser =
                LaunchOptions(
                    Headless = true,
                    Browser = downloadedBrowser.Browser,
                    ExecutablePath = downloadedBrowser.GetExecutablePath()
                )
                |> Puppeteer.LaunchAsync
                |> Async.AwaitTask
            return new PdfPrinter(browser)
    }
