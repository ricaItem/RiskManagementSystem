(function () {
    const storageKey = "theme"; // "light" | "dark"
    const root = document.documentElement;

    function getPreferredTheme() {
        const saved = localStorage.getItem(storageKey);
        if (saved === "light" || saved === "dark") return saved;
        // default to system
        return window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches
            ? "dark"
            : "light";
    }

    function applyTheme(theme) {
        if (theme === "dark") root.classList.add("dark");
        else root.classList.remove("dark");
        root.style.colorScheme = theme; // helps built-in form controls
    }

    // Apply ASAP to avoid flash
    applyTheme(getPreferredTheme());

    // Expose a toggle function globally
    window.__toggleTheme = function () {
        const isDark = root.classList.contains("dark");
        const next = isDark ? "light" : "dark";
        localStorage.setItem(storageKey, next);
        applyTheme(next);

        // Notify any listeners (optional)
        window.dispatchEvent(new CustomEvent("themechange", { detail: { theme: next } }));
    };
})();
