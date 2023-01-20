open Sokrates
open System
open System.IO
open System.Net.Http
open System.Net.Http.Json
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions
open System.Web

type SearchResult = {
    Lat: string
    Lon: string
    [<JsonPropertyName("display_name")>]DisplayName: string
}

type Coordinates = {
    Lat: float
    Lon: float
}

type Address = {
    Street: string
    City: string
}

type TeacherLocation = {
    Name: string
    Location: Coordinates
    Address: Address
}
module TeacherLocation =
    let create teacherName (lat, lon) (address: Sokrates.Address) =
        {
            Name = teacherName
            Location = { Lat = lat; Lon = lon }
            Address = { Street = address.Street; City = $"{address.Zip} {address.City}" }
        }

let searchUrl (address: string) = $"http://localhost:8080/search?format=json&q=%s{HttpUtility.UrlEncode address}"
// let searchUrl (address: string) = $"https://nominatim.openstreetmap.org/search?format=json&q=%s{HttpUtility.UrlEncode address}"

let getSearchAddress (address: Sokrates.Address) =
    let street =
        address.Street
        |> fun v ->
            match v.IndexOf('/') with
            | -1 -> v
            | x -> v.Substring(0, x)
        |> fun v -> Regex.Replace(v, @" (Haus|Top) \d+", "")
    $"%s{street}, %s{address.Zip}"

let tryFetchCoordinates (teacher: Sokrates.Teacher) = async {
    match teacher.Address with
    | Some address ->
        // do! Async.Sleep 1000 // when using nominatim.openstreetmap.org
        printfn $"Loading address of %s{teacher.ShortName}"
        let fullAddress = getSearchAddress address
        let url = searchUrl fullAddress
        use httpClient = new HttpClient()
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HTL Utils Person Map 1.0");
        let serializerOptions = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)
        let! response = httpClient.GetFromJsonAsync<SearchResult list>(url, serializerOptions) |> Async.AwaitTask
        match response with
        | [x] ->
            return
                TeacherLocation.create
                    teacher.ShortName
                    (float x.Lat, float x.Lon)
                    address
                |> Some
        | x :: _ ->
            Console.ForegroundColor <- ConsoleColor.Blue
            printfn $"Warning: Multiple locations for %s{teacher.ShortName} (Address: %s{fullAddress}):"
            response
            |> List.iter (fun v -> printfn $"  * (%s{v.Lat}, {v.Lon}): {v.DisplayName}")
            Console.ResetColor()

            return
                TeacherLocation.create
                    teacher.ShortName
                    (float x.Lat, float x.Lon)
                    address
                |> Some
        | [] ->
            Console.ForegroundColor <- ConsoleColor.Red
            printfn $"Warning: Couldn't resolve address of %s{teacher.ShortName} (%s{fullAddress})"
            Console.ResetColor()
            return None
    | None ->
        Console.ForegroundColor <- ConsoleColor.Yellow
        printfn $"Warning: %s{teacher.ShortName} doesn't have an address"
        Console.ResetColor()
        return None
}

let sokratesApi = SokratesApi.FromEnvironment()
async {
    let! teachers = sokratesApi.FetchTeachers
    let! teacherCoordinates =
        teachers
        |> List.sortBy (fun v -> v.LastName, v.FirstName, v.ShortName)
        |> List.map tryFetchCoordinates
        |> Async.Sequential
        |> Async.map (Array.choose id)
    teacherCoordinates
    |> fun v -> JsonSerializer.Serialize(v, JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase))
    |> fun v -> File.WriteAllText("persons.json", v)
}
|> Async.RunSynchronously
