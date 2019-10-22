module Home

open Fable.React
open Fable.React.Props
open Fulma
open Pages

let view =
    Tile.ancestor []
        [
            Tile.parent
                [
                    Tile.IsVertical
                    Tile.Size Tile.Is2
                ]
                [
                    Tile.child []
                        [
                            Button.a
                                [
                                    Button.Props
                                        [
                                            Href (toHash SyncAADGroups)
                                        ]
                                ]
                                [
                                    span [ Class "title" ] [ str "Sync AAD groups" ]
                                ]
                        ]
                ]
        ]
