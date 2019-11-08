module Pages

open Elmish.UrlParser

type Page =
    | Home
    | WakeUp
    | AddAADTeacherContacts
    | CreateStudentDirectories

let toHash = function
    | Home -> ""
    | WakeUp -> "#wake-up"
    | AddAADTeacherContacts -> "#add-aad-teacher-contacts"
    | CreateStudentDirectories -> "#create-student-directories"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map WakeUp (s "wake-up")
        map AddAADTeacherContacts (s "add-aad-teacher-contacts")
        map CreateStudentDirectories (s "create-student-directories")
    ]
