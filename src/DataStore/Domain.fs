module DataStore.Domain

open System

[<CLIMutable>]
type ComputerInfo = {
    ComputerName: string
    Timestamp: DateTimeOffset
    Properties: Result<Map<string, Result<list<Map<string, obj>>, string>>, string>
}
