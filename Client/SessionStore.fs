/// One Elmish loop, in a store, with two views onto it.
///
/// The other four components each own a loop: their own init, update and model, mounted
/// with `Program.withLitHydrated`. These two share one, because they show the same facts
/// and must agree about them — so the init and the update live here, once, and the
/// islands are only views. `Session.update` is the same shape as any Elmish update; what
/// differs is that the loop is not attached to an element.
///
/// Client only. The server has no store: it has the session in hand, renders both views
/// from it, and writes it into the page so this can start from the same one. If it
/// started from anything else the first render would disagree with the markup it is
/// adopting, and lit would notice.
module SessionStore

open Browser
open Fable
open Fable.Core
open Fable.Core.JsInterop
open Lit
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

/// Where the first value comes from: the page if the server wrote one, and a stand-in if
/// it did not, which leaves the islands rendering over the server's markup rather than
/// adopting it -- a warning and a rebuild instead of a page that lies.
let private init () =
    let start =
        fromPage ()
        |> Option.defaultValue
            { Warehouse = "unknown"
              Bays = 1
              Reserved = 0
              SignedInAs = "" }

    start, ElmishStore.Cmd.none

/// The shared update, with the store's own command type wrapped around it.
///
/// `Session.update` is the whole of the decision and lives in the shared file, because the
/// server compiles that one. Commands stay here: the store's `Cmd` is its own type, and
/// nothing in this demo has an effect to run. A real one would fetch, and would do it from
/// this line rather than from the shared update.
let private update msg session = Session.update msg session, ElmishStore.Cmd.none

/// The store is the loop: the same init and update any Elmish program takes, and back
/// come the state to read and the dispatch to write with.
let private store, dispatch = Store.makeElmish init update ignore ()

/// Mounts a view on the element with this id: it adopts the server's markup once, and
/// renders again whenever the store changes.
///
/// No Elmish program per island, because there is nothing for one to own. The state is
/// the store's, the update is the store's, and what is left is a function from the
/// current value to a template.
let mount (id: string) (view: Session -> (Msg -> unit) -> TemplateResult) =
    let el = document.getElementById id

    if isNull el then
        failwith $"Cannot find element with id {id}"

    // Subscribing reports the current value here and every later one to the callback, so
    // the first render is the adoption and the rest are ordinary renders.
    let current, _ =
        store |> Store.subscribeImmediate (fun session -> Lit.render el (view session dispatch))

    Hydrate.adopt el (view current dispatch)
