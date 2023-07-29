module AD.Test.Setup

open AD.Configuration

let connectionConfig = {
    Ldap = {
        HostName = "WIN-1L398PVPQN3"
        UserName = "CN=Administrator,CN=Users,DC=htlvb,DC=intern"
        Password = "Admin123"
        AuthType = Basic
    }
    NetworkShare = {
        UserName = "htlvb.intern\\Administrator"
        Password = "Admin123"
    }
}

let networkShare = @"\\WIN-1L398PVPQN3\data"
