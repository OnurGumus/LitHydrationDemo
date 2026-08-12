/// Renders both components on every request, from the same F# the browser runs.
module Server

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open HtmlTypeProvider
open Lit

type Page = Template<"page.html">

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
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
                    .Render()

            Results.Content(html, "text/html"))
    )
    |> ignore

    app.Run("http://localhost:5199")
    0
