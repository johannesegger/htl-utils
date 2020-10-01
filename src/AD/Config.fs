namespace AD.Configuration

type DistinguishedName = DistinguishedName of string

type Config = {
    DomainControllerHostName: string
    UserName: string
    Password: string
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
    let fromEnvironment () =
        {
            DomainControllerHostName = Environment.getEnvVarOrFail "AD_SERVER"
            UserName = Environment.getEnvVarOrFail "AD_USER"
            Password = Environment.getEnvVarOrFail "AD_PASSWORD"
            ComputerContainer = Environment.getEnvVarOrFail "AD_COMPUTER_CONTAINER" |> DistinguishedName
            TeacherContainer = Environment.getEnvVarOrFail "AD_TEACHER_CONTAINER" |> DistinguishedName
            ClassContainer = Environment.getEnvVarOrFail "AD_CLASS_CONTAINER" |> DistinguishedName
            TeacherGroup = Environment.getEnvVarOrFail "AD_TEACHER_GROUP" |> DistinguishedName
            StudentGroup = Environment.getEnvVarOrFail "AD_STUDENT_GROUP" |> DistinguishedName
            ClassGroupsContainer = Environment.getEnvVarOrFail "AD_CLASS_GROUPS_CONTAINER" |> DistinguishedName
            TestUserGroup = Environment.getEnvVarOrFail "AD_TEST_USER_GROUP" |> DistinguishedName
            SokratesIdAttributeName = Environment.getEnvVarOrFail "AD_SOKRATES_ID_ATTRIBUTE_NAME"
            MailDomain = Environment.getEnvVarOrFail "AD_MAIL_DOMAIN"
            TeacherHomePath = Environment.getEnvVarOrFail "AD_TEACHER_HOME_PATH"
            TeacherExercisePath = Environment.getEnvVarOrFail "AD_TEACHER_EXERCISE_PATH"
            StudentHomePath = Environment.getEnvVarOrFail "AD_STUDENT_HOME_PATH"
            HomeDrive = Environment.getEnvVarOrFail "AD_HOME_DRIVE"
        }
