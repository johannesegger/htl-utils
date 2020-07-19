module Pages

open Elmish.UrlParser

type Page =
    | Home
    | SyncAADGroups
    | ConsultationHours

let toHash = function
    | Home -> "#home"
    | SyncAADGroups -> "#sync-aad-groups"
    | ConsultationHours -> "#consultation-hours"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map SyncAADGroups (s "sync-aad-groups")
        map ConsultationHours (s "consultation-hours")
    ]
