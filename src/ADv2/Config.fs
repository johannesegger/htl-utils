namespace AD.Configuration

open Microsoft.Extensions.Configuration

type DistinguishedName = DistinguishedName of string

type LdapConnectionConfig = {
    HostName: string
    UserName: string
    Password: string
}
type NetworkShareConnectionConfig = {
    UserName: string
    Password: string
}
type ConnectionConfig = {
    Ldap: LdapConnectionConfig
    NetworkShare: NetworkShareConnectionConfig
}
type Properties = {
    ComputerContainer: DistinguishedName
    TeacherContainer: DistinguishedName
    ClassContainer: DistinguishedName
    ExTeacherContainer: DistinguishedName
    ExStudentContainer: DistinguishedName
    TeacherGroup: DistinguishedName
    StudentGroup: DistinguishedName
    ClassGroupsContainer: DistinguishedName
    TestUserGroup: DistinguishedName
    SokratesIdAttributeName: string
    MailDomain: string
    TeacherHomePath: string
    TeacherExercisePath: string
    StudentHomePath: string
    HomeDrive: string
}
type Config = {
    ConnectionConfig: ConnectionConfig
    Properties: Properties
}
module Config =
    type LdapConnectionConfig() =
        member val HostName= "" with get, set
        member val UserName= "" with get, set
        member val Password= "" with get, set
        member x.Build() = {
            HostName = x.HostName
            UserName = x.UserName
            Password = x.Password
        }
    type NetworkShareConnectionConfig() =
        member val UserName= "" with get, set
        member val Password= "" with get, set
        member x.Build() = {
            UserName = x.UserName
            Password = x.Password
        }
    type ConnectionConfig() =
        member val Ldap = LdapConnectionConfig() with get, set
        member val NetworkShare = NetworkShareConnectionConfig() with get, set
        member x.Build() = {
            Ldap = x.Ldap.Build()
            NetworkShare = x.NetworkShare.Build()
        }
    type Properties() =
        member val ComputerContainer = "" with get, set
        member val TeacherContainer = "" with get, set
        member val ClassContainer = "" with get, set
        member val ExTeacherContainer = "" with get, set
        member val ExStudentContainer = "" with get, set
        member val TeacherGroup = "" with get, set
        member val StudentGroup = "" with get, set
        member val ClassGroupsContainer = "" with get, set
        member val TestUserGroup = "" with get, set
        member val SokratesIdAttributeName = "" with get, set
        member val MailDomain = "" with get, set
        member val TeacherHomePath = "" with get, set
        member val TeacherExercisePath = "" with get, set
        member val StudentHomePath = "" with get, set
        member val HomeDrive = "" with get, set
        member x.Build() = {
            ComputerContainer = DistinguishedName x.ComputerContainer
            TeacherContainer = DistinguishedName x.TeacherContainer
            ClassContainer = DistinguishedName x.ClassContainer
            ExTeacherContainer = DistinguishedName x.ExTeacherContainer
            ExStudentContainer = DistinguishedName x.ExStudentContainer
            TeacherGroup = DistinguishedName x.TeacherGroup
            StudentGroup = DistinguishedName x.StudentGroup
            ClassGroupsContainer = DistinguishedName x.ClassGroupsContainer
            TestUserGroup = DistinguishedName x.TestUserGroup
            SokratesIdAttributeName = x.SokratesIdAttributeName
            MailDomain = x.MailDomain
            TeacherHomePath = x.TeacherHomePath
            TeacherExercisePath = x.TeacherExercisePath
            StudentHomePath = x.StudentHomePath
            HomeDrive = x.HomeDrive
        }
    type Config() =
        member val ConnectionConfig = ConnectionConfig() with get, set
        member val Properties = Properties() with get, set
        member x.Build() = {
            ConnectionConfig = x.ConnectionConfig.Build()
            Properties = x.Properties.Build()
        }
    let fromEnvironment () =
        let config =
            ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddUserSecrets<Config>()
                .Build()
        ConfigurationBinder.Get<Config>(config.GetSection("AD")).Build()
