/// A component, where everything above is an island.
///
/// The difference is not cosmetic. The five cards above are markup the server wrote and
/// lit adopted: `Lit.render` into a container, driven by a program or a store
/// subscription that was set up from outside, in `App.fs`, and that nothing will ever
/// tear down -- because a `<div>` has no lifecycle to hang it on.
///
/// This is a real custom element. The browser constructs it, upgrades it, and tells it
/// when it joins and leaves the document, so `Hook.useStore` can take the subscription in
/// the initialiser and hand it to `useEffectOnce` -- set up on connect, disposed on
/// disconnect, without a line of it written here.
///
/// The server draws it all the same. What it renders is `Theme.Badge.view`, in the shared
/// file, so the server can write the same thing into this element's tag as a shadow
/// root; `Hydrate.elements` in `App.fs` is what makes the component take that root over
/// when it is defined, rather than draw a second copy beside it.
module ThemeBadge

open Lit
open LitStore

[<LitElement("bfb-theme-badge")>]
let ThemeBadge () =
    LitElement.init (fun config -> config.styles <- [ Lit.unsafeCSS Theme.Badge.styles ])
    |> ignore

    // The same store the two islands read. It was filled from the page before any element
    // on it was upgraded, so this component's very first render already has the server's
    // answer -- no default to flash, no fetch to await. It is also what lets it adopt:
    // the first render has to be the one the server made.
    let theme = Hook.useStore ThemeStore.store

    // Writing does not come from the hook, and does not need to. `dispatch` is the one
    // `makeElmish` handed back next to the store, at module level, the same function for
    // every caller -- so there is nothing about it that depends on this component and
    // nothing for a hook to hold. Reading is per-component and subscribed; writing is a
    // module you call.
    //
    // Note what does *not* happen here: no local state, no marking this component as the
    // one that changed it. The message goes to the store, the store updates, and this
    // component hears about it on the same subscription as everybody else.
    Theme.Badge.view theme (ViewTransitions.dispatch "theme" ThemeStore.dispatch)

/// Nothing in F# ever calls a custom element: it is asked for by tag name, from HTML no
/// bundler reads. Without a reference from `App.fs` this module is never imported, never
/// evaluated, and the element is never defined -- so the page would show an empty tag and
/// no error anywhere.
let register () = ()
