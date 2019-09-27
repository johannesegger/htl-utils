module Thoth

open Expecto
open Giraffe.Serialization
open System.Text
open Thoth.Json.Giraffe

let tests = testList "Thoth" [
    testCase "ThothSerializer works with arrays" <| fun () ->
        let serializer = ThothSerializer() :> IJsonSerializer
        let serialized =
            serializer.SerializeToBytes([ 1; 2; 3 ] :> obj)
            |> Encoding.UTF8.GetString
        Expect.equal serialized "[1,2,3]" "Should serialize correctly"
]