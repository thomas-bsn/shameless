import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  // Le thème est mémorisé dans le navigateur et posé sur <html> : on repart propre entre deux tests.
  localStorage.clear();
  document.documentElement.classList.remove("dark");
});
