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
                        a [ Href (toHash WakeUp); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Wake up" ]
                            span [] [ str "Send a Wake-on-Lan \"magic packet\" to a computer by specifying its MAC address." ]
                        ]
                    ]
                ]
                Tile.parent [ Tile.Size Tile.Is4 ] [
                    Tile.child [ Tile.CustomClass "box" ] [
                        a [ Href (toHash Home); Style [ Display DisplayOptions.Block ] ] [
                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str "Sync AAD user info" ]
                            span [] [ str "Update photos, contact information and more of Office 365 users." ]
                        ]
                    ]
                ]
            ]
        ]
    ]
