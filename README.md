# Fable.Lit: server hydration + View Transitions

**[Try the live demo →](https://litdemo.novian.works)**


https://github.com/user-attachments/assets/58971b1d-48a0-4a2a-9371-1a89bdc73131


A small demo of server-side rendering in F# with [Fable](https://fable.io) and
[lit](https://lit.dev). In short:

1. You write each component **once** in F#, in the Elmish style (model, update, view).
2. ASP.NET renders it to HTML, so the page is complete before any JavaScript runs.
3. In the browser, lit **hydrates** that HTML: it keeps the DOM the server sent and
   attaches event handlers and state to it, instead of building the page a second time.
4. After that, every update is animated with the browser's View Transition API.

There is no Node on the server and no `@lit-labs/ssr`. Node is only used to bundle the
client at build time.

## Run it

You need the .NET 10 SDK and Node.js.

```bash
dotnet run --project Server
```

Then open <http://localhost:5199>. That is the whole setup: building the server also
installs the npm packages (the first time) and compiles and bundles the client.

To work on it with hot reload, see [Editing it while it runs](#editing-it-while-it-runs).

## Words used in this README

| Term | Meaning here |
|---|---|
| **Hydrate / adopt** | The browser script takes over HTML the server already rendered, rather than replacing it. |
| **Island** | One interactive region of the page: a container element whose content the server rendered and one piece of client code drives. |
| **Elmish program** | A model, an `update` function and a `view` function, running as a loop. Counter, Basket, Palette and Panel are each one. |
| **Store** | An Elmish loop that is not tied to any element, so several islands can read the same state. |
| **Shadow root** | A sealed part of the DOM with its own styles. Page styles do not reach in and its styles do not leak out. |
| **Light DOM** | The ordinary DOM, outside any shadow root. |

## What is on the page

Seven cards, each showing one more thing:

| Card | What it demonstrates |
|---|---|
| **Theme switch** | State that only the server knows in time (a cookie), shared between islands through a store. |
| **Counter** | The basic case: a server-rendered component that an Elmish program adopts. |
| **Basket** | A second, fully independent program on the same page. |
| **Palette** | The same thing inside a shadow root. |
| **Panel** | A shadow root used only as a frame, with the content slotted in from the light DOM. |
| **Theme reader** | A second island reading the same store as the theme switch. |
| **Theme badge** | A real lit component (not an island), built in the browser, reading that store too. |

The first six arrive as HTML from .NET. The badge arrives as an empty tag on purpose.

## How hydration works

### One view, compiled twice

`Shared/Views.fs` holds the components. It is compiled by two compilers:

- by .NET, against [`Lit.Server.Unofficial`](https://www.nuget.org/packages/Lit.Server.Unofficial), to render HTML on the server;
- by Fable, against [`Fable.Lit.Unofficial`](https://www.nuget.org/packages/Fable.Lit.Unofficial), to run in the browser.

Both packages provide the same `Lit` namespace, and a project references one or the
other, never both. So there is no `#if` in the views, and no template written twice in
two languages.

Elmish is plain F# with no browser code in it, so `init` and `update` compile on both
sides as well. This matters: the server can call the same `init` the browser will call,
and render from the same starting model. Nothing has to be sent as JSON.

```fsharp
// Server: run the same init the browser will
let counter, _ = Views.Counter.init ()
Page().Counter(toHydratableNode (Views.Counter.view counter ignore)).Render()
```

```fsharp
// Client: one program per component, each adopting its own container
Program.mkProgram Views.Counter.init Views.Counter.update Views.Counter.view
|> Program.withLitHydrated "counter"
|> Program.run
```

Event handlers such as `@click` are dropped on the server, because a function cannot be
written into HTML. They become real listeners when lit adopts the markup in the browser.

When the script loads, each program takes ownership of its own container. No element is
re-created, nothing is rendered twice, and the programs never touch each other's DOM.

### Where things are

| File | What is in it |
|---|---|
| `Shared/Views.fs` | The four Elmish components: Counter, Basket, Palette, Panel |
| `Shared/Theme.fs` | The theme state, its `update`, and the two views that show it |
| `Client/App.fs` | Starts everything in the browser: one program per component |
| `Client/ThemeStore.fs` | The shared theme store, and the one DOM read that starts it |
| `Client/ThemeBadge.fs` | The lit component that reads the store |
| `Client/ViewTransitions.fs` | Wraps updates in a view transition |
| `Server/Program.fs` | Minimal ASP.NET app; renders each component with `toHydratableNode` |
| `Server/page.html` | The page shell: an `HtmlTypeProvider` template with one container per component |

The server inserts each rendered view into the page as a `Node`, the type
[`HtmlTypeProvider`](https://github.com/OnurGumus/HtmlTypeProvider) templates already
accept. No HTML strings are passed around, so nothing is escaped twice.

### Checking that it really hydrated

You cannot see hydration by looking: markup that was thrown away and rebuilt looks the
same as markup that was kept. Two ways to be sure:

**Turn JavaScript off and reload.** The basket table is still there, because .NET
rendered it.

**Hold on to a DOM node and check it survives.** In the browser console:

```js
const card = document.querySelector('#basket section')
document.querySelector('#counter button:nth-of-type(2)').click()   // the counter's +
document.querySelector('#basket section') === card                 // true: untouched
document.querySelector('#basket tbody tr button').click()          // remove a row
document.querySelector('#counter .value').textContent              // unchanged
```

The counter's `<section>` and the basket's are the same DOM objects before and after:
two programs, each patching its own DOM and ignoring the other's.

If you see a console warning that starts with `lit could not adopt`, hydration failed
and the component was rendered from scratch instead. That fallback is deliberate. lit's
`hydrate` throws part way through when the markup does not match, so without
`Hydrate.adopt` catching it you would get a half-working page.

### Two rules to remember

**Hydrate the element the server rendered into.** The server wraps what it renders in
marker comments, and lit looks for them inside the element you give it. Here that means
`<div id="counter">` and `<div id="basket">`, not the cards inside them. If you hydrate
the card, lit starts inside its own marker and never finds it.

**The client must start from the same model the server rendered.** There are two ways
to break this, and only one is caught:

- A different *template* is detected, reported in the console, and rendered normally.
- A different *model* hydrates without complaint and then shows values the server never
  sent. Nothing catches this.

Here both sides call the same `init`, so the models always match. If your `init` depends
on server state, you have to send that state to the browser (see
[When the state comes from the server](#when-the-state-comes-from-the-server)).

## Shadow DOM

### Palette: a component inside a shadow root

`Palette` is an ordinary Elmish component. The only difference is that the server
delivers it inside a shadow root. It uses the same `.card` class as Counter and Basket
but looks different, because the page's stylesheet does not reach into a shadow root and
its own stylesheet does not reach out.

```fsharp
// Server: styles and markup together, inside the template
.Palette(toShadowRootNode Views.Palette.styles (Views.Palette.view palette ignore))
```

```fsharp
// Client: hydrate inside the shadow root, where the markers are
Program.mkProgram Views.Palette.init Views.Palette.update Views.Palette.view
|> Program.withLitHydratedInShadowRoot "palette"
|> Program.run
```

The server writes a `<template shadowrootmode="open">`. The HTML parser turns that into
a shadow root while it reads the page, so the element has its shadow DOM and its styles
before any script runs. With JavaScript off it is still there.

Hydration itself did not need to change. lit finds its bindings by walking comment
nodes, and comments work the same inside a shadow root. The styles sit outside the
markers, so they are neither adopted nor re-rendered.

A caveat on scope: this is server-rendered *templates* placed in a declarative shadow
root. It is not server rendering of `LitElement` components the way `@lit-labs/ssr` does
it (walking custom element tags, serialising `static styles`, ordering hydration with
`defer-hydration`). That is a much larger job and `Lit.Server` does not attempt it.

### Panel: a shadow root with slots

`Palette` puts everything inside its shadow root. `Panel` does the opposite: its shadow
root is only a frame, and the content stays in the light DOM.

```html
<bfb-panel id="panel">
  <template shadowrootmode="open">
    <style>:host { display: block } .frame { ... }</style>
    <div class="frame">
      <header><slot name="title"></slot></header>
      <div class="body"><slot></slot></div>
    </div>
  </template>

  <h2 slot="title">Panel</h2>
  <section class="card">...</section>
</bfb-panel>
```

The parser does the assembly: the template becomes the shadow root, everything after it
stays where it is, and the slots display it in place. Again, no script is needed.

Which stylesheet applies to what:

- The **frame** is inside the shadow root, so only the shadow root's styles reach it.
- The **card** is light DOM, so the page's `.card` rule styles it, exactly like the
  Counter and Basket cards.
- `::slotted(h2)` lets the shadow root style a slotted element, but only that element
  itself, not anything inside it.

Here hydration happens on the **host element**, not on the shadow root:

```fsharp
Program.mkProgram Views.Panel.init Views.Panel.update Views.Panel.view
|> Program.withLitHydrated "panel"
|> Program.run
```

By the time the script runs, the `<template>` is gone (the parser used it to build the
shadow root). The host's remaining children are the light content, which is exactly what
the server's markers wrap. The shadow root has no bindings, so there is nothing in it to
adopt.

### Knowing when an element leaves the page

The browser treats any tag with a dash in its name as a custom element, and custom
elements are told when they are added to or removed from the document. `Client/App.fs`
uses that for `bfb-panel` and logs it:

```fsharp
Lit.trackConnection (
    "bfb-panel",
    fun _ ->
        console.log "bfb-panel connected"

        { new System.IDisposable with
            member _.Dispose() = console.log "bfb-panel disconnected" }
)
```

The function runs when the element is connected, and the `IDisposable` it returns runs
when the element is disconnected. A timer or a socket would be started and stopped in
the same two places.

Try it in the console:

```js
const panel = document.querySelector('#panel'), parent = panel.parentNode
panel.remove()                 // bfb-panel disconnected
parent.appendChild(panel)      // bfb-panel connected
```

These are the browser's own callbacks, not polling, and they are the only notification
of this kind the platform offers. lit uses the same ones for its components. What lit
rendered inside the element is paused and resumed with them, so an element that was only
moved comes back with its state intact.

A plain `<div>` gets no such callbacks. Remove `#counter` and its program never finds
out.

## When the state comes from the server

Every component so far starts from an `init` that both sides can run, so the server and
the browser arrive at the same first model on their own.

The theme cannot work that way. Whether you chose dark mode is stored in a cookie, and
two islands display it, so both must start from the same answer.

### Avoiding the flash of the wrong theme

If the browser worked out the theme itself, the page would arrive in the wrong colours
and correct itself a moment later. So the server reads the cookie first, and the very
first bytes of the page are already right:

```html
<html lang="en" data-theme="dark">
```

Further down, it writes the same value for the scripts to read:

```html
<script type="application/json" id="bfb-theme">{"Dark":true}</script>
```

The store reads this once at startup, and both islands start from the store. So the
model they begin with is the model their markup was rendered from, which is the rule
hydration depends on. Nothing is fetched afterwards.

### One store, two islands

The two theme islands do not each have their own Elmish loop. They share one, and it
lives in the store (`Client/ThemeStore.fs`):

```fsharp
let private init () = fromPage () |> Option.defaultValue { Dark = false }, Cmd.none
let private update msg model = Theme.update msg model, Cmd.none

let store, dispatch = Store.makeElmish init update ignore ()
```

`Theme.update` is a normal Elmish update function. It lives in the shared file next to
the views, so the server compiles it too. Toggling the theme is `dispatch Toggle`, just
as it would be inside a program.

The difference from the four components above is what the store does *not* take.
`Program.mkProgram` takes `init`, `update` **and** `view` and ties them together. A
store takes only `init` and `update` and knows nothing about rendering. That is what
lets two islands share it. The store touches the DOM exactly once, to read the JSON
above.

An island is then only a view:

```fsharp
mount "theme-switch" Theme.switch
mount "theme-reader" (fun model _ -> Theme.reader model)
```

`mount` is defined in `App.fs`, not in the store. It adopts the server's markup using
the store's current value, then re-renders on every change. Neither island knows the
other exists.

### Applying the theme

Painting the page and remembering the choice is not either island's job. It is one more
subscriber in `App.fs`:

```fsharp
ThemeStore.store
|> Store.subscribeImmediate (fun model ->
    document.documentElement.setAttribute ("data-theme", Theme.name model)
    document.cookie <- $"{Theme.Cookie}={Theme.name model}; path=/; max-age=31536000")
```

Click the switch and four things change together: the two islands, the `data-theme`
attribute on `<html>`, and the cookie. On the next load the server reads the new cookie,
so the page is never briefly the wrong colour.

With JavaScript off the page is still themed correctly, because .NET read the cookie.
Only the toggle stops working.

Watch the palette while you switch: its styles are sealed in a shadow root, yet it
changes colour too. CSS custom properties are inherited *through* a shadow boundary even
though ordinary rules are not, so the card still picks up the page's colours.

The store is [`Fable.Store`](https://github.com/davedawkins/Fable.Store). Its commands
stay on the client, which is why the shared `update` returns only a model.

### Things to watch when sending state in the page

Only put in the page what the first render needs. Everything else can be fetched once
the page is running.

**Keep the type simple.** The model is `Dark: bool`, not a `Light | Dark` union. The
value travels as JSON and is read back with `unbox`, which only works because a bool
field looks the same in JSON as in F#. A union or an option would not survive the trip.
If you need one, write a proper encoder/decoder that both sides compile.

**Escaping.** If the JSON contained `</script>`, the browser would end the script
element there and parse the rest as HTML. `System.Text.Json` escapes `<` by default,
which makes this safe. A serialiser set to "relaxed" escaping would not be.

**Failing soft.** If the JSON is missing or unreadable, the store logs a warning and
starts from light. The islands then render over the server's markup instead of adopting
it. You get a warning and a rebuild, not a broken page.

## An island is not a component

The seventh card, the theme badge, shows where the island pattern ends. Everything above
it is markup the server wrote and lit adopted, started from `App.fs`. The badge is built
by the browser:

```fsharp
[<LitElement("bfb-theme-badge")>]
let ThemeBadge () =
    LitElement.init (fun config -> config.styles <- [ css $"..." ]) |> ignore
    let theme = Hook.useStore ThemeStore.store
    html $"""...{Theme.name theme}..."""
```

`Hook.useStore` (from `Fable.LitStore.Unofficial`) is all it takes to connect the
component to the store. The server sent `<bfb-theme-badge></bfb-theme-badge>` with
nothing inside, so there is nothing to hydrate. There is nothing to fetch either: the
store already held the server's value before the element was created, so the
component's first render shows it.

### What a component has that an island does not

A lifecycle. Take the badge out of the document and put it back:

```js
const badge = document.querySelector("bfb-theme-badge")
const parent = badge.parentNode, next = badge.nextSibling
badge.remove()                        // unsubscribes
// ...toggle the theme while it is gone: the badge does not follow
parent.insertBefore(badge, next)      // resubscribes, and shows what it missed
```

The two theme islands cannot do this. Their subscription is created in `App.fs` and
lasts as long as the page, because a `<div>` has no callback to clean up in. (That gap
is what `Lit.trackConnection` fills for `bfb-panel`.)

### Two pitfalls

**Reconnecting.** `disconnectedCallback` disposes whatever `useEffectOnce` set up. If
`connectedCallback` does not set it up again, an element that was only *moved* (dragged
to a new position, re-parented by a list re-render) comes back on screen but no longer
updates, with no error. Fable.Lit 2.18.0 fixed this, including inside `useStore`. There
it needed two things: resubscribing, and re-reading the current value, because
`subscribeImmediate` hands back the current value as a return value instead of calling
the callback. A component that only resubscribed would update from then on but keep
showing the value from when it left.

**A store with no subscribers disposes itself.** `Fable.Store` tears a store down when
its last subscriber leaves. Here the page-level subscriber in `App.fs` keeps it alive,
so the badge can come and go. A store read *only* by components would be destroyed the
moment the last one disconnects.

## View Transitions

Things to try on the page:

- Switch the theme: the whole page crossfades, including the shadow roots and the badge.
- Remove a basket row: the row disappears and the others slide into place. **Reset
  basket** lets you replay it.
- The counter, palette and panel animate their changes too.

How it works:

- `Client/ViewTransitions.fs` wraps every user-triggered `dispatch` in
  `document.startViewTransition`. The initial hydration is not animated.
- The browser takes its "after" snapshot when the update callback finishes. The islands
  render synchronously, but the badge is a LitElement and renders a moment later, so the
  callback waits for the badge's `updateComplete`.
- All islands share one queue, so quick clicks in different cards run in order.
- If the user prefers reduced motion, or the browser lacks the API, the update just
  happens without animation.
- The CSS in `Server/page.html` gives basket rows their own transition names only
  during a basket transition. A theme transition captures the whole page as one image.

All browser-specific code is in the client project, so the shared views still compile
on .NET.

## Editing it while it runs

```bash
npm run dev
```

This works from a fresh clone; npm installs what it needs first. You get the same page
on the same port, but saved changes reach the browser without a reload and without
losing state. Increment the counter, remove a basket row, edit `Shared/Views.fs`, and
the new markup appears with the count and the basket as you left them.

Three things run:

- **Fable**, watching and compiling the F#;
- **Vite**, serving the compiled client with hot module replacement;
- **the server**, restarted by nodemon when a file it compiles changes.

The rest of this section explains why it is set up this way. You do not need it to use
the demo.

**The page still comes from ASP.NET.** In dev, the app proxies anything it does not
serve itself to Vite, so the browser sees a single origin. That is `UseSpa` with
`UseProxyToSpaDevelopmentServer`. It is guarded to leave `/` alone: it is terminal
middleware, and unguarded it would answer for the page too, which would bypass the
server rendering this demo is about.

**Vite runs next to the server, not under it.** The usual choice is
`UseReactDevelopmentServer`, which (despite the name) just runs an npm script and waits
for a port, and is what an ordinary app should use. It cannot be used here. Shared views
are compiled into the server, so editing one restarts the server, and a dev server
started by the server would be killed with it. Fable would recompile from cold on every
edit and the browser would be told its dev server had disappeared. So Vite is started
separately and the proxy waits for it, using the overload that takes a task rather than
a URL. The page still renders immediately; only the request for the script waits.

**State survives a hot update.** `Program.withLitHydrated` records the running program
on the element it renders into. A hot update re-runs the module, the module mounts
again, and the second mount finds the first: it stops it, takes its model, and renders.
The one line that opts in is `HMR.acceptSelf()` in `Client/App.fs`, which makes the
module accept its own updates instead of reloading the page.

**A dev server is needed, not just a rebuilt bundle.** Vite gives every version of the
code the *same* copy of lit. Rebuilding a self-contained bundle and importing it again
looks simpler, but the page then holds two copies of lit, and the second is asked to
patch DOM created by the first. That fails with `part._$setValue is not a function`
when their internals are named differently, and leaves two template caches when they
are not.

**The server restarts instead of hot reloading.** F# has no hot reload, and shared
views are compiled into the server. A restart takes a few seconds, and it is what makes
the *next* page load render the view you just wrote. It has to happen *after* the
browser has fetched the hot update, because that update is fetched through the app
being restarted. That is what `nodemon --delay` is for.

**Static files are off in dev.** The page asks for `/App.js` and `wwwroot` contains
`app.js`. On a case-insensitive filesystem those are the same file, so the old bundle
would be served, Vite would never be reached, and the page would quietly run the last
build's code.

**Vite's hot-update socket bypasses the proxy.** A socket through the app would drop
every time the app restarts. Vite would take that to mean the dev server is gone and
reload the page.

## Running it in Docker

The Docker image builds the client and publishes the ASP.NET app with .NET 10. Node is
used only during the build. The container runs as a non-root user on port 8080.

```bash
docker build -t litdemo .
docker run --rm -p 8080:8080 litdemo
```

## What it does not do

`Lit.Server` renders templates, not components. A `HookComponent` or a `LitElement`
cannot be rendered on the server.

With Elmish this costs less than it sounds. The model does the job of `useState` and
`Cmd` does the job of `useEffect`, so state and effects live in the loop and the view
stays a plain function of the model. That function is the part both compilers can
render.

Directives the server cannot reproduce faithfully, such as `styleMap` or `until`, throw
an error instead of producing an approximation.

## Packages

| Package | What it is for |
|---|---|
| `Fable.Lit.Unofficial` | lit bindings for Fable, plus `Hydrate.adopt` |
| `Fable.Lit.Elmish.Unofficial` | `Program.withLitHydrated` |
| `Fable.LitStore.Unofficial` | `Hook.useStore`, for components that read a store |
| `Lit.Server.Unofficial` | renders the same templates to HTML on .NET |
| `HtmlTypeProvider` | typed HTML page templates |
| `Microsoft.AspNetCore.SpaServices.Extensions` | dev only: proxies to the dev server |

`Fable.Lit.Unofficial` and `Fable.Lit.Elmish.Unofficial` come from an
[unofficial fork](https://github.com/OnurGumus/Fable.Lit) of
[Fable.Lit](https://github.com/fable-compiler/Fable.Lit), which was last released in
2022. They are published under `.Unofficial` ids and will be deprecated if the changes
land upstream.

## Licence

MIT.
