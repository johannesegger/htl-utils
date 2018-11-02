module ClientLib.Test.CorrectionSnapshot

open Expecto
open ClientLib.Correction

let tests =
    testList "Correction snapshots" [
        testPropertyWithConfig FsCheckSettings.config "Single correction should have original span" <| fun correction ->
            let expected =
                { Correction = correction
                  CurrentPosition = Some correction.Position }
            getSnapshots [ correction ] = [ expected ]

        test "Shifted insertion has correct span" {
            let actual =
                getSnapshots
                    [ Correction.create (Insert (InsertCharacter.NormalCharacter '1')) { Line = 1; Column = 1 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '2')) { Line = 1; Column = 1 } ]
            let expected =
                [ { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '1'))  { Line = 1; Column = 1 }
                    CurrentPosition = Some { Line = 1; Column = 1 } }
                  { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '2'))  { Line = 1; Column = 1 }
                    CurrentPosition = Some { Line = 1; Column = 2 } } ]
            Expect.equal actual expected "Insertion should shift later corrections"
        }

        test "Deleted insertion has no span" {
            let actual =
                getSnapshots
                    [ Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 4 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '4')) { Line = 1; Column = 4 } ]
            let expected =
                [ { Correction = Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 4 }
                    CurrentPosition = Some { Line = 1; Column = 4 } }
                  { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '4')) { Line = 1; Column = 4 }
                    CurrentPosition = None } ]
            Expect.equal actual expected "Deleted insertion span should be None"
        }

        test "Deleted insertions have no span" {
            let actual =
                getSnapshots
                    [ Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 4 }
                      Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 5 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '5')) { Line = 1; Column = 5 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '4')) { Line = 1; Column = 4 } ]
            let expected =
                [ { Correction = Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 4 }
                    CurrentPosition = Some { Line = 1; Column = 4 } }
                  { Correction = Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 5 }
                    CurrentPosition = Some { Line = 1; Column = 4 } }
                  { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '5')) { Line = 1; Column = 5 }
                    CurrentPosition = None }
                  { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '4')) { Line = 1; Column = 4 }
                    CurrentPosition = None } ]
            Expect.equal actual expected "Deleted insertion span should be None"
        }

        test "Insert multiline" {
            let actual =
                getSnapshots
                    [ Correction.create (Insert InsertCharacter.EndOfLine) { Line = 1; Column = 6 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '5')) { Line = 1; Column = 5 } ]
            let expected =
                [ { Correction = Correction.create (Insert InsertCharacter.EndOfLine) { Line = 1; Column = 6 }
                    CurrentPosition = Some { Line = 1; Column = 6 } }
                  { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '5')) { Line = 1; Column = 5 }
                    CurrentPosition = Some { Line = 1; Column = 5 } } ]
            Expect.equal actual expected "Should not correct line span when adding a new line"
        }

        test "Insert multiline then delete line ending" {
            let actual =
                getSnapshots
                    [ Correction.create (Delete RemoveCharacter.EndOfLine) { Line = 1; Column = 6 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '6')) { Line = 2; Column = 1 }
                      Correction.create (Insert InsertCharacter.EndOfLine) { Line = 1; Column = 6 }
                      Correction.create (Insert (InsertCharacter.NormalCharacter '5')) { Line = 1; Column = 5 } ]
            let expected =
                [ { Correction = Correction.create (Delete RemoveCharacter.EndOfLine) { Line = 1; Column = 6 }
                    CurrentPosition = Some { Line = 1; Column = 6 } }
                  { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '6')) { Line = 2; Column = 1 }
                    CurrentPosition = Some { Line = 1; Column = 6 } }
                  { Correction = Correction.create (Insert InsertCharacter.EndOfLine) { Line = 1; Column = 6 }
                    CurrentPosition = None }
                  { Correction = Correction.create (Insert (InsertCharacter.NormalCharacter '5')) { Line = 1; Column = 5 }
                    CurrentPosition = Some { Line = 1; Column = 5 } } ]
            Expect.equal actual expected "Should correct line span when deleting line feed"
        }

        test "Delete on a different line" {
            let actual =
                getSnapshots
                    [ Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 3 }
                      Correction.create (StrikeThrough RemoveCharacter.NormalCharacter) { Line = 2; Column = 4 } ]
            let expected =
                [ { Correction = Correction.create (Delete RemoveCharacter.NormalCharacter) { Line = 1; Column = 3 }
                    CurrentPosition = Some { Line = 1; Column = 3 } }
                  { Correction = Correction.create (StrikeThrough RemoveCharacter.NormalCharacter) { Line = 2; Column = 4 }
                    CurrentPosition = Some { Line = 2; Column = 4 } } ]
            Expect.equal actual expected "Should not affect correction on different lines"
        }
    ]