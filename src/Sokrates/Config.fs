namespace Sokrates.Configuration

type Config = {
    UserName: string
    Password: string
    SchoolId: string
    ClientCertificatePath: string
    ClientCertificatePassphrase: string
}
module Config =
    let fromEnvironment () =
        {
            UserName = Environment.getEnvVarOrFail "SOKRATES_USER_NAME"
            Password = Environment.getEnvVarOrFail "SOKRATES_PASSWORD"
            SchoolId = Environment.getEnvVarOrFail "SOKRATES_SCHOOL_ID"
            ClientCertificatePath = Environment.getEnvVarOrFail "SOKRATES_CLIENT_CERTIFICATE_PATH"
            ClientCertificatePassphrase = Environment.getEnvVarOrFail "SOKRATES_CLIENT_CERTIFICATE_PASSPHRASE"
        }
