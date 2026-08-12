/// Starts one Elmish program per component, each adopting the markup the server sent.
module App

open Elmish
open Lit.Elmish

// Two independent loops on one page. Neither knows about the other; each adopts the
// container its own markers were written into, and renders normally from then on.
Program.mkProgram Views.Counter.init Views.Counter.update Views.Counter.view
|> Program.withLitHydrated "counter"
|> Program.run

Program.mkProgram Views.Basket.init Views.Basket.update Views.Basket.view
|> Program.withLitHydrated "basket"
|> Program.run
