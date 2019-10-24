module Home

open Fable.React
open Fable.React.Props
open Fulma
open Pages

let view =
    Container.container []
        [
            Tile.ancestor []
                [
                    Tile.parent
                        [
                            // Tile.IsVertical
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
                                            span [] [ str "Update members of Azure Active Directory groups based on data from Sokrates, Untis and more" ]
                                        ]
                                ]
                        ]
                ]
        ]
