/// What only the browser can do for a drag on the board: read the pointer, and say what
/// is under it. Everything else about a drag -- where the card is drawn, which lane is lit,
/// what a drop does -- is the board's model and view (Shared/Kanban.fs).
module Drag

open Fable.Core
open Browser
open Browser.Types
open Lit

[<Emit("document.elementsFromPoint($0, $1)")>]
let private elementsAt (x: float) (y: float) : Element array = jsNative

/// The lane under the pointer, and how many of its other cards have their middle above
/// it. The card itself is under the pointer too, still in the lane it came from, so it
/// does not count.
let private target (id: int) x y =
    let card = document.querySelector $"#kanban [data-card=\"{id}\"]"

    elementsAt x y
    |> Array.tryPick (fun element -> if card.contains element then None else element.closest "[data-lane]")
    |> Option.map (fun lane ->
        let cards = lane.querySelectorAll "[data-card]"

        let above =
            [ for i in 0 .. cards.length - 1 -> cards.[i] ]
            |> List.filter (fun other -> not (obj.ReferenceEquals(other, card)))
            |> List.filter (fun other ->
                let box = other.getBoundingClientRect ()
                box.top + box.height / 2.0 < y)
            |> List.length

        Kanban.laneOf (lane.getAttribute "data-lane"), above)

/// The handler a card is pressed with. A press on one of its buttons is the button's.
let press dispatch (id: int) =
    Ev(fun (event: PointerEvent) ->
        let onButton = (event.target :?> Element).closest "button" |> Option.isSome

        if event.button = 0 && not onButton then
            // Everything this pointer does until it is let go comes to the card, wherever
            // it goes, and bubbles up to the document, where `listen` hears it.
            (event.currentTarget :?> HTMLElement).setPointerCapture event.pointerId
            dispatch (Kanban.Press(id, event.pointerId, event.clientX, event.clientY)))

/// The document, listened to while a card is pressed. `drop` is how a drop that moved is
/// dispatched: through the transition, so the card glides from the pointer to where it
/// lands, or back.
let listen (drag: Kanban.Drag) (drop: Kanban.Msg -> unit) (dispatch: Kanban.Msg -> unit) =
    // Let go, but not yet put down: the drop may be waiting for the transition before it.
    let released = ref false

    let release msg =
        released.Value <- true
        (if drag.Moving then drop else dispatch) msg

    let ours (event: Event) =
        not released.Value && (event :?> PointerEvent).pointerId = drag.Pointer

    let move (event: Event) =
        if ours event then
            let event = event :?> PointerEvent
            dispatch (Kanban.Point(event.clientX, event.clientY, target drag.Card event.clientX event.clientY))

    let up (event: Event) = if ours event then release Kanban.Drop
    let cancel (event: Event) = if ours event then release Kanban.Cancel

    let key (event: Event) =
        if not released.Value && (event :?> KeyboardEvent).key = "Escape" then
            release Kanban.Cancel

    let listeners = [ "pointermove", move; "pointerup", up; "pointercancel", cancel; "keydown", key ]

    for name, listener in listeners do
        document.addEventListener (name, listener)

    { new System.IDisposable with
        member _.Dispose() =
            for name, listener in listeners do
                document.removeEventListener (name, listener) }
