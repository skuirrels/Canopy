import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import settings from '../server/Planner.Api/appsettings.json' with { type: 'json' };
export default defineConfig(({ mode }) => {
 const apiUrl = loadEnv(mode, '.', 'PLANNER_').PLANNER_API_URL || settings.Urls;
 return { plugins: [react(), tailwindcss()], server: { port: 5173, proxy: { '/api': apiUrl, '/openapi': apiUrl } }, build: { outDir: '../server/Planner.Api/wwwroot', emptyOutDir: true } };
});
