module Pages

open Elmish.UrlParser

type Page =
    | Home
    | SyncAD
    | SyncAADGroups
    | ConsultationHours

let toHash = function
    | Home -> "#home"
    | SyncAD -> "#sync-ad"
    | SyncAADGroups -> "#sync-aad-groups"
    | ConsultationHours -> "#consultation-hours"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map SyncAD (s "sync-ad")
        map SyncAADGroups (s "sync-aad-groups")
        map ConsultationHours (s "consultation-hours")
    ]
