module Home

open Fable.React
open Fable.React.Props
open Fulma
open Pages

let view =
    Container.container [] [
        Section.section [] [
            Tile.ancestor [] [
                Tile.parent [ Tile.Size Tile.Is5 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash IncrementADClassGroups); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Increment AD class groups" ]
                            span [] [ str "Increment the class number of Active Directory class groups." ]
                        ]
                    ]
                ]
                Tile.parent [ Tile.Size Tile.Is3 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash SyncAD); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Sync AD" ]
                            span [] [ str "Sync Active Directory users and groups based on data from Sokrates." ]
                        ]
                    ]
                ]
                Tile.parent [ Tile.Size Tile.Is4 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash ModifyAD); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Manually modify AD" ]
                            span [] [ str "Manually modify Active Directory users and groups." ]
                        ]
                    ]
                ]
            ]
            Tile.ancestor [] [
                Tile.parent [ Tile.Size Tile.Is5 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash IncrementAADClassGroups); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Increment AAD class groups" ]
                            span [] [ str "Increment the class number of Azure Active Directory class groups." ]
                        ]
                    ]
                ]
                Tile.parent [ Tile.Size Tile.Is4 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash SyncAADGroups); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Sync AAD groups" ]
                            span [] [ str "Update members of Office 365 groups based on data from Active Directory, Untis and more." ]
                        ]
                    ]
                ]
            ]
            Tile.ancestor [] [
                Tile.parent [ Tile.Size Tile.Is4 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash ListConsultationHours); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "List consultation hours" ]
                            span [] [ str "Get a list of consultation hours per class for students and parents." ]
                        ]
                    ]
                ]
            ]
            Tile.ancestor [] [
                Tile.parent [ Tile.Size Tile.Is4 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash ShowComputerInfo); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Show computer info" ]
                            span [] [ str "Show info about domain computers." ]
                        ]
                    ]
                ]
            ]
        ]
    ]
