namespace ADModifications.Mapping

open ADModifications.DataTransferTypes

module UserName =
    let fromADDto (AD.UserName userName) = UserName userName
    let toADDto (UserName userName) = AD.UserName userName

module SokratesId =
    let fromADDto (AD.SokratesId sokratesId) = SokratesId sokratesId
    let fromSokratesDto (Sokrates.SokratesId sokratesId) = SokratesId sokratesId
    let toADDto (SokratesId sokratesId) = AD.SokratesId sokratesId

module ClassName =
    let fromADDto (AD.GroupName groupName) = ClassName groupName
    let toADDto (ClassName groupName) = AD.GroupName groupName

module UserType =
    let fromADDto = function
        | AD.Teacher -> Teacher
        | AD.Student className -> Student (ClassName.fromADDto className)
    let toADDto = function
        | Teacher -> AD.Teacher
        | Student className -> AD.Student (ClassName.toADDto className)

module User =
    let fromSokratesTeacherDto (teacher: Sokrates.Teacher) =
        {
            Name = UserName teacher.ShortName
            SokratesId = Some (SokratesId.fromSokratesDto teacher.Id)
            FirstName = teacher.FirstName
            LastName = teacher.LastName
            Type = Teacher
        }
    let fromSokratesStudentDto (student: Sokrates.Student) userName =
        {
            Name = userName
            SokratesId = Some (SokratesId.fromSokratesDto student.Id)
            FirstName = student.FirstName1
            LastName = student.LastName
            Type = Student (ClassName student.SchoolClass)
        }
    let fromADDto (user: AD.ExistingUser) =
        {
            Name = UserName.fromADDto user.Name
            SokratesId = user.SokratesId |> Option.map SokratesId.fromADDto
            FirstName = user.FirstName
            LastName = user.LastName
            Type = UserType.fromADDto user.Type
        }
    let toADDto (user: User) : AD.NewUser =
        {
            Name = UserName.toADDto user.Name
            SokratesId = user.SokratesId |> Option.map SokratesId.toADDto
            FirstName = user.FirstName
            LastName = user.LastName
            Type = UserType.toADDto user.Type
        }

module MailAlias =
    let toADDto v : AD.MailAlias =
        {
            IsPrimary = v.IsPrimary
            UserName = v.UserName
        }

module UserUpdate =
    let toADDto = function
        | ChangeUserName (userName, firstName, lastName, mailAliases) -> AD.ChangeUserName (UserName.toADDto userName, firstName, lastName, mailAliases |> List.map MailAlias.toADDto)
        | SetSokratesId sokratesId -> AD.SetSokratesId (SokratesId.toADDto sokratesId)
        | MoveStudentToClass className -> AD.MoveStudentToClass (ClassName.toADDto className)

module GroupUpdate =
    let toADDto = function
        | ChangeStudentClassName newName -> AD.ChangeGroupName (ClassName.toADDto newName)

module DirectoryModification =
    let toADDto = function
        | CreateUser (user, mailAliases, password) -> AD.CreateUser (User.toADDto user, mailAliases |> List.map MailAlias.toADDto, password)
        | UpdateUser (user, update) -> AD.UpdateUser (UserName.toADDto user.Name, UserType.toADDto user.Type, UserUpdate.toADDto update)
        | DeleteUser user -> AD.DeleteUser (UserName.toADDto user.Name, UserType.toADDto user.Type)
        | CreateGroup userType -> AD.CreateGroup (UserType.toADDto userType)
        | UpdateStudentClass (className, groupUpdate) -> AD.UpdateGroup (UserType.toADDto (Student className), GroupUpdate.toADDto groupUpdate)
        | DeleteGroup userType -> AD.DeleteGroup (UserType.toADDto userType)

module ClassGroupModification =
    let toDirectoryModification = function
        | IncrementClassGroups.DataTransferTypes.ChangeClassGroupName (oldName, newName) ->
            UpdateStudentClass (ClassName oldName, ChangeStudentClassName (ClassName newName))
        | IncrementClassGroups.DataTransferTypes.DeleteClassGroup name ->
            DeleteGroup (Student (ClassName name))
