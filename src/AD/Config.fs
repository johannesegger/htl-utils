namespace AD

type DistinguishedName = DistinguishedName of string

type Config = {
    DomainControllerHostName: string
    UserName: string
    Password: string
    NetworkShareUser: string
    NetworkSharePassword: string
    ComputerContainer: DistinguishedName
    TeacherContainer: DistinguishedName
    ClassContainer: DistinguishedName
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

module Config =
    open Microsoft.Extensions.Configuration

    type ADConfig() =
        member val DomainControllerHostName = "" with get, set
        member val UserName = "" with get, set
        member val Password = "" with get, set
        member val NetworkShareUser = "" with get, set
        member val NetworkSharePassword = "" with get, set
        member val ComputerContainer = "" with get, set
        member val TeacherContainer = "" with get, set
        member val ClassContainer = "" with get, set
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
        member x.Build() : Config = {
            DomainControllerHostName = x.DomainControllerHostName
            UserName = x.UserName
            Password = x.Password
            NetworkShareUser = x.NetworkShareUser
            NetworkSharePassword = x.NetworkSharePassword
            ComputerContainer = DistinguishedName x.ComputerContainer
            TeacherContainer = DistinguishedName x.TeacherContainer
            ClassContainer = DistinguishedName x.ClassContainer
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

    let fromEnvironment () =
        let config = ConfigurationBuilder().AddEnvironmentVariables().Build()
        ConfigurationBinder.Get<ADConfig>(config.GetSection("AD")).Build()
