/// Renders both components on every request, from the same F# the browser runs.
module Server

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open HtmlTypeProvider
open Lit

type Page = Template<"page.html">

/// Where the page gets its code.
///
/// In production, the bundle in wwwroot. Under `npm run dev`, the same modules from
/// Vite's dev server, unbundled, plus Vite's client -- which is what turns a saved file
/// into a re-run of the changed modules rather than a page reload. `HMR.acceptSelf()` in
/// Client/App.fs is the other half of that, and Lit.Elmish handles the handover from the
/// program the previous run left behind.
///
/// Nothing is proxied. The scripts are fetched from another origin, which the dev server
/// allows, and the page itself is still rendered here.
let private scripts hotReload =
    if hotReload then
        """
    <script type="module" src="http://localhost:5173/@vite/client"></script>
    <script type="module" src="http://localhost:5173/App.js"></script>"""
    else
        """<script type="module" src="/app.js"></script>"""

[<EntryPoint>]
let main args =
    // Passed by `npm run dev` and by nothing else, so `dotnet run` serves exactly what it
    // serves in production.
    let hotReload = args |> Array.contains "--hmr"

    let builder = WebApplication.CreateBuilder(args |> Array.filter (fun a -> a <> "--hmr"))
    let app = builder.Build()

    // Serves wwwroot/app.js, the bundled client.
    app.UseStaticFiles() |> ignore

    app.MapGet(
        "/",
        System.Func<IResult>(fun () ->
            // The same init the browser will run, so both sides render the same model.
            // Handlers are dropped on the server, hence `ignore` for dispatch.
            let counter, _ = Views.Counter.init ()
            let basket, _ = Views.Basket.init ()

            let html =
                Page()
                    .Counter(toHydratableNode (Views.Counter.view counter ignore))
                    .Basket(toHydratableNode (Views.Basket.view basket ignore))
                    .Scripts(Node.RawHtml(scripts hotReload))
                    .Render()

            Results.Content(html, "text/html"))
    )
    |> ignore

    app.Run("http://localhost:5199")
    0
