/// The one thing the browser cannot work out for itself.
///
/// Every other component here starts from an `init` both sides can run, which is why
/// none of them needs anything shipped alongside the markup. This one starts from
/// something only the server knows -- which warehouse, how many bays it has -- so the
/// server has to say, and both islands have to hear the same answer or the page
/// disagrees with itself before anyone has clicked anything.
///
/// Deliberately flat and made of primitives. It crosses as JSON and comes back through
/// `unbox`, which is honest for a record of strings and numbers and wrong the moment a
/// union or an option appears -- at which point this wants a real codec, one built from
/// the same source on both sides, rather than a cast.
module Session

open Lit

type Session =
    { Warehouse: string
      Bays: int
      Reserved: int
      /// Empty when nobody is signed in. A string rather than an option, because this
      /// record crosses as JSON and comes back through `unbox`: an option would arrive
      /// as null or a value and neither is what F# expects an option to look like.
      SignedInAs: string }

/// Where the server leaves it and the client looks for it.
[<Literal>]
let PayloadId = "bfb-session"

let free (session: Session) = session.Bays - session.Reserved

let signedIn (session: Session) = session.SignedInAs <> ""

/// The loop, which lives in the store rather than in either island: one update, one
/// dispatch, and two views that read the result. Written here because the server compiles
/// this file too and renders from the same starting value -- but note that the store's
/// own `Cmd` stays on the client, which is why this update returns a model and nothing
/// else. Effects here would be the client's business.
type Msg = Reserve

let update msg session =
    match msg with
    | Reserve ->
        if signedIn session && free session > 0 then
            { session with Reserved = session.Reserved + 1 }
        else
            session

/// One of the two views onto it. Reserving is not done here: the island says what
/// happened, the store decides, and both islands hear about it.
let bays (session: Session) (dispatch: Msg -> unit) =
    // Signed out, the button is there and refuses -- the same state the server rendered,
    // rather than a button that appears a moment after the page does.
    let stopped = not (signedIn session) || free session = 0

    // Bound above rather than inline: a triple-quoted string cannot appear inside an
    // interpolation hole of another one.
    let hint =
        if signedIn session then
            Lit.nothing
        else
            html $"""<p class="hint">Sign in to reserve.</p>"""

    html
        $"""<section class="card">
              <h2>Bays</h2>
              <p><b class="reserved">{session.Reserved}</b> of <b class="total">{session.Bays}</b> reserved,
                 <b class="free">{free session}</b> free.</p>
              <button ?disabled={stopped} @click={Ev(fun _ -> dispatch Reserve)}>reserve one</button>
              {hint}
            </section>"""

/// The other, showing the same numbers from somewhere else on the page. Nothing here
/// knows about the card above; both read the store, and the store was told once.
let summary (session: Session) =
    html
        $"""<section class="card">
              <h2>Summary</h2>
              <p>{session.Warehouse} is <b class="usage">{session.Reserved * 100 / session.Bays}%%</b> reserved.</p>
            </section>"""

/// Not an island: a form the server renders and the browser posts. It works with
/// JavaScript switched off, which is the point -- signing in is a navigation, not a
/// state change some script has to be present to make.
let account (session: Session) =
    if signedIn session then
        html
            $"""<form method="post" action="/logout" class="account">
                  <span>Signed in as <b class="who">{session.SignedInAs}</b></span>
                  <button type="submit">sign out</button>
                </form>"""
    else
        html
            $"""<form method="post" action="/login" class="account">
                  <label>Name <input name="name" value="pat" required></label>
                  <button type="submit">sign in</button>
                </form>"""
