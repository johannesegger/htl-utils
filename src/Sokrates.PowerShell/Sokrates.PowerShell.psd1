@{
    RootModule        = 'Sokrates.PowerShell.dll'
    ModuleVersion     = '0.1.0'
    GUID              = '6717e63b-e8e8-4851-bbe5-b350056d9772'
    Author            = 'HTLVB'
    Description       = 'PowerShell cmdlets for the Sokrates API.'
    PowerShellVersion = '7.4'

    CmdletsToExport = @(
        'Connect-Sokrates'
        'Disconnect-Sokrates'
        'Get-SokratesSession'
        'Get-SokratesTeacher'
        'Get-SokratesClass'
        'Get-SokratesStudent'
        'Get-SokratesStudentAddress'
        'Get-SokratesStudentContactInfo'
    )
    FunctionsToExport = @()
    VariablesToExport = @()
    AliasesToExport   = @()
}
