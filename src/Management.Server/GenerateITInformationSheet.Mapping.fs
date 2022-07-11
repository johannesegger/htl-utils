namespace GenerateITInformationSheet.Mapping

open GenerateITInformationSheet.DataTransferTypes

module User =
    let fromADDto (user: AD.ExistingUser) =
        {
            ShortName = let (AD.UserName userName) = user.Name in userName
            FirstName = user.FirstName
            LastName = user.LastName
        }
