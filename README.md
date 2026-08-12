# F# hydration demo: one view, two runtimes

A small, self-contained example of writing a view **once** in F# and having it rendered
by .NET on the server and then *adopted* by [lit](https://lit.dev) in the browser —
without Node on the server and without `@lit-labs/ssr`.

```bash
dotnet run --project Server
```

Then open <http://localhost:5199>. That is the whole setup: the server project compiles
and bundles the client on its way to building, and installs the npm packages the first
time.

## What this shows

The page arrives fully rendered from ASP.NET. When the script loads, lit **hydrates** it:
it takes ownership of the existing DOM rather than replacing it, and the button starts
working. No element is re-created, and no markup is rendered twice.

`Shared/Views.fs` is compiled twice — by the .NET compiler against
[`Lit.Server.Unofficial`](https://www.nuget.org/packages/Lit.Server.Unofficial), and by
[Fable](https://fable.io) against
[`Fable.Lit.Unofficial`](https://www.nuget.org/packages/Fable.Lit.Unofficial). Both
resolve `open Lit`; a project references one or the other, never both. So there is no
conditional compilation, and no template written twice in two languages.

```fsharp
let page (count: int) (onClick: unit -> unit) =
    html
        $"""<div class="card">
              <h2>Packing list</h2>
              <table><tbody>{Lit.ofList (items |> List.map row)}</tbody></table>
              <p>Clicked <b class="count">{count}</b> times.</p>
              <button type="button" @click={Ev(fun _ -> onClick ())}>Click me</button>
            </div>"""
```

On the server, `@click` is dropped — a handler is a closure and cannot be serialised.
In the browser it becomes a real listener when lit adopts the markup.

## Layout

| | |
|---|---|
| `Shared/Views.fs` | the view both sides compile |
| `Server/Program.fs` | minimal ASP.NET; renders with `toHydratableNode` |
| `Server/page.html` | the page shell, an `HtmlTypeProvider` template |
| `Client/App.fs` | calls `Hydrate.adopt` |

The server composes the rendered view into the page as a `Node`, the type
[`HtmlTypeProvider`](https://github.com/OnurGumus/HtmlTypeProvider) templates already
accept, so no strings cross the boundary and nothing is escaped twice.

## Checking that it really hydrated

Adoption is invisible from the outside: markup that was re-rendered looks the same as
markup that was kept. Two ways to be sure.

Turn JavaScript off and reload — the table is still there, because .NET rendered it.

Or hold on to a node and watch it survive an update:

```js
const row = document.querySelector('#app tbody tr')
document.querySelector('#app button').click()
document.querySelector('#app tbody tr') === row   // true: lit patched, it did not rebuild
```

A console warning beginning `lit could not adopt` means it fell back to a full render.
That is `Hydrate.adopt` doing its job: lit's `hydrate` throws part way through when the
markup does not match, so the alternative to catching it is a half-wired page.

## Two rules worth knowing

**Hydrate the element the markers were written into.** The root marker wraps whatever the
template rendered, so here it is `<div id="app">`, not the card inside it. Hydrating the
card places lit inside its own marker, where it never finds it.

**The client must pass the same template and the same data.** A different template is a
digest mismatch: caught, reported, and rendered normally. Different data hydrates cleanly
and then shows values the server never rendered, which nothing catches.

## What it does not do

There is no HMR. The client is compiled and bundled before the server starts, so editing
`Client/App.fs` needs a restart. A dev server could be proxied instead, at the cost of a
second moving part.

## Packages

| | |
|---|---|
| `Fable.Lit.Unofficial` | lit bindings for Fable, plus `Hydrate.adopt` |
| `Lit.Server.Unofficial` | renders those templates to HTML on .NET |
| `HtmlTypeProvider` | typed HTML page templates |

The first two come from an [unofficial fork](https://github.com/OnurGumus/Fable.Lit) of
[Fable.Lit](https://github.com/fable-compiler/Fable.Lit), which was last released in
2022. They are published under `.Unofficial` ids and will be deprecated if the changes
land upstream.

## Licence

MIT.
