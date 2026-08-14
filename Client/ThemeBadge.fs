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
/// It is also the one card on the page the server did not render. There is nothing to
/// hydrate: the markup did not exist until this ran.
module ThemeBadge

open Lit
open LitStore

[<LitElement("bfb-theme-badge")>]
let ThemeBadge () =
    LitElement.init (fun config ->
        config.styles <-
            [ css
                  $"""
                  /* Same crossing as the palette: rules stop at this boundary, custom
                     properties do not, so the badge is themed by the page it is sealed
                     against. */
                  :host {{ display: block; margin-bottom: 1rem; }}
                  .card {{ border: 1px solid var(--line); background: var(--card);
                           border-radius: 10px; padding: 1rem 1.25rem; max-width: 30rem;
                           font: 15px/1.5 system-ui; color: var(--ink); }}
                  h2 {{ margin: 0 0 .5rem; font-size: 1.05rem; }}
                  code {{ background: var(--field); padding: .1rem .3rem; border-radius: 4px; }}
                  button {{ font: inherit; padding: .25rem .7rem; color: inherit;
                            background: var(--field); border: 1px solid var(--line);
                            border-radius: 6px; }}
                  """ ])
    |> ignore

    // The same store the two islands read. It was filled from the page before any element
    // on it was upgraded, so this component's very first render already has the server's
    // answer -- no default to flash, no fetch to await.
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
    html
        $"""<section class="card">
              <h2>A component, not an island</h2>
              <p>The server sent an empty <code>&lt;bfb-theme-badge&gt;</code> and nothing
                 inside it. This is <b class="theme">{Theme.name theme}</b> because the
                 store already knew.</p>
              <p>It can write to the store as well as read it, and the two islands above
                 follow &mdash; the same way this one follows them.</p>
              <button @click={Ev(fun _ -> ThemeStore.dispatch Theme.Toggle)}>
                switch to {if theme.Dark then "light" else "dark"}
              </button>
              <p>Remove it and put it back &mdash; <code>document.querySelector("bfb-theme-badge").remove()</code>
                 &mdash; and it unsubscribes and resubscribes on its own. That is what a
                 component gets for free and an island needs
                 <code>Lit.trackConnection</code> for.</p>
            </section>"""

/// Nothing in F# ever calls a custom element: it is asked for by tag name, from HTML no
/// bundler reads. Without a reference from `App.fs` this module is never imported, never
/// evaluated, and the element is never defined -- so the page would show an empty tag and
/// no error anywhere.
let register () = ()
