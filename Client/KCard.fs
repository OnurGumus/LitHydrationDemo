/// A card on the board, as a component that owns its own drag.
///
/// What it draws is in Shared/Kanban.fs, as `Kanban.CardView`, because the server draws
/// it too. This file is the element: the card and the board's dispatch, handed in as
/// properties like the meter's level, and a gesture that is nobody's business but the
/// card's until it is dropped.
///
/// While the pointer moves, only this card renders. The board hears which lane the card
/// is over when that changes, so it can light the lane, and where the card was dropped.
/// The drop goes through the transition queue, and inside it the card puts itself down
/// before it tells the board: so the transition's first picture has the card under the
/// pointer and its last has it in its lane, and the browser draws the glide between them.
module KCard

open Fable.Core
open Browser
open Browser.Types
open Lit
open Kanban

/// A press on the card, from pointer-down until the card is put down. It is a drag once
/// it has moved; it is `Released` once let go, while its drop waits for its turn.
type private Gesture =
    { Card: int
      Pointer: float
      X: float
      Y: float
      Moving: bool
      Released: bool }

// A press is a drag only once it has moved this far, so a click stays a click.
let private threshold = 4.0

[<Emit("document.elementsFromPoint($0, $1)")>]
let private elementsAt (x: float) (y: float) : Element array = jsNative

/// The lane under the pointer, and how many of its other cards have their middle above
/// it. The card itself is under the pointer too, still in the lane it came from, so it
/// does not count.
let private target (card: HTMLElement) x y =
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

        laneOf (lane.getAttribute "data-lane"), above)

[<LitElement("bfb-kcard")>]
let KCard () =
    let host, props =
        LitElement.init (fun config ->
            config.styles <- [ Lit.unsafeCSS CardView.styles ]
            // Properties with no attribute behind them, as for the meter: neither is in
            // the HTML, so the server marks the tag to wait for the board to hand them in.
            config.props <-
                {| card = Prop.Of(Unchecked.defaultof<Card>, attribute = "")
                   send = Prop.Of((ignore: Msg -> unit), attribute = "") |})

    let self = unbox<HTMLElement> host
    let card = props.card.Value
    let send = props.send.Value
    let pressed, setGesture = Hook.useState (None: Gesture option)

    // A press belongs to the card it picked up, not to this element. The board draws its
    // cards into elements by position, so a redraw while a card is held -- another card
    // dropped above it, with a second finger -- can leave this element showing a
    // different card. The press does not apply to that one, and the card it picked up
    // goes back to its place.
    let gesture =
        pressed
        |> Option.filter (fun press -> not (isNull (box card)) && press.Card = card.Id)

    let offset, setOffset = Hook.useState ((0.0, 0.0))
    // The lane last reported to the board, so a move within a lane says nothing.
    let over = Hook.useRef (None: Lane option)

    let report lane =
        if lane <> over.Value then
            over.Value <- lane
            send (Over lane)

    // A press that stopped applying may have lit a lane; nobody else will put it out.
    Hook.useEffectOnChange (
        (if isNull (box card) then 0 else card.Id),
        fun _ ->
            if gesture.IsNone && over.Value.IsSome then
                over.Value <- None
                send (Over None)
    )

    /// Puts the card down, inside the transition, and then tells the board where.
    let putDown (press: Gesture) where =
        ViewTransitions.dispatch
            "kanban"
            (fun msg ->
                setGesture None
                setOffset (0.0, 0.0)
                over.Value <- None
                send msg)
            (Drop(press.Card, where))

    let letGo where =
        match gesture with
        | Some press when press.Moving ->
            setGesture (Some { press with Released = true })
            putDown press where
        | _ -> setGesture None

    let ours (event: PointerEvent) =
        match gesture with
        | Some press when not press.Released && event.pointerId = press.Pointer -> Some press
        | _ -> None

    Hook.useEventListener (document, "keydown", fun (event: KeyboardEvent) ->
        match gesture with
        | Some press when not press.Released && event.key = "Escape" -> letGo None
        | _ -> ())

    let hands: CardView.Hands =
        { Press =
            Ev(fun (event: PointerEvent) ->
                let onButton = (event.target :?> Element).closest "button" |> Option.isSome

                if event.button = 0 && not onButton && gesture.IsNone then
                    // Everything this pointer does until it is let go comes to the card,
                    // wherever it goes.
                    (event.currentTarget :?> HTMLElement).setPointerCapture event.pointerId

                    setGesture (
                        Some
                            { Card = card.Id
                              Pointer = event.pointerId
                              X = event.clientX
                              Y = event.clientY
                              Moving = false
                              Released = false }
                    ))
          Move =
            Ev(fun (event: PointerEvent) ->
                ours event
                |> Option.iter (fun press ->
                    let dx, dy = event.clientX - press.X, event.clientY - press.Y

                    if press.Moving || sqrt (dx * dx + dy * dy) >= threshold then
                        if not press.Moving then setGesture (Some { press with Moving = true })
                        setOffset (dx, dy)
                        report (target self event.clientX event.clientY |> Option.map fst)))
          Up =
            Ev(fun (event: PointerEvent) ->
                ours event |> Option.iter (fun _ -> letGo (target self event.clientX event.clientY)))
          Cancel = Ev(fun (event: PointerEvent) -> ours event |> Option.iter (fun _ -> letGo None))
          Remove = Ev(fun _ -> ViewTransitions.dispatch "kanban" send (Remove card.Id))
          Left = Ev(fun _ -> ViewTransitions.dispatch "kanban" send (Step(card.Id, -1)))
          Right = Ev(fun _ -> ViewTransitions.dispatch "kanban" send (Step(card.Id, 1))) }

    // Only before the board has handed the card in, which a card drawn by the server
    // waits for, and one the board adds gets before it is connected.
    let lifted =
        match gesture with
        | Some press when press.Moving -> Some offset
        | _ -> None

    if isNull (box card) then Lit.nothing else CardView.view card lifted hands

/// See ThemeBadge.register: nothing in F# calls a custom element, so something has to
/// mention this module for it to be loaded at all.
let register () = ()
