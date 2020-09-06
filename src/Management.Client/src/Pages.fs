module Pages

open Elmish.UrlParser

type Page =
    | Home
    | IncrementADClassGroups
    | SyncAD
    | SyncAADGroups
    | ListConsultationHours

let toHash = function
    | Home -> "#home"
    | IncrementADClassGroups -> "#increment-ad-class-groups"
    | SyncAD -> "#sync-ad"
    | SyncAADGroups -> "#sync-aad-groups"
    | ListConsultationHours -> "#list-consultation-hours"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map IncrementADClassGroups (s "increment-ad-class-groups")
        map SyncAD (s "sync-ad")
        map SyncAADGroups (s "sync-aad-groups")
        map ListConsultationHours (s "list-consultation-hours")
    ]
