// Bundles the Fable output into one file the ASP.NET app can serve.
//
// Without this the browser would meet `import { html } from "lit"` and have nowhere to
// look: bare specifiers are a build-time convention, not something a static file server
// can answer.
export default {
  root: 'Client/build',
  build: {
    outDir: '../../Server/wwwroot',
    emptyOutDir: true,
    // Readable output, because this is a sample and someone will open it.
    minify: false,
    rollupOptions: {
      input: 'Client/build/App.js',
      output: { entryFileNames: 'app.js', format: 'es' },
    },
  },
}
