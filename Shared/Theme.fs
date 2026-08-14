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
