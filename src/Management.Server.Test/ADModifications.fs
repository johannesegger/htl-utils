module Management.Server.Test.ADModifications

open ADModifications.HttpHandler
open Expecto

module private Sokrates =
    open Sokrates

    let asStudent className teacher =
        {
            Id = teacher.Id
            LastName = teacher.LastName
            FirstName1 = teacher.FirstName
            FirstName2 = None
            DateOfBirth = teacher.DateOfBirth
            SchoolClass = className
            Gender = Male
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

    let mailDomain = "htl.at"

    let toExistingADUser mailAliases (user: User) =
        let mail =
            let (UserName userName) = user.Name
            { UserName = userName; Domain = mailDomain }
        let user : AD.Domain.ExistingUser = {
            Name = UserName.toADDto user.Name
            SokratesId = user.SokratesId |> Option.map SokratesId.toADDto
            FirstName = user.FirstName
            LastName = user.LastName
            Type = UserType.toADDto user.Type
            CreatedAt = System.DateTime.Today
            Mail = Some mail
            ProxyAddresses = mailAliases |> List.map (AD.Domain.MailAlias.toProxyAddress mailDomain)
            UserPrincipalName = mail
        }
        user

    let toExistingDomainUser mailAliases user =
        toExistingADUser (mailAliases |> List.map MailAlias.toADDto) user

    let asStudent className (user: User) =
        { user with Name = userNameFromName user.FirstName user.LastName; Type = Student (ClassName className) }

    let withId sokratesId (user: User) =
        { user with SokratesId = Some (SokratesId sokratesId) }

    let withUserName userName (user: User) =
        { user with Name = UserName userName }

    let withName firstName lastName (user: User) =
        { user with FirstName = firstName; LastName = lastName }

    let einstein =
        {
            Name = UserName "EINA"
            SokratesId = Some (SokratesId "0001")
            FirstName = "Albert"
            LastName = "Einstein"
            Type = Teacher
        }
    let einsteinMailAliasNames = rawMailAliases einstein

    let bohr =
        {
            Name = UserName "BOHN"
            SokratesId = Some (SokratesId "0002")
            FirstName = "Niels"
            LastName = "Bohr"
            Type = Teacher
        }
    let bohrMailAliasNames = rawMailAliases bohr

    let primaryMailAlias userName = {
        IsPrimary = true
        UserName = userName
    }

    let nonPrimaryMailAlias userName = {
        IsPrimary = false
        UserName = userName
    }

    let createTeacherGroup = CreateGroup Teacher

    let createStudentGroup className = CreateGroup (Student (ClassName className))

    let deleteStudentGroup className = DeleteGroup (Student (ClassName className))

    let createUser user mailAliasNames password = CreateUser (NewUser.fromUser user mailAliasNames password)

    let changeUserName user shortName firstName lastName mailAliasNames = UpdateUser (user, ChangeUserName (UserName shortName, firstName, lastName, mailAliasNames))

    let moveStudentToClass user className = UpdateUser (user, MoveStudentToClass (ClassName className))


let tests =
    testList "ADModifications" [
        testCase "Create first teacher" <| fun _ ->
            let actual = modifications [ Sokrates.einstein ] [] []
            let expected = [ AD.createTeacherGroup; AD.createUser AD.einstein AD.einsteinMailAliasNames "14.03.1879" ]
            Expect.equal actual expected "New teacher and group should be created"

        testCase "Create another teacher" <| fun _ ->
            let actual = modifications [ Sokrates.einstein; Sokrates.bohr ] [] [ AD.einstein |> AD.toExistingDomainUser AD.einsteinMailAliasNames ]
            let expected = [ AD.createUser AD.bohr AD.bohrMailAliasNames "07.10.1885" ]
            Expect.equal actual expected "New teacher should be created"

        testCase "Create first student" <| fun _ ->
            let actual = modifications [] [ Sokrates.einstein |> Sokrates.asStudent "1A" ] []
            let expected = [ AD.createStudentGroup "1A"; AD.createUser (AD.asStudent "1A" AD.einstein) AD.einsteinMailAliasNames "14.03.1879" ]
            Expect.equal actual expected "New student and group should be created"

        testCase "Create another student from same class" <| fun _ ->
            let actual =
                modifications []
                    [ Sokrates.einstein |> Sokrates.asStudent "1A"; Sokrates.bohr |> Sokrates.asStudent "1A" ]
                    [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser AD.einsteinMailAliasNames ]
            let expected = [ AD.createUser (AD.asStudent "1A" AD.bohr) AD.bohrMailAliasNames "07.10.1885" ]
            Expect.equal actual expected "New student should be created"

        testCase "Create another student from different class" <| fun _ ->
            let actual =
                modifications []
                    [ Sokrates.einstein |> Sokrates.asStudent "1A"; Sokrates.bohr |> Sokrates.asStudent "1B" ]
                    [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser AD.einsteinMailAliasNames ]
            let expected = [ AD.createStudentGroup "1B"; AD.createUser (AD.asStudent "1B" AD.bohr) AD.bohrMailAliasNames "07.10.1885" ]
            Expect.equal actual expected "New student and group should be created"

        testCase "Create unique user name if user name already exists" <| fun _ ->
            let actual =
                modifications []
                    [ Sokrates.einstein |> Sokrates.asStudent "1A"; Sokrates.einstein |> Sokrates.withId "9999" |> Sokrates.asStudent "1B" ]
                    [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser AD.einsteinMailAliasNames ]
            let expectedMailAliases =
                AD.einsteinMailAliasNames
                |> List.map (fun v -> { v with UserName = v.UserName.Replace("Einstein", "Einstein2") })
            let expected = [
                AD.createStudentGroup "1B"
                AD.createUser (AD.einstein |> AD.asStudent "1B" |> AD.withId "9999" |> AD.withUserName "A.Einstein2") expectedMailAliases "14.03.1879"
            ]
            Expect.equal actual expected "Student with unique name should be created"

        testCase "Create unique user name if two users with same name are added" <| fun _ ->
            let actual =
                modifications []
                    [ Sokrates.einstein |> Sokrates.asStudent "1A"; Sokrates.einstein |> Sokrates.withId "9999" |> Sokrates.asStudent "1B" ]
                    []
            let expected = [
                AD.createStudentGroup "1A"
                AD.createStudentGroup "1B"
                AD.createUser (AD.einstein |> AD.asStudent "1A") AD.einsteinMailAliasNames "14.03.1879"
                AD.createUser (AD.einstein |> AD.asStudent "1B" |> AD.withId "9999" |> AD.withUserName "A.Einstein2") [ AD.primaryMailAlias "Albert.Einstein2" ] "14.03.1879"
            ]
            Expect.equal actual expected "Students with unique names should be created"

        testCase "Change user name" <| fun _ ->
            let actual =
                modifications
                    [ Sokrates.einstein |> Sokrates.withName "ZWEA" "Albert" "Zweistein" ]
                    []
                    [ AD.einstein |> AD.toExistingDomainUser AD.einsteinMailAliasNames ]
            let expected = [
                AD.changeUserName AD.einstein "ZWEA" "Albert" "Zweistein" [
                    AD.primaryMailAlias "Albert.Zweistein"
                    AD.nonPrimaryMailAlias "EINA"
                    AD.nonPrimaryMailAlias "Albert.Einstein"
                ]
            ]
            Expect.equal actual expected "Teacher name should be changed"

        testCase "Move student to another class" <| fun _ ->
            let actual =
                modifications []
                    [ Sokrates.einstein |> Sokrates.asStudent "1B" ]
                    [ AD.einstein |> AD.asStudent "1A" |> AD.toExistingDomainUser AD.einsteinMailAliasNames ]
            let expected = [
                AD.createStudentGroup "1B"
                AD.moveStudentToClass (AD.einstein |> AD.asStudent "1A") "1B"
                AD.deleteStudentGroup "1A"
            ]
            Expect.equal actual expected "Student should be moved to class 1B"
    ]
