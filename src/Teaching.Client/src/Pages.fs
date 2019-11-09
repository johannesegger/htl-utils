module Pages

open Elmish.UrlParser

type Page =
    | Home
    | WakeUp
    | AddAADTeacherContacts
    | CreateStudentDirectories
    | CreateStudentGroups

let toHash = function
    | Home -> ""
    | WakeUp -> "#wake-up"
    | AddAADTeacherContacts -> "#add-aad-teacher-contacts"
    | CreateStudentDirectories -> "#create-student-directories"
    | CreateStudentGroups -> "#create-student-groups"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map WakeUp (s "wake-up")
        map AddAADTeacherContacts (s "add-aad-teacher-contacts")
        map CreateStudentDirectories (s "create-student-directories")
        map CreateStudentGroups (s "create-student-groups")
    ]
