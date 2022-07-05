namespace ADModifications.Mapping

open ADModifications.DataTransferTypes

module UserName =
    let fromADDto (AD.Domain.UserName userName) = UserName userName
    let toADDto (UserName userName) = AD.Domain.UserName userName

module SokratesId =
    let fromADDto (AD.Domain.SokratesId sokratesId) = SokratesId sokratesId
    let fromSokratesDto (Sokrates.Domain.SokratesId sokratesId) = SokratesId sokratesId
    let toADDto (SokratesId sokratesId) = AD.Domain.SokratesId sokratesId

module ClassName =
    let fromADDto (AD.Domain.GroupName groupName) = ClassName groupName
    let toADDto (ClassName groupName) = AD.Domain.GroupName groupName

module UserType =
    let fromADDto = function
        | AD.Domain.Teacher -> Teacher
        | AD.Domain.Student className -> Student (ClassName.fromADDto className)
    let toADDto = function
        | Teacher -> AD.Domain.Teacher
        | Student className -> AD.Domain.Student (ClassName.toADDto className)

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
            Type = Student (ClassName student.SchoolClass)
        }
    let fromADDto (user: AD.Domain.ExistingUser) =
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

module MailAlias =
    let toADDto v =
        {
            AD.Domain.MailAlias.IsPrimary = v.IsPrimary
            AD.Domain.MailAlias.UserName = v.UserName
        }

module UserUpdate =
    let toADDto = function
        | ChangeUserName (userName, firstName, lastName, mailAliases) -> AD.Domain.ChangeUserName (UserName.toADDto userName, firstName, lastName, mailAliases |> List.map MailAlias.toADDto)
        | SetSokratesId sokratesId -> AD.Domain.SetSokratesId (SokratesId.toADDto sokratesId)
        | MoveStudentToClass className -> AD.Domain.MoveStudentToClass (ClassName.toADDto className)

module GroupUpdate =
    let toADDto = function
        | ChangeStudentClassName newName -> AD.Domain.ChangeGroupName (ClassName.toADDto newName)

module DirectoryModification =
    let toADDto = function
        | CreateUser (user, mailAliases, password) -> AD.Domain.CreateUser (User.toADDto user, mailAliases |> List.map MailAlias.toADDto, password)
        | UpdateUser (user, update) -> AD.Domain.UpdateUser (UserName.toADDto user.Name, UserType.toADDto user.Type, UserUpdate.toADDto update)
        | DeleteUser user -> AD.Domain.DeleteUser (UserName.toADDto user.Name, UserType.toADDto user.Type)
        | CreateGroup userType -> AD.Domain.CreateGroup (UserType.toADDto userType)
        | UpdateStudentClass (className, groupUpdate) -> AD.Domain.UpdateGroup (UserType.toADDto (Student className), GroupUpdate.toADDto groupUpdate)
        | DeleteGroup userType -> AD.Domain.DeleteGroup (UserType.toADDto userType)

module ClassGroupModification =
    let toDirectoryModification = function
        | IncrementClassGroups.DataTransferTypes.ChangeClassGroupName (oldName, newName) ->
            UpdateStudentClass (ClassName oldName, ChangeStudentClassName (ClassName newName))
        | IncrementClassGroups.DataTransferTypes.DeleteClassGroup name ->
            DeleteGroup (Student (ClassName name))
