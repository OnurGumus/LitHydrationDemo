/// Starts one Elmish program per component, each adopting the markup the server sent.
module App

open Browser
open Elmish
open Fable
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
Program.mkProgram Views.Counter.init Views.Counter.update (fun model dispatch ->
    Views.Counter.view model (ViewTransitions.dispatch "counter" dispatch))
|> Program.withLitHydrated "counter"
|> Program.run

Program.mkProgram Views.Basket.init Views.Basket.update (fun model dispatch ->
    Views.Basket.view model (ViewTransitions.dispatch "basket" dispatch))
|> Program.withLitHydrated "basket"
|> Program.run

// The third mounts on the shadow root rather than on the element: the root is where the
// server's markers are, and hydrating the host would leave lit hunting for a part that
// is not in the tree it was given.
Program.mkProgram Views.Palette.init Views.Palette.update (fun model dispatch ->
    Views.Palette.view model (ViewTransitions.dispatch "palette" dispatch))
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
Program.mkProgram Views.Panel.init Views.Panel.update (fun model dispatch ->
    Views.Panel.view model (ViewTransitions.dispatch "panel" dispatch))
|> Program.withLitHydrated "panel"
|> Program.run

// The two islands that share a store.
//
// No program each: the loop is in the store, and these are views onto it. Attaching them
// happens here rather than in the store, because a store has no view and should not know
// that anything renders.
//
// Both start from the value the store read out of the page, which is the value their
// markup was rendered from, so both adopt rather than rebuild.
let private mount (id: string) (view: Theme.Model -> (Theme.Msg -> unit) -> TemplateResult) =
    let el = document.getElementById id

    if isNull el then
        failwith $"Cannot find element with id {id}"

    // Subscribing hands back the current value and reports every later one to the
    // callback, so the first render is the adoption and the rest are ordinary renders.
    let dispatch = ViewTransitions.dispatch "theme" ThemeStore.dispatch
    let current, _ =
        ThemeStore.store
        |> Store.subscribeImmediate (fun model -> Lit.render el (view model dispatch))

    Hydrate.adopt el (view current dispatch)

mount "theme-switch" Theme.switch
mount "theme-reader" (fun model _ -> Theme.reader model)

// And the same store read by something that is not an island at all. Defining the element
// is the whole of it: the browser finds the tag the server sent, upgrades it, and the
// component subscribes and unsubscribes with its own connection.
//
// The server drew this one as well, into a shadow root inside its tag. The first line is
// what makes the component take that root over when it is defined, rather than draw a
// second copy beside it. It is a call and not an import, so where it comes is of no
// consequence, as long as it is while this file is still starting things up.
Hydrate.elements ()
ThemeBadge.register ()

// What a theme actually has to do, which is neither island's business and certainly not
// the store's: paint the page, and remember the choice so the *server* can paint it next
// time. A subscriber, in the file where this app touches the document.
ThemeStore.store
|> Store.subscribeImmediate (fun model ->
    document.documentElement.setAttribute ("data-theme", Theme.name model)
    document.cookie <- $"{Theme.Cookie}={Theme.name model}; path=/; max-age=31536000")
|> ignore
