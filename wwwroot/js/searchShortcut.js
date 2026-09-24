window.registerSearchShortcut = (dotNetRef) => {
    const handleKeyDown = (e) => {
        // Cmd/Ctrl + K to open search
        if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
            e.preventDefault();
            dotNetRef.invokeMethodAsync('OpenFromShortcut');
        }
    };

    document.addEventListener('keydown', handleKeyDown);

    // Return cleanup function
    return () => {
        document.removeEventListener('keydown', handleKeyDown);
    };
};

/* ---------------------------------------------------------------------
   Theme
   The class now lives on <html>, not <body>, so the bootstrap script in
   App.razor can set it before <body> exists and before first paint. That
   removes the flash of light theme and makes the preference survive a
   refresh. CSS is unaffected: .dark-mode is an ancestor either way.
   --------------------------------------------------------------------- */
window.setDarkMode = (enabled) => {
    document.documentElement.classList.toggle('dark-mode', !!enabled);
    try { localStorage.setItem('darkMode', enabled ? 'true' : 'false'); } catch (e) { }
};

window.getDarkMode = () => {
    return document.documentElement.classList.contains('dark-mode');
};

/* ---------------------------------------------------------------------
   Platform-aware shortcut hint. The search trigger hardcoded the Mac
   symbol; on Windows it read as gibberish to users.
   --------------------------------------------------------------------- */
window.getShortcutLabel = () => {
    const isMac = /Mac|iPhone|iPad/.test(navigator.platform || navigator.userAgent);
    return isMac ? '\u2318K' : 'Ctrl K';
};

window.setVisitorInfo = (company, name) => {
    localStorage.setItem('visitorCompany', company || '');
    localStorage.setItem('visitorName', name || '');
};

window.getVisitorCompany = () => {
    return localStorage.getItem('visitorCompany') || '';
};

window.getVisitorName = () => {
    return localStorage.getItem('visitorName') || '';
};

window.clearVisitorInfo = () => {
    localStorage.removeItem('visitorCompany');
    localStorage.removeItem('visitorName');
};
