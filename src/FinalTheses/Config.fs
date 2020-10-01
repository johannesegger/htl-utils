namespace FinalTheses.Configuration

type Config = {
    MentorsFilePath: string
}
module Config =
    let fromEnvironment () =
        {
            MentorsFilePath = Environment.getEnvVarOrFail "FINAL_THESES_MENTORS_FILE_PATH"
        }