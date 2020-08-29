module WakeUpComputer.Core

open EasyWakeOnLan
open System.Net.NetworkInformation

let private tryParsePhysicalAddress value =
    try
        String.toUpper value
        |> String.replace ":" "-"
        |> PhysicalAddress.Parse
        |> Ok
    with e -> Error e.Message

type WakeUpError =
    | InvalidMacAddress of message: string

let wakeUp macAddress = async {
    match tryParsePhysicalAddress macAddress with
    | Ok macAddress ->
        let wolClient = new EasyWakeOnLanClient()
        do! wolClient.WakeAsync(macAddress.ToString()) |> Async.AwaitTask
        return Ok ()
    | Error message -> return Error (InvalidMacAddress message)
}
