﻿module CRUD.Program

open System.Threading.Tasks
open CRUD.Env
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Oxpecker

let getEndpoints env = [
    subRoute "/order" [
        GET [
            route "/" <| Handlers.getOrders env
            routef "/{%O:guid}" <| Handlers.getOrderDetails env
        ]
        POST [ route "/" <| Handlers.createOrder env ]
        PUT [ routef "/{%O:guid}" <| Handlers.updateOrder env ]
        DELETE [ routef "/{%O:guid}" <| Handlers.deleteOrder env ]
    ]
]

let notFoundHandler (ctx: HttpContext) =
    let logger = ctx.GetLogger()
    logger.LogWarning("Unhandled 404 error")
    ctx.SetStatusCode 404
    ctx.WriteText "Resource was not found"

let errorHandler (ctx: HttpContext) (next: RequestDelegate) =
    task {
        try
            return! next.Invoke(ctx)
        with
        | :? ModelBindException
        | :? RouteParseException as ex ->
            let logger = ctx.GetLogger()
            logger.LogWarning(ex, "Unhandled 400 error")
            ctx.SetStatusCode StatusCodes.Status400BadRequest
            return! ctx.WriteText <| string ex
        | ex ->
            let logger = ctx.GetLogger()
            logger.LogError(ex, "Unhandled 500 error")
            ctx.SetStatusCode StatusCodes.Status500InternalServerError
            return! ctx.WriteText <| string ex
    }
    :> Task

let configureApp (appBuilder: IApplicationBuilder) =
    let env = {
        DbClient = Database.Fake.fakeClient
        Logger = appBuilder.ApplicationServices.GetService<ILogger>()
    }
    appBuilder
        .UseRouting()
        .Use(errorHandler)
        .UseOxpecker(getEndpoints env)
        .Run(notFoundHandler)

let configureServices (services: IServiceCollection) =
    services
        .AddRouting()
        .AddOxpecker()
        .AddSingleton<ILogger>(fun sp -> sp.GetRequiredService<ILoggerFactory>().CreateLogger("Oxpecker.Examples.CRUD"))
    |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    let app = builder.Build()
    configureApp app
    app.Run()
    0
