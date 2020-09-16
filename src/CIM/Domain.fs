module CIM.Domain

open System

type ConnectionError =
    | ConnectionError of exn

type QueryError =
    | SendQueryError of exn

type ComputerInfo = {
    ComputerName: string
    Timestamp: DateTimeOffset
    Properties: Result<Map<string, Result<list<Map<string, obj>>, QueryError>>, ConnectionError>
}
