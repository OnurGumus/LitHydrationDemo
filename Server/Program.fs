/// Renders both components on every request, from the same F# the browser runs.
module Server

open System
open System.Net.Http
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.SpaServices
open Microsoft.Extensions.Hosting
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

/// Waits for the dev server, once, and then hands the proxy its address.
///
/// The waiting matters because this page renders from .NET whether or not anything can
/// serve its code: reach it while Vite is still starting and you get a page that looks
/// finished and does nothing when you press a button. Held here rather than in the page,
/// so the markup still arrives immediately and it is the request for the code that waits.
///
/// ASP.NET has a way to do this without any of the below -- `UseReactDevelopmentServer`,
/// which despite the name only means "run this npm script and wait for that port", and is
/// what a normal app should use. It waits because it *starts* the dev server, and that is
/// the part this demo cannot have: a shared view is compiled into the server too, so
/// editing one restarts the server, and a dev server owned by the server would be
/// restarted with it -- Fable from cold, and a browser told its dev server has gone.
/// Here Vite is started alongside instead, and this waits for it.
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

            return Uri DevServer
         })

[<EntryPoint>]
let main args =
    // Passed by `npm run dev` and by nothing else, so `dotnet run` serves exactly what it
    // serves in production.
    let hotReload = args |> Array.contains "--hmr"

    let builder = WebApplication.CreateBuilder(args |> Array.filter (fun a -> a <> "--hmr"))

    if hotReload then
        // Leave immediately when asked to. The default is to wait up to five seconds for
        // open connections to finish, and a browser keeps its connections open, so the
        // restarter has already started the next server by the time this one lets go of
        // the port -- which then fails to bind, exits, and is restarted, several times
        // over, before one of them wins.
        builder.Host.ConfigureHostOptions(fun options -> options.ShutdownTimeout <- TimeSpan.FromMilliseconds 200.0)
        |> ignore

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
            fun branch -> branch.UseSpa(fun spa -> spa.UseProxyToSpaDevelopmentServer(Func<Task<Uri>>(fun () -> devServerReady.Value)))
        )
        |> ignore

    app.MapGet(
        "/",
        Func<Task<IResult>>(fun () ->
            task {
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
