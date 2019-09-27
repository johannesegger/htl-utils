module Untis

open Expecto

let private untisExportData = """1,4,,4,,"TCH1","NCAD",,"Z",0,4.42000,,,,"20190909","20200712",0.01760,,,,,,,"In",,,,,,,,,,0,0,,,,,400000,4.00000,,,,,0,
4,3,3,3,"4ABMB","TCH2","AM","R211",,0,2.12160,,,,"20190909","20200712",0.00798,,,"R211",,,,"aBn",,,,1,1,,,,,0,0,,,,,163636,1.63636,,,,,0,
6,1.5,,1.5,,"TCH3","NBU",,"Z",0,1.65750,,,,"20190909","20200712",0.00640,,,,,,,"hIn",,,,,,,,,,0,0,,,,,150000,1.50000,,,,,0,
7,3,,3,,"TCH4","NBU",,"Z",0,3.31500,,,,"20190909","20200712",0.01320,,,,,,,"In",,,,,,,,,,0,0,,,,,300000,3.00000,,,,,0,
8,1.5,,1.5,,"TCH5","NBU",,"Z",0,1.65750,,,,"20190909","20200712",0.00640,,,,,,,"hIn",,,,,,,,,,0,0,,,,,150000,1.50000,,,,,0,
9,2,2,2,"4ABMB","TCH6","E1","R211",,0,1.48510,,,,"20190909","20200712",0.00567,,,"R211",,,,"aBn",,,,1,1,,,,,0,0,,,,,109090,1.09090,,,,,0,
10,2,2,2,"4ABMB","TCH7","FET_1","R211",,0,1.48510,,,,"20190909","20200712",0.00567,,,"R211",,,,"aBn",,,,1,1,,,,,0,0,,,,,109090,1.09090,,,,,0,
12,3,3,3,"4ABMB","TCH8","TMB","R211",,0,2.54550,,,,"20190909","20200712",0.00966,,,"R211",,,,"aBn",,,,1,1,,,,,0,0,,,,,163636,1.63636,,,,,0,
13,2,2,2,"4ABMB","TCH1","MEL","R211",,0,1.48510,,,,"20190909","20200712",0.00567,,,"R211",,,,"aBn",,,,1,1,,,,,0,0,,,,,109090,1.09090,,,,,0,
14,1,,1,,"TCH2","NBU",,"Z",0,1.10500,,,,"20190909","20200712",0.00440,,,,,,,"In",,,,,,,,,,0,0,,,,,100000,1.00000,,,,,0,
20,11,,11,,"TCH3","ABTV",,"Z",0,12.83700,,,,"20190909","20200712",0.05120,,,,,,,"In",,,,,,,,,,0,0,,,,,1100000,11.00000,,,,,0,
21,13,,13,,"TCH4","ABTV",,"Z",0,15.17100,,,,"20190909","20200712",0.06040,,,,,,,"In",,,,,,,,,,0,0,,,,,1300000,13.00000,,,,,0,
22,1,,1,,"TCH5","NBU",,"Z",0,1.10500,,,,"20190909","20200712",0.00440,,,,,,,"In",,,,,,,,,,0,0,,,,,100000,1.00000,,,,,0,
221,1,1,1,"1AFMBM","TCH6","ORD","R001","c",0,0.00000,,,,"20190909","20200712",0.00000,,,"R001",,,,"In",,,,,,,,,,0,0,,,,,100000,1.00000,,,,,0,
1218,2,2,2,"1AFMBM","TCH7","RK","R001",,0,2.10000,,,,"20190909","20200712",0.00840,,,"R001",,,,"aBn",,,,,,,,,,0,0,,,,,200000,2.00000,"RK_1AFMBM",,,,0,
"""

let tests = testList "Untis" [
    testCase "Get classes with teachers" <| fun () ->
        let classesWithTeachers = Untis.TeachingData.ParseRows untisExportData |> Untis.getClassesWithTeachers
        let expected =
            [
                Class.create 4 "A" "B" "MB", Set.ofList [ "TCH2"; "TCH6"; "TCH7"; "TCH8"; "TCH1" ]
                Class.create 1 "A" "F" "MBM", Set.ofList [ "TCH6"; "TCH7" ]
            ]
            |> Set.ofList
        Expect.equal classesWithTeachers expected "Classes with teachers don't match"

    testCase "Get class teachers" <| fun () ->
        let classTeachers = Untis.TeachingData.ParseRows untisExportData |> Untis.getClassTeachers
        let expected =
            [
                Class.create 1 "A" "F" "MBM", "TCH6"
            ]
            |> Map.ofList
        Expect.equal classTeachers expected "Class teachers don't match"
]
