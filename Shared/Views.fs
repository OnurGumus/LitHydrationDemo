/// Views shared by the server and the browser.
///
/// This file is compiled twice: by the .NET compiler against Lit.Server, and by Fable
/// against the lit bindings. It may therefore use only what both provide -- no
/// Browser.Types, no hooks, no document.
module Views

open Lit

type Item = { Name: string; Qty: int }

/// Both sides must render from the same data, or hydration succeeds and then displays
/// values the server never sent.
let items =
    [ { Name = "Crate"; Qty = 40 }
      { Name = "Pallet"; Qty = 2 }
      { Name = "<script>alert('x')</script>"; Qty = 1 } ]

let row (item: Item) =
    html $"""<tr><td>{item.Name}</td><td>{item.Qty}</td></tr>"""

let page (count: int) (onClick: unit -> unit) =
    html
        $"""<div class="card">
              <h2>Packing list</h2>
              <table><tbody>{Lit.ofList (items |> List.map row)}</tbody></table>
              <p>Clicked <b class="count">{count}</b> times.</p>
              <button type="button" @click={Ev(fun _ -> onClick ())}>Click me</button>
            </div>"""
