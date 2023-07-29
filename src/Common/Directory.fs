module Directory

open System
open System.Diagnostics
open System.IO

let delete path =
    let empty = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString()))

    let robocopy =
        let psi = ProcessStartInfo("robocopy", $"\"%s{empty.FullName}\" \"%s{path}\" /purge", UseShellExecute = true)
        Process.Start(psi)
    robocopy.WaitForExit()
    if robocopy.ExitCode > 8 then failwith $"Robocopy exited with code %d{robocopy.ExitCode}"

    empty.Delete()
    Directory.Delete(path)
