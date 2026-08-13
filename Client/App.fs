/// Starts one Elmish program per component, each adopting the markup the server sent.
module App

open Browser
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

// The third mounts on the shadow root rather than on the element: the root is where the
// server's markers are, and hydrating the host would leave lit hunting for a part that
// is not in the tree it was given.
Program.mkProgram Views.Palette.init Views.Palette.update Views.Palette.view
|> Program.withLitHydratedInShadowRoot "palette"
|> Program.run

// The host is a custom element as far as the browser is concerned -- any tag with a
// dash is -- so it can be upgraded to one that reports joining and leaving the document.
// That is the only such report the platform offers, and it is the same one LitElement
// borrows for its own components.
//
// To watch it happen: open the console and remove the element,
//
//     document.querySelector("#panel").remove()
//
// then put it back with document.body.append(...) -- the messages are the browser's, not
// a poll of ours. What lit rendered inside is paused and resumed along with them.
// Shaped like an effect: what runs on arrival hands back what should be undone on
// departure, so the two halves cannot drift apart. A timer or a socket would live here;
// this only says so out loud.
//
// Held for as long as the page is. In a component this is what you would return from
// Hook.useEffectOnce, so that leaving the page stops the listening as well.
let private panelConnection =
    Lit.trackConnection (
        "bfb-panel",
        fun _ ->
            console.log "bfb-panel connected"

            { new System.IDisposable with
                member _.Dispose() = console.log "bfb-panel disconnected" }
    )

// The fourth mounts on the host, not on its shadow root: the shadow root here is a
// static frame, and what needs driving is the light content the slots display.
Program.mkProgram Views.Panel.init Views.Panel.update Views.Panel.view
|> Program.withLitHydrated "panel"
|> Program.run
