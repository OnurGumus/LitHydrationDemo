/// Animation belongs at the client interaction boundary, after initial hydration.
module ViewTransitions

open Fable.Core

// One queue for all islands: document transitions cannot run independently. Waiting
// for each one also preserves rapid clicks without replaying a failed update.
// The small native bridge keeps the browser API out of the shared .NET views.
[<Emit("""
(() => {
    let pending = Promise.resolve();
    return (kind, change) => {
        pending = pending.then(async () => {
            const update = async () => {
                change();
                // Islands render immediately; a LitElement schedules its render, whether
                // it heard from the store or was handed a property by an island.
                await Promise.all(Array.from(document.querySelectorAll('bfb-theme-badge, bfb-meter'),
                    element => element.updateComplete));
            };
            if (!document.startViewTransition ||
                matchMedia('(prefers-reduced-motion: reduce)').matches) {
                await update();
                return;
            }
            document.documentElement.dataset.transition = kind;
            try {
                const transition = document.startViewTransition(update);
                // A skipped animation is fine; an update failure is reported below.
                transition.ready.catch(() => {});
                await transition.finished;
            } finally {
                delete document.documentElement.dataset.transition;
            }
        }).catch(error => console.error('View transition update failed', error));
    };
})()
""")>]
let private createRunner () : obj = jsNative

[<Emit("$0($1, $2)")>]
let private enqueue (runner: obj) (kind: string) (change: unit -> unit) : unit = jsNative

let private runner = createRunner ()

let dispatch kind send message =
    enqueue runner kind (fun () -> send message)
