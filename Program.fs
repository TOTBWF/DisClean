open System
open Hopac
open HttpFs.Client
open Chiron

let baseUrl = "https://discordapp.com/api"

let OAuth2Request token = Request.setHeader (Custom ("Authorization", "Bot " + token))
let GetResponseAsJson = 
    getResponse 
    >> Job.bind(Response.readBodyAsString) 
    >> Job.map(Json.parse)

let isByAuthor author = function
    | Object o ->
        match Map.tryFind "author" o with
        | Some(Object(a)) ->
            match Map.tryFind "username" a with
            | Some(String(u)) -> author = u
            | _ -> false
        | _ -> false
    | _ -> false

let parseMessageId = function
    | Object o ->
        match Map.tryFind "id" o with
        | Some(String(id)) -> id
        | _ -> ""
    | _ -> ""

let getMessages author channel token last = 
    job {
        let! response = 
            match last with
            | Some l ->  
                Request.createUrl Get (baseUrl + "/channels/" + channel + "/messages?limit=1&before=" + l ) 
                |> OAuth2Request token 
                |> GetResponseAsJson
            | None -> 
                Request.createUrl Get (baseUrl + "/channels/" + channel + "/messages?limit=1" ) 
                |> OAuth2Request token 
                |> GetResponseAsJson
        return 
            match response with
            | Array a -> 
                a
                |> List.filter(isByAuthor author)
                |> List.map(parseMessageId)
                |> List.filter(String.isEmpty >> not)
            | _ -> []
    }
let getAllMessages author channel token = 
    Seq.unfold(fun last -> 
        job {
            let! messages = getMessages author channel token last
            match messages with
            | [] -> return None
            | startingPoint::_ -> return Some (messages, Some startingPoint)
        } |> run) None

type DeletionResult =
    | Success of string
    | Failure of string
let deleteMessage channel token id =
    job {
        let! response = 
            Request.createUrl Delete (baseUrl + "/channels/" + channel + "/messages/" + id)
            |> OAuth2Request token
            |> getResponse
        match response.statusCode with
        | 204 -> return Success "Message Deleted"
        | _ -> 
            let! body = Response.readBodyAsString response
            return Failure body
    }

let deleteMessages author channel token =
    getAllMessages author channel token
    |> Seq.map((run << Job.conCollect << List.map(deleteMessage channel token)))


let authorize token = 
        job {
            let! response =
                Request.createUrl Get (baseUrl + "/oauth2/applications/@me")
                |> OAuth2Request token
                |> getResponse
            let! body = (Response.readBodyAsString response)
            return response.statusCode <> 401
        } 

let script author channel token =
    job {
        let! success = authorize token
        if success 
        then
            deleteMessages author channel token
            |> Seq.iter(fun a -> 
                a.ToArray() 
                |> Array.toList
                |>List.iter(function
                    | Success m -> printfn "%s" m
                    | Failure m -> printfn "FAILED: %s" m))
        else
            printfn "You need to authorize this bot. Please go to fill in the blanks in this URL and go to it to authorize"
            printfn "https://discordapp.com/api/oauth2/authorize?client_id=CLIENT_ID&client_secret=CLIENT_SECRET&scope=bot&permissions=8192"
    } |> run
    0

[<EntryPoint>]
let main argv =
    if argv.Length <> 3
    then 
        printfn "Scrubber: Because sometimes banning just isnt enough"
        printfn "Usage: scrubber [author] [channel] [token]"
        printfn "Author: the author of the messages that you would like to delete"
        printfn "Channel: The channel id that you would like to clean. Can be found by turning on developer mode"
        printfn "Access Token: The access token of your bot. This is given when the bot user is created"
        0
    else script argv.[0] argv.[1] argv.[2]

