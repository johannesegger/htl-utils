namespace GenerateITInformationSheet.Mapping

open GenerateITInformationSheet.DataTransferTypes

module User =
    let fromADDto (user: AD.Domain.ExistingUser) =
        {
            ShortName = let (AD.Domain.UserName userName) = user.Name in userName
            FirstName = user.FirstName
            LastName = user.LastName
        }
