# Design

## Decisions

1. **Light in the file, dark by override.** `:root` holds the light tokens. The dark tokens are set twice: under `prefers-color-scheme: dark` for `:root:not([data-theme="light"])`, and for `:root[data-theme="dark"]`. No script is needed for "system", and nothing flashes.
2. **Choice in `localStorage` (`dalan.theme`)**, like the language (`dalan.lang`). "system" removes `data-theme`.
3. **One button, three states.** It moves system, light, dark, system. The icon shows the current state; the title and `aria-label` name it.
4. **Code blocks stay dark** in both themes, like ngrok.
5. Login screen is redesigned in another change, so it only gets the new colours here, no button.
