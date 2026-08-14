/// One Elmish loop, in a store, for two views that live elsewhere.
///
/// The four components above each own a loop: their own init, update and model, mounted
/// with `Program.withLitHydrated`. These two share one, because they show the same fact
/// and must agree about it — so the init and the update live here, once.
///
/// Nothing here renders, and the DOM is touched exactly once: to read the value the
/// server wrote into the page. After that this is state, an update, and whoever asked to
/// be told.
module ThemeStore

open Browser
open Fable
open Fable.Core
open Fable.Core.JsInterop
open Theme

[<Emit("JSON.parse($0)")>]
let private parse (json: string) : obj = jsNative

/// The one DOM read: the script tag the server wrote.
///
/// `unbox` rather than a decoder, which holds because the record is a single bool: the
/// field name survives JSON unchanged, so the parsed object already is the shape.
let private fromPage () =
    match document.getElementById PayloadId with
    | null -> None
    | script ->
        try
            Some(unbox<Model> (parse script.textContent))
        with error ->
            console.warn ("the theme payload could not be read; starting light", error)
            None

/// Where the first value comes from: the page if the server wrote one, and light if it
/// did not — which leaves the islands rendering over the server's markup rather than
/// adopting it, a warning and a rebuild instead of a page that lies.
let private init () =
    let start = fromPage () |> Option.defaultValue { Dark = false }
    start, ElmishStore.Cmd.none

/// The shared update, with the store's own command type wrapped around it.
///
/// `Theme.update` is the whole of the decision and lives in the shared file, because the
/// server compiles that one. Commands stay here: the store's `Cmd` is its own type.
let private update msg model = Theme.update msg model, ElmishStore.Cmd.none

/// The store is the loop: the same init and update any Elmish program takes, and back
/// come the state to read and the dispatch to write with.
///
/// Note what is missing. `Program.mkProgram` takes init, update *and* view and binds the
/// three together; `makeElmish` takes init and update and knows nothing about rendering.
/// A store has no view, is not told when something starts reading it, and does not count
/// its readers — which is exactly what lets two islands share this one.
let store, dispatch = Store.makeElmish init update ignore ()
