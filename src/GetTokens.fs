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

module GetTokens =
    type Token = FSharpTokenInfo * int

    let sourceTok = FSharpSourceTokenizer([], Some "C:\\test.fsx")
    let private isTokenAfterGreater token greaterToken =
        greaterToken.TokenName = "GREATER" && token.TokenName <> "GREATER"
        && greaterToken.RightColumn <> (token.LeftColumn + 1)

    /// Tokenize a single line of F# code
    let rec tokenizeLine (tokenizer: FSharpLineTokenizer) state lineNumber tokens =
        match tokenizer.ScanToken(state), List.tryHead tokens with
        | (Some tok, state), Some(greaterToken, ln) when (isTokenAfterGreater tok greaterToken) ->
            let extraToken =
                { tok with
                      TokenName = "DELAYED"
                      LeftColumn = greaterToken.RightColumn + 1
                      Tag = -1
                      CharClass = FSharpTokenCharKind.Operator
                      RightColumn = tok.LeftColumn - 1 }, lineNumber

            let token = tok, lineNumber
            tokenizeLine tokenizer state lineNumber ([ token; extraToken ] @ tokens)
        | (Some tok, state), _ ->
            // Print token name
            // printf "%s " tok.TokenName
            let token = tok, lineNumber
            // Tokenize the rest, in the new state
            tokenizeLine tokenizer state lineNumber (token :: tokens)
        | (None, state), _ -> state, tokens

    /// Print token names for multiple lines of code
    let rec tokenizeLines state count tokens lines =
        match lines with
        | line :: lines ->
            // Create tokenizer & tokenize single line
            // printfn "\nLine %d" count
            let tokenizer = sourceTok.CreateLineTokenizer(line)
            let state, tokensOfLine = tokenizeLine tokenizer state count []
            // Tokenize the rest using new state
            tokenizeLines state (count + 1) (tokensOfLine @ tokens) lines
        | [] -> List.rev tokens

    let splitOnNewLines (value: string) =
        value.Split([| System.Environment.NewLine; "\n"; "\r" |], StringSplitOptions.None)

    let tokens (sourceCode: string) =
        splitOnNewLines sourceCode
        |> List.ofSeq
        |> tokenizeLines FSharpTokenizerLexState.Initial 1 []

    let getTokenText sourceCode line (token: FSharpTokenInfo) =
        printfn "%A" token
        let lines = splitOnNewLines sourceCode
        lines.[line - 1].Substring(token.LeftColumn, token.RightColumn - token.LeftColumn + 1) 

    let encodeEnum<'t> (value: 't) = value.ToString() |> Encode.string

    let decodeEnum<'t> (path: string) (token: JsonValue) =
        let v = token.Value<string>()
        match System.Enum.Parse(typeof<'t>, v, true) with
        | :? 't as t -> Ok t
        | _ ->
            let typeName = typeof<'t>.Name
            Error(DecoderError(sprintf "Cannot decode to %s" typeName, ErrorReason.BadField(path, token)))

    let toJson (tokens: Token list) =
        let extra =
            Extra.empty
            |> Extra.withCustom encodeEnum<FSharpTokenColorKind> decodeEnum<FSharpTokenColorKind>
            |> Extra.withCustom encodeEnum<FSharpTokenCharKind> decodeEnum<FSharpTokenCharKind>
            |> Extra.withCustom encodeEnum<FSharpTokenTriggerClass> decodeEnum<FSharpTokenTriggerClass>
        tokens
        |> List.map
            (fun i ->
            Encode.tuple2 (Thoth.Json.Net.Encode.Auto.generateEncoder<FSharpTokenInfo> (false, extra))
                Thoth.Json.Net.Encode.int i)
        |> Encode.list
        |> Encode.toString 4

    [<FunctionName("GetTokens")>]
    let Run([<HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)>] req: HttpRequest, log: ILogger) =
        log.LogInformation("F# HTTP trigger function processed a request.")
        let content = using (new StreamReader(req.Body)) (fun stream -> stream.ReadToEnd())
        let json = tokens content |> toJson
        new HttpResponseMessage(HttpStatusCode.OK, Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"))
