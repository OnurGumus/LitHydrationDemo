/// Renders both components on every request, from the same F# the browser runs.
module Server

open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SpaServices
open HtmlTypeProvider
open Lit

type Page = Template<"page.html">

[<Literal>]
let private DevServer = "http://localhost:5173"

/// Where the page gets its code.
///
/// Both paths name the same origin. In production that is the bundle in wwwroot; under
/// `npm run dev` the requests are proxied to Vite, which serves the same modules
/// unbundled and adds the client that turns a saved file into a re-run of the changed
/// module rather than a page reload. `HMR.acceptSelf()` in Client/App.fs is the other
/// half of that, and Lit.Elmish handles the handover from the program the previous run
/// left behind.
let private scripts hotReload =
    if hotReload then
        """
    <script type="module" src="/@vite/client"></script>
    <script type="module" src="/App.js"></script>"""
    else
        """<script type="module" src="/app.js"></script>"""

/// Waits for the dev server, once, before the first page is served.
///
/// This page renders from .NET whether or not anything can serve its code, so a dev
/// server that is still starting gives you a page that looks finished and does nothing
/// when you press a button -- which is the one failure this demo should never
/// demonstrate by accident. Waiting turns it into a slow first load.
let private devServerReady =
    lazy
        (task {
            use http = new HttpClient(Timeout = TimeSpan.FromSeconds 2.0)
            let deadline = DateTime.UtcNow.AddSeconds 90.0
            let mutable ready = false

            while not ready && DateTime.UtcNow < deadline do
                try
                    let! response = http.GetAsync(DevServer + "/App.js")
                    ready <- response.IsSuccessStatusCode
                with _ ->
                    ()

                if not ready then
                    do! Task.Delay 500

            if not ready then
                Console.Error.WriteLine $"the dev server at {DevServer} never answered; the page will render but nothing will run"
         })

[<EntryPoint>]
let main args =
    // Passed by `npm run dev` and by nothing else, so `dotnet run` serves exactly what it
    // serves in production.
    let hotReload = args |> Array.contains "--hmr"

    let builder = WebApplication.CreateBuilder(args |> Array.filter (fun a -> a <> "--hmr"))
    let app = builder.Build()

    if hotReload then
        // Deliberately no static files in dev. The page asks for /App.js and wwwroot
        // holds app.js, which on a case-insensitive filesystem is the same request:
        // the bundle answers, the dev server is never reached, and what you get is a
        // page running last build's code -- hydrating, responding to clicks, and
        // ignoring every edit. Nothing in wwwroot is wanted here anyway.
        ()
    else
        // Serves wwwroot/app.js, the bundled client.
        app.UseStaticFiles() |> ignore

    if hotReload then
        // Everything the page asks for other than the page itself goes to Vite: the
        // entry module, the modules it imports, lit, and Vite's own client. Guarded on
        // the path because this is terminal middleware and runs before the endpoint
        // below would: without the guard it would answer "/" as well, and the page
        // would come from Vite rather than from here, with none of the server rendering
        // this demo exists to show.
        app.UseWhen(
            (fun ctx -> ctx.Request.Path <> PathString "/"),
            fun branch -> branch.UseSpa(fun spa -> spa.UseProxyToSpaDevelopmentServer DevServer)
        )
        |> ignore

    app.MapGet(
        "/",
        Func<Task<IResult>>(fun () ->
            task {
                if hotReload then
                    do! devServerReady.Value

                // The same init the browser will run, so both sides render the same
                // model. Handlers are dropped on the server, hence `ignore` for dispatch.
                let counter, _ = Views.Counter.init ()
                let basket, _ = Views.Basket.init ()
                let palette, _ = Views.Palette.init ()

                let html =
                    Page()
                        .Counter(toHydratableNode (Views.Counter.view counter ignore))
                        .Basket(toHydratableNode (Views.Basket.view basket ignore))
                        // Styles and markup together, inside the template the parser
                        // turns into a shadow root.
                        .Palette(toShadowRootNode Views.Palette.styles (Views.Palette.view palette ignore))
                        .Scripts(Node.RawHtml(scripts hotReload))
                        .Render()

                // The template has a doctype and the rendered page does not: the provider
                // parses the template into nodes, and a doctype is not one. Without it the
                // browser reads the page in quirks mode.
                return Results.Content("<!doctype html>\n" + html, "text/html")
            })
    )
    |> ignore

    app.Run("http://localhost:5199")
    0
