module WakeUp

open System.Net
open System.Net.NetworkInformation
open System.Net.Sockets

type WakeUpError =
    | HostResolutionError of string
    | GetIpAddressFromHostNameError of string * IPAddress list
    | WakeOnLanError of IPAddress * PhysicalAddress

let sendWakeUpCommand (hostName: string) (macAddress: PhysicalAddress) = Result.result {
    let! hostEntry =
        try
            Dns.GetHostEntry hostName
            |> Ok
        with _exn ->
            HostResolutionError hostName
            |> Error

    let! ipAddress =
        hostEntry.AddressList
        |> Seq.tryFind (fun p -> p.AddressFamily = AddressFamily.InterNetwork)
        |> Option.orElse (Seq.tryHead hostEntry.AddressList)
        |> Result.ofOption (GetIpAddressFromHostNameError (hostName, List.ofArray hostEntry.AddressList))

    return!
        try
            ipAddress.SendWol macAddress
            Ok ()
        with _exn ->
            WakeOnLanError (ipAddress, macAddress)
            |> Error
}