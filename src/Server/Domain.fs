[<AutoOpen>]
module Domain

type Class = {
    Level: int
    ParallelClass: string
    Type: string
    Department: string
}

module Class =
    open System.Text.RegularExpressions

    let tryParse text =
        let m = Regex.Match(text, @"^(\d+)(\w)(\w)(\w+)$")
        if m.Success then
            Some {
                Level = int m.Groups.[1].Value
                ParallelClass = m.Groups.[2].Value
                Type = m.Groups.[3].Value
                Department = m.Groups.[4].Value
            }
        else None

    let create level parallelClass ``type`` department =
        { Level = level; ParallelClass = parallelClass; Type = ``type``; Department = department}

    let toString v =
        sprintf "%d%s%s%s" v.Level v.ParallelClass v.Type v.Department