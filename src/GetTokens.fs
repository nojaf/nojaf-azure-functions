namespace Nojaf.Functions

open FSharp.Compiler.SourceCodeServices
open Microsoft.AspNetCore.Http
open Microsoft.Azure.WebJobs
open Microsoft.Azure.WebJobs.Extensions.Http
open Microsoft.Extensions.Logging
open Newtonsoft.Json.Linq
open System
open System.IO
open System.Net
open System.Net.Http
open Thoth.Json.Net
open Fantomas

module GetTokens =
    let private encodeEnum<'t> (value: 't) = value.ToString() |> Encode.string

    let private decodeEnum<'t> (path: string) (token: JsonValue) =
        let v = token.Value<string>()
        match System.Enum.Parse(typeof<'t>, v, true) with
        | :? 't as t -> Ok t
        | _ ->
            let typeName = typeof<'t>.Name
            Error(DecoderError(sprintf "Cannot decode to %s" typeName, ErrorReason.BadField(path, token)))

    let private toJson (tokens: TokenParser.Token list) =
        let extra =
            Extra.empty
            |> Extra.withCustom encodeEnum<FSharpTokenColorKind> decodeEnum<FSharpTokenColorKind>
            |> Extra.withCustom encodeEnum<FSharpTokenCharKind> decodeEnum<FSharpTokenCharKind>
            |> Extra.withCustom encodeEnum<FSharpTokenTriggerClass> decodeEnum<FSharpTokenTriggerClass>
        tokens
        |> List.map
            (Thoth.Json.Net.Encode.Auto.generateEncoder<TokenParser.Token> (false, extra))
        |> Encode.list
        |> Encode.toString 4
        
    let private tokenize (content : string) : TokenParser.Token list =
        let defines = TokenParser.getDefines content |> Array.toList
        TokenParser.tokenize defines content

    [<FunctionName("GetTokens")>]
    let Run([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)>] req: HttpRequest, log: ILogger) =
        log.LogInformation("F# HTTP trigger function processed a request.")
        let content = using (new StreamReader(req.Body)) (fun stream -> stream.ReadToEnd())
        let json = tokenize content |> toJson
        new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"))
