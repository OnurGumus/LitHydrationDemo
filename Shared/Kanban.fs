/// A board of cards in three lanes, which you can drag between.
///
/// Compiled twice like Views.fs, and adopted the same way: the server draws the board
/// from `init`, still, and the browser adopts it. Everything that moves is the browser's
/// alone. Cards glide between lanes, and come and go, on the page's View Transitions,
/// which is CSS. Dragging is browser code too, and the view never sees it: it is handed
/// `grab`, which makes the handler a card is picked up with. The browser hands it
/// Client/Drag.fs; the server hands it one that does nothing, because the server drops
/// handlers anyway.
module Kanban

open Elmish
open Lit

type Lane =
    | Backlog
    | InProgress
    | Shipped

type Card = { Id: int; Title: string; Lane: Lane }

/// `History` is the earlier boards, newest first, so undo is taking the head.
type Model =
    { Cards: Card list
      NextId: int
      History: Card list list }

type Msg =
    | Add of Lane
    | Remove of int
    /// A card's arrow: one lane left (-1) or right (+1).
    | Step of int * int
    /// A drop: the card, the lane, and how many of that lane's cards it lands after.
    | Move of int * Lane * int
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
      History = [] },
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
    | Move(id, lane, index) -> commit model (place id lane index model.Cards), Cmd.none
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
        | previous :: earlier -> { model with Cards = previous; History = earlier }, Cmd.none
        | [] -> model, Cmd.none

/// `--card-transition` is the card's name while the board is animating: one name per
/// card, so the browser can follow each one to wherever it went.
let private cardView grab dispatch (card: Card) =
    let position = List.findIndex ((=) card.Lane) lanes

    html
        $"""<article class="kcard" data-card={card.Id} style={"--card-transition: kanban-" + string card.Id}
                     @pointerdown={grab card.Id}>
              <header>
                <span class="kid">#{card.Id}</span>
                <button class="icon" aria-label="Remove" @click={Ev(fun _ -> dispatch (Remove card.Id))}>&times;</button>
              </header>
              <p>{card.Title}</p>
              <footer>
                <button class="icon" aria-label="Move left" ?disabled={position = 0}
                        @click={Ev(fun _ -> dispatch (Step(card.Id, -1)))}>&larr;</button>
                <button class="icon" aria-label="Move right" ?disabled={position = lanes.Length - 1}
                        @click={Ev(fun _ -> dispatch (Step(card.Id, 1)))}>&rarr;</button>
              </footer>
            </article>"""

let private laneView grab dispatch cards lane =
    let inLane = cards |> List.filter (fun c -> c.Lane = lane)

    html
        $"""<section class={"lane " + laneKey lane} data-lane={laneKey lane}>
              <header>
                <h3>{laneName lane}</h3>
                <span class="count">{inLane.Length}</span>
                <button class="icon" aria-label={"Add to " + laneName lane}
                        @click={Ev(fun _ -> dispatch (Add lane))}>+</button>
              </header>
              {Lit.ofList (inLane |> List.map (cardView grab dispatch))}
            </section>"""

/// `grab` makes the handler a card is picked up with, given the card's id.
let view (grab: int -> EvHandler) model dispatch =
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
              <div class="lanes">{Lit.ofList (lanes |> List.map (laneView grab dispatch model.Cards))}</div>
            </section>"""
