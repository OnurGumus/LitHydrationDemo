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
      Reserved: int }

/// Where the server leaves it and the client looks for it.
[<Literal>]
let PayloadId = "bfb-session"

let free (session: Session) = session.Bays - session.Reserved

/// One of the two views onto it. Reserving is not done here: the island says what
/// happened, the store decides, and both islands hear about it.
let bays (session: Session) (onReserve: unit -> unit) =
    html
        $"""<section class="card">
              <h2>Bays</h2>
              <p><b class="reserved">{session.Reserved}</b> of <b class="total">{session.Bays}</b> reserved,
                 <b class="free">{free session}</b> free.</p>
              <button ?disabled={free session = 0} @click={Ev(fun _ -> onReserve ())}>reserve one</button>
            </section>"""

/// The other, showing the same numbers from somewhere else on the page. Nothing here
/// knows about the card above; both read the store, and the store was told once.
let summary (session: Session) =
    html
        $"""<section class="card">
              <h2>Summary</h2>
              <p>{session.Warehouse} is <b class="usage">{session.Reserved * 100 / session.Bays}%%</b> reserved.</p>
            </section>"""
