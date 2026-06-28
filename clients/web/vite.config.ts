import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

// Dev requests are same-origin: Vite proxies /api and /hubs to the Nginx
// reverse proxy that fronts the API (VITE_PROXY_TARGET). This keeps the
// browser CORS-free and gives SignalR a clean WebSocket upgrade path.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const proxyTarget = env.VITE_PROXY_TARGET || 'http://localhost:8080'

  return {
    plugins: [
      react(),
      tailwindcss(),
      // SignalR 10.x places /*#__PURE__*/ on function declarations, which Rolldown
      // rejects. Harmless at runtime; strip to keep builds quiet until signalr 11.
      // https://github.com/dotnet/aspnetcore/issues/55286
      {
        name: 'strip-signalr-pure-annotations',
        enforce: 'pre',
        transform(code, id) {
          if (!id.includes('node_modules/@microsoft/signalr')) return
          return code.replace(/\/\*#__PURE__\*\//g, '')
        },
      },
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    test: {
      environment: 'node',
      include: ['src/**/*.test.ts'],
    },
    server: {
      port: 5173,
      proxy: {
        '/api': { target: proxyTarget, changeOrigin: true },
        '/hubs': { target: proxyTarget, changeOrigin: true, ws: true },
        '/health': { target: proxyTarget, changeOrigin: true },
        // Local Azurite blob uploads (see resolveStorageUploadUrl in media/api.ts).
        '/devstoreaccount1': { target: 'http://127.0.0.1:10000', changeOrigin: true },
      },
    },
  }
})
