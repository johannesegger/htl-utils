module Http

open Giraffe
open Microsoft.AspNetCore.Http
open System.Net
open System.Net.Http
open System.Text
open Thoth.Json.Net

type FetchError =
    | SendError of url: string * message: string
    | HttpError of url: string * HttpStatusCode * content: string
    | DecodeError of url: string * message: string

let private sendWithHeaders (ctx: HttpContext) (url: string) httpMethod body decoder = async {
    let httpClientFactory = ctx.GetService<IHttpClientFactory>()
    use httpClient = httpClientFactory.CreateClient()
    use requestMessage = new HttpRequestMessage(httpMethod, url)

    match ctx.Request.Headers.TryGetValue("Authorization") with
    | (true, values) -> requestMessage.Headers.Add("Authorization", values)
    | (false, _) -> ()

    body
    |> Option.iter (fun content -> requestMessage.Content <- new StringContent(Encode.toString 0 content, Encoding.UTF8, "application/json"))

    let! response = async {
        try
            let! response = httpClient.SendAsync(requestMessage) |> Async.AwaitTask
            return Ok response
        with
            e -> return Error (SendError (url, e.Message))
    }
    match response with
    | Ok response ->
        let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
        if not response.IsSuccessStatusCode then
            return Error (HttpError (url, response.StatusCode, responseContent))
        else
            return
                Decode.fromString decoder responseContent
                |> Result.mapError (fun message -> DecodeError(url, message))
    | Error e -> return Error e
}

let get (ctx: HttpContext) url decoder =
    sendWithHeaders ctx url HttpMethod.Get None decoder

let post (ctx: HttpContext) url body decoder =
    sendWithHeaders ctx url HttpMethod.Post (Some body) decoder

let proxy (url: string) : HttpHandler =
    fun next ctx -> task {
        let httpClientFactory = ctx.GetService<IHttpClientFactory>()
        use httpClient = httpClientFactory.CreateClient()
        let httpMethod = HttpMethod ctx.Request.Method
        use requestMessage = new HttpRequestMessage(httpMethod, url)

        let! response = async {
            try
                let! response = httpClient.SendAsync requestMessage |> Async.AwaitTask
                return Ok response
            with
                e -> return Error e
        }
        match response with
        | Ok response ->
            let handler = setStatusCode (int response.StatusCode)
            let handler =
                if not <| isNull response.Content
                then
                    handler
                    >=> fun next ctx -> task {
                        let! responseContent = response.Content.ReadAsByteArrayAsync()
                        return! setBody responseContent next ctx
                    }
                    >=> setHttpHeader "Content-Type" (sprintf "%O" response.Content.Headers.ContentType)
                else handler
            return! handler next ctx
        | Error e -> return! ServerErrors.internalError (setBodyFromString e.Message) next ctx
    }
