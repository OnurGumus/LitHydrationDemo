/// Renders the page on every request, from the same F# view the browser runs.
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
            // toHydratableNode, not toNode: the same HTML plus the comment markers lit
            // needs in order to adopt it. The client must render the same view with the
            // same count.
            let content = toHydratableNode (Views.page 0 ignore)
            Results.Content(Page().Content(content).Render(), "text/html"))
    )
    |> ignore

    app.Run("http://localhost:5199")
    0
