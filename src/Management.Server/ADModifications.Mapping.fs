module ADModifications.Mapping

open ADModifications.DataTransferTypes

module UserName =
    let fromADDto (AD.DataTransferTypes.UserName userName) = UserName userName
    let toADDto (UserName userName) = AD.DataTransferTypes.UserName userName

module SokratesId =
    let fromADDto (AD.DataTransferTypes.SokratesId sokratesId) = SokratesId sokratesId
    let fromSokratesDto (Sokrates.DataTransferTypes.SokratesId sokratesId) = SokratesId sokratesId
    let toADDto (SokratesId sokratesId) = AD.DataTransferTypes.SokratesId sokratesId

module GroupName =
    let fromADDto (AD.DataTransferTypes.GroupName groupName) = GroupName groupName
    let toADDto (GroupName groupName) = AD.DataTransferTypes.GroupName groupName

module UserType =
    let fromADDto = function
        | AD.DataTransferTypes.Teacher -> Teacher
        | AD.DataTransferTypes.Student className -> Student (GroupName.fromADDto className)
    let toADDto = function
        | Teacher -> AD.DataTransferTypes.Teacher
        | Student className -> AD.DataTransferTypes.Student (GroupName.toADDto className)

module User =
    let fromSokratesTeacherDto (teacher: Sokrates.DataTransferTypes.Teacher) =
        {
            Name = UserName teacher.ShortName
            SokratesId = Some (SokratesId.fromSokratesDto teacher.Id)
            FirstName = teacher.FirstName
            LastName = teacher.LastName
            Type = Teacher
        }
    let fromSokratesStudentDto (student: Sokrates.DataTransferTypes.Student) userName =
        {
            Name = userName
            SokratesId = Some (SokratesId.fromSokratesDto student.Id)
            FirstName = student.FirstName1
            LastName = student.LastName
            Type = Student (GroupName student.SchoolClass)
        }
    let fromADDto (user: AD.DataTransferTypes.User) =
        {
            Name = UserName.fromADDto user.Name
            SokratesId = user.SokratesId |> Option.map SokratesId.fromADDto
            FirstName = user.FirstName
            LastName = user.LastName
            Type = UserType.fromADDto user.Type
        }
    let toADDto (user: User) =
        {
            AD.DataTransferTypes.Name = UserName.toADDto user.Name
            AD.DataTransferTypes.SokratesId = user.SokratesId |> Option.map SokratesId.toADDto
            AD.DataTransferTypes.FirstName = user.FirstName
            AD.DataTransferTypes.LastName = user.LastName
            AD.DataTransferTypes.Type = UserType.toADDto user.Type
        }

module UserUpdate =
    let toADDto = function
        | ChangeUserName (userName, firstName, lastName) -> AD.DataTransferTypes.ChangeUserName (UserName.toADDto userName, firstName, lastName)
        | MoveStudentToClass className -> AD.DataTransferTypes.MoveStudentToClass (GroupName.toADDto className)

module GroupUpdate =
    let toADDto = function
        | ChangeGroupName newName -> AD.DataTransferTypes.ChangeGroupName (GroupName.toADDto newName)

module DirectoryModification =
    let toADDto = function
        | CreateUser (user, password) -> AD.DataTransferTypes.CreateUser (User.toADDto user, password)
        | UpdateUser (user, update) -> AD.DataTransferTypes.UpdateUser (UserName.toADDto user.Name, UserType.toADDto user.Type, UserUpdate.toADDto update)
        | DeleteUser user -> AD.DataTransferTypes.DeleteUser (UserName.toADDto user.Name, UserType.toADDto user.Type)
        | CreateGroup (userType, members) -> AD.DataTransferTypes.CreateGroup (UserType.toADDto userType, members |> List.map UserName.toADDto)
        | UpdateGroup (userType, groupUpdate) -> AD.DataTransferTypes.UpdateGroup (UserType.toADDto userType, GroupUpdate.toADDto groupUpdate)
        | DeleteGroup userType -> AD.DataTransferTypes.DeleteGroup (UserType.toADDto userType)
