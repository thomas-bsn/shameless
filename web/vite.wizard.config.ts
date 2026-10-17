// Build du front du wizard d'installation (image `wizard`), distinct du site Shameless.
// Même thème et mêmes composants shadcn/ui ; sortie dans dist-wizard avec son index.html.
import path from "node:path";
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [react(), tailwindcss()],
  publicDir: false,
  resolve: { alias: { "@": path.resolve(import.meta.dirname, "src") } },
  build: {
    outDir: "dist-wizard",
    emptyOutDir: true,
    rollupOptions: { input: path.resolve(import.meta.dirname, "wizard.html") },
  },
});
