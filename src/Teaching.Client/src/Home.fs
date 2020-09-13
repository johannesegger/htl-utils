module Home

open Fable.React
open Fable.React.Props
open Fulma
open Pages

let view =
    let tiles = [
        [
            WakeUp, Tile.Is4, "Wake up", str "Send a Wake-on-Lan \"magic packet\" to a computer by specifying its MAC address."
            AddAADTeacherContacts, Tile.Is4, "Add teacher contacts", str "Add teachers as Outlook contacts with photo, contact information and more."
            KnowName, Tile.Is3, "Know name", str "Learn teacher and student names given their photos."
        ]
        [
            CreateStudentDirectories, Tile.Is5, "Create student directories", str "Create a directory per student for exercises, tests, etc."
            CreateStudentGroups, Tile.Is4, "Create student groups", str "Create random groups of students for team work, seating plans, ordering and more."
            InspectDirectory, Tile.Is3, "Inspect directory", str "Get information about a directory and its files."
        ]
    ]
    Container.container [] [
        Section.section [] [
            yield!
                tiles
                |> List.map (fun rowTiles ->
                    Tile.ancestor [] [
                        yield!
                            rowTiles
                            |> List.map (fun (page, size, title, content) ->
                                Tile.parent [ Tile.Size size ] [
                                    Tile.child [ Tile.CustomClass "box" ] [
                                        a [ Href (toHash page); Style [ Display DisplayOptions.Block ] ] [
                                            span [ Class "title"; Style [ Display DisplayOptions.Block ] ] [ str title ]
                                            span [] [ content ]
                                        ]
                                    ]
                                ]
                            )
                    ]
                )
        ]
    ]
