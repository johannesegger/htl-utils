module AD.Test.Setup

open AD.Configuration

let private server = "WIN-N12KEP11Q2R" // PC
// let private server = "WIN-1L398PVPQN3" // Laptop

let connectionConfig = {
    Ldap = {
        HostName = server
        UserName = "CN=Administrator,CN=Users,DC=htlvb,DC=intern"
        Password = "Admin123"
    }
    NetworkShare = {
        UserName = "htlvb.intern\\Administrator"
        Password = "Admin123"
    }
}

let config = {
    ConnectionConfig = connectionConfig
    Properties = {
        ComputerContainer = DistinguishedName "OU=HTLVB-Computer,DC=htlvb,DC=intern"
        TeacherContainer = DistinguishedName "OU=Lehrer,OU=HTLVB-Benutzer,DC=htlvb,DC=intern"
        ClassContainer = DistinguishedName "OU=Schueler,OU=HTLVB-Benutzer,DC=htlvb,DC=intern"
        ExTeacherContainer = DistinguishedName "OU=Lehrer,OU=Inaktiv,OU=HTLVB-Benutzer,DC=htlvb,DC=intern"
        ExStudentContainer = DistinguishedName "OU=Schueler,OU=Inaktiv,OU=HTLVB-Benutzer,DC=htlvb,DC=intern"
        TeacherGroup = DistinguishedName "CN=Lehrer,OU=User,OU=HTLVB-Gruppen,DC=htlvb,DC=intern"
        StudentGroup = DistinguishedName "CN=Schueler,OU=User,OU=HTLVB-Gruppen,DC=htlvb,DC=intern"
        ClassGroupsContainer = DistinguishedName "OU=Klassen,OU=User,OU=HTLVB-Gruppen,DC=htlvb,DC=intern"
        TestUserGroup = DistinguishedName "CN=g_Schularbeitenuser,OU=User,OU=HTLVB-Gruppen,DC=htlvb,DC=intern"
        SokratesIdAttributeName = "description"
        MailDomain = "htlvb.at"
        TeacherHomePath = $@"\\%s{server}\data\lehrerhome"
        TeacherExercisePath = $@"\\%s{server}\data\angabe_abgabe"
        StudentHomePath = $@"\\%s{server}\data\schuelerhome"
        HomeDrive = "Z:"
    }
}

let networkSharePath = $@"\\%s{server}\data"
