namespace ADModifications.Mapping

open ADModifications.DataTransferTypes

module UserName =
    let fromADDto (AD.Domain.UserName userName) = UserName userName
    let toADDto (UserName userName) = AD.Domain.UserName userName

module SokratesId =
    let fromADDto (AD.Domain.SokratesId sokratesId) = SokratesId sokratesId
    let fromSokratesDto (Sokrates.Domain.SokratesId sokratesId) = SokratesId sokratesId
    let toADDto (SokratesId sokratesId) = AD.Domain.SokratesId sokratesId

module GroupName =
    let fromADDto (AD.Domain.GroupName groupName) = GroupName groupName
    let toADDto (GroupName groupName) = AD.Domain.GroupName groupName

module UserType =
    let fromADDto = function
        | AD.Domain.Teacher -> Teacher
        | AD.Domain.Student className -> Student (GroupName.fromADDto className)
    let toADDto = function
        | Teacher -> AD.Domain.Teacher
        | Student className -> AD.Domain.Student (GroupName.toADDto className)

module User =
    let fromSokratesTeacherDto (teacher: Sokrates.Domain.Teacher) =
        {
            Name = UserName teacher.ShortName
            SokratesId = Some (SokratesId.fromSokratesDto teacher.Id)
            FirstName = teacher.FirstName
            LastName = teacher.LastName
            Type = Teacher
        }
    let fromSokratesStudentDto (student: Sokrates.Domain.Student) userName =
        {
            Name = userName
            SokratesId = Some (SokratesId.fromSokratesDto student.Id)
            FirstName = student.FirstName1
            LastName = student.LastName
            Type = Student (GroupName student.SchoolClass)
        }
    let fromADDto (user: AD.Domain.User) =
        {
            Name = UserName.fromADDto user.Name
            SokratesId = user.SokratesId |> Option.map SokratesId.fromADDto
            FirstName = user.FirstName
            LastName = user.LastName
            Type = UserType.fromADDto user.Type
        }
    let toADDto (user: User) =
        {
            AD.Domain.Name = UserName.toADDto user.Name
            AD.Domain.SokratesId = user.SokratesId |> Option.map SokratesId.toADDto
            AD.Domain.FirstName = user.FirstName
            AD.Domain.LastName = user.LastName
            AD.Domain.Type = UserType.toADDto user.Type
        }

module UserUpdate =
    let toADDto = function
        | ChangeUserName (userName, firstName, lastName) -> AD.Domain.ChangeUserName (UserName.toADDto userName, firstName, lastName)
        | SetSokratesId sokratesId -> AD.Domain.SetSokratesId (SokratesId.toADDto sokratesId)
        | MoveStudentToClass className -> AD.Domain.MoveStudentToClass (GroupName.toADDto className)

module GroupUpdate =
    let toADDto = function
        | ChangeGroupName newName -> AD.Domain.ChangeGroupName (GroupName.toADDto newName)

module DirectoryModification =
    let toADDto = function
        | CreateUser (user, password) -> AD.Domain.CreateUser (User.toADDto user, password)
        | UpdateUser (user, update) -> AD.Domain.UpdateUser (UserName.toADDto user.Name, UserType.toADDto user.Type, UserUpdate.toADDto update)
        | DeleteUser user -> AD.Domain.DeleteUser (UserName.toADDto user.Name, UserType.toADDto user.Type)
        | CreateGroup userType -> AD.Domain.CreateGroup (UserType.toADDto userType)
        | UpdateGroup (userType, groupUpdate) -> AD.Domain.UpdateGroup (UserType.toADDto userType, GroupUpdate.toADDto groupUpdate)
        | DeleteGroup userType -> AD.Domain.DeleteGroup (UserType.toADDto userType)

module ClassGroupModification =
    let toDirectoryModification = function
        | IncrementClassGroups.DataTransferTypes.ChangeClassGroupName (oldName, newName) ->
            UpdateGroup (Student (GroupName oldName), ChangeGroupName (GroupName newName))
        | IncrementClassGroups.DataTransferTypes.DeleteClassGroup name ->
            DeleteGroup (Student (GroupName name))
