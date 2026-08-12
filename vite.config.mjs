// Two jobs, and the second one is why `npm run dev` exists.
//
// Building: the Fable output becomes one file the ASP.NET app can serve. Without this
// the browser would meet `import { html } from "lit"` and have nowhere to look: bare
// specifiers are a build-time convention, not something a static file server answers.
//
// Serving: in dev the same modules are served unbundled, with lit resolved to a single
// URL that every version of the app shares. That sharing is the whole reason for a dev
// server here. Reloading a self-contained bundle instead gives the page a second copy
// of lit, and the second copy is then asked to patch DOM whose parts belong to the
// first -- "part._$setValue is not a function", once their internals are named
// differently, and two template caches when they are not.
// The two outputs are kept apart on purpose. Fable compiles the dev one with DEBUG
// defined and the release one without; sharing a directory would mean whichever ran
// last is what the other one serves, and DEBUG is what decides whether the page can
// accept a hot update at all.
export default ({ command }) => ({
  root: command === 'serve' ? 'Client/build-dev' : 'Client/build',
  server: {
    port: 5173,
    strictPort: true,
    // The page comes from the ASP.NET app on another port, so the modules are
    // cross-origin. Vite allows that by default; this says so out loud.
    cors: true,
  },
  build: {
    outDir: '../../Server/wwwroot',
    emptyOutDir: true,
    // Readable output, because this is a sample and someone will open it.
    minify: false,
    rollupOptions: {
      input: 'Client/build/App.js',
      output: {
        entryFileNames: 'app.js',
        format: 'es',
      },
    },
  },
})
