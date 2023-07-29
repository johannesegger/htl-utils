module AD.Test.DN

open AD
open AD.Configuration
open Expecto

let tests =
    testList "DN" [
        testCase "Can get parents and self" <| fun () ->
            let actual = DN.parentsAndSelf (DistinguishedName "CN=Testuser,OU=Users,OU=Persons,OU=Org,DC=my,DC=School")
            let expected = [
                DistinguishedName "DC=School"
                DistinguishedName "DC=my,DC=School"
                DistinguishedName "OU=Org,DC=my,DC=School"
                DistinguishedName "OU=Persons,OU=Org,DC=my,DC=School"
                DistinguishedName "OU=Users,OU=Persons,OU=Org,DC=my,DC=School"
                DistinguishedName "CN=Testuser,OU=Users,OU=Persons,OU=Org,DC=my,DC=School"
            ]
            Expect.equal actual expected "Should get all parents and self"
    ]
