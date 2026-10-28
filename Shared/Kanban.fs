/// A board of cards in three lanes, which you can drag between.
///
/// Compiled twice like Views.fs, and adopted the same way: the server draws the board
/// from `init`, still, and the browser adopts it. Everything that moves is the browser's
/// alone. Cards glide between lanes, and come and go, on the page's View Transitions,
/// which is CSS. Each card is a component, `<bfb-kcard>` (Client/KCard.fs), and a drag
/// is the card's own business: it follows the pointer, and tells the board two things,
/// the lane it is over and where it was dropped. The board keeps the cards; the cards
/// keep their gestures. The server draws every card's shadow root from `CardView`
/// below, and the browser's cards take them over.
module Kanban

open Elmish
open Lit

type Lane =
    | Backlog
    | InProgress
    | Shipped

type Card = { Id: int; Title: string; Lane: Lane }

/// `History` is the earlier boards, newest first, so undo is taking the head.
/// `Over` is the lane a dragged card is over, and `Dropped` the card that landed last,
/// while it is landing.
type Model =
    { Cards: Card list
      NextId: int
      History: Card list list
      Over: Lane option
      Dropped: int option }

type Msg =
    | Add of Lane
    | Remove of int
    /// A card's arrow: one lane left (-1) or right (+1).
    | Step of int * int
    /// The lane a dragged card is over, if any.
    | Over of Lane option
    /// A dragged card, and where it landed: a lane and how many of that lane's other
    /// cards are above it, or nowhere, and it goes back.
    | Drop of int * (Lane * int) option
    | Shuffle
    | Clear
    | Undo

let lanes = [ Backlog; InProgress; Shipped ]

/// How a lane is written into the page, and read back from it by a drop.
let laneKey =
    function
    | Backlog -> "backlog"
    | InProgress -> "progress"
    | Shipped -> "shipped"

let laneOf key =
    lanes |> List.find (fun lane -> laneKey lane = key)

let private laneName =
    function
    | Backlog -> "Backlog"
    | InProgress -> "In progress"
    | Shipped -> "Shipped"

let private titles =
    [| "Sketch the board"
       "Wire the reducer"
       "Render on the server"
       "Measure the bundle"
       "Hydrate the story"
       "Name the transitions"
       "Write the lesson"
       "Ship it" |]

let private card id lane =
    { Id = id; Title = titles.[(id - 1) % titles.Length]; Lane = lane }

/// No randomness and no clock: the server and the browser both run this, and have to
/// get the same board.
let init () =
    { Cards = [ card 1 Backlog; card 2 Backlog; card 3 InProgress; card 4 Shipped ]
      NextId = 5
      History = []
      Over = None
      Dropped = None },
    Cmd.none

/// Only `update` shuffles, and `update` only ever runs in the browser, after the board
/// has been adopted.
let private random = System.Random()

/// Takes a card out and puts it back in `lane`, in front of the card at `index` among
/// that lane's others, or after the last of them.
let private place id lane index cards =
    match cards |> List.tryFind (fun c -> c.Id = id) with
    | None -> cards
    | Some moved ->
        let moved = { moved with Lane = lane }
        let rest = cards |> List.filter (fun c -> c.Id <> id)

        match rest |> List.filter (fun c -> c.Lane = lane) |> List.tryItem index with
        | Some next -> rest |> List.collect (fun c -> if c.Id = next.Id then [ moved; c ] else [ c ])
        | None -> rest @ [ moved ]

/// A new board, with the old one kept for undo. A change that changes nothing keeps
/// nothing, so undo never steps back to the board you are looking at.
let private commit model cards =
    let model = { model with Dropped = None }

    if cards = model.Cards then
        model
    else
        { model with
            Cards = cards
            History = List.truncate 50 (model.Cards :: model.History) }

let update msg model =
    match msg with
    | Add lane ->
        { commit model (model.Cards @ [ card model.NextId lane ]) with NextId = model.NextId + 1 }, Cmd.none
    | Remove id -> commit model (model.Cards |> List.filter (fun c -> c.Id <> id)), Cmd.none
    | Step(id, direction) ->
        match model.Cards |> List.tryFind (fun c -> c.Id = id) with
        | Some moved ->
            let target = List.findIndex ((=) moved.Lane) lanes + direction

            if target < 0 || target >= lanes.Length then
                model, Cmd.none
            else
                commit model (place id lanes.[target] System.Int32.MaxValue model.Cards), Cmd.none
        | None -> model, Cmd.none
    | Over lane -> { model with Over = lane }, Cmd.none
    | Drop(id, target) ->
        let cards =
            match target with
            | Some(lane, index) -> place id lane index model.Cards
            | None -> model.Cards

        // The one card that lands over the others as it glides: the stylesheet lifts a
        // card with `data-dropped` above the rest of the transition.
        { commit model cards with Over = None; Dropped = Some id }, Cmd.none
    | Shuffle ->
        model.Cards
        |> List.map (fun c -> random.Next(), { c with Lane = lanes.[random.Next lanes.Length] })
        |> List.sortBy fst
        |> List.map snd
        |> commit model,
        Cmd.none
    | Clear -> commit model [], Cmd.none
    | Undo ->
        match model.History with
        | previous :: earlier -> { model with Cards = previous; History = earlier; Dropped = None }, Cmd.none
        | [] -> model, Cmd.none

/// What a card draws inside its shadow root: the same on the server, which writes it
/// into the card's tag, and in Client/KCard.fs, which takes it over.
module CardView =

    /// A string, because the server writes it into a style element in the card's root,
    /// and Client/KCard.fs hands the same text to lit. The page's rules stop at the root;
    /// its custom properties, the lane's `--accent` among them, come in.
    let styles =
        """
        :host { display: block; position: relative; margin-bottom: .5rem; }
        article { background: var(--card); border: 1px solid var(--line); border-left: 3px solid var(--accent);
                  border-radius: 8px; padding: .4rem .5rem .3rem .75rem;
                  cursor: grab; touch-action: none; user-select: none; }
        header, footer { display: flex; justify-content: space-between; align-items: center; }
        footer { justify-content: flex-end; }
        .kid { font-size: .75rem; color: var(--quiet); }
        p { margin: .1rem 0; }
        button { font: inherit; padding: 0 .45rem; color: var(--quiet); background: none;
                 border: 1px solid transparent; border-radius: 6px; cursor: pointer; }
        button:hover { border-color: var(--ink); }
        button:focus-visible { outline: 2px solid var(--ink); outline-offset: 3px; }
        button:disabled { opacity: .4; cursor: default; }
        button:disabled:hover { border-color: transparent; }
        """

    /// Everything a card does with the pointer and its buttons. Only the browser's card
    /// has real ones; the server's do nothing, because the server drops handlers anyway.
    type Hands =
        { Press: EvHandler
          Move: EvHandler
          Up: EvHandler
          Cancel: EvHandler
          Remove: EvHandler
          Left: EvHandler
          Right: EvHandler }

    let still =
        { Press = Ev ignore
          Move = Ev ignore
          Up = Ev ignore
          Cancel = Ev ignore
          Remove = Ev ignore
          Left = Ev ignore
          Right = Ev ignore }

    /// A dragged card, `dx` and `dy` from where it was pressed. The transition follows
    /// the card's tag, so it is the tag, the shadow root's host, that moves; this is a
    /// second stylesheet in the root that says so, and is only there during a drag.
    let private lifted (dx: float, dy: float) =
        let rules =
            $":host {{ z-index: 2; transform: translate({dx}px, {dy}px) rotate(2deg); }} "
            + "article { cursor: grabbing; box-shadow: 0 12px 28px rgb(0 0 0 / .25); }"

        html $"""<style>{rules}</style>"""

    /// `offset` is how far a dragged card has moved, or `None` when it is not being
    /// dragged, which is always the case on the server.
    let view (card: Card) (offset: (float * float) option) (hands: Hands) =
        let position = List.findIndex ((=) card.Lane) lanes
        let drag = offset |> Option.map lifted |> Option.defaultValue Lit.nothing

        html
            $"""{drag}<article @pointerdown={hands.Press} @pointermove={hands.Move}
                         @pointerup={hands.Up} @pointercancel={hands.Cancel}>
                  <header>
                    <span class="kid">#{card.Id}</span>
                    <button aria-label="Remove" @click={hands.Remove}>&times;</button>
                  </header>
                  <p>{card.Title}</p>
                  <footer>
                    <button aria-label="Move left" ?disabled={position = 0} @click={hands.Left}>&larr;</button>
                    <button aria-label="Move right" ?disabled={position = lanes.Length - 1}
                            @click={hands.Right}>&rarr;</button>
                  </footer>
                </article>"""

    /// The card's shadow root, for the board's view: what the server writes into the
    /// card's tag, and `Lit.nothing` in the browser.
    let drawn card = Lit.shadowRoot styles (view card None still)

/// `--card-transition` is the card's name while the board is animating: one name per
/// card, so the browser can follow each one to wherever it went.
///
/// The card is handed itself and `send`, the program's own dispatch: it puts its
/// messages through the transition queue itself, because a drop has to put the card down
/// inside the same transition.
let private cardView send dropped (card: Card) =
    html
        $"""<bfb-kcard class="kcard" data-card={card.Id} style={"--card-transition: kanban-" + string card.Id}
                       ?data-dropped={dropped = Some card.Id} .card={card} .send={send}>{CardView.drawn card}</bfb-kcard>"""

let private laneView send model dispatch lane =
    let inLane = model.Cards |> List.filter (fun c -> c.Lane = lane)
    let over = model.Over = Some lane

    html
        $"""<section class={"lane " + laneKey lane} data-lane={laneKey lane} ?data-over={over}>
              <header>
                <h3>{laneName lane}</h3>
                <span class="count">{inLane.Length}</span>
                <button class="icon" aria-label={"Add to " + laneName lane}
                        @click={Ev(fun _ -> dispatch (Add lane))}>+</button>
              </header>
              {Lit.ofList (inLane |> List.map (cardView send model.Dropped))}
            </section>"""

/// `send` is what the cards are handed: the program's own dispatch, where `dispatch`
/// is the one the toolbar's buttons go through.
let view (send: Msg -> unit) model dispatch =
    html
        $"""<section class="card board">
              <h2>Kanban</h2>
              <p>Drag a card to another lane, or use its arrows. The cards glide, arrive and
                 leave on the same View Transitions as the basket rows.</p>
              <div class="toolbar">
                <button @click={Ev(fun _ -> dispatch (Add Backlog))}>add card</button>
                <button @click={Ev(fun _ -> dispatch Shuffle)}>shuffle</button>
                <button @click={Ev(fun _ -> dispatch Clear)}>clear</button>
                <button ?disabled={List.isEmpty model.History} @click={Ev(fun _ -> dispatch Undo)}>undo</button>
                <span class="tally"><b class="cards">{model.Cards.Length}</b> cards</span>
              </div>
              <div class="lanes">{Lit.ofList (lanes |> List.map (laneView send model dispatch))}</div>
            </section>"""
