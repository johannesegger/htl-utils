module FileStorage

open Thoth.Json.Net

type CreateDirectoriesData =
    {
        Path: string
        Names: string list
    }
module CreateDirectoriesData =
    let encode v =
        Encode.object [
            "path", Encode.string v.Path
            "names", (List.map Encode.string >> Encode.list) v.Names
        ]
