/// Two components, each with its own model, update and view.
///
/// This file is compiled twice: by the .NET compiler against Lit.Server, and by Fable
/// against the lit bindings. Elmish itself is ordinary F# and compiles on both, so the
/// server can run the same `init` the browser will and render from the same model —
/// which is the contract hydration rests on.
module Views

open Elmish
open Lit

/// A counter. About as small as a component gets.
module Counter =

    type Model = { Count: int }

    type Msg =
        | Increment
        | Decrement

    let init () = { Count = 0 }, Cmd.none

    let update msg model =
        match msg with
        | Increment -> { model with Count = model.Count + 1 }, Cmd.none
        | Decrement -> { model with Count = model.Count - 1 }, Cmd.none

    let view model dispatch =
        html
            $"""<section class="card">
                  <h2>Counter</h2>
                  <p>Value: <b class="value">{model.Count}</b></p>
                  <button @click={Ev(fun _ -> dispatch Decrement)}>&minus;</button>
                  <button @click={Ev(fun _ -> dispatch Increment)}>+</button>
                </section>"""

/// A list you can take things out of, with a total that follows.
module Basket =

    type Item = { Name: string; Qty: int }

    type Model = { Items: Item list }

    type Msg = Remove of string

    let init () =
        { Items =
            [ { Name = "Crate"; Qty = 40 }
              { Name = "Pallet"; Qty = 2 }
              { Name = "Drum"; Qty = 7 } ] },
        Cmd.none

    let update msg model =
        match msg with
        | Remove name ->
            { model with Items = model.Items |> List.filter (fun i -> i.Name <> name) }, Cmd.none

    let private row dispatch (item: Item) =
        html
            $"""<tr>
                  <td>{item.Name}</td>
                  <td>{item.Qty}</td>
                  <td><button @click={Ev(fun _ -> dispatch (Remove item.Name))}>remove</button></td>
                </tr>"""

    let view model dispatch =
        let total = model.Items |> List.sumBy (fun i -> i.Qty)

        html
            $"""<section class="card">
                  <h2>Basket</h2>
                  <table><tbody>{Lit.ofList (model.Items |> List.map (row dispatch))}</tbody></table>
                  <p>Total: <b class="total">{total}</b> in <b class="lines">{model.Items.Length}</b> lines.</p>
                </section>"""
