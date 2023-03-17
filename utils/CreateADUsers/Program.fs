open AD

let adConfig = Config.fromEnvironment ()
let adApi = ADApi(adConfig)

let userType = "Physicists" |> GroupName |> Student
let users =
    [
        "Albert", "Einstein", "A.Einstein"
    ]
    |> List.map (fun (firstName, lastName, userName) ->
        {
            Name = UserName userName
            SokratesId = None
            FirstName = firstName
            LastName = lastName
            Type = userType
        }
    )
let mailAliases = []
let password = "A!b2C3"
adApi.ApplyDirectoryModifications [
    yield CreateGroup userType
    for user in users -> CreateUser (user, mailAliases, password)
]
