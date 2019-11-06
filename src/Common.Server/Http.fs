module Http

open Giraffe
open Microsoft.AspNetCore.Http
open System.Net
open System.Net.Http
open System.Text
open Thoth.Json.Net

type FetchError =
    | HttpError of url: string * HttpStatusCode * content: string
    | DecodeError of url: string * message: string

let private sendWithHeaders (ctx: HttpContext) (url: string) httpMethod headers body decoder = async {
    let httpClientFactory = ctx.GetService<IHttpClientFactory>()
    use httpClient = httpClientFactory.CreateClient()
    use requestMessage = new HttpRequestMessage(httpMethod, url)

    headers
    |> Seq.iter (fun (key, value: string) -> requestMessage.Headers.Add(key, value))

    match ctx.Request.Headers.TryGetValue("Authorization") with
    | (true, values) -> requestMessage.Headers.Add("Authorization", values)
    | (false, _) -> ()

    body
    |> Option.iter (fun content -> requestMessage.Content <- new StringContent(Encode.toString 0 content, Encoding.UTF8, "application/json"))

    let! response = httpClient.SendAsync(requestMessage) |> Async.AwaitTask
    let! responseContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
    if not response.IsSuccessStatusCode then
        return Error (HttpError (url, response.StatusCode, responseContent))
    else
        return
            Decode.fromString decoder responseContent
            |> Result.mapError (fun message -> DecodeError(url, message))
}

let get (ctx: HttpContext) url decoder =
    sendWithHeaders ctx url HttpMethod.Get [] None decoder

let post (ctx: HttpContext) url body decoder =
    sendWithHeaders ctx url HttpMethod.Post [] (Some body) decoder