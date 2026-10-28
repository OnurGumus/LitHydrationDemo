/// Picking a card up and putting it down. Browser only: the board's shared view is
/// handed `grab` and never sees any of this.
///
/// While the pointer moves, only the card's transform changes: no message, no render, no
/// transition. The program hears about a drag once, when it is dropped. The card stays
/// where it was dropped until `release`, which the drop's own update calls, so the
/// transition's first snapshot has it under the pointer and its last in its lane, and
/// the browser draws the glide between them.
module Drag

open Fable.Core
open Browser
open Browser.Types
open Lit

/// A press on a card, from pointer-down to pointer-up. It is a drag once it has moved.
type private Press =
    { Card: HTMLElement
      Pointer: float
      X: float
      Y: float
      Drop: string -> int -> unit
      mutable Moving: bool }

// A press is a drag only once it has moved this far, so a click stays a click.
let private threshold = 4.0

let mutable private pressed: Press option = None

/// The card a drop has left under the pointer, until `release` puts it down.
let mutable private lifted: HTMLElement option = None

// While a dropped card glides into its lane it passes the lane's other cards, and it
// should pass over them. Its picture in the transition is found by its name, not by its
// element: lit may draw a different card into the element it left.
let private onTop = document.head.appendChild (document.createElement "style")

[<Emit("document.elementsFromPoint($0, $1)")>]
let private elementsAt (x: float) (y: float) : Element array = jsNative

let private clearOver () =
    let lanes = document.querySelectorAll "[data-over]"

    for i in 0 .. lanes.length - 1 do
        lanes.[i].removeAttribute "data-over"

/// The lane under the pointer. The card itself is under the pointer too, and is still
/// in the lane it came from, so it does not count.
let private laneAt (card: HTMLElement) x y =
    elementsAt x y
    |> Array.tryPick (fun element ->
        if card.contains element then None else element.closest "[data-lane]")

/// How many of the lane's other cards have their middle above the pointer.
let private dropIndex (lane: Element) (card: HTMLElement) y =
    let cards = lane.querySelectorAll "[data-card]"

    [ for i in 0 .. cards.length - 1 -> cards.[i] ]
    |> List.filter (fun other -> not (obj.ReferenceEquals(other, card)))
    |> List.filter (fun other ->
        let box = other.getBoundingClientRect ()
        box.top + box.height / 2.0 < y)
    |> List.length

/// Back where it came from, and the program never hears of it.
let private settle (card: HTMLElement) =
    clearOver ()
    card.style.transition <- "transform 200ms ease"
    card.style.removeProperty "transform" |> ignore

    window.setTimeout (
        (fun () ->
            card.style.removeProperty "transition" |> ignore
            card.removeAttribute "data-dragging"),
        200
    )
    |> ignore

let private move (event: Event) =
    let event = event :?> PointerEvent

    match pressed with
    | Some press when event.pointerId = press.Pointer ->
        let dx = event.clientX - press.X
        let dy = event.clientY - press.Y

        if not press.Moving && sqrt (dx * dx + dy * dy) >= threshold then
            press.Moving <- true
            press.Card.setAttribute ("data-dragging", "")

        if press.Moving then
            press.Card.style.transform <- $"translate({dx}px, {dy}px) rotate(2deg)"
            clearOver ()

            laneAt press.Card event.clientX event.clientY
            |> Option.iter (fun lane -> lane.setAttribute ("data-over", ""))
    | _ -> ()

let private up (event: Event) =
    let event = event :?> PointerEvent

    match pressed with
    | Some press when event.pointerId = press.Pointer ->
        pressed <- None

        if press.Moving then
            match laneAt press.Card event.clientX event.clientY with
            | Some lane ->
                let name = (window.getComputedStyle press.Card).getPropertyValue("--card-transition").Trim()
                onTop.textContent <- $"::view-transition-group({name}) {{ z-index: 1; }}"
                lifted <- Some press.Card
                press.Drop (lane.getAttribute "data-lane") (dropIndex lane press.Card event.clientY)
            | None -> settle press.Card
    | _ -> ()

let private cancel () =
    match pressed with
    | Some press ->
        pressed <- None
        press.Card.releasePointerCapture press.Pointer
        if press.Moving then settle press.Card
    | None -> ()

// On the document, and listening for good: a press is captured to its card, and its
// events bubble up here. With nothing pressed, each of these does nothing.
document.addEventListener ("pointermove", move)
document.addEventListener ("pointerup", up)
document.addEventListener ("pointercancel", fun _ -> cancel ())
document.addEventListener ("keydown", fun event -> if (event :?> KeyboardEvent).key = "Escape" then cancel ())

let private start drop (event: PointerEvent) =
    let onButton = (event.target :?> Element).closest "button" |> Option.isSome

    // Nothing is picked up while a dropped card is still waiting to be put down.
    if event.button = 0 && not onButton && pressed.IsNone && lifted.IsNone then
        let card = event.currentTarget :?> HTMLElement
        card.setPointerCapture event.pointerId

        pressed <-
            Some
                { Card = card
                  Pointer = event.pointerId
                  X = event.clientX
                  Y = event.clientY
                  Drop = drop
                  Moving = false }

/// The handler a card is picked up with. `drop` hears the lane it lands in, and how
/// many of that lane's other cards are above it.
let grab (drop: string -> int -> unit) = Ev(fun (event: PointerEvent) -> start drop event)

/// Puts a dropped card down in its lane. Call it in the update the drop dispatched.
let release () =
    lifted
    |> Option.iter (fun card ->
        card.removeAttribute "data-dragging"
        card.style.removeProperty "transform" |> ignore)

    lifted <- None
    clearOver ()
