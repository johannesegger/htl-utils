namespace Untis.Configuration

open System
open Untis.Domain

type Config = {
    GPU001TimetableFilePath: string
    GPU002TeachingDataFilePath: string
    GPU005RoomsFilePath: string
    GPU006SubjectsFilePath: string
    TimeFrames: TimeFrame list
}
module Config =
    let fromEnvironment () =
        {
            GPU001TimetableFilePath = Environment.getEnvVarOrFail "UNTIS_GPU001_TIMETABLE_FILE_PATH"
            GPU002TeachingDataFilePath = Environment.getEnvVarOrFail "UNTIS_GPU002_TEACHING_DATA_FILE_PATH"
            GPU005RoomsFilePath = Environment.getEnvVarOrFail "UNTIS_GPU005_ROOMS_FILE_PATH"
            GPU006SubjectsFilePath = Environment.getEnvVarOrFail "UNTIS_GPU006_SUBJECTS_FILE_PATH"
            TimeFrames =
                Environment.getEnvVarOrFail "UNTIS_TIME_FRAMES"
                |> String.split ";"
                |> Seq.map (fun t ->
                    String.split "-" t
                    |> Seq.choose (tryDo TimeSpan.TryParse)
                    |> Seq.toList
                    |> function
                    | ``begin`` :: [ ``end`` ] -> { BeginTime = ``begin``; EndTime = ``end`` }
                    | _ -> failwithf "Can't parse \"%s\" as time frame" t
                )
                |> Seq.toList
        }
