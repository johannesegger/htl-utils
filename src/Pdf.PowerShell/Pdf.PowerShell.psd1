@{
    RootModule        = 'Pdf.PowerShell.dll'
    ModuleVersion     = '0.1.0'
    GUID              = '0f3b6a2e-6c4d-4b1a-9a7e-2d5c8f4b1e93'
    Author            = 'HTLVB'
    Description       = 'PowerShell cmdlets for rendering HTML to PDF.'
    PowerShellVersion = '7.4'

    CmdletsToExport = @(
        'ConvertTo-Pdf'
    )
    FunctionsToExport = @()
    VariablesToExport = @()
    AliasesToExport   = @()
}
