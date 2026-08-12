/// Takes over the page the server rendered.
module App

open Browser
open Lit

/// The element the server rendered into. It must be the one that contains lit's
/// markers, not the card inside it.
let private container = document.getElementById "app" :> Browser.Types.Element

let mutable private count = 0

let rec private bump () =
    count <- count + 1
    Lit.render container (Views.page count bump)

// Adopts the server's DOM if the markup matches, and renders over it if not.
Hydrate.adopt container (Views.page count bump)
