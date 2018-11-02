module ClientLib.Test.Correction

open Expecto
open ClientLib.Correction

let tests =
    testList "Corrections" [
        testProperty "Remove intention to strike-through correction" <| fun removeCharacter position ->
            let actual =
                addCorrection
                    (CorrectionIntention.create (RemoveText [ removeCharacter ]) position)
                    []

            let expected =
                [ Correction.create (StrikeThrough removeCharacter) position ]
            Expect.equal actual expected "Should do a simple translation"

        testList "Remove intention to delete correction" [
            test "Insert -> Remove" {
                let actual =
                    []
                    |> addCorrection (CorrectionIntention.create (InsertText "a") { Line = 1; Column = 3 })
                    |> addCorrection (CorrectionIntention.create (RemoveText [ RemoveCharacter.NormalCharacter ]) { Line = 1; Column = 3 })

                let expected =
                    [ Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 3 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter 'a')) { Line = 1; Column = 3 } ]
                Expect.equal actual expected "Should add delete correction"
            }

            test "Insert -> Shift -> Remove" {
                let actual =
                    []
                    |> addCorrection (CorrectionIntention.create (InsertText "a") { Line = 1; Column = 3 })
                    |> addCorrection (CorrectionIntention.create (InsertText "1") { Line = 1; Column = 1 })
                    |> addCorrection (CorrectionIntention.create (InsertText "2") { Line = 1; Column = 2 })
                    |> addCorrection (CorrectionIntention.create (InsertText "3") { Line = 1; Column = 3 })
                    |> addCorrection (CorrectionIntention.create (RemoveText [ RemoveCharacter.NormalCharacter ]) { Line = 1; Column = 6 })

                let expected =
                    [ Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 6 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '3')) { Line = 1; Column = 3 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '2')) { Line = 1; Column = 2 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '1')) { Line = 1; Column = 1 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter 'a')) { Line = 1; Column = 3 } ]
                Expect.equal actual expected "Should add delete correction"
            }

            test "Insert -> Insert -> Delete -> Delete -> Strike-through" {
                let actual =
                    []
                    |> addCorrection (CorrectionIntention.create (InsertText "1") { Line = 1; Column = 2 })
                    |> addCorrection (CorrectionIntention.create (InsertText "2") { Line = 1; Column = 3 })
                    |> addCorrection (CorrectionIntention.create (RemoveText [ RemoveCharacter.NormalCharacter ]) { Line = 1; Column = 3 })
                    |> addCorrection (CorrectionIntention.create (RemoveText [ RemoveCharacter.NormalCharacter ]) { Line = 1; Column = 2 })
                    |> addCorrection (CorrectionIntention.create (RemoveText [ RemoveCharacter.NormalCharacter ]) { Line = 1; Column = 1 })

                let expected =
                    [ Correction.create (StrikeThrough RemoveCharacter.NormalCharacter) { Line = 1; Column = 1 }
                      Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 2 }
                      Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 3 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '2')) { Line = 1; Column = 3 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '1')) { Line = 1; Column = 2 } ]
                Expect.equal actual expected "Should add delete and strike-through correction"
            }

            test "Insert newline" {
                let actual =
                    []
                    |> addCorrection (CorrectionIntention.create (InsertText "1\r\n2") { Line = 1; Column = 2 })

                let expected =
                    [ Correction.create (Insert (InsertCharacter.NormalCharacter '2')) { Line = 2; Column = 1 }
                      Correction.create (Insert InsertCharacter.EndOfLine) { Line = 1; Column = 3 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '1')) { Line = 1; Column = 2 } ]
                Expect.equal actual expected "Should add new line offset for inserts"
            }

            test "Multi-delete inserted text" {
                let actual =
                    []
                    |> addCorrection (CorrectionIntention.create (InsertText "123") { Line = 1; Column = 2 })
                    |> addCorrection (CorrectionIntention.create (RemoveText [ RemoveCharacter.NormalCharacter; RemoveCharacter.NormalCharacter ]) { Line = 1; Column = 3 })

                let expected =
                    [ Correction.create (Delete (RemoveCharacter.NormalCharacter)) { Line = 1; Column = 3 }
                      Correction.create (Delete (RemoveCharacter.NormalCharacter)) { Line = 1; Column = 4 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '3')) { Line = 1; Column = 4 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '2')) { Line = 1; Column = 3 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '1')) { Line = 1; Column = 2 } ]
                Expect.equal actual expected "Should add new line offset for inserts"
            }
        ]
    ]