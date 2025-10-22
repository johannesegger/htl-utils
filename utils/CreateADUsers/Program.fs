open AD.Configuration
open AD.Core
open AD.Domain

let adConfig = Config.fromEnvironment ()
let adApi = new ADApi(adConfig)

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
            MailAliases = []
            Password = "A!b2C3"
        }
    )
adApi.ApplyDirectoryModifications [
    yield CreateGroup userType
    for user in users -> CreateUser user
]
|> Async.RunSynchronously
|> function
| Ok () -> printfn "Successfully applied modifications."
| Error list ->
    list |> List.iter (printfn "%s")