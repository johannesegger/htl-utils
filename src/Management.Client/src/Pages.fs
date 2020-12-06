module Pages

open Elmish.UrlParser

type Page =
    | Home
    | IncrementADClassGroups
    | SyncAD
    | ModifyAD
    | IncrementAADClassGroups
    | SyncAADGroups
    | GenerateITInformationSheet
    | ListConsultationHours
    | ShowComputerInfo

let toHash = function
    | Home -> "#home"
    | IncrementADClassGroups -> "#increment-ad-class-groups"
    | SyncAD -> "#sync-ad"
    | ModifyAD -> "#modify-ad"
    | IncrementAADClassGroups -> "#increment-aad-class-groups"
    | SyncAADGroups -> "#sync-aad-groups"
    | GenerateITInformationSheet -> "#generate-it-information-sheet"
    | ListConsultationHours -> "#list-consultation-hours"
    | ShowComputerInfo -> "#show-computer-info"

let pageParser: Parser<Page->Page,Page> =
    oneOf [
        map Home top
        map IncrementADClassGroups (s "increment-ad-class-groups")
        map SyncAD (s "sync-ad")
        map ModifyAD (s "modify-ad")
        map IncrementAADClassGroups (s "increment-aad-class-groups")
        map SyncAADGroups (s "sync-aad-groups")
        map GenerateITInformationSheet (s "generate-it-information-sheet")
        map ListConsultationHours (s "list-consultation-hours")
        map ShowComputerInfo (s "show-computer-info")
    ]
