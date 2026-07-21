namespace Managementv2.Server

open System
open System.IO

[<RequireQualifiedAccess>]
module PowerShellModulePath =
    /// Makes modules below the given directory discoverable by every runspace created
    /// afterwards. PowerShell prepends its own defaults ($PSHOME/Modules and the
    /// user/shared module paths) to whatever PSModulePath holds, so the modules bundled
    /// with the PowerShell SDK stay available. Modules are placed into the directory out
    /// of band (as <Name>/<Version>/<Name>.psd1); the app never installs them itself.
    let register (modulesDirectory: string) =
        let directory = Path.GetFullPath modulesDirectory
        let existing = Environment.GetEnvironmentVariable "PSModulePath"

        let value =
            if String.IsNullOrEmpty existing then
                directory
            else
                $"%s{directory}%c{Path.PathSeparator}%s{existing}"

        Environment.SetEnvironmentVariable("PSModulePath", value)
