/// The store the two session islands share, and where it gets its first value.
///
/// Client only. The server has no store: it has the session in hand, renders both views
/// from it, and writes it into the page so that this can start from the same one. That
/// is the whole contract -- if this started from anything else, the first render would
/// disagree with the markup it is adopting, and lit would notice.
module SessionStore

open Browser
open Fable.Core
open Fable.Core.JsInterop
open Session

[<Emit("JSON.parse($0)")>]
let private parse (json: string) : obj = jsNative

/// Read once, from the script tag the server wrote.
///
/// `unbox` rather than a decoder, which holds only because the record is strings and
/// numbers: the field names survive JSON unchanged, so the parsed object already is the
/// shape. A union or an option here would need a codec written once and compiled by both
/// sides, which is a larger thing than this demo needs.
let private fromPage () =
    match document.getElementById PayloadId with
    | null -> None
    | script ->
        try
            Some(unbox<Session> (parse script.textContent))
        with error ->
            console.warn ("the session payload could not be read; the islands will start empty", error)
            None

let mutable private current =
    fromPage ()
    |> Option.defaultValue
        { Warehouse = "unknown"
          Bays = 1
          Reserved = 0
          SignedInAs = "" }

let private listeners = ResizeArray<Session -> unit>()

let value () = current

/// Subscribe for as long as you need it. Returns what stops it, so an island that goes
/// away does not leave a listener rendering into nothing.
let subscribe (onChange: Session -> unit) =
    listeners.Add onChange

    { new System.IDisposable with
        member _.Dispose() = listeners.Remove onChange |> ignore }

let private publish () =
    for listener in Seq.toArray listeners do
        listener current

/// The one writer. Both islands ask for this; neither owns the answer.
let reserve () =
    if free current > 0 then
        current <- { current with Reserved = current.Reserved + 1 }
        publish ()
