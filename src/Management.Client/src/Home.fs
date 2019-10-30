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
                        a [ Href (toHash SyncAADGroups); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Sync AAD groups" ]
                            span [] [ str "Update members of Office 365 groups based on data from Sokrates, Untis and more." ]
                        ]
                    ]
                ]
                Tile.parent [ Tile.Size Tile.Is4 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash SyncAADGroups); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Sync AAD user info" ]
                            span [] [ str "Update photos, contact information and more of Office 365 users." ]
                        ]
                    ]
                ]
            ]
        ]
    ]
