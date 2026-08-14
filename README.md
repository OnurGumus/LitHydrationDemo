# F# hydration demo: six Elmish components, server-rendered

A small, self-contained example of writing components **once** in F# — model, update and
view — rendering them to HTML with .NET, and having [lit](https://lit.dev) *adopt* that
HTML in the browser, where each becomes an independent Elmish program. No Node on the
server, and no `@lit-labs/ssr`.

```bash
dotnet run --project Server
```

Then open <http://localhost:5199>. That is the whole setup: the server project compiles
and bundles the client on its way to building, and installs the npm packages the first
time.

## What this shows

The page arrives fully rendered from ASP.NET: a counter, a basket, a palette, a panel, and
two islands that share one piece of state, each in its own container. When the script loads, each becomes an Elmish program that **adopts**
its own markup — taking ownership of the existing DOM rather than replacing it. No element
is re-created, no markup is rendered twice, and the loops never touch each other.

`Shared/Views.fs` is compiled twice — by the .NET compiler against
[`Lit.Server.Unofficial`](https://www.nuget.org/packages/Lit.Server.Unofficial), and by
[Fable](https://fable.io) against
[`Fable.Lit.Unofficial`](https://www.nuget.org/packages/Fable.Lit.Unofficial). Both
resolve `open Lit`; a project references one or the other, never both. So there is no
conditional compilation, and no template written twice in two languages.

Elmish itself is ordinary F# with no browser in it, so `init` and `update` compile on
both sides too. That is what lets the server render from the model the browser is about
to start with, without shipping it as JSON:

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

On the server `@click` is dropped — a handler is a closure and cannot be serialised. In
the browser it becomes a real listener the moment lit adopts the markup.

## Layout

| | |
|---|---|
| `Shared/Views.fs` | four components: model, msg, init, update, view |
| `Shared/Session.fs` | the state two islands share, and the views onto it |
| `Client/SessionStore.fs` | where the store's first value comes from, and the one write |
| `Server/Program.fs` | minimal ASP.NET; renders each with `toHydratableNode` |
| `Server/page.html` | the page shell, an `HtmlTypeProvider` template, one div per component |
| `Client/App.fs` | four Elmish programs, three lines each |

The server composes the rendered view into the page as a `Node`, the type
[`HtmlTypeProvider`](https://github.com/OnurGumus/HtmlTypeProvider) templates already
accept, so no strings cross the boundary and nothing is escaped twice.

## Checking that it really hydrated

Adoption is invisible from the outside: markup that was re-rendered looks the same as
markup that was kept. Two ways to be sure.

Turn JavaScript off and reload — the table is still there, because .NET rendered it.

Or hold on to a node and watch it survive an update:

```js
const card = document.querySelector('#basket section')
document.querySelector('#counter button:nth-of-type(2)').click()   // the counter's +
document.querySelector('#basket section') === card                 // true: untouched
document.querySelector('#basket tbody tr button').click()          // remove a row
document.querySelector('#counter .value').textContent              // unchanged
```

The counter's `<section>` and the basket's are each the same DOM object before and after
both interactions: two loops, patching their own DOM, ignoring each other's.

A console warning beginning `lit could not adopt` means it fell back to a full render.
That is `Hydrate.adopt` doing its job: lit's `hydrate` throws part way through when the
markup does not match, so the alternative to catching it is a half-wired page.

## Two rules worth knowing

**Hydrate the element the markers were written into.** The root marker wraps whatever the
template rendered, so here they are `<div id="counter">` and `<div id="basket">`, not the
cards inside them. Hydrating a card places lit inside its own marker, where it never
finds it.

**The client must start from the model the server rendered.** A different template is a
digest mismatch: caught, reported, and rendered normally. A different *model* hydrates
cleanly and then shows values the server never sent, which nothing catches. Here both
sides call the same `init`, so it holds by construction; an init that depends on server
state has to be handed that state.

## The third one is in a shadow root

`Palette` is an ordinary Elmish component that happens to arrive inside its own shadow
root, and it uses the same `.card` class as the two above. It looks different because the
page's stylesheet does not reach into a shadow root and its own does not reach out.

```fsharp
// Server: styles and markup together, inside the template
.Palette(toShadowRootNode Views.Palette.styles (Views.Palette.view palette ignore))
```

```fsharp
// Client: the root is the container the markers were written into, not the host
Program.mkProgram Views.Palette.init Views.Palette.update Views.Palette.view
|> Program.withLitHydratedInShadowRoot "palette"
|> Program.run
```

What the server writes is `<template shadowrootmode="open">`, which the HTML parser
attaches as a shadow root while it reads the page — so the element has its shadow DOM,
and its styles, before any script has run. Turn JavaScript off and it is still there.

Nothing in the hydration protocol needed to change for this. lit finds its bindings by
walking comment nodes, and comments live in a shadow root like anywhere else. The styles
sit outside the markers, so they are neither adopted nor re-rendered.

This is the islands version of declarative shadow DOM, and it is worth being clear about
what it is not: rendering `LitElement` components on the server the way `@lit-labs/ssr`
does — walking custom element tags, serialising `static styles`, ordering hydration with
`defer-hydration` — is a much larger thing that `Lit.Server` does not attempt.

## The fourth one uses slots

`Palette` puts everything inside its shadow root. `Panel` does the opposite: its shadow
root is only a frame, and the content it frames stays in the light DOM.

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

The parser does the composing: the template becomes the shadow root, everything after it
stays where it is, and the slots pull it into place — before a line of script has run.

The interesting part is which stylesheet reaches what. The frame is styled from inside
the shadow root, where the page cannot reach it. The card is light DOM, so the page's own
`.card` rule styles it exactly as it styles the two cards at the top. One element, two
rulebooks, and the slot is the border between them. `::slotted(h2)` reaches across it,
but only to the slotted element itself — not to anything inside it.

Hydration happens on the **host**, not on the shadow root:

```fsharp
Program.mkProgram Views.Panel.init Views.Panel.update Views.Panel.view
|> Program.withLitHydrated "panel"
|> Program.run
```

By then the `<template>` is gone — the parser took it to build the shadow root — so the
host's children are the light content and nothing else, which is exactly what the markers
were written around. The shadow root here has no bindings at all, so there is nothing in
it to adopt.

### Watching it connect and disconnect

The host is a custom element as far as the browser is concerned — any tag with a dash is
— so it can be upgraded to one that reports joining and leaving the document. `Client/App.fs`
does that and logs it:

```fsharp
Lit.trackConnection ("bfb-panel", fun _ connected ->
    console.log ("bfb-panel " + (if connected then "connected" else "disconnected")))
```

Open the console and take it out:

```js
const panel = document.querySelector('#panel'), parent = panel.parentNode
panel.remove()                 // bfb-panel disconnected
parent.appendChild(panel)      // bfb-panel connected
```

Those are the browser's own callbacks, not a poll — and they are the only such report the
platform offers, which is why lit itself borrows them for its components. What lit rendered
inside is paused and resumed along with them, so an element that was merely moved comes back
with everything it had. Nothing else in the page has this: remove `#counter` and its program
never hears about it, because a plain `<div>` has no callbacks to lend.

## When the state comes from the server

Every component above starts from an `init` both sides can run, which is why none of them
needs anything shipped alongside the markup: the server and the browser reach the same
first model separately, and hydration matches by construction.

The last two cannot. Which warehouse, how many bays, **and who is signed in** — the browser
has no way to work that out, and *both* islands render from it, so both have to hear the
same answer. The server builds it once, renders both views from it, and writes it into the
page:

```html
<script type="application/json" id="bfb-session">{"Warehouse":"Rotterdam","Bays":12,"Reserved":4,"SignedInAs":"pat"}</script>
```

The store reads that once, on the way up, and each island's `init` reads the store. So the
model they start from is the model their markup was rendered from — which is the contract
hydration rests on, and the reason nothing here is fetched after the fact.

These two do not have a loop each. They share one, and it lives in the store:

```fsharp
let private init () = valueFromThePage (), Cmd.none
let private update msg session = Session.update msg session, Cmd.none

let private store, dispatch = Store.makeElmish init update ignore ()
```

`Session.update` is an ordinary Elmish update — `Msg`, model in, model out — sitting in
the shared file next to the views, so the server compiles it too. What differs from the
four components above is only that the loop is not attached to an element. Reserving a
bay is `dispatch Reserve`, exactly as it would be inside a program.

An island is then a view and nothing else:

```fsharp
SessionStore.mount "bays" Session.bays
SessionStore.mount "summary" (fun session _ -> Session.summary session)
```

`mount` lives in `App.fs`, next to where the other four islands are started, not in the
store: it adopts the server's markup with the store's current value and re-renders on
every change after that. The store itself touches nothing that renders — it is asked for
its value and told when to change, and that is all it knows. Both start from the value the store read out of the page, which is the
value their markup was rendered from, so both adopt rather than rebuild — and neither
knows the other exists.

The store is [`Fable.Store`](https://github.com/davedawkins/Fable.Store); its commands stay
on the client, which is why the shared update returns a model and nothing else. A component
rather than a view can skip the subscription entirely with `Hook.useStore` from
`Fable.LitStore.Unofficial`.

Signing in is the part worth watching, because it is *not* an island. The form posts,
the server sets a cookie and redirects, and the page comes back rendered from the new
answer — `SignedInAs` in the payload, the reserve button no longer refusing, the hint
gone. No script is involved, so it works with JavaScript switched off, which is the
honest test of whether a page is server-rendered or merely server-delivered.

Note the type: `SignedInAs: string`, empty when nobody is, rather than an option. The
payload crosses as JSON and comes back through `unbox`, and an option would arrive as
null or a value — neither of which is what F# expects one to look like. The moment you
want a real option here, you want a codec.

Three details that are easy to get wrong:

**Escaping.** A payload containing `</script>` would end the element and the rest of it
would be parsed as markup. `System.Text.Json` escapes `<` by default, which is what makes
this safe; a serialiser configured for "relaxed" escaping would not be.

**The cast.** `unbox` works here because the record is strings and numbers, whose field
names survive JSON unchanged. A union or an option would need a real codec, written once
and compiled by both sides — at which point you are choosing between conditional
compilation and a serialiser that ships for both runtimes.

**Failing soft.** If the payload is missing or unreadable the store warns and starts from a
default, and the islands then render over the server's markup instead of adopting it —
a warning and a rebuild rather than a broken page.

Only what the first render needs belongs in there. Everything else can be fetched once the
page is alive.

## Editing it while it runs

```bash
npm run dev
```

From a fresh clone too: npm installs what it needs on the way in, the way `dotnet run`
does. Same page on the same port, but a saved view now reaches the browser without a reload,
and without losing what is on screen: increment the counter, remove a row, edit
`Shared/Views.fs`, and the new markup arrives with the count and the basket as you left
them. Three things run — Fable watching the F#, Vite serving the result, and the server
restarting when a file it compiles changes.

The page still comes from ASP.NET, and so does everything else it asks for: in dev the
app proxies what it does not serve itself to the dev server, so the browser sees one
origin and the markup names no second port. That is `UseSpa` with
`UseProxyToSpaDevelopmentServer`, guarded to leave `/` alone — it is terminal middleware,
and unguarded it would answer for the page too, which would end the server rendering
this demo is about.

The usual way to do this is `UseReactDevelopmentServer`, which — despite the name — just
means "run this npm script and wait for that port", and is what an ordinary app should
reach for. It waits because it *starts* the dev server, and that is the part this demo
cannot have. A shared view is compiled into the server as well, so editing one restarts
the server, and a dev server the server owned would go down with it: Fable from cold on
every edit, and a browser told its dev server has disappeared. So Vite runs alongside
instead, and the proxy waits for it — through the same seam, the overload that takes a
task rather than a URL, so the page still renders at once and it is the request for the
*code* that waits.

The state survives because `Program.withLitHydrated` records the running program on the
element it renders into. A hot update re-runs the module, the module mounts again, and
the second mount finds the first: it stops it, takes its model, and renders. One line
asks for that — `HMR.acceptSelf()` in `Client/App.fs`, which makes the module accept its
own updates instead of the page being reloaded.

A dev server is doing real work here. What it provides is one module graph: every
version of the code shares the *same* lit. Rebuilding a self-contained bundle and
importing it again looks simpler and is not, because the page then holds two copies of
lit, and the second is asked to patch DOM whose parts belong to the first —
`part._$setValue is not a function`, when their internals happen to be named
differently, and two template caches when they are not.

The server is restarted rather than hot-reloaded: F# has no hot reload, and a shared
view is compiled into the server as well. That costs a few seconds, and it is what keeps
the *next* page load rendering the view you just wrote. It also has to happen *after* the
browser has taken the update, since the update is fetched through the very app being
restarted — hence `nodemon --delay`, which lets the hot update go first.

Two things in dev are switched off rather than configured. Static files: the page asks
for `/App.js`, `wwwroot` holds `app.js`, and on a case-insensitive filesystem those are
the same request — the bundle answers, the dev server is never reached, and the page runs
last build's code while looking perfectly alive. And Vite's hot-update socket connects
straight to Vite rather than through the proxy, because a socket through the app dies
whenever the app restarts, and Vite reasonably reads that as "the dev server is gone" and
reloads the page.

## What it does not do

`Lit.Server` renders templates, not components, so a `HookComponent` or a `LitElement`
has no server rendering. Under Elmish that costs less than it sounds: the model is
`useState` and `Cmd` is `useEffect`, so the state and the effects live in the loop and
the view stays a function of the model — which is the part both compilers can render.
Directives it cannot honour faithfully, such as `styleMap` or `until`, raise rather than
being approximated.

## Packages

| | |
|---|---|
| `Fable.Lit.Unofficial` | lit bindings for Fable, plus `Hydrate.adopt` |
| `Fable.Lit.Elmish.Unofficial` | `Program.withLitHydrated` |
| `Lit.Server.Unofficial` | renders those templates to HTML on .NET |
| `HtmlTypeProvider` | typed HTML page templates |
| `Microsoft.AspNetCore.SpaServices.Extensions` | dev only: proxies to the dev server |

The first two come from an [unofficial fork](https://github.com/OnurGumus/Fable.Lit) of
[Fable.Lit](https://github.com/fable-compiler/Fable.Lit), which was last released in
2022. They are published under `.Unofficial` ids and will be deprecated if the changes
land upstream.

## Licence

MIT.
