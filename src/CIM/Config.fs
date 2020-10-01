namespace CIM.Configuration

type Config = {
    Domain: string
    UserName: string
    Password: string
}
module Config =
    let fromEnvironment () =
        let (domain, userName) =
            Environment.getEnvVarOrFail "AD_USER"
            |> String.trySplitAt "\\"
            |> Option.defaultWith (fun () -> failwith "AD_USER is missing domain information")
        {
            Domain = domain
            UserName = userName
            Password = Environment.getEnvVarOrFail "AD_PASSWORD"
        }