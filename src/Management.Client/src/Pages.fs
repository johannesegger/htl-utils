module Pages

open Elmish.UrlParser

type Page =
    | Home
    | SyncAADGroups

let toHash = function
    | Home -> "#home"
    | SyncAADGroups -> "#sync-aad-groups"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map SyncAADGroups (s "sync-aad-groups")
    ]
