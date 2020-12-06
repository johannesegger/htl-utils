module Home

open Fable.React
open Fable.React.Props
open Fulma
open Pages

let view =
    let item page title description size =
        Tile.parent [ Tile.CustomClass (sprintf "is-%d" size) ] [
            Tile.child [ Tile.CustomClass "box" ] [
                a [ Href (toHash page); Style [ Display DisplayOptions.Block ] ] [
                    span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str title ]
                    span [] [ str description ]
                ]
            ]
        ]

    Container.container [] [
        Section.section [] [
            Tile.ancestor [] [
                item IncrementADClassGroups "Increment AD class groups" "Increment the class number of Active Directory class groups." 5
                item SyncAD "Sync AD" "Sync Active Directory users and groups based on data from Sokrates." 3
                item SyncAD "Manually modify AD" "Manually modify Active Directory users and groups." 4
            ]
            Tile.ancestor [] [
                item GenerateITInformationSheet "Generate IT information sheet" "Generate information sheet for new teachers about IT systems used within the school." 5
            ]
            Tile.ancestor [] [
                item IncrementAADClassGroups "Increment AAD class groups" "Increment the class number of Azure Active Directory class groups." 5
                item SyncAADGroups "Sync AAD groups" "Update members of Office 365 groups based on data from Active Directory, Untis and more." 4
            ]
            Tile.ancestor [] [
                item ListConsultationHours "List consultation hours" "Get a list of consultation hours per class for students and parents." 4
            ]
            Tile.ancestor [] [
                item ShowComputerInfo "Show computer info" "Show info about domain computers." 4
            ]
        ]
    ]
