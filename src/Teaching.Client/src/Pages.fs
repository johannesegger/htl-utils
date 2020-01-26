module Pages

open Elmish.UrlParser

type Page =
    | Home
    | WakeUp
    | AddAADTeacherContacts
    | CreateStudentDirectories
    | CreateStudentGroups
    | InspectDirectory
    | KnowName

let toHash = function
    | Home -> ""
    | WakeUp -> "#wake-up"
    | AddAADTeacherContacts -> "#add-aad-teacher-contacts"
    | CreateStudentDirectories -> "#create-student-directories"
    | CreateStudentGroups -> "#create-student-groups"
    | InspectDirectory -> "#inspect-directory"
    | KnowName -> "#know-name"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map WakeUp (s "wake-up")
        map AddAADTeacherContacts (s "add-aad-teacher-contacts")
        map CreateStudentDirectories (s "create-student-directories")
        map CreateStudentGroups (s "create-student-groups")
        map InspectDirectory (s "inspect-directory")
        map KnowName (s "know-name")
    ]
