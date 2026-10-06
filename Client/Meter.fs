/// The component inside the shelf island.
///
/// What it draws is in Shared/Views.fs, as `Views.Meter`, because the server draws it
/// too. This file is the element: a tag, a property, and those two things put to use.
///
/// A real custom element, like the badge. Unlike the badge it has no state and reads no
/// store: it draws whatever level it is handed, and it is handed it as a property, by
/// the island's view, the way any parent hands anything to a component.
///
/// That property is why it waits. The server drew this component's shadow root from a
/// level of three, and wrote `defer-hydration` on its tag because three is not in the
/// HTML: left to itself the component would start at zero and draw something else.
/// Hydrating the island takes the mark off and sets the level in the same pass, and
/// only then does the component start, and take over what the server drew.
module Meter

open Lit

[<LitElement("bfb-meter")>]
let Meter () =
    let _, props =
        LitElement.init (fun config ->
            config.styles <- [ Lit.unsafeCSS Views.Meter.styles ]
            // A property with no attribute behind it. With one, the level would be in
            // the markup and there would be nothing to wait for.
            config.props <- {| level = Prop.Of(0, attribute = "") |})

    Views.Meter.view props.level.Value

/// See ThemeBadge.register: nothing in F# calls a custom element, so something has to
/// mention this module for it to be loaded at all.
let register () = ()
