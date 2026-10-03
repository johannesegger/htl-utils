namespace PdfPowerShell

open Microsoft.Extensions.Logging.Abstractions
open Pdf
open System
open System.Management.Automation

/// How a page is laid out. An enum rather than Pdf.PrintOrientation so that
/// -Orientation Landscape binds from a string and tab-completes.
type PdfOrientation =
    | Portrait = 0
    | Landscape = 1

/// ConvertTo-Pdf: renders HTML to a PDF and writes its bytes to the pipeline.
///   ConvertTo-Pdf -Path report.html
///   ConvertTo-Pdf -Html "<h1>Hello</h1>" -Orientation Landscape -Margin '1cm'
/// A browser is launched per call, so build the whole document and convert it once
/// rather than calling this per page.
[<Cmdlet(VerbsData.ConvertTo, "Pdf", DefaultParameterSetName = "Path")>]
[<OutputType(typeof<byte[]>)>]
type ConvertToPdfCommand() =
    inherit PSCmdlet()

    /// An HTML file to render. Relative links in it are not resolved: the document is
    /// rendered from a temporary location, so it has to carry its styles and images
    /// inline or reference them absolutely.
    [<Parameter(Mandatory = true, ParameterSetName = "Path", Position = 0)>]
    member val Path = "" with get, set

    /// HTML to render, for a document the script built itself.
    [<Parameter(Mandatory = true, ParameterSetName = "Html", Position = 0)>]
    member val Html = "" with get, set

    /// Printed above every page, as HTML. Chromium's placeholder classes work in it:
    /// date, title, url, pageNumber and totalPages, as in
    /// '&lt;span class="pageNumber"&gt;&lt;/span&gt;'. Empty by default, which prints no header.
    [<Parameter>]
    member val HeaderTemplate = "<span></span>" with get, set

    /// Printed below every page. See -HeaderTemplate.
    [<Parameter>]
    member val FooterTemplate = "<span></span>" with get, set

    /// The margin on every side, as a CSS length. Overridden per side by -MarginTop and friends.
    [<Parameter>]
    member val Margin = "2cm" with get, set

    [<Parameter>]
    member val MarginTop = "" with get, set

    [<Parameter>]
    member val MarginRight = "" with get, set

    [<Parameter>]
    member val MarginBottom = "" with get, set

    [<Parameter>]
    member val MarginLeft = "" with get, set

    [<Parameter>]
    member val Orientation = PdfOrientation.Portrait with get, set

    override this.EndProcessing() =
        let html =
            match this.ParameterSetName with
            | "Path" -> IO.File.ReadAllText(this.GetUnresolvedProviderPathFromPSPath this.Path)
            | _ -> this.Html

        let side value =
            if String.IsNullOrEmpty value then this.Margin else value

        let printSettings =
            { HeaderTemplate = this.HeaderTemplate
              FooterTemplate = this.FooterTemplate
              Margin =
                { Top = side this.MarginTop
                  Right = side this.MarginRight
                  Bottom = side this.MarginBottom
                  Left = side this.MarginLeft }
              Orientation =
                match this.Orientation with
                | PdfOrientation.Landscape -> Landscape
                | _ -> Portrait }

        // No host to take a logger from, and the factory only logs where it found the
        // browser; anything that goes wrong surfaces as an exception instead.
        let factory = PdfPrinterFactory(NullLogger<PdfPrinterFactory>.Instance)

        let pdf =
            async {
                use! printer = factory.LaunchPrinter()
                return! printer.Print printSettings html
            }
            |> Async.RunSynchronously

        this.WriteObject pdf
