namespace global

type LoadableClassList =
    | NotLoadedClassList
    | FailedToLoadClassList
    | LoadedClassList of string list list

module Class =
    let level (className: string) = className.Substring(0, 1) |> int

module Classes =
    let groupAndSort (classes: string list) =
        classes
        |> List.groupBy Class.level
        |> List.sortBy fst
        |> List.map (snd >> List.sort)
