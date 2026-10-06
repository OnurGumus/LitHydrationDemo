/// The one thing the browser must not be left to work out for itself.
///
/// A theme is the classic case: decide it in the browser and the page arrives in the
/// wrong colours and corrects itself a moment later, in front of the reader. Decide it on
/// the server and the first byte is already right — but then the server has to know the
/// preference, and both islands that show it have to start from the same answer.
///
/// A record of primitives on purpose: it crosses to the browser as JSON and comes back
/// through `unbox`, which is honest for a bool and would be wrong for a union. `Dark` of
/// bool rather than `Light | Dark` is that constraint showing.
module Theme

open Lit

type Model = { Dark: bool }

/// Where the server leaves the state, and the client looks for it.
[<Literal>]
let PayloadId = "bfb-theme"

/// The cookie the server reads it from, and the browser writes it back to.
[<Literal>]
let Cookie = "bfb-theme"

let name (model: Model) = if model.Dark then "dark" else "light"

type Msg = Toggle

let update msg model =
    match msg with
    | Toggle -> { model with Dark = not model.Dark }

/// The island with the button: the one that changes the theme.
let switch (model: Model) (dispatch: Msg -> unit) =
    html
        $"""<section class="card">
              <h2>Theme</h2>
              <p>The page is <b class="theme">{name model}</b>, and was already when it arrived.</p>
              <button @click={Ev(fun _ -> dispatch Toggle)}>
                switch to {if model.Dark then "light" else "dark"}
              </button>
            </section>"""

/// The island with no button, further down the page. It is never told that the other one
/// was clicked -- it reads the same store, and that is the whole demonstration.
let reader (model: Model) =
    html
        $"""<section class="card">
              <h2>Elsewhere on the page</h2>
              <p>This card was not told about the switch. It reads the same store, so it
                 says <b class="theme">{name model}</b> too.</p>
            </section>"""

/// The stylesheet of the badge, which is a component and not an island: its lifecycle
/// and its place in the store are in Client/ThemeBadge.fs.
///
/// What it draws is here so that the server can draw it as well, into a shadow root
/// inside the component's own tag, which the component then takes over. A string rather
/// than `css`, because the server has no such thing: it writes this into a style
/// element, and the component hands the same text to lit.
let badgeStyles =
    """
    /* Same crossing as the palette: rules stop at this boundary, custom properties do
       not, so the badge is themed by the page it is sealed against. */
    :host { display: block; margin-bottom: 1rem; }
    .card { border: 1px solid var(--line); background: var(--card);
            border-radius: 10px; padding: 1rem 1.25rem; max-width: 30rem;
            font: 15px/1.5 system-ui; color: var(--ink); }
    h2 { margin: 0 0 .5rem; font-size: 1.05rem; }
    code { background: var(--field); padding: .1rem .3rem; border-radius: 4px; }
    button { font: inherit; padding: .25rem .7rem; color: inherit;
             background: var(--field); border: 1px solid var(--line);
             border-radius: 6px; }
    """

/// The badge itself. The same function on both sides, which is the whole of the
/// contract: the component's first render has to be the one the server made.
let badge (model: Model) (dispatch: Msg -> unit) =
    html
        $"""<section class="card">
              <h2>A component, not an island</h2>
              <p>The server drew this too, inside the shadow root of
                 <code>&lt;bfb-theme-badge&gt;</code>, and the component took it over. It
                 says <b class="theme">{name model}</b> because the server and the store
                 agree.</p>
              <p>It can write to the store as well as read it, and the two islands above
                 follow &mdash; the same way this one follows them.</p>
              <button @click={Ev(fun _ -> dispatch Toggle)}>
                switch to {if model.Dark then "light" else "dark"}
              </button>
              <p>Remove it and put it back &mdash; <code>document.querySelector("bfb-theme-badge").remove()</code>
                 &mdash; and it unsubscribes and resubscribes on its own. That is what a
                 component gets for free and an island needs
                 <code>Lit.trackConnection</code> for.</p>
            </section>"""
