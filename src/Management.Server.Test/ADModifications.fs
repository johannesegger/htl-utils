module Management.Server.Test.ADModifications

open ADModifications.HttpHandler
open Expecto

module private Sokrates =
    open Sokrates.Domain

    let asStudent className teacher =
        {
            Id = teacher.Id
            LastName = teacher.LastName
            FirstName1 = teacher.FirstName
            FirstName2 = None
            DateOfBirth = teacher.DateOfBirth
            SchoolClass = className
        }

    let withId sokratesId (user: Teacher) =
        { user with Id = SokratesId sokratesId }

    let withName shortName firstName lastName user =
        { user with FirstName = firstName; LastName = lastName; ShortName = shortName }

    let einstein =
        {
            Id = SokratesId "0001"
            Title = None
            LastName = "Einstein"
            FirstName = "Albert"
            ShortName = "EINA"
            DateOfBirth = System.DateTime(1879, 3, 14)
            DegreeFront = None
            DegreeBack = None
            Phones = []
            Address = None
        }

    let bohr =
        {
            Id = SokratesId "0002"
            Title = None
            LastName = "Bohr"
            FirstName = "Niels"
            ShortName = "BOHN"
            DateOfBirth = System.DateTime(1885, 10, 7)
            DegreeFront = None
            DegreeBack = None
            Phones = []
            Address = None
        }

module private AD =
    open ADModifications.DataTransferTypes
    open ADModifications.Mapping

    let toExistingADUser (user: AD.Domain.NewUser) =
        let mail =
            let (AD.Domain.UserName userName) = user.Name
            sprintf "%s@x.y" userName
        {
            AD.Domain.ExistingUser.Name = user.Name
            AD.Domain.ExistingUser.SokratesId = user.SokratesId
            AD.Domain.ExistingUser.FirstName = user.FirstName
            AD.Domain.ExistingUser.LastName = user.LastName
            AD.Domain.ExistingUser.Type = user.Type
            AD.Domain.ExistingUser.CreatedAt = System.DateTime.Today
            AD.Domain.ExistingUser.Mail = Some mail
            AD.Domain.ExistingUser.ProxyAddresses = []
            AD.Domain.ExistingUser.UserPrincipalName = mail
        }

    let toExistingDomainUser = User.toADDto >> toExistingADUser

    let asStudent className user =
        { user with Name = userNameFromName user.FirstName user.LastName; Type = Student (GroupName className) }

    let withId sokratesId user =
        { user with SokratesId = Some (SokratesId sokratesId) }

    let withUserName userName user =
        { user with Name = UserName userName }

    let einstein =
        {
            Name = UserName "EINA"
            SokratesId = Some (SokratesId "0001")
            FirstName = "Albert"
            LastName = "Einstein"
            Type = Teacher
        }

    let bohr =
        {
            Name = UserName "BOHN"
            SokratesId = Some (SokratesId "0002")
            FirstName = "Niels"
            LastName = "Bohr"
            Type = Teacher
        }

    let createTeacherGroup = CreateGroup Teacher

    let createStudentGroup className = CreateGroup (Student (GroupName className))

    let deleteStudentGroup className = DeleteGroup (Student (GroupName className))

    let createUser user password = CreateUser (user, password)

    let changeUserName user shortName firstName lastName = UpdateUser (user, ChangeUserName (UserName shortName, firstName, lastName))

    let moveStudentToClass user className = UpdateUser (user, MoveStudentToClass (GroupName className))


let tests =
    testList "ADModifications" [
        testCase "Create first teacher" <| fun _ ->
            let actual = modifications [ Sokrates.einstein ] [] []
            let expected = [ AD.createTeacherGroup; AD.createUser AD.einstein "14.03.1879" ]
            Expect.equal actual expected "New teacher and group should be created"

        testCase "Create another teacher" <| fun _ ->
            let actual = modifications [ Sokrates.einstein; Sokrates.bohr ] [] [ AD.toExistingDomainUser AD.einstein ]
            let expected = [ AD.createUser AD.bohr "07.10.1885" ]
            Expect.equal actual expected "New teacher should be created"

        testCase "Create first student" <| fun _ ->
            let actual = modifications [] [ Sokrates.asStudent "1A" Sokrates.einstein ] []
            let expected = [ AD.createStudentGroup "1A"; AD.createUser (AD.asStudent "1A" AD.einstein) "14.03.1879" ]
            Expect.equal actual expected "New student and group should be created"

        testCase "Create another student from same class" <| fun _ ->
            let actual = modifications [] [ Sokrates.asStudent "1A" Sokrates.einstein; Sokrates.asStudent "1A" Sokrates.bohr ] [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser ]
            let expected = [ AD.createUser (AD.asStudent "1A" AD.bohr) "07.10.1885" ]
            Expect.equal actual expected "New student should be created"

        testCase "Create another student from different class" <| fun _ ->
            let actual = modifications [] [ Sokrates.asStudent "1A" Sokrates.einstein; Sokrates.asStudent "1B" Sokrates.bohr ] [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser ]
            let expected = [ AD.createStudentGroup "1B"; AD.createUser (AD.asStudent "1B" AD.bohr) "07.10.1885" ]
            Expect.equal actual expected "New student and group should be created"

        testCase "Create unique user name if user name already exists" <| fun _ ->
            let actual = modifications [] [ Sokrates.asStudent "1A" Sokrates.einstein; Sokrates.asStudent "1B" (Sokrates.einstein |> Sokrates.withId "9999") ] [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser ]
            let expected = [
                AD.createStudentGroup "1B"
                AD.createUser (AD.einstein |> AD.asStudent "1B" |> AD.withId "9999" |> AD.withUserName "Albert.Einstein2") "14.03.1879"
            ]
            Expect.equal actual expected "Student with unique name should be created"

        testCase "Create unique user name if two users with same name are added" <| fun _ ->
            let actual = modifications [] [ Sokrates.asStudent "1A" Sokrates.einstein; Sokrates.asStudent "1B" (Sokrates.einstein |> Sokrates.withId "9999") ] []
            let expected = [
                AD.createStudentGroup "1A"
                AD.createStudentGroup "1B"
                AD.createUser (AD.einstein |> AD.asStudent "1A") "14.03.1879"
                AD.createUser (AD.einstein |> AD.asStudent "1B" |> AD.withId "9999" |> AD.withUserName "Albert.Einstein2") "14.03.1879"
            ]
            Expect.equal actual expected "Students with unique names should be created"

        testCase "Change user name" <| fun _ ->
            let actual = modifications [ Sokrates.einstein |> Sokrates.withName "ZWEA" "Albert" "Zweistein" ] [] [ AD.toExistingDomainUser AD.einstein ]
            let expected = [ AD.changeUserName AD.einstein "ZWEA" "Albert" "Zweistein" ]
            Expect.equal actual expected "Teacher name should be changed"

        testCase "Move student to another class" <| fun _ ->
            let actual = modifications [] [ Sokrates.einstein |> Sokrates.asStudent "1B" ] [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser ]
            let expected = [
                AD.createStudentGroup "1B"
                AD.moveStudentToClass (AD.einstein |> AD.asStudent "1A") "1B"
                AD.deleteStudentGroup "1A"
            ]
            Expect.equal actual expected "Student should be moved to class 1B"
    ]
