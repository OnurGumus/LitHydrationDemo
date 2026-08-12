# F# hydration demo: two Elmish components, server-rendered

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

The page arrives fully rendered from ASP.NET: a counter and a basket, each in its own
container. When the script loads, each becomes an Elmish program that **adopts** its own
markup — taking ownership of the existing DOM rather than replacing it. No element is
re-created, no markup is rendered twice, and the two loops never touch each other.

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
| `Shared/Views.fs` | both components: model, msg, init, update, view |
| `Server/Program.fs` | minimal ASP.NET; renders each with `toHydratableNode` |
| `Server/page.html` | the page shell, an `HtmlTypeProvider` template, one div per component |
| `Client/App.fs` | two Elmish programs, six lines |

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

## What it does not do

There is no HMR. The client is compiled and bundled before the server starts, so editing
a view needs a restart. A dev server could be proxied instead, at the cost of a second
moving part.

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

The first two come from an [unofficial fork](https://github.com/OnurGumus/Fable.Lit) of
[Fable.Lit](https://github.com/fable-compiler/Fable.Lit), which was last released in
2022. They are published under `.Unofficial` ids and will be deprecated if the changes
land upstream.

## Licence

MIT.
