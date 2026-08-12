/// Starts one Elmish program per component, each adopting the markup the server sent.
module App

open Elmish
open Lit
open Lit.Elmish

// Under `npm run dev`, a change to this file or to the views it uses re-runs this module
// instead of reloading the page. Everything below then happens a second time, which is
// exactly what Program.withLitHydrated is prepared for: it finds the program the last
// run left on the element, stops it, and starts this one from the model it had reached.
//
// Compiled out of a release build, and inert with no dev server behind the page.
HMR.acceptSelf()

// Two independent loops on one page. Neither knows about the other; each adopts the
// container its own markers were written into, and renders normally from then on.
Program.mkProgram Views.Counter.init Views.Counter.update Views.Counter.view
|> Program.withLitHydrated "counter"
|> Program.run

Program.mkProgram Views.Basket.init Views.Basket.update Views.Basket.view
|> Program.withLitHydrated "basket"
|> Program.run
