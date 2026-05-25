import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5154',
        changeOrigin: true,
      },
    },
    watch: {
      // Impede que a cache do Vite e o package.json
      // disparem restarts falsos (bug Vite 8 + rolldown)
      ignored: [
        '**/.vite/**',
        '**/node_modules/**',
        '**/package.json',
        '**/package-lock.json',
      ],
    },
  },
  optimizeDeps: {
    // Pré-bundling explícito das libs principais — Vite não precisa de percorrer
    // o código-fonte para as descobrir, o que acelera o arranque a frio.
    include: ['react', 'react-dom', 'react-dom/client', 'react-router-dom'],
  },
})
