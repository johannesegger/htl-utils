module Home

open Fable.React
open Fable.React.Props
open Fulma
open Pages

let view =
    Container.container [] [
        Section.section [] [
            Tile.ancestor [] [
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
                        a [ Href (toHash SyncAADGroups); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Sync AAD groups" ]
                            span [] [ str "Update members of Office 365 groups based on data from Sokrates, Untis and more." ]
                        ]
                    ]
                ]
                Tile.parent [ Tile.Size Tile.Is4 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash ListConsultationHours); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "List consultation hours" ]
                            span [] [ str "Get a list of consultation hours per class for students and parents." ]
                        ]
                    ]
                ]
            ]
        ]
    ]
