namespace PhotoLibrary.Configuration

open Microsoft.Extensions.Configuration

type Config = {
    TeacherPhotosDirectory: string
    StudentPhotosDirectory: string
}
module Config =
    type RawConfig() =
        member val TeacherPhotosDirectory = "" with get, set
        member val StudentPhotosDirectory = "" with get, set
        member x.Build() = {
            TeacherPhotosDirectory = x.TeacherPhotosDirectory
            StudentPhotosDirectory = x.StudentPhotosDirectory
        }
    let fromEnvironment () =
        let config =
            ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddUserSecrets<RawConfig>()
                .Build()
        ConfigurationBinder.Get<RawConfig>(config.GetSection("PhotoLibrary")).Build()
