namespace FileStorage.Configuration

type Config = {
    BaseDirectories: Map<string, string>
}
module Config =
    let fromEnvironment () =
        {
            BaseDirectories =
                Environment.getEnvVarOrFail "FILE_STORAGE_BASE_DIRECTORIES"
                |> String.split ";"
                |> Seq.chunkBySize 2
                |> Seq.map (fun s -> s.[0], s.[1])
                |> Map.ofSeq
        }