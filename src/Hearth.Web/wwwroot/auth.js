(() => {
    document.querySelectorAll('[data-toggle-password]').forEach((button) => {
        button.addEventListener('click', () => {
            const inputId = button.getAttribute('data-toggle-password');
            const input = inputId ? document.getElementById(inputId) : null;
            if (!input) {
                return;
            }

            const isPassword = input.type === 'password';
            input.type = isPassword ? 'text' : 'password';
            button.textContent = isPassword ? 'Ocultar' : 'Mostrar';
            button.setAttribute('aria-pressed', isPassword ? 'true' : 'false');
            button.setAttribute('aria-label', isPassword ? 'Ocultar senha' : 'Mostrar senha');
            input.focus();
        });
    });

    document.querySelectorAll('form[data-auth-form="true"]').forEach((form) => {
        form.addEventListener('submit', () => {
            const submit = form.querySelector('button[type="submit"]');
            if (!submit || submit.disabled) {
                return;
            }

            submit.disabled = true;
            const loadingText = submit.getAttribute('data-loading-text');
            if (loadingText) {
                submit.textContent = loadingText;
            }
        });
    });
})();
