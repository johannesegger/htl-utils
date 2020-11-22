namespace FileStorage.Configuration

type Config = {
    BaseDirectories: Map<string, string>
}
module Config =
    let fromEnvironment () =
        {
            BaseDirectories =
                Environment.getEnvVar "FILE_STORAGE_BASE_DIRECTORIES"
                |> Option.ofObj
                |> Option.map (
                    String.split ";"
                    >> Seq.chunkBySize 2
                    >> Seq.map (fun s -> s.[0], s.[1])
                    >> Map.ofSeq
                )
                |> Option.defaultValue Map.empty
        }