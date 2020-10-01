namespace DataStore.Configuration

type Config = {
    ComputerInfoFilePath: string
}
module Config =
    let fromEnvironment () =
        {
            ComputerInfoFilePath = Environment.getEnvVarOrFail "DATA_STORE_COMPUTER_INFO_FILE_PATH"
        }